using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 寮生が外に出たときの動き（2026-11-04 famuru「メインの寮のキャラが外に出ると消える。種類ごとに分ける」）。今の寮・新しい学園のどちらでも。
    /// ・散歩：学園の屋外（道・寮の前庭・本館の中庭・中庭の南の広場）を歩いて回り、時間がたったら寮へ歩いて戻る（見えたまま）
    /// ・おでかけ・アルバイト・行事の外出（街へ）：正門まで歩いてから見えなくなり、帰りは正門から歩いて戻る
    /// ・授業：その科目の教室まで歩いて、教室の中で見えなくなる（ほかの寮の学生と同じ）。帰りは教室から歩いて戻る
    /// ・部活：学校の部の場所まで歩いて、そこで活動する。終わったら寮へ歩いて戻る
    /// 道が見つからないときだけ、今までどおり寮の出口で見えなくなる。
    /// </summary>
    public partial class SimCharacter
    {
        public static float ClassLeadMin = 15f;      // 授業の何分前に寮を出るか（教室まで歩く）
        public static float StrollLegMin = 8f, StrollLegMax = 35f; // 散歩の1区間の長さ（m）

        Vector3 leaveTo;          // 見えなくなった所（帰りはここから出てくる）
        bool leaveSet;
        bool strolling;           // 散歩中（見えたまま屋外を歩く）
        double strollUntil;
        float strollPauseUntil = -1f;

        public bool Strolling { get { return strolling; } }

        /// <summary>出かけるときの行き先（見えなくなる所）。散歩は別（StartStroll）。</summary>
        Vector3 LeavePointFor()
        {
            switch (awayKind)
            {
                case AwayKind.Outing:
                case AwayKind.Job:
                    return world.NearestFree(LoungeWorld.SchoolGate);
                case AwayKind.Class:
                    {
                        bool exam = awayLabel != null && (awayLabel.Contains("試験"));
                        int sj = classSubjects.Count > 0 ? classSubjects[0] : -1;
                        Vector3 p;
                        if (MobClassPoint(exam, sj, out p)) return world.NearestFree(p);
                        return world.exitPoint;
                    }
                default:
                    return world.exitPoint;
            }
        }

        // ================= 散歩 =================
        static List<Vector3> strollPoints;

        /// <summary>散歩で歩く所（学園の道の上の点・寮の前庭・本館の中庭・中庭の南の広場）。</summary>
        List<Vector3> StrollPoints()
        {
            if (strollPoints != null && strollPoints.Count > 0) return strollPoints;
            var res = new List<Vector3>();
            var nav = world.Nav(0);
            System.Action<Vector3> add = p =>
            {
                var q = nav.NearestFreePoint(p);
                if ((q - p).sqrMagnitude < 1.5f * 1.5f) res.Add(q);
            };
            // 新しい学園：正門→中庭の入口・正門→わたしたちの寮の前庭・正門→騎士団の寮（はじめの方だけ）
            for (int k = 0; LoungeWorld.NewLayout && k < CampusPlan.Paths.Length && k < 3; k++)
            {
                var pts = CampusPlan.Paths[k].pts;
                for (int i = 0; i + 1 < pts.Length; i++)
                {
                    float len = Vector2.Distance(pts[i], pts[i + 1]);
                    int n = Mathf.Max(1, Mathf.RoundToInt(len / 6f));
                    for (int s = 0; s < n; s++) { var v = Vector2.Lerp(pts[i], pts[i + 1], s / (float)n); add(CampusPlan.G(v.x, v.y)); }
                }
            }
            // 中庭の南の広場（新しい学園）
            for (int i = 0; LoungeWorld.NewLayout && i < 6; i++) add(CampusPlan.G(Random.Range(-14f, 14f), Random.Range(33f, 43f)));
            // 寮の前庭・学校の前庭・本館の中庭・寮の中庭（開いているとき）
            foreach (var a in world.areaList)
            {
                if (a == null || a.floor != 0) continue;
                bool ok = a.name == "前庭" || a.name == "本館の中庭" || (a.name == LoungeWorld.Courtyard && world.PlaceOpen(LoungeWorld.Courtyard));
                if (ok) for (int i = 0; i < 5; i++) add(world.RandomPointIn(a));
            }
            strollPoints = res;
            return res;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStrollPoints() { strollPoints = null; }

        /// <summary>散歩に出る。歩けなければ false（今までどおり出口で見えなくなる）。</summary>
        bool StartStroll(string text)
        {
            if (awayKind != AwayKind.Stroll) return false;
            if (StrollPoints().Count == 0) { StrollWhy = "散歩の点がない"; return false; }
            strolling = true;
            strollUntil = awayUntil;
            strollPauseUntil = -1f;
            world.Log(displayName + "が" + (awayLabel == "飛び出していった" ? "寮を飛び出していった" : "散歩に出た") + "（" + ScheduleMaker.Clock(strollUntil) + "ごろまで）");
            if (!StrollLeg(text)) { strolling = false; StrollWhy = "道が見つからない（" + displayName + "・点 " + StrollPoints().Count + "）"; Debug.Log("散歩：" + StrollWhy); return false; }
            StrollWhy = "ok";
            return true;
        }
        public static string StrollWhy = "";

        /// <summary>試験用：今すぐ散歩に出す（命令 life stroll 記号）。</summary>
        public string LifeTestStroll()
        {
            if (!Present || state == State.Sleeping || InPrivate) return "今は出せない（" + state + "）";
            awayUntil = world.Now + 25.0;
            awayKind = AwayKind.Stroll;
            awayLabel = "散歩に出ている";
            StartLeave("散歩に出かける");
            return strolling ? "散歩に出た（" + ScheduleMaker.Clock(strollUntil) + "まで）" : "散歩にならなかった：" + StrollWhy ;
        }

        bool StrollLeg(string text)
        {
            var pts = StrollPoints();
            Vector3 me = transform.position;
            var near = new List<Vector3>();
            foreach (var p in pts)
            {
                float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(me.x, 0f, me.z));
                if (d >= StrollLegMin && d <= StrollLegMax) near.Add(p);
            }
            Vector3 dest;
            if (near.Count > 0) dest = near[Random.Range(0, near.Count)];
            else
            {
                // まだ寮の中：いちばん近い屋外の点へ
                dest = pts[0]; float bd = float.MaxValue;
                foreach (var p in pts) { float d = (p - me).sqrMagnitude; if (d < bd) { bd = d; dest = p; } }
            }
            LeaveCurrentSpot();
            goal = Goal.Wander;
            actionText = text ?? (awayLabel == "飛び出していった" ? "外を歩きまわっている" : "散歩している");
            return GoTo(dest, State.Walking);
        }

        /// <summary>散歩の続き（Think から）。true ならこの回はこれで終わり。</summary>
        bool StrollThink(double now)
        {
            if (!strolling) return false;
            if (hasOrder || privatePending || sickRest || state == State.Away || state == State.Sleeping || state == State.Private || state == State.Bathing) { strolling = false; return false; }
            if (state == State.Walking && goal != Goal.Wander) { strolling = false; return false; } // 予定（食事など）で別の所へ向かった
            if (state == State.Walking || state == State.Talking || state == State.Approaching || state == State.Waiting) return true;
            if (now >= strollUntil)
            {
                strolling = false;
                LeaveCurrentSpot();
                goal = Goal.Return;
                actionText = "散歩から帰っている";
                if (!GoTo(world.entrancePoint, State.Walking)) { goal = Goal.None; SetState(State.Idle); }
                return true;
            }
            // 着いたら少し立ち止まって眺めてから、次の所へ
            if (strollPauseUntil < 0f) { strollPauseUntil = Time.time + Random.Range(2f, 6f); sceneLabel = Random.value < 0.5f ? "眺め" : null; return true; }
            if (Time.time < strollPauseUntil) return true;
            strollPauseUntil = -1f;
            sceneLabel = null;
            if (!StrollLeg(null)) { strolling = false; return false; }
            return true;
        }

        // ================= 部活 =================
        /// <summary>学校の部の場所まで歩く。歩けなければ false。</summary>
        bool WalkToClub()
        {
            if (club < 0) return false;
            var areas = world.MobAreas(Clubs.Place(club, world.BadWeatherToday));
            if (areas.Count == 0) return false;
            var a = areas[Random.Range(0, areas.Count)];
            Vector3 p = world.RandomPointIn(a);
            LeaveCurrentSpot();
            clubPhase = ClubPhase.Going;
            goal = Goal.Club;
            actionText = Clubs.NameOf(club) + "の活動に向かっている";
            if (!GoTo(p, State.Walking)) { goal = Goal.None; return false; }
            return true;
        }

        /// <summary>部活が終わって寮へ歩いて帰る。</summary>
        bool WalkHomeFromClub()
        {
            LeaveCurrentSpot();
            path.Clear();
            clubPhase = ClubPhase.None;
            goal = Goal.Return;
            actionText = "部活から帰っている";
            if (!GoTo(world.entrancePoint, State.Walking)) { goal = Goal.None; SetState(State.Idle); }
            return true;
        }

        /// <summary>部の場所へ向かう途中で止まった（話しかけられた など）：もう一度向かう。</summary>
        void CheckClubGoing()
        {
            if (clubPhase != ClubPhase.Going || state == State.Away) return;
            if (state != State.Idle || hasOrder || privatePending) return;
            if (!WalkToClub()) clubPhase = ClubPhase.None;
        }

        // ================= 散歩中の欲求 =================
        void StrollNeeds(float gm)
        {
            if (!strolling || state == State.Away) return;
            hobbyNeed -= gm * 2.5f; bored -= gm * 2f;
            GrowAbility(Abilities.Body, gm * 0.004f);
        }
    }
}
