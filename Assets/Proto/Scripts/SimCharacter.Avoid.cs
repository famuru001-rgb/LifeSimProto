using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 歩いているとき、ほかのキャラをよける（2026-11-09 famuru「キャラが固まってスタックしがちなので回り道をするか横を通り抜けられるようにして」）。
    /// 前は、歩く道にほかのキャラがいても気にせず重なって進み、立ち止まっている子の所で詰まっていた。
    /// ・前（AvoidLook m 以内）の自分の通り道にいる子の横を、少しずれて通り抜ける（ずれる先が通れる所のときだけ）。並んで歩く子とも少し間をあける
    /// ・両側ともふさがっていたら、ゆっくりになって待つ。それが AvoidDetourAfter 秒つづいたら、立ち止まっている子のまわりを通れないことにして回り道を探す
    /// ・よけないとき：階段の上・行き先のすぐ近く・手をつないで歩いている2人・座っている／寝ている子（見えない子）
    /// AvoidEnabled = false で今までどおり。
    /// </summary>
    public partial class SimCharacter
    {
        public static bool AvoidEnabled = true;
        public static float AvoidLook = 1.4f;       // 前をどこまで見るか（m）
        public static float AvoidLane = 0.55f;      // 自分の通り道の幅（片側。m）
        public static float AvoidKeep = 0.42f;      // これより近づかない（m）
        public static float AvoidDetourAfter = 1.5f;// 前がふさがって待つのがこの秒数つづいたら回り道

        float avoidBlockedFor;
        public static int AvoidSteps, AvoidWaits, AvoidDetours;   // 試験用（life avoid）

        bool IgnoreForAvoid(SimCharacter c)
        {
            if (c == null || c == this || !c.Present) return true;
            if (c.walkWith == this || walkWith == c) return true;            // 手をつないで歩いている相手
            var st = c.state;
            if (st == State.Sleeping || st == State.Away || st == State.Private || st == State.Bathing) return true;
            if (st == State.Sitting || st == State.Eating) return true;       // 席にいる子は通り道にいない
            return false;
        }

        /// <summary>この1歩の動き（dir の向きに step 進む）を、ほかの子をよける向きに直す。</summary>
        Vector3 SteerStep(Vector3 pos, Vector3 dir, float step, float dt, float distToWaypoint)
        {
            Vector3 straight = dir * step;
            if (!AvoidEnabled || IsMob && !Present) return straight;
            if (!LoungeWorld.OnFloorLevel(pos)) { avoidBlockedFor = 0f; return straight; }           // 階段の上
            if (path.Count <= 1 && distToWaypoint < 0.8f) { avoidBlockedFor = 0f; return straight; } // 行き先のすぐ近く（席に着く）
            Vector3 right = new Vector3(dir.z, 0f, -dir.x);
            Vector3 push = Vector3.zero;
            bool frontBlocked = false;
            SimCharacter blocker = null; float blockerAhead = float.MaxValue;
            foreach (var c in world.characters)
            {
                if (IgnoreForAvoid(c)) continue;
                Vector3 rel = c.transform.position - pos;
                if (Mathf.Abs(rel.y) > 1.0f) continue;
                rel.y = 0f;
                float d2 = rel.sqrMagnitude;
                if (d2 > (AvoidLook + 0.3f) * (AvoidLook + 0.3f) || d2 < 1e-6f) continue;
                float d = Mathf.Sqrt(d2);
                // 近すぎる：離れる向きへ
                if (d < AvoidKeep) push -= rel / d * (AvoidKeep - d) / AvoidKeep * 1.2f;
                float ahead = Vector3.Dot(rel, dir);
                if (ahead <= 0.05f || ahead > AvoidLook) continue;
                float lat = Vector3.Dot(rel, right);
                if (Mathf.Abs(lat) > AvoidLane) continue;
                // 同じ向きに同じくらいの速さで歩いている前の子：横に並ばず後ろについて歩く（前が近いときだけゆっくり）
                bool sameWay = (c.state == State.Walking || c.state == State.Approaching) && c.path.Count > 0 && Vector3.Dot(Flat(c.path[0] - c.transform.position).normalized, dir) > 0.7f;
                if (sameWay && ahead > AvoidKeep + 0.15f) continue;
                float w = (1f - Mathf.Abs(lat) / AvoidLane) * (1f - ahead / AvoidLook);
                // その子の反対側へ（真正面なら、通れる方。どちらも通れれば右）
                float side = Mathf.Abs(lat) > 0.05f ? -Mathf.Sign(lat) : 1f;
                push += right * side * w * 1.6f;
                if (ahead < blockerAhead) { blockerAhead = ahead; blocker = c; }
            }
            if (push.sqrMagnitude < 1e-6f) { avoidBlockedFor = Mathf.Max(0f, avoidBlockedFor - dt); return straight; }
            var nav = world.Nav(LoungeWorld.FloorOf(pos.y));
            Vector3 want = dir + Vector3.ClampMagnitude(push, 1.4f);
            want.y = 0f;
            Vector3 mv = want.normalized * step;
            if (nav.IsFree(pos + mv * 4f) && nav.IsFree(pos + mv)) { AvoidSteps++; avoidBlockedFor = Mathf.Max(0f, avoidBlockedFor - dt); return mv; }
            // 反対側を試す
            Vector3 lateral = Vector3.Project(push, right);
            Vector3 want2 = dir - lateral;
            Vector3 mv2 = want2.normalized * step;
            if (lateral.sqrMagnitude > 1e-4f && nav.IsFree(pos + mv2 * 4f) && nav.IsFree(pos + mv2)) { AvoidSteps++; return mv2; }
            // 両側ともふさがっている：前の子が近ければ待つ
            frontBlocked = blocker != null && blockerAhead < AvoidKeep + 0.35f;
            if (!frontBlocked) return straight;
            AvoidWaits++;
            avoidBlockedFor += dt;
            if (avoidBlockedFor >= AvoidDetourAfter) { avoidBlockedFor = 0f; TryDetour(); }
            return straight * 0.15f;   // ほとんど止まって待つ
        }

        /// <summary>前で止まっている子たちのまわりを通れないことにして、少し先の道の点まで回り道を探す。</summary>
        void TryDetour()
        {
            if (path.Count == 0) return;
            Vector3 pos = transform.position;
            int floor = LoungeWorld.FloorOf(pos.y);
            var nav = world.Nav(floor);
            var block = new List<Vector3>();
            foreach (var c in world.characters)
            {
                if (IgnoreForAvoid(c)) continue;
                Vector3 rel = c.transform.position - pos;
                if (Mathf.Abs(rel.y) > 1f || Flat(rel).magnitude > 3f) continue;
                if (c.state == State.Walking || c.state == State.Approaching) continue;   // 歩いている子はそのうち動く
                block.Add(c.transform.position);
            }
            if (block.Count == 0) return;
            // 合流する点：止まっている子より 1.5m 以上先の、同じ階の道の点
            int join = -1;
            for (int i = 0; i < path.Count; i++)
            {
                if (LoungeWorld.FloorOf(path[i].y) != floor) break;
                bool far = true;
                foreach (var b in block) if (Flat(path[i] - b).magnitude < 1.5f) { far = false; break; }
                if (far && Flat(path[i] - pos).magnitude > 1.2f) { join = i; break; }
            }
            if (join < 0) return;
            var detour = nav.FindPathAvoiding(pos, path[join], block, 0.5f);
            if (detour == null || detour.Count == 0) return;
            // 回り道が長すぎる（元の道の4倍＋6m より長い）なら使わない
            float orig = 0f; Vector3 prev = pos;
            for (int i = 0; i <= join; i++) { orig += Flat(path[i] - prev).magnitude; prev = path[i]; }
            float len = 0f; prev = pos;
            foreach (var p in detour) { len += Flat(p - prev).magnitude; prev = p; }
            if (len > orig * 4f + 6f) return;
            path.RemoveRange(0, join + 1);
            path.InsertRange(0, detour);
            AvoidDetours++;
        }
    }
}
