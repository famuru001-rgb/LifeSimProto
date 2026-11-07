using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 二人きりの時間・自慰の動きを、本番のゲームで流す（2026-11-04 famuru「まだ入れていなかったので入れて」）。
    /// 動きは Resources/Motions/H（名前 H_＜形＞_＜部品＞_＜役＞。役：M＝篤、F＝相手、P・S・H＝4人のときのほかの子）。
    /// 形と部品の並べ方は確認の場（HPreview.cs）と同じ。全員を同じ基準の点（ベッドの上・床）に置き、動きの時刻をそろえて流す。
    /// </summary>
    public static class HMotionBook
    {
        /// <summary>ベッドの上の高さ（床から。ベッドの上で流す形の基準）。</summary>
        public static float BedTop = 0.5f;
        /// <summary>形ごとの向きの補正（度。ベッドの向きに合わせる）。</summary>
        public static float YawBed = 0f, YawFloor = 0f, YawSolo = 180f;   // ひとりの時間は頭を枕の方へ（2026-11-04 写真で確かめた）

        /// <summary>立位（2026-11-04 モーション。2人だけ）。どこでもできる形と、壁・机を使う形。</summary>
        public static readonly string[] StandFree = { "StandFront", "StandSide", "StandCarry", "StandSplit" };
        /// <summary>壁の形：女の腰の基準から壁の面まで（m）。机の形：女の腰の基準から机の手前の縁まで（m。動きは奥行き0.7mの机で作った）。</summary>
        public static float StandWallBack = 0.78f, StandDeskEdge = 0.14f;
        /// <summary>立位で壁・机を使う割合（壁はシャワーと、ほかの場所で近くに壁があるとき。机は個室）。</summary>
        public static float StandWallChanceShower = 0.5f, StandWallChance = 0.25f, StandDeskChance = 0.35f;

        public class Step { public string part; public float speed; public int count; public Step(string p, float s, int c) { part = p; speed = s; count = c; } }

        public static string Family(string form)
        {
            if (form.StartsWith("Ride")) return "ride";
            if (form.StartsWith("Seat")) return "seat";
            if (form.StartsWith("Solo")) return "solo";
            return "mis";
        }

        public static List<Step>[] Phases(string family)
        {
            var r = new List<Step>[4];
            switch (family)
            {
                case "ride":
                    r[0] = new List<Step> { new Step("Grind", 0.6f, 3), new Step("BounceSmall", 0.7f, 2) };
                    r[1] = new List<Step> { new Step("BounceSmall", 1.1f, 1), new Step("Bounce", 1.1f, 1), new Step("BounceSmall", 1.1f, 1), new Step("Bounce", 1.1f, 2), new Step("Circle", 0.9f, 1) };
                    r[2] = new List<Step> { new Step("Bounce", 1.8f, 6), new Step("Grind", 1.7f, 3) };
                    break;
                case "seat":
                    r[0] = new List<Step> { new Step("Rock", 0.6f, 3), new Step("Circle", 0.7f, 1) };
                    r[1] = new List<Step> { new Step("RockSmall", 1.1f, 1), new Step("Rock", 1.1f, 1), new Step("RockSmall", 1.1f, 1), new Step("Rock", 1.1f, 2), new Step("Bounce", 0.9f, 2) };
                    r[2] = new List<Step> { new Step("Rock", 1.9f, 6), new Step("Bounce", 1.5f, 2) };
                    break;
                case "solo":
                    r[0] = new List<Step> { new Step("Slow", 0.9f, 3), new Step("Slow", 1.3f, 2) };
                    r[1] = new List<Step> { new Step("Fast", 1.0f, 5) };
                    r[2] = new List<Step> { new Step("Fast", 1.4f, 4) };
                    break;
                default:
                    r[0] = new List<Step> { new Step("Deep", 0.7f, 3), new Step("Grind", 0.8f, 1) };
                    r[1] = new List<Step> { new Step("Deep", 1.2f, 2), new Step("Shallow", 1.2f, 1), new Step("Deep", 1.2f, 2) };
                    r[2] = new List<Step> { new Step("Shallow", 2.1f, 6), new Step("Deep", 1.5f, 2) };
                    break;
            }
            r[3] = new List<Step> { new Step("Finish", 1f, 1) };
            return r;
        }

        /// <summary>体位の名前 → 形（2人・4人）。立位は2人だけの形（4人の形は null。壁・机の形は置く所が決まってから選び直す）。</summary>
        public static void FormsFor(string pose, int seed, out string two, out string four)
        {
            switch (pose)
            {
                case "騎乗位": two = "Ride"; four = "Ride4"; break;
                case "後背位": two = "Doggy"; four = "Doggy4"; break;
                case "立位": two = StandFree[seed % StandFree.Length]; four = null; break;
                case "対面座位": two = "Seat"; four = "Seat4"; break;
                default: two = (seed & 1) == 0 ? "MisA" : "MisB"; four = "MisB3"; break;
            }
        }

        public static readonly string[] GroupRigs = { "P", "S", "H" };
        public static string Clip(string form, string part, string rig) { return "H_" + form + "_" + part + "_" + rig; }
    }

    /// <summary>形の部品を順に流す（前半・中盤・後半・フィニッシュ）。時刻は全員で共通。</summary>
    public class HMotionPlayer
    {
        public string form;
        List<HMotionBook.Step>[] phases;
        public int phaseNow { get; private set; }
        int stepIx, rep;
        public float t { get; private set; }
        bool hold;
        readonly System.Func<string, AnimationClip> find;
        /// <summary>効果音と声（HSound.cs。2026-11-05）：鳴らす場所と、声を出す子の記号（null なら声なし）。</summary>
        public Vector3 soundPos; public string voiceLetter; public bool sound = true;
        readonly HVoice voice = new HVoice(); bool insertPending;

        public HMotionPlayer(string form, System.Func<string, AnimationClip> find)
        {
            this.find = find;
            SetForm(form);
        }

        public void SetForm(string f)
        {
            if (f == form) return;
            string fam0 = form != null ? HMotionBook.Family(form) : null;
            form = f;
            if (HMotionBook.Family(f) != fam0) { phases = HMotionBook.Phases(HMotionBook.Family(f)); SetPhase(phaseNow); }
        }

        public void SetPhase(int p) { phaseNow = Mathf.Clamp(p, 0, 3); stepIx = 0; rep = 0; t = 0f; hold = false; if (phaseNow == 0) insertPending = true; }

        HMotionBook.Step Cur { get { var l = phases[phaseNow]; return l[Mathf.Clamp(stepIx, 0, l.Count - 1)]; } }
        public string Part { get { return Cur.part; } }

        AnimationClip CurClip(string rig) { return find != null ? find(HMotionBook.Clip(form, Cur.part, rig)) : null; }

        /// <summary>進める（実時間 dt。speedMul は状況での速さ）。</summary>
        public void Update(float dt, float speedMul)
        {
            if (phases == null) return;
            bool snd = sound && HSound.Enabled && dt <= Mathf.Max(Time.deltaTime, 0.0001f) * HSound.MaxFastForward;   // 早送りのときは鳴らさない
            if (snd && voiceLetter != null) voice.Tick(voiceLetter, phaseNow, Time.deltaTime, soundPos);
            if (hold) return;
            var c = CurClip(HMotionBook.Family(form) == "solo" ? "F" : "M") ?? CurClip("F");
            float len = c != null ? Mathf.Max(0.1f, c.length) : 1f;
            float fps = c != null && c.frameRate > 1f ? 30f / c.frameRate : 1f;   // 24コマ/秒 で記録された物は、コマ数どおりの長さで流す
            if (snd && insertPending && HMotionBook.Family(form) != "solo") HSound.Play("insert", soundPos, 0.8f);
            insertPending = false;
            float t0 = t;
            t += dt * Cur.speed * speedMul * fps;
            if (snd) HSound.Events(form, Cur.part, t0 / len, Mathf.Min(t, len) / len, Cur.speed * speedMul, soundPos);
            if (t < len) return;
            if (Cur.part == "Finish") { t = len; hold = true; return; }   // フィニッシュは1回で止まる（次の場面まで）
            rep++;
            if (rep >= Cur.count) { rep = 0; stepIx++; }
            if (stepIx >= phases[phaseNow].Count) stepIx = 0;
            t = 0f;
        }

        public string ClipFor(string rig) { return HMotionBook.Clip(form, Cur.part, rig); }
    }
}
