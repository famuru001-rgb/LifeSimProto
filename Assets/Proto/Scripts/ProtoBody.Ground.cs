using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 足の裏を床・階段・坂に合わせる／座ったときにお尻を座面へ（IK の C。2026-11-09 famuru「C をすすめて」）。
    /// famuru の決定：座る（椅子・ソファ・ベンチ）・階段（足を段ごとに置く）・立っているときの床・外の坂と段差の全部。
    /// しくみ：
    ///   ・階段・座る家具・外の地面（Island_Ground など）に、見えない当たりの面（GroundLayer の層）を最初に1回作る
    ///   ・体の真下の地面の高さへ体を上げ下げ（階段の段・坂）。座っているときは、お尻が座面の高さに来るよう上げ下げ
    ///   ・それぞれの足の真下の地面の高さを調べ、床に着いている足（足首が低い所）は足の裏が地面に着くように、
    ///     ももとすねの曲げ方で届かせる。届かない（下の段）ときは腰を少し下げる。浮かせている足（歩くけり出し）は高さの差だけ足す
    ///   ・当たりの面が無い所は、今までどおり立っている床の高さ
    /// 画面に映っている子だけ計算する（揺れと同じ考え方）。GroundEnabled = false で今までどおり。
    /// </summary>
    public partial class ProtoBody
    {
        public static bool GroundEnabled = true;
        public const int GroundLayer = 30;
        public static float GroundSmooth = 14f;     // 体の上げ下げの追いつく速さ
        public static float FootPlantBand = 0.06f;  // 足首がこの高さより低ければ「床に着いている」
        public static float FootFadeBand = 0.10f;   // そこから、この幅で浮いている足へ移る
        public static float SeatButt = 0.065f;      // 腰の骨から座面までの高さ（背の高さに対する割合）

        static bool groundBuilt;
        static int GroundMask { get { return 1 << GroundLayer; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetGround() { groundBuilt = false; }

        static readonly string[] GroundWords = { "Stair", "_Step", "Island_Ground", "Ground", "Beach", "Shore", "Slope", "Ramp", "Terrace", "Bridge" };

        static bool NameHas(Transform t, string[] words)
        {
            for (int k = 0; t != null && k < 4; k++, t = t.parent)
                foreach (var w in words) if (t.name.Contains(w)) return true;
            return false;
        }

        /// <summary>見えない当たりの面を作る（1回だけ）。座る所の家具は、その席の上にかかっている形を使う。</summary>
        static void BuildGround(LoungeWorld w)
        {
            groundBuilt = true;
            if (w == null) return;
            var seatPts = new List<Vector3>();
            foreach (var s in w.seats) if (s != null && s.kind == SeatKind.Sit && !s.floorSeat) seatPts.Add(s.position);
            foreach (var s in w.classSeats) if (s != null) seatPts.Add(s.position);
            int nG = 0, nS = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (mf == null || mf.sharedMesh == null || !mf.gameObject.activeInHierarchy) continue;
                if (mf.GetComponentInParent<ProtoBody>() != null) continue;     // キャラの体は除く
                var r = mf.GetComponent<Renderer>();
                if (r == null) continue;
                bool ground = NameHas(mf.transform, GroundWords);
                bool seat = false;
                if (!ground)
                {
                    var b = r.bounds;
                    if (b.size.y < 2.5f && b.size.x < 4.5f && b.size.z < 4.5f)
                        foreach (var p in seatPts)
                            if (p.x > b.min.x - 0.05f && p.x < b.max.x + 0.05f && p.z > b.min.z - 0.05f && p.z < b.max.z + 0.05f && b.min.y < p.y + 0.35f && b.max.y > p.y + 0.2f) { seat = true; break; }
                }
                if (!ground && !seat) continue;
                var go = new GameObject("IK_Hit");
                go.layer = GroundLayer;
                go.transform.SetParent(mf.transform, false);
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                if (ground) nG++; else nS++;
            }
            Debug.Log("足の合わせ：当たりの面 地面 " + nG + "・座る家具 " + nS);
            GroundInfo = "地面 " + nG + "・座る家具 " + nS;
        }
        public static string GroundInfo = "まだ";

        /// <summary>p の真下（上 up m から down m まで）の当たりの面の高さ。無ければ false。</summary>
        static bool GroundBelow(Vector3 p, float up, float down, out float y, out Vector3 n)
        {
            RaycastHit h;
            if (Physics.Raycast(p + Vector3.up * up, Vector3.down, out h, up + down, GroundMask, QueryTriggerInteraction.Ignore))
            {
                y = h.point.y; n = h.normal;
                // 床より下の当たり（建物の下の島の地面など）は、階段でなければ使わない（床の下へ沈まないように）
                if (y < p.y - 0.02f && !NameHas(h.collider.transform.parent, StairWords)) { y = p.y; n = Vector3.up; return false; }
                return true;
            }
            y = p.y; n = Vector3.up; return false;
        }
        static readonly string[] StairWords = { "Stair", "_Step" };

        float ankleH = -1f;           // 足首の骨の高さ（立った形で床から）
        float groundShift, pelvisDrop;
        public string GroundDebug { get; private set; }

        void CaptureAnkle()
        {
            var f = B("LeftFoot");
            if (f != null && model != null) ankleH = Mathf.Clamp(f.position.y - model.position.y, 0.03f, 0.2f);
        }

        /// <summary>動きを流したあとに呼ぶ。</summary>
        void UpdateGround(float dt, BodyPose pose, bool on)
        {
            GroundDebug = null;
            if (!groundBuilt && sim != null && sim.World != null && sim.World.seats.Count > 0) BuildGround(sim.World);
            bool sit = pose == BodyPose.Sit;
            bool stand = pose == BodyPose.Stand;
            if (!on || !GroundEnabled || testMode || sim == null || !(sit || stand || pose == BodyPose.SitFloor) || hips == null)
            {
                groundShift = Mathf.Lerp(groundShift, 0f, 1f - Mathf.Exp(-dt * GroundSmooth)); pelvisDrop = 0f;
                if (Mathf.Abs(groundShift) > 0.002f && model != null) model.position += Vector3.up * groundShift;
                return;
            }
            if (!AnyVisible() || !BigOnScreen()) return;
            if (ankleH < 0f) ankleH = 0.08f * Mathf.Max(1f, height) / 1.6f;
            float baseY = transform.position.y;
            float k = 1f - Mathf.Exp(-dt * GroundSmooth);
            string why = "床";

            // (1) 体の上げ下げ：座っている＝お尻を座面へ、立っている＝体の真下の地面（階段の段・坂）へ
            float want = 0f;
            if (sit)
            {
                var s = sim.SeatNow;
                float top; Vector3 nn;
                Vector3 hp = new Vector3(hips.position.x, baseY, hips.position.z);
                if (s != null && GroundBelow(hp, 1.0f, 0.2f, out top, out nn) && top > baseY + 0.12f && top < baseY + 0.95f)
                {
                    float butt = hips.position.y - SeatButt * Mathf.Max(1f, height);
                    want = Mathf.Clamp(top - butt, -0.35f, 0.35f);
                    why = "座面 " + ((top - baseY) * 100f).ToString("0") + "cm";
                }
                else why = "座面が見つからない";
            }
            else if (stand)
            {
                float gy; Vector3 nn;
                if (GroundBelow(new Vector3(transform.position.x, baseY, transform.position.z), 0.6f, 0.6f, out gy, out nn)) { want = Mathf.Clamp(gy - baseY, -0.5f, 0.5f); why = "地面 " + ((gy - baseY) * 100f).ToString("0") + "cm"; }
            }
            groundShift = Mathf.Lerp(groundShift, want, k);
            if (Mathf.Abs(groundShift) > 0.001f) model.position += Vector3.up * groundShift;

            // (2) 足：床に着いている足は足の裏を地面へ。届かない分は腰を下げる
            float dropNeed = 0f;
            var tl = FootGoal("Left", baseY, out float wl, out string gl);
            var tr = FootGoal("Right", baseY, out float wr, out string gr2);
            Transform fl = B("LeftFoot"), fr = B("RightFoot");
            if (stand)
            {
                if (fl != null && wl > 0.5f) dropNeed = Mathf.Min(dropNeed, tl.y - fl.position.y);
                if (fr != null && wr > 0.5f) dropNeed = Mathf.Min(dropNeed, tr.y - fr.position.y);
                dropNeed = Mathf.Max(dropNeed, -0.25f);
            }
            pelvisDrop = Mathf.Lerp(pelvisDrop, dropNeed, k);
            if (pelvisDrop < -0.001f) { hips.position += Vector3.up * pelvisDrop; tl.y += 0f; }
            string dl = LegIK("Left", tl, wl), dr = LegIK("Right", tr, wr);
            GroundDebug = why + "・体 " + (groundShift * 100f).ToString("0") + "cm・腰 " + (pelvisDrop * 100f).ToString("0") + "cm・左足 " + gl + dl + "・右足 " + gr2 + dr;
        }

        /// <summary>足首の行き先（w＝合わせる強さ。床に着いている足ほど 1）。</summary>
        Vector3 FootGoal(string side, float baseY, out float w, out string info)
        {
            w = 0f; info = "";
            Transform f = B(side + "Foot");
            if (f == null) return Vector3.zero;
            Vector3 p = f.position;
            float gy; Vector3 n;
            bool hit = GroundBelow(new Vector3(p.x, baseY + groundShift, p.z), 0.6f, 0.6f, out gy, out n);
            if (!hit) gy = baseY;
            float above = p.y - gy - ankleH;                          // 足首が、着いたときの高さよりどれだけ上か
            float plant = 1f - Mathf.Clamp01((above - FootPlantBand) / FootFadeBand);
            Vector3 goal = p;
            if (above < 0f) { goal.y = gy + ankleH; w = 1f; }         // めりこんでいる：必ず上へ
            else if (plant > 0f) { goal.y = Mathf.Lerp(p.y, gy + ankleH, plant); w = plant; }
            info = hit ? "段/坂" : "床";
            return goal;
        }

        string LegIK(string side, Vector3 goal, float w)
        {
            if (w <= 0.001f) return "";
            Transform a = B(side + "UpLeg"), b = B(side + "Leg"), c = B(side + "Foot");
            if (a == null || b == null || c == null) return "";
            Quaternion keep = c.rotation;
            float before = goal.y - c.position.y;
            TwoBoneIK(a, b, c, goal, -transform.forward);
            c.rotation = keep;
            return "（" + (before * 100f).ToString("0") + "cm）";
        }
    }
}
