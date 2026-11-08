using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 手を相手の体に合わせる（IK の B。2026-11-08 famuru「B をためして」）。
    /// famuru の決定：動きごとの表で「どの手を相手のどこへ」を決める・まず手だけ（腰の高さはそのまま）。
    /// しくみ：二人の動きを流したあと、表の手が、相手の決めた部位（見えない筒）の近く（ReachTol 以内）にあれば、
    ///   その筒の面へ手を寄せる（離れていれば近づけ、めりこんでいれば外へ出す）。筒に沿った手の動き（なでる・もむ）は残る。
    ///   腕は2本の骨（二の腕・前腕）の曲げ方を計算して届かせる。届かないときは届く所まで。手首の向きは動きのまま。
    /// 部位の名前：頭・首・肩・胴上（胸の高さの胴）・胴下（お腹・腰）・背中（＝胴上と胴下）・胸（胸のふくらみ。無ければ胴上）・
    ///   二の腕・前腕・手・太もも（左右があるものは、近い方を使う）。「|」で並べると、いちばん近い部位へ。
    /// </summary>
    public partial class ProtoBody
    {
        public static bool ReachEnabled = true;
        public static float ReachTol = 0.35f;    // 部位の面からこれより離れている手は合わせない（動きが触っていない所）。生活のできごとの2人の動き
        public static float ReachTolH = 0.22f;   // 二人きりの時間（体が近く、手がベッドなどにあるときに引っぱらないよう狭く）
        public static float ReachPalm = 0.025f;  // 手のひらの厚み（面から少し浮かせる）
        public static float ReachFade = 0.25f;   // 合わせ始め・やめるときにかける時間（秒）

        // ---- 表（動きの名前の始め・役・左手・右手。役は名前の最後の _M／_F など）----
        class ReachRule { public string prefix, role, left, right; public ReachRule(string p, string r, string l, string rt) { prefix = p; role = r; left = l; right = rt; } }

        static readonly ReachRule[] LifeRules =
        {
            new ReachRule("L_Massage_", "M", "肩", "肩"),                         // 肩もみ：もむ側の両手 → 相手の両肩の上
            new ReachRule("L_HairBrush_", "M", "頭", "頭"),                       // 髪をとかす：右手（くし）→ 頭の後ろ・左手 → 頭の横
            new ReachRule("L_BackWash_", "M", "背中", "背中"),                    // 背中を流す
            new ReachRule("L_DressHelp_", "M", "首|胴上", "首|胴上"),             // 着替えの手伝い：えり・胸の前
            new ReachRule("L_HeadPat_", "M", "頭", "頭"),                         // 頭をなでる
            new ReachRule("L_TripCatch_", "M", "背中", "背中"),                   // 転びかけて受け止める：背中・腰
            new ReachRule("L_Cling_", "F", "胴上|二の腕", "胴上|二の腕"),         // しがみつく：篤の胸・二の腕
            new ReachRule("L_MorningCling_ArmCling", "F", "二の腕|前腕", "二の腕|前腕"), // 腕に抱きつく：篤の二の腕
            new ReachRule("L_MorningCling_", "F", "胴下|胴上", "胴下|胴上"),      // 後ろから抱きつく：篤のお腹の前
            new ReachRule("L_WakeShake_", "M", "肩|二の腕", "肩|二の腕"),         // ゆすって起こす
            new ReachRule("L_NurseCare_", "M", "頭|胴上", "頭|胴上"),             // 看病
            new ReachRule("L_PrincessCarry_", "M", "背中|太もも", "背中|太もも"), // お姫さま抱っこ：背中とひざの裏
            new ReachRule("L_PrincessCarry_", "F", "首|肩", "首|肩"),             //   抱かれる側は首・肩へ
            new ReachRule("L_HandWalk_", "F", "手", "手"),                        // 手をつなぐ（女の子の側が篤の手に合わせる）
            new ReachRule("L_ArmWalk_", "F", "二の腕|前腕", "二の腕|前腕"),       // 腕を組む
        };

        // 二人きりの時間は形の種類ごと（HMotionBook.Family）。M＝篤、G＝女の子（F・P・S・H）
        static readonly Dictionary<string, string[]> HRules = new Dictionary<string, string[]>
        {
            { "kiss:M", new[] { "背中|胴下", "背中|胴下" } },  { "kiss:G", new[] { "首|肩|背中", "首|肩|背中" } },
            { "pet:M", new[] { "胸|胴|太もも", "胸|胴|太もも" } }, { "pet:G", new[] { "肩|二の腕|胴", "肩|二の腕|胴" } },
            { "start:M", new[] { "肩|二の腕|胴", "肩|二の腕|胴" } }, { "start:G", new[] { "胴上|肩", "胴上|肩" } },
            { "after:M", new[] { "肩|背中|頭", "肩|背中|頭" } }, { "after:G", new[] { "胴上", "胴上" } },
            { "oral:M", new[] { "頭|肩", "頭|肩" } },            { "oral:G", new[] { "太もも|胴下", "太もも|胴下" } },
            { "ride:M", new[] { "胴下|太もも|胸", "胴下|太もも|胸" } }, { "ride:G", new[] { "胴上|二の腕", "胴上|二の腕" } },
            { "seat:M", new[] { "背中", "背中" } },               { "seat:G", new[] { "首|肩|背中", "首|肩|背中" } },
            { "mis:M", new[] { "胴下|太もも|肩", "胴下|太もも|肩" } }, { "mis:G", new[] { "肩|二の腕|胴上", "肩|二の腕|胴上" } },
        };

        /// <summary>動きの名前から、左手・右手の部位（無ければ null）。</summary>
        static bool RulesFor(string clip, out string left, out string right)
        {
            left = right = null;
            if (string.IsNullOrEmpty(clip)) return false;
            if (clip.StartsWith("L_"))
            {
                foreach (var r in LifeRules)
                    if (clip.StartsWith(r.prefix) && clip.EndsWith("_" + r.role)) { left = r.left; right = r.right; return true; }
                return false;
            }
            if (clip.StartsWith("H_"))
            {
                var a = clip.Split('_');   // H_形_部品_役
                if (a.Length < 4) return false;
                string fam = HMotionBook.Family(a[1]);
                string role = a[a.Length - 1] == "M" ? "M" : "G";
                string[] v;
                if (!HRules.TryGetValue(fam + ":" + role, out v)) return false;
                left = v[0]; right = v[1]; return true;
            }
            return false;
        }

        // ---- 部位（見えない筒。太さは肩幅から）----
        float partW = -1f;

        float ShoulderW()
        {
            if (partW > 0f) return partW;
            Transform la = B("LeftArm"), ra = B("RightArm");
            partW = la != null && ra != null ? Mathf.Max(0.2f, (la.position - ra.position).magnitude) : 0.3f;
            return partW;
        }

        /// <summary>この体の部位の面で、点 p にいちばん近い所。部位が無ければ false。</summary>
        public bool PartSurface(string group, Vector3 p, out Vector3 surf, out float gap, out string hit)
        {
            surf = p; gap = float.MaxValue; hit = null;
            if (bones == null || hips == null) return false;
            float w = ShoulderW();
            bool any = false;
            foreach (var name in group.Split('|'))
            {
                switch (name)
                {
                    case "頭":
                        {
                            Transform h = B("Head");
                            if (h == null) break;
                            Transform top = B("HeadTop_End");
                            Vector3 a = h.position, b = top != null ? top.position : h.position + transform.up * 0.18f;
                            Try(p, Vector3.Lerp(a, b, 0.35f), Vector3.Lerp(a, b, 0.65f), w * 0.30f, "頭", ref surf, ref gap, ref hit, ref any);
                            break;
                        }
                    case "首": Seg(p, "Neck", "Head", w * 0.14f, "首", ref surf, ref gap, ref hit, ref any); break;
                    case "肩":
                        Seg(p, "LeftShoulder", "LeftArm", w * 0.15f, "左肩", ref surf, ref gap, ref hit, ref any);
                        Seg(p, "RightShoulder", "RightArm", w * 0.15f, "右肩", ref surf, ref gap, ref hit, ref any);
                        break;
                    case "胴上": TorsoUp(p, w, ref surf, ref gap, ref hit, ref any); break;
                    case "胴下": TorsoLow(p, w, ref surf, ref gap, ref hit, ref any); break;
                    case "背中": case "胴":
                        TorsoUp(p, w, ref surf, ref gap, ref hit, ref any);
                        TorsoLow(p, w, ref surf, ref gap, ref hit, ref any);
                        break;
                    case "胸":
                        {
                            bool chest = false;
                            foreach (var s in springs)
                                if (s.child != null && BaseName(s.bone).StartsWith("Spring_Chest")) { Try(p, s.bone.position, s.child.position, w * 0.24f, "胸", ref surf, ref gap, ref hit, ref any); chest = true; }
                            if (!chest) TorsoUp(p, w, ref surf, ref gap, ref hit, ref any);
                            break;
                        }
                    case "二の腕":
                        Seg(p, "LeftArm", "LeftForeArm", w * 0.17f, "左の二の腕", ref surf, ref gap, ref hit, ref any);
                        Seg(p, "RightArm", "RightForeArm", w * 0.17f, "右の二の腕", ref surf, ref gap, ref hit, ref any);
                        break;
                    case "前腕":
                        Seg(p, "LeftForeArm", "LeftHand", w * 0.13f, "左の前腕", ref surf, ref gap, ref hit, ref any);
                        Seg(p, "RightForeArm", "RightHand", w * 0.13f, "右の前腕", ref surf, ref gap, ref hit, ref any);
                        break;
                    case "手":
                        Seg(p, "LeftHand", "LeftHandMiddle1", w * 0.08f, "左手", ref surf, ref gap, ref hit, ref any);
                        Seg(p, "RightHand", "RightHandMiddle1", w * 0.08f, "右手", ref surf, ref gap, ref hit, ref any);
                        break;
                    case "太もも":
                        Seg(p, "LeftUpLeg", "LeftLeg", w * 0.22f, "左の太もも", ref surf, ref gap, ref hit, ref any);
                        Seg(p, "RightUpLeg", "RightLeg", w * 0.22f, "右の太もも", ref surf, ref gap, ref hit, ref any);
                        break;
                }
            }
            return any;
        }

        void TorsoUp(Vector3 p, float w, ref Vector3 surf, ref float gap, ref string hit, ref bool any)
        {
            Transform sp2 = B("Spine2") ?? B("Spine1") ?? B("Spine");
            Transform nk = B("Neck");
            if (sp2 != null && nk != null) Try(p, sp2.position, Vector3.Lerp(sp2.position, nk.position, 0.7f), w * 0.33f, "胸の高さの胴", ref surf, ref gap, ref hit, ref any);
        }

        void TorsoLow(Vector3 p, float w, ref Vector3 surf, ref float gap, ref string hit, ref bool any)
        {
            Transform sp2 = B("Spine2") ?? B("Spine1") ?? B("Spine");
            if (sp2 != null) Try(p, hips.position, sp2.position, w * 0.38f, "お腹・腰", ref surf, ref gap, ref hit, ref any);
        }

        void Seg(Vector3 p, string a, string b, float r, string label, ref Vector3 surf, ref float gap, ref string hit, ref bool any)
        {
            Transform ta = B(a), tb = B(b);
            if (ta == null) return;
            Try(p, ta.position, tb != null ? tb.position : ta.position, r, label, ref surf, ref gap, ref hit, ref any);
        }

        static void Try(Vector3 p, Vector3 a, Vector3 b, float r, string label, ref Vector3 surf, ref float gap, ref string hit, ref bool any)
        {
            Vector3 q;
            float d = SegDist(p, a, b, out q);
            float g = d - r;
            if (Mathf.Abs(g) >= Mathf.Abs(gap) && any) return;
            Vector3 n = d > 1e-4f ? (p - q) / d : Vector3.up;
            surf = q + n * (r + ReachPalm);
            gap = g; hit = label; any = true;
        }

        // ---- 毎コマ ----
        readonly List<SimCharacter> reachPartners = new List<SimCharacter>();
        float reachWL, reachWR;
        bool reachIsH;
        public string ReachDebug { get; private set; }

        /// <summary>二人の動きを流したあとに呼ぶ（動きの名前 clip）。</summary>
        void UpdateReach(string clip, float dt, bool on)
        {
            ReachDebug = null;
            reachIsH = clip != null && clip.StartsWith("H_");
            string lg = null, rg = null;
            bool want = on && ReachEnabled && !testMode && sim != null && RulesFor(clip, out lg, out rg);
            if (want) { sim.MotionPartners(reachPartners); if (reachPartners.Count == 0) want = false; }
            if (!want) { reachWL = Mathf.MoveTowards(reachWL, 0f, dt / ReachFade); reachWR = Mathf.MoveTowards(reachWR, 0f, dt / ReachFade); if (reachWL <= 0f && reachWR <= 0f) return; }
            if (!AnyVisible()) return;   // 映っていない子は計算しない（揺れと同じ考え方）
            string dl = ReachHand(true, want ? lg : null, dt, ref reachWL);
            string dr = ReachHand(false, want ? rg : null, dt, ref reachWR);
            ReachDebug = "左手：" + dl + "／右手：" + dr;
        }

        string ReachHand(bool left, string group, float dt, ref float w)
        {
            Transform up = B(left ? "LeftArm" : "RightArm"), lo = B(left ? "LeftForeArm" : "RightForeArm"), hand = B(left ? "LeftHand" : "RightHand");
            if (up == null || lo == null || hand == null) return "骨なし";
            Vector3 target = hand.position; string hit = null; float gap = 0f;
            bool near = false;
            if (group != null)
            {
                foreach (var c in reachPartners)
                {
                    if (c == null) continue;
                    var pb = c.GetComponent<ProtoBody>();
                    if (pb == null || pb == this) continue;
                    Vector3 s; float g; string h;
                    if (!pb.PartSurface(group, hand.position, out s, out g, out h)) continue;
                    if (!near || Mathf.Abs(g) < Mathf.Abs(gap)) { target = s; gap = g; hit = c.displayName + "の" + h; near = true; }
                }
                if (near && gap > (reachIsH ? ReachTolH : ReachTol)) near = false;   // 遠い：動きはここでは触っていない
            }
            w = Mathf.MoveTowards(w, near ? 1f : 0f, dt / ReachFade);
            if (w <= 0f) return near ? "合わせ始め" : "なし";
            if (!near) target = hand.position;
            Quaternion keep = hand.rotation;
            Vector3 goal = Vector3.Lerp(hand.position, target, w);
            TwoBoneIK(up, lo, hand, goal);
            hand.rotation = keep;
            float rest = (hand.position - goal).magnitude;
            return hit != null ? hit + "（" + (gap * 100f).ToString("0") + "cm を合わせた・残り " + (rest * 100f).ToString("0.0") + "cm）" : "もどしている";
        }

        /// <summary>二の腕・前腕の曲げ方を変えて、手首を target へ（届かなければ届く所まで）。ひじの曲がる向きは動きのまま。</summary>
        static void TwoBoneIK(Transform a, Transform b, Transform c, Vector3 t) { TwoBoneIK(a, b, c, t, null); }

        /// <summary>pole：まっすぐ伸びているとき、先（手首・足首）をどちらへ曲げるか（ひざは足首が後ろへ＝-forward）。</summary>
        static void TwoBoneIK(Transform a, Transform b, Transform c, Vector3 t, Vector3? pole)
        {
            Vector3 pa = a.position, pb = b.position, pc = c.position;
            float lab = (pb - pa).magnitude, lcb = (pc - pb).magnitude;
            if (lab < 1e-4f || lcb < 1e-4f) return;
            // (1) ひじ：肩から手首までの長さが、肩から target までの長さになるように曲げる
            float lat = Mathf.Clamp((t - pa).magnitude, Mathf.Abs(lab - lcb) + 0.01f, lab + lcb - 0.002f);
            Vector3 ba = (pa - pb) / lab, bc = (pc - pb) / lcb;
            float now = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ba, bc), -1f, 1f));
            float want = Mathf.Acos(Mathf.Clamp((lab * lab + lcb * lcb - lat * lat) / (2f * lab * lcb), -1f, 1f));
            Vector3 axis = Vector3.Cross(ba, bc);
            bool straight = now > 165f * Mathf.Deg2Rad;
            if (pole.HasValue && straight) axis = Vector3.Cross(bc, pole.Value);   // ほぼまっすぐ：曲げる向きを決める
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(ba, a.forward);   // まっすぐ伸びているとき
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(ba, Vector3.up);
            axis.Normalize();
            float delta = (want - now) * Mathf.Rad2Deg;
            // 回す向きを確かめる（開く・閉じるのどちらか合う方）
            Vector3 test = Quaternion.AngleAxis(delta, axis) * bc;
            if (pole.HasValue && straight) { if (Vector3.Dot(test - bc, pole.Value) < 0f) delta = -delta; }
            else if (Mathf.Abs(Mathf.Acos(Mathf.Clamp(Vector3.Dot(ba, test), -1f, 1f)) - want) > 0.01f) delta = -delta;
            b.rotation = Quaternion.AngleAxis(delta, axis) * b.rotation;
            // (2) 肩：手首が target の方を向くように腕全体を回す
            Vector3 cur = c.position - pa, to = t - pa;
            if (cur.sqrMagnitude > 1e-8f && to.sqrMagnitude > 1e-8f) a.rotation = Quaternion.FromToRotation(cur, to) * a.rotation;
        }
    }
}
