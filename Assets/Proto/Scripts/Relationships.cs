using UnityEngine;

namespace LifeSimProto
{
    public enum TalkResult { Fun, Normal, Awkward }

    /// <summary>
    /// キャラ同士の好感度（0〜100、マイナスにならない）と相性を管理する。
    /// 段階：知り合い 0〜39 / 友達 40〜79 / 親友 80〜100。
    /// </summary>
    public class Relationships
    {
        public readonly int count;
        readonly float[,] affinity;
        readonly float[,] compat;
        readonly float[,] flashTime;

        public const float FunDelta = 5f, NormalDelta = 1f, AwkwardDelta = -3f;

        public Relationships(int count, int seed)
        {
            this.count = count;
            affinity = new float[count, count];
            compat = new float[count, count];
            flashTime = new float[count, count];
            var rnd = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    // 最初の関係は「知り合い(20)」か「友達(50)」（試作用の仮の割り振り）
                    float start = rnd.NextDouble() < 0.4 ? 50f : 20f;
                    // 相性 -0.6〜0.8（試作用の仮の値）
                    float c = (float)(rnd.NextDouble() * 1.4 - 0.6);
                    affinity[i, j] = affinity[j, i] = start;
                    compat[i, j] = compat[j, i] = c;
                    flashTime[i, j] = flashTime[j, i] = -100f;
                }
            }
        }

        public float Get(int i, int j) { return affinity[i, j]; }
        public float Compat(int i, int j) { return compat[i, j]; }
        public float LastChange(int i, int j) { return flashTime[i, j]; }

        public static int StageOf(float a) { return a >= 80f ? 2 : (a >= 40f ? 1 : 0); }

        public static string StageName(int s)
        {
            switch (s)
            {
                case 2: return "親友";
                case 1: return "友達";
                default: return "知り合い";
            }
        }

        public int Stage(int i, int j) { return StageOf(affinity[i, j]); }

        /// <summary>会話の結果を決める。相性がよく好感度が高いほど「楽しい」になりやすい。</summary>
        public TalkResult Roll(int i, int j)
        {
            float c = compat[i, j];
            float a = affinity[i, j] / 100f;
            float pFun = Mathf.Clamp(0.30f + 0.35f * c + 0.25f * a, 0.05f, 0.85f);
            float pAwk = Mathf.Clamp(0.22f - 0.22f * c - 0.12f * a, 0.03f, 0.5f);
            float r = Random.value;
            if (r < pFun) return TalkResult.Fun;
            if (r < pFun + pAwk) return TalkResult.Awkward;
            return TalkResult.Normal;
        }

        /// <summary>好感度を変える。戻り値は（変化前の段階, 変化後の段階）。</summary>
        public Vector2Int Apply(int i, int j, TalkResult result, float now)
        {
            float delta = result == TalkResult.Fun ? FunDelta : (result == TalkResult.Normal ? NormalDelta : AwkwardDelta);
            int before = Stage(i, j);
            float v = Mathf.Clamp(affinity[i, j] + delta, 0f, 100f);
            affinity[i, j] = affinity[j, i] = v;
            flashTime[i, j] = flashTime[j, i] = now;
            return new Vector2Int(before, Stage(i, j));
        }
    }
}
