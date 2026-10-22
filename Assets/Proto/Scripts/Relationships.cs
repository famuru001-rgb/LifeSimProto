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

        /// <summary>最初の間柄を決める（CharProfiles.ApplyInitialRelations から）。</summary>
        public void SetInitial(int i, int j, float aff, float c)
        {
            affinity[i, j] = affinity[j, i] = Mathf.Clamp(aff, 0f, 100f);
            compat[i, j] = compat[j, i] = c;
        }

        public float Get(int i, int j) { return affinity[i, j]; }
        /// <summary>好感度をそのまま入れる（セーブの読み込み用）。</summary>
        public void SetRaw(int i, int j, float v) { affinity[i, j] = affinity[j, i] = Mathf.Clamp(v, 0f, 100f); }
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

        /// <summary>会話の結果を決める。相性がよく好感度が高いほど「楽しい」になりやすい。bonus は楽しくなりやすさの上乗せ。</summary>
        public TalkResult Roll(int i, int j, float bonus = 0f)
        {
            float c = compat[i, j];
            float a = affinity[i, j] / 100f;
            float pFun = Mathf.Clamp(0.30f + 0.35f * c + 0.25f * a + bonus, 0.05f, 0.92f);
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

        /// <summary>好感度を直接足し引きする（0〜100）。戻り値は（変化前の段階, 変化後の段階）。</summary>
        public Vector2Int Add(int i, int j, float delta, float now)
        {
            int before = Stage(i, j);
            float v = Mathf.Clamp(affinity[i, j] + delta, 0f, 100f);
            affinity[i, j] = affinity[j, i] = v;
            flashTime[i, j] = flashTime[j, i] = now;
            return new Vector2Int(before, Stage(i, j));
        }
    }

    /// <summary>
    /// みんなから1人（篤）への恋愛の気持ち。
    /// 段階：なし 0〜19 / 気になる 20〜49 / 好き 50〜100 / 恋人（告白が成功したら）。恋人は何人でもなれる。
    /// </summary>
    public class Romance
    {
        public readonly int targetId;
        public readonly float[] love;
        public readonly bool[] lover;
        public readonly double[] cooldownUntil;
        public readonly float[] flashTime;

        public const float ConfessAt = 80f;

        public Romance(int count, int targetId, int seed)
        {
            this.targetId = targetId;
            love = new float[count];
            lover = new bool[count];
            cooldownUntil = new double[count];
            flashTime = new float[count];
            var rnd = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                flashTime[i] = -100f;
                if (i == targetId) continue;
                love[i] = (float)(rnd.NextDouble() * 12.0); // はじめはみんな篤とは知り合い（気持ちは「なし」）
            }
        }

        public static int StageOf(float l, bool isLover)
        {
            if (isLover) return 3;
            return l >= 50f ? 2 : (l >= 20f ? 1 : 0);
        }

        public static string StageName(int s)
        {
            switch (s)
            {
                case 3: return "恋人";
                case 2: return "好き";
                case 1: return "気になる";
                default: return "なし";
            }
        }

        public int Stage(int i) { return i == targetId ? -1 : StageOf(love[i], lover[i]); }

        public bool HasAnyLover()
        {
            for (int i = 0; i < lover.Length; i++) if (lover[i]) return true;
            return false;
        }

        /// <summary>気持ちを足し引きする。戻り値は（変化前の段階, 変化後の段階）。</summary>
        public Vector2Int Add(int i, float delta, float now)
        {
            if (i == targetId) return new Vector2Int(-1, -1);
            int before = Stage(i);
            love[i] = Mathf.Clamp(love[i] + delta, 0f, 100f);
            if (Mathf.Abs(delta) >= 1f) flashTime[i] = now;
            return new Vector2Int(before, Stage(i));
        }
    }
}
