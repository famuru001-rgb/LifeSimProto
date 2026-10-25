using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// エッチの能力（性技）・スキル・快感ゲージ・中出しのバフの数字（2026-10-25 famuru 決定。数字の多くは仮）。
    /// ・快感ゲージ：挿入中 1分10、愛撫中 1分5、参加（ペッティング）1分2。最大値までたまると絶頂（0に戻る）
    /// ・最大値：100。そわそわ90・悶々70・発情50。欲を高める物を使っていると −5ずつ（−10まで）。恋人 −5、篤と5回以上 −5。
    ///   その場面で絶頂するたびに −5（3回まで）。スキル1段ごとに −2。いちばん低くて10
    /// ・増え方：性技10ごとに＋10％、篤の性技が10上回るごとに＋10％（四捨五入）、スキル1段ごとに＋3％（篤のスキルも＋3％）
    /// ・スキル：エッチの通算回数（ループしても残る）が 1・3・5・8・12・16・20・25・30・40回で1段ずつ上がる（10段）
    /// ・中出しの量：2mlごとに能力すべて＋1（＋10まで）。1時間に0.1mlずつ減る（スキル1段ごとに5％ゆっくり）
    /// </summary>
    public static class HSkill
    {
        public static readonly int[] LevelAt = { 1, 3, 5, 8, 12, 16, 20, 25, 30, 40 };

        // 快感ゲージ（1分あたり）
        public static float InsertPerMin = 10f, CaressPerMin = 5f, PettingPerMin = 2f;
        public static float GaugeMin = 10f;

        // 性技の伸び
        public static float GrowPerSession = 1.5f;                  // 1回の場面ごと
        public static float GrowInsertPerMin = 0.03f, GrowCaressPerMin = 0.02f, GrowPettingPerMin = 0.01f; // 1分あたり

        // 中出しのバフ
        public static float InsideMlPerAbility = 2f, InsideMaxBonus = 10f, InsideDecayPerHour = 0.1f;

        /// <summary>スキルの段（0〜10）。エッチの通算回数で決まる。</summary>
        public static int Level(string letter)
        {
            int n = HLog.TotalOf(letter), lv = 0;
            foreach (int t in LevelAt) if (n >= t) lv++;
            return lv;
        }

        /// <summary>次の段までのあと何回（最高の段なら -1）。</summary>
        public static int ToNext(string letter)
        {
            int n = HLog.TotalOf(letter);
            foreach (int t in LevelAt) if (n < t) return t - n;
            return -1;
        }

        // ---- 篤のスキル ----
        public static float VigorMax(int lv) { return 100f + 5f * lv; }                 // 精力の最大値（10段で150）
        public static float VigorPerFinish(int lv) { return 10f * (1f - 0.04f * lv); }  // フィニッシュで減る精力（10段で6）
        public static float MlMul(int lv) { return 1f + 0.05f * lv; }                   // 量（10段で1.5倍）
        public static float PartnerSpeed(int lv) { return 1f + 0.03f * lv; }            // 相手の快感ゲージの増え方

        // ---- ヒロインのスキル ----
        public static float DesireRate(int lv) { return 1f + 0.03f * lv; }              // 性欲の増え方
        public static float GaugeMaxDrop(int lv) { return 2f * lv; }                    // 快感ゲージの最大値が下がる
        public static float GaugeSpeed(int lv) { return 1f + 0.03f * lv; }              // 快感ゲージの増え方
        public static float FinishTimeMul(int lv) { return 1f - 0.03f * lv; }           // 篤の相手のとき、篤のフィニッシュまでの時間
        public static float InsideDecayMul(int lv) { return 1f - 0.05f * lv; }          // 中出しの量の減り方

        /// <summary>性技による快感ゲージの増え方（自分の性技10ごとに＋10％、相手が10上回るごとに＋10％。四捨五入）。</summary>
        public static float SkillSpeed(float own, float partner)
        {
            float a = 1f + Mathf.Floor(own / 10f) * 0.1f;
            float b = 1f + Mathf.Round(Mathf.Max(0f, partner - own) / 10f) * 0.1f;
            return a * b;
        }

        public static string FEffectText(int lv)
        {
            return "精力の最大 " + Mathf.RoundToInt(VigorMax(lv)) + "・フィニッシュで減る精力 " + VigorPerFinish(lv).ToString("0.0") + "・量 ×" + MlMul(lv).ToString("0.00") + "・相手の快感 ×" + PartnerSpeed(lv).ToString("0.00");
        }

        public static string GirlEffectText(int lv)
        {
            return "性欲の増え方 ×" + DesireRate(lv).ToString("0.00") + "・快感の最大 −" + Mathf.RoundToInt(GaugeMaxDrop(lv)) + "・快感の増え方 ×" + GaugeSpeed(lv).ToString("0.00")
                + "・篤のフィニッシュまで ×" + FinishTimeMul(lv).ToString("0.00") + "・中出しの量の減り方 ×" + InsideDecayMul(lv).ToString("0.00");
        }
    }
}
