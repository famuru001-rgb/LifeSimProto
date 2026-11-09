using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 揺れの当たりを足す（IK の D。2026-11-09 famuru「D に進んで」）。
    /// famuru の決定：座ったときのスカートと髪を座面・背もたれ・床に当てる／肩の上を足して背中を合わせる／
    ///   2人がくっつくとき（抱きしめる・手をつなぐ・二人きりの時間・生活の2人の動き）は、髪・スカート・胸を相手の体に当てる。
    /// しくみ：揺れの計算（UpdateSprings）で骨の先の位置が決まったあとに、1コマに1回だけ押し出す（重くならないように）。
    ///   ・相手の体：相手の胴・胸・腕・脚の見えない筒（相手の hitShapes・legShapes）の外へ
    ///   ・床：立っている床（階段では段）より下へ行かない
    ///   ・座面・背もたれ：座っているとき、座る家具の見えない当たりの面（ProtoBody.Ground.cs の層29）を通り抜けない
    /// ExtraHitEnabled = false で今までどおり。
    /// </summary>
    public partial class ProtoBody
    {
        public static bool ExtraHitEnabled = true;
        public static float ExtraMargin = 0.012f;   // 面から少し浮かせる

        bool extraSit;
        float extraFloorY;
        readonly List<ProtoBody> extraPartners = new List<ProtoBody>();
        readonly List<SimCharacter> extraTmp = new List<SimCharacter>();
        int extraPartnerHits, extraFloorHits, extraSeatHits, extraBackHits, extraFrames;
        /// <summary>試験用：前に聞いたときからの合計（聞くと0に戻る）。</summary>
        public string ExtraDebug
        {
            get
            {
                if (extraFrames == 0) return null;
                string r = extraFrames + "コマで：相手 " + extraPartners.Count + "人に " + extraPartnerHits + "回・床 " + extraFloorHits + "回・座面 " + extraSeatHits + "回・背もたれ " + extraBackHits + "回" + (extraSit ? "（座っている）" : "");
                extraPartnerHits = extraFloorHits = extraSeatHits = extraBackHits = extraFrames = 0;
                return r;
            }
        }

        /// <summary>揺れの計算の前に呼ぶ（姿勢・床の高さ・相手）。</summary>
        void PrepareExtraHits(BodyPose pose)
        {
            extraSit = pose == BodyPose.Sit;
            extraFloorY = transform.position.y + groundShift;
            extraPartners.Clear();
            if (!ExtraHitEnabled || testMode || sim == null) return;
            sim.MotionPartners(extraTmp);
            foreach (var c in extraTmp)
            {
                if (c == null) continue;
                var pb = c.GetComponent<ProtoBody>();
                if (pb != null && pb != this && (pb.transform.position - transform.position).sqrMagnitude < 4f) extraPartners.Add(pb);
            }
        }

        void ExtraDebugText() { extraFrames++; }

        /// <summary>骨の先 tip を押し出す（長さは root からの len に戻す）。</summary>
        Vector3 ExtraHit(SpringBone s, Vector3 tip, Vector3 root, float len)
        {
            if (!ExtraHitEnabled) return tip;
            Vector3 p = tip;
            // (1) 相手の体（髪・スカート・胸）
            foreach (var pb in extraPartners)
            {
                foreach (var h in pb.hitShapes) p = PushCapsule(p, h, root, len, ref extraPartnerHits);
                foreach (var h in pb.legShapes) p = PushCapsule(p, h, root, len, ref extraPartnerHits);
            }
            if (s.kind == 2) return p;   // 胸は相手の体だけ
            // (2) 床
            if (p.y < extraFloorY + ExtraMargin)
            {
                p.y = extraFloorY + ExtraMargin; extraFloorHits++;
                p = Relen(p, root, len);
                if (p.y < extraFloorY + ExtraMargin) p.y = extraFloorY + ExtraMargin;
            }
            // (3) 座っているとき：座面・背もたれ
            if (extraSit && hips != null)
            {
                RaycastHit hit;
                if (Physics.Raycast(p + Vector3.up * 0.25f, Vector3.down, out hit, 0.3f, SeatMask, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.6f && hit.point.y > p.y - 0.005f)
                {
                    p.y = hit.point.y + ExtraMargin; extraSeatHits++;
                    p = Relen(p, root, len);
                }
                Vector3 c = hips.position + transform.up * 0.18f;
                Vector3 d = p - c;
                float L = d.magnitude;
                if (L > 0.05f && Physics.Raycast(c, d / L, out hit, L, SeatMask, QueryTriggerInteraction.Ignore) && hit.normal.y < 0.6f)
                {
                    p = hit.point + hit.normal * ExtraMargin; extraBackHits++;
                    p = Relen(p, root, len);
                }
            }
            return p;
        }

        static Vector3 Relen(Vector3 p, Vector3 root, float len)
        {
            Vector3 v = p - root;
            return v.sqrMagnitude > 1e-10f ? root + v.normalized * len : p;
        }

        static Vector3 PushCapsule(Vector3 p, HitShape h, Vector3 root, float len, ref int count)
        {
            if (h.a == null) return p;
            Vector3 a = h.a.position, b = h.b.position, q;
            float d = SegDist(p, a, b, out q);
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            float r = Mathf.Lerp(h.r, h.rb, t) + ExtraMargin;
            if (d >= r || d < 1e-5f) return p;
            count++;
            return Relen(q + (p - q) / d * r, root, len);
        }
    }
}
