using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 生活のできごとの「2人の動き」（2026-11-08 モーション。famuru「モーションを入れて」）。
    /// できごとの表（できごと.csv）の「動き」の欄に「二人:形:部品[>次の部品]:M=相手|M=主役」と書くと、
    /// できごとの最中に2人を同じ基準の点に置いて、動き L_＜形＞_＜部品＞_＜M|F＞ を時刻をそろえて流す（Resources/Motions/Life）。
    /// M＝する側（肩をもむ・髪をとかす・受け止める 等。しがみつく・抱きつくでは、される側の篤）、F＝女の子の側。
    /// 「>」でつなぐと、前の部品を1回流してから次の部品をくり返す（例 TripCatch:Catch>Hold）。
    /// </summary>
    public static class LifePairBook
    {
        // 形（または 形_部品）→ 1コマ目の F の腰の位置（Blender の x, y）と向き（Blender の水平の角度・度）。作業\lpair.py で測った
        static readonly Dictionary<string, float[]> FHip = new Dictionary<string, float[]>
        {
            { "Massage", new[] { 0f, 0f, -90f } }, { "HairBrush", new[] { 0f, 0f, -90f } }, { "BackWash", new[] { 0f, 0f, -90f } },
            { "DressHelp", new[] { 0f, 0f, -90f } }, { "HeadPat", new[] { 0f, 0.2f, -90f } }, { "Kabedon", new[] { 0f, 0.235f, -90f } },
            { "TripCatch", new[] { 0f, 0.34f, -90f } }, { "TripCatch_Hold", new[] { 0f, -0.02f, -90f } },
            { "Cling", new[] { 0.26f, 0.27f, -112f } }, { "Cling_Hide", new[] { 0.32f, 0.23f, -174f } },
            { "MorningCling", new[] { 0f, 0.18f, -90f } }, { "MorningCling_ArmCling", new[] { 0.32f, -0.06f, -108f } },
            { "LapPillow", new[] { -0.55f, 0.1f, -90f } }, { "NurseCare", new[] { 0.3f, -0.15f, -90f } }, { "WakeShake", new[] { 0.4f, -0.1f, -90f } },
            { "PrincessCarry", new[] { -0.05f, -0.27f, 161.7f } }, { "HandWalk", new[] { 0.22f, 0f, -94f } }, { "ArmWalk", new[] { 0.13f, 0f, -94f } },
        };

        public class Spec { public string form; public string[] parts; public bool mIsMain; }

        public static Spec Parse(string s)
        {
            if (string.IsNullOrEmpty(s) || !s.StartsWith("二人:")) return null;
            var a = s.Split(':');
            if (a.Length < 3 || a[1].Length == 0 || a[2].Length == 0) return null;
            return new Spec { form = a[1], parts = a[2].Split('>'), mIsMain = a.Length > 3 && a[3].Trim() == "M=主役" };
        }

        /// <summary>F の側の人の位置と向きから、動きの基準の点を決める（F の腰がその人の位置に来て、その人の向きを向く）。</summary>
        public static void Anchor(Spec sp, Vector3 fPos, Quaternion fRot, out Vector3 pos, out Quaternion rot)
        {
            float[] d;
            if (!FHip.TryGetValue(sp.form + "_" + sp.parts[0], out d) && !FHip.TryGetValue(sp.form, out d)) d = new[] { 0f, 0f, -90f };
            float th = d[2] * Mathf.Deg2Rad;
            float clipYaw = Mathf.Atan2(-Mathf.Cos(th), -Mathf.Sin(th)) * Mathf.Rad2Deg;   // Blender (x,y) → Unity (−x, −y)
            rot = Quaternion.Euler(0f, fRot.eulerAngles.y - clipYaw, 0f);
            pos = fPos - rot * new Vector3(-d[0], 0f, -d[1]);
        }

        public static string Clip(Spec sp, int part, bool m) { return "L_" + sp.form + "_" + sp.parts[Mathf.Clamp(part, 0, sp.parts.Length - 1)] + "_" + (m ? "M" : "F"); }
    }
}
