using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 状態でせりふの言い方を変える（語尾・ため息など）。決まりを足すときはここに書く。
    /// 吹き出しのうち「せりふ」（ひらがな・カタカナ・漢字を含み、（）で始まらない物）にだけ効く。
    /// </summary>
    public static class SpeechStyle
    {
        // ---- 決まり（数字は試作用の仮の値）----
        public const float TiredAt = 75f;      // 疲れがこれ以上で「…」
        public const float DesireAt = 70f;     // 性欲がこれ以上で「♡」（famuru 決定 2026-10-21）
        public const float SighChance = 0.3f;  // 弱っているときに、ため息から話し始める
        public const float HappyChance = 0.35f;

        public static string Apply(SimCharacter c, string s)
        {
            if (c == null || !IsSpeech(s)) return s;
            bool weak = c.tired >= TiredAt || c.GetStage(StatusLine.Cold) >= 1 || c.GetStage(StatusLine.Sleep) >= 2 || c.GetStage(StatusLine.MoodDown) >= 2;
            if (weak)
            {
                // 疲れている・病気・眠い・ひどく落ち込んでいる：語尾が「…」、ときどきため息
                s = Trim(s);
                if (!s.EndsWith("…")) s += "…";
                if (Random.value < SighChance && !s.StartsWith("はぁ")) s = "はぁ…" + s;
                return s;
            }
            if (c.desire >= DesireAt)
            {
                // 性欲が高い：語尾に♡
                if (!s.EndsWith("♡")) s = Trim(s) + "♡";
                return s;
            }
            if (c.GetStage(StatusLine.MoodUp) >= 1 && Random.value < HappyChance)
            {
                // ご機嫌：語尾に♪
                if (!s.EndsWith("♪")) s = Trim(s) + "♪";
                return s;
            }
            return s;
        }

        /// <summary>語尾の「。」「！」を取る（「？」は残す）。</summary>
        static string Trim(string s)
        {
            while (s.Length > 1)
            {
                char e = s[s.Length - 1];
                if (e == '。' || e == '！' || e == '!' || e == '♪' || e == '♡') s = s.Substring(0, s.Length - 1);
                else break;
            }
            return s;
        }

        /// <summary>せりふかどうか（♪♥・（汗）・…・！ などの記号だけの吹き出しは変えない）。</summary>
        public static bool IsSpeech(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 2) return false;
            if (s[0] == '（' || s[0] == '(' || s[0] == '「') return false;
            int letters = 0;
            foreach (char ch in s)
            {
                if ((ch >= 'ぁ' && ch <= 'ゖ') || (ch >= 'ァ' && ch <= 'ヺ') || (ch >= '一' && ch <= '鿿')) letters++;
            }
            return letters >= 2;
        }
    }
}
