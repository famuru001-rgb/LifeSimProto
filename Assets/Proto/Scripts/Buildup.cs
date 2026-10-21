using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 欲求の「たまり」の決まり（画面には出さない、0〜100）。
    /// ゲージが高いあいだ少しずつたまり、たまりの量で状態異常の段階が変わる。
    /// たまりが多いほど、満たしてもゲージが下がりにくい。いちばん上の段階のあいだは、たまりやすく、ゲージが低くても少しずつたまる。
    /// ほかの欲求にも使えるように、欲求ごとの数字はここで決める（BuildupBook）。
    /// </summary>
    public class BuildupDef
    {
        public NeedKind need;
        public StatusLine line;
        public float highAt = 60f;            // ゲージがこれ以上でたまる
        public float rateFloor = 40f;         // たまる速さ ＝ 最大の速さ ×（ゲージ − rateFloor）／（100 − rateFloor）
        public float fillMinutesAtMax = 360f; // ゲージ100のままなら、この分数で空からいっぱいになる
        public float decayPerMin = 0.1f;      // ゲージが highAt より下のとき減る量（1分あたり）
        public float[] stageAt = { 40f, 70f, 100f }; // 各段階になるたまり
        public float keepMargin = 5f;         // 段階が下がるのは、区切りよりこれだけ下がってから（ちらつき防止）
        public float reliefAtFull = 0.25f;    // たまり100のとき、ゲージの下がり方の倍率
        public float topRateMul = 2f;         // いちばん上の段階のあいだ、たまる速さの倍率
        public float topCreepPerMin = 0.05f;  // いちばん上の段階のあいだ、ゲージが低くてもたまる量（1分あたり）
    }

    public class Buildup
    {
        public readonly BuildupDef def;
        public float value;

        public Buildup(BuildupDef d) { def = d; }

        /// <summary>ゲージの下がり方の倍率（たまりが多いほど小さい）。</summary>
        public float Relief { get { return Mathf.Lerp(1f, def.reliefAtFull, value / 100f); } }

        public int TopStage { get { return def.stageAt.Length; } }

        /// <summary>ゲーム内 gm 分ぶん進める。drain は満たす行動で減らす量（1分あたり）、rateMul はたまりやすさの倍率（種族・衣装・アイテムなど）。</summary>
        public void Tick(float gm, float gauge, int stage, float drain, float rateMul = 1f)
        {
            bool top = stage >= TopStage;
            float add = 0f;
            if (gauge >= def.highAt)
                add = (100f / def.fillMinutesAtMax) * Mathf.Clamp01((gauge - def.rateFloor) / (100f - def.rateFloor)) * (top ? def.topRateMul : 1f);
            else
                add = top ? def.topCreepPerMin : -def.decayPerMin;
            if (add > 0f) add *= rateMul;
            value = Mathf.Clamp(value + gm * (add - drain), 0f, 100f);
        }

        /// <summary>たまりの量から段階を決める（今の段階は少し下がるまで保つ。いちばん上は1つ下の区切りまで保つ）。</summary>
        public int StageFor(int current)
        {
            int s = 0;
            for (int i = 0; i < def.stageAt.Length; i++) if (value >= def.stageAt[i]) s = i + 1;
            if (current > s)
            {
                float keep = current >= TopStage && TopStage >= 2 ? def.stageAt[TopStage - 2] : def.stageAt[current - 1] - def.keepMargin;
                if (value >= keep) s = current;
            }
            return s;
        }
    }

    public static class BuildupBook
    {
        /// <summary>性欲：60以上でたまる（100なら約6時間、80なら約9時間、60なら約18時間）。40・60・80で そわそわ・悶々・発情。</summary>
        public static readonly BuildupDef Desire = new BuildupDef
        {
            need = NeedKind.Desire,
            line = StatusLine.Desire,
            highAt = 60f,
            rateFloor = 40f,
            fillMinutesAtMax = 360f,
            decayPerMin = 0.1f,
            stageAt = new[] { 40f, 60f, 80f }, // 発情は80から（famuru 決定 2026-10-21）
            reliefAtFull = 0.25f,
            topRateMul = 2f,
            topCreepPerMin = 0.05f,
        };
    }
}
