using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>状態異常の系統。1つの系統につき、同時に1段階だけ持つ。</summary>
    public enum StatusLine { MoodDown, MoodUp, Sleep, Love, Cold, Lonely, Frustration, Focus, Shy, Jealousy, Mana, ManaOut, Full, Desire }

    public enum NeedKind { Lonely, Tired, Bored, Hunger, Study, Hobby, Love, Sleepy, Desire }

    public enum StatusPolarity { Negative, Positive, Love, Body, Magic }

    /// <summary>1段階分の効果。rate は欲求の増え方の倍率。</summary>
    public class StageEffect
    {
        public readonly Dictionary<NeedKind, float> rate = new Dictionary<NeedKind, float>();
        public float speed = 1f;          // 歩く速さ
        public float talkBonus;           // 会話の楽しくなりやすさ
        public float studyEfficiency = 1f;
        public float socialScore = 1f;    // 人に会いに行きたい気持ちの倍率
        public float studyScore = 1f;
        public float funScore = 1f;
        public float loveScore = 1f;
        public float privateScore = 1f;
        public bool noTalk;               // 話しかけられても気づかない
        public string desc;
    }

    /// <summary>系統の定義（名前・色・段階ごとの効果・続く時間）。数値は試作用の仮の値。</summary>
    public class StatusLineDef
    {
        public StatusLine line;
        public StatusPolarity polarity;
        public string[] names;
        public StageEffect[] stages;
        public float[] minutes;          // 段階ごとに続く時間（ゲーム内の分）。0 は条件で外れるまで
        public bool decays = true;       // 時間切れで1段階下がる（false なら消える）

        public int MaxStage { get { return names.Length; } }
        public string NameOf(int stage) { return names[Mathf.Clamp(stage, 1, names.Length) - 1]; }
        public StageEffect EffectOf(int stage) { return stages[Mathf.Clamp(stage, 1, stages.Length) - 1]; }

        public Color Color
        {
            get
            {
                switch (polarity)
                {
                    case StatusPolarity.Negative: return new Color(0.62f, 0.55f, 0.95f);
                    case StatusPolarity.Positive: return new Color(1f, 0.82f, 0.3f);
                    case StatusPolarity.Love: return new Color(1f, 0.55f, 0.72f);
                    case StatusPolarity.Body: return new Color(0.6f, 0.82f, 0.68f);
                    default: return new Color(0.45f, 0.8f, 1f);
                }
            }
        }
    }

    public class ActiveStatus
    {
        public StatusLine line;
        public int stage;
        public double until;     // ゲーム内の通算分（0 以下なら期限なし）
        public float timer;      // 悪化の判定などに使う（ゲーム内の分）
    }

    /// <summary>
    /// 状態異常の一覧。名前のルール：1段階目はふだんの言葉、2段階目は少し強い言葉、3段階目は漢字2〜3字の強い言葉。
    /// 色：マイナスは青紫、プラスは金、恋は桃、体調は灰緑、魔力は水色。新しい系統はここに足していく。
    /// </summary>
    public static class StatusBook
    {
        static Dictionary<StatusLine, StatusLineDef> defs;

        public static StatusLineDef Get(StatusLine l)
        {
            if (defs == null) Build();
            return defs[l];
        }

        public static IEnumerable<StatusLineDef> All
        {
            get { if (defs == null) Build(); return defs.Values; }
        }

        static StageEffect E(string desc) { return new StageEffect { desc = desc }; }

        static StatusLineDef Def(StatusLine l, StatusPolarity p, string[] names, float[] minutes, params StageEffect[] st)
        {
            var d = new StatusLineDef { line = l, polarity = p, names = names, minutes = minutes, stages = st };
            defs[l] = d;
            return d;
        }

        static void Build()
        {
            defs = new Dictionary<StatusLine, StatusLineDef>();
            StageEffect a, b, c;

            // ---- 気分（マイナス） ----
            a = E("会話が楽しくなりにくく、退屈しやすい"); a.rate[NeedKind.Bored] = 1.3f; a.rate[NeedKind.Lonely] = 1.2f; a.talkBonus = -0.12f; a.socialScore = 0.7f; a.speed = 0.95f;
            b = E("人を避けがち。勉強もはかどらない"); b.rate[NeedKind.Bored] = 1.5f; b.rate[NeedKind.Lonely] = 1.4f; b.talkBonus = -0.2f; b.socialScore = 0.4f; b.studyEfficiency = 0.7f; b.funScore = 0.7f; b.speed = 0.9f;
            c = E("何も手につかず、食事も抜きがち"); c.rate[NeedKind.Bored] = 1.6f; c.rate[NeedKind.Lonely] = 1.6f; c.rate[NeedKind.Hunger] = 0.5f; c.talkBonus = -0.3f; c.socialScore = 0.2f; c.studyEfficiency = 0.5f; c.funScore = 0.5f; c.speed = 0.85f;
            Def(StatusLine.MoodDown, StatusPolarity.Negative, new[] { "落ち込み", "憂うつ", "絶不調" }, new[] { 360f, 720f, 1440f }, a, b, c);

            // ---- 気分（プラス） ----
            a = E("会話が楽しくなりやすく、眠気がたまりにくい"); a.rate[NeedKind.Lonely] = 0.8f; a.rate[NeedKind.Bored] = 0.8f; a.rate[NeedKind.Sleepy] = 0.85f; a.talkBonus = 0.1f;
            b = E("足取りが軽い。短い睡眠ですむ"); b.rate[NeedKind.Lonely] = 0.7f; b.rate[NeedKind.Bored] = 0.7f; b.rate[NeedKind.Sleepy] = 0.7f; b.talkBonus = 0.15f; b.speed = 1.05f; b.socialScore = 1.2f;
            c = E("自分から話しかけに行き、まわりもご機嫌になる"); c.rate[NeedKind.Lonely] = 0.6f; c.rate[NeedKind.Bored] = 0.6f; c.rate[NeedKind.Sleepy] = 0.55f; c.rate[NeedKind.Tired] = 0.8f; c.talkBonus = 0.2f; c.speed = 1.1f; c.socialScore = 1.4f;
            Def(StatusLine.MoodUp, StatusPolarity.Positive, new[] { "ご機嫌", "上機嫌", "絶好調" }, new[] { 240f, 360f, 480f }, a, b, c);

            // ---- 睡眠 ----
            a = E("眠気がたまりやすく、動きが少し遅い"); a.rate[NeedKind.Sleepy] = 1.4f; a.rate[NeedKind.Tired] = 1.2f; a.speed = 0.9f; a.studyEfficiency = 0.8f;
            b = E("昼寝したくてたまらない"); b.rate[NeedKind.Sleepy] = 1.7f; b.rate[NeedKind.Tired] = 1.4f; b.speed = 0.85f; b.studyEfficiency = 0.6f;
            c = E("どこでも居眠りしてしまう"); c.rate[NeedKind.Sleepy] = 2f; c.rate[NeedKind.Tired] = 1.6f; c.speed = 0.8f; c.studyEfficiency = 0.4f;
            Def(StatusLine.Sleep, StatusPolarity.Negative, new[] { "寝不足", "睡魔", "限界" }, new[] { 0f, 0f, 0f }, a, b, c).decays = false;

            // ---- 恋 ----
            a = E("篤のことを考えてしまう"); a.rate[NeedKind.Love] = 1.3f; a.studyEfficiency = 0.8f; a.studyScore = 0.8f;
            b = E("篤を目で追い、まわりをうろつく"); b.rate[NeedKind.Love] = 1.6f; b.studyEfficiency = 0.6f; b.studyScore = 0.6f; b.loveScore = 1.3f;
            Def(StatusLine.Love, StatusPolarity.Love, new[] { "恋わずらい", "恋の病" }, new[] { 720f, 720f }, a, b);

            // ---- 性欲（たまりで段階が決まる。Buildup.cs）----
            a = E("なんだか落ち着かない"); a.studyEfficiency = 0.9f; a.studyScore = 0.9f;
            b = E("もやもやして、勉強も手につかない"); b.studyEfficiency = 0.7f; b.studyScore = 0.7f; b.funScore = 0.9f;
            c = E("二人きりになりたくてたまらない"); c.rate[NeedKind.Desire] = 3f; c.studyEfficiency = 0.5f; c.studyScore = 0.6f; c.privateScore = 1.5f;
            Def(StatusLine.Desire, StatusPolarity.Love, new[] { "そわそわ", "悶々", "発情" }, new[] { 0f, 0f, 0f }, a, b, c).decays = false;

            // ---- 体調 ----
            a = E("疲れやすく、話した相手にうつることがある"); a.rate[NeedKind.Tired] = 1.5f; a.rate[NeedKind.Hunger] = 0.8f; a.speed = 0.9f; a.talkBonus = -0.05f;
            b = E("部屋で寝込んでいる。お見舞いが来る"); b.rate[NeedKind.Tired] = 2f;
            Def(StatusLine.Cold, StatusPolarity.Body, new[] { "風邪", "発熱" }, new[] { 900f, 0f }, a, b);

            // ---- 孤独 ----
            a = E("誰でもいいから話したい"); a.rate[NeedKind.Lonely] = 1.3f; a.socialScore = 1.6f;
            b = E("仲のいい子にべったりくっついている"); b.rate[NeedKind.Lonely] = 1.5f; b.socialScore = 2f;
            Def(StatusLine.Lonely, StatusPolarity.Negative, new[] { "人恋しい", "孤独" }, new[] { 360f, 480f }, a, b);

            // ---- 鬱憤 ----
            a = E("何か面白いことがしたい"); a.rate[NeedKind.Bored] = 1.3f; a.funScore = 1.3f;
            b = E("いらいらしている"); b.rate[NeedKind.Bored] = 1.5f; b.funScore = 1.6f; b.talkBonus = -0.1f;
            c = E("もう我慢できない！"); c.rate[NeedKind.Bored] = 1.5f;
            Def(StatusLine.Frustration, StatusPolarity.Negative, new[] { "もやもや", "鬱憤", "爆発" }, new[] { 360f, 360f, 60f }, a, b, c);

            // ---- 集中 ----
            a = E("勉強や趣味がはかどる"); a.studyEfficiency = 1.3f;
            b = E("話しかけられても気づかない"); b.studyEfficiency = 1.6f; b.noTalk = true;
            c = E("時間を忘れて没頭している"); c.studyEfficiency = 2f; c.noTalk = true; c.rate[NeedKind.Sleepy] = 0.7f; c.rate[NeedKind.Hunger] = 0.7f;
            Def(StatusLine.Focus, StatusPolarity.Positive, new[] { "集中", "没頭", "ゾーン" }, new[] { 0f, 0f, 0f }, a, b, c).decays = false;

            // ---- 照れ ----
            a = E("篤の近くにいたいけど、少し距離をとる"); a.talkBonus = 0.1f;
            b = E("篤の顔が見られない"); b.rate[NeedKind.Love] = 1.2f;
            Def(StatusLine.Shy, StatusPolarity.Love, new[] { "照れ", "赤面" }, new[] { 60f, 90f }, a, b);

            // ---- やきもち（仲は悪くならない） ----
            a = E("ちょっと気になる"); a.rate[NeedKind.Love] = 1.2f;
            b = E("篤に会いに行きたくなる"); b.rate[NeedKind.Love] = 1.5f; b.loveScore = 1.3f;
            c = E("篤の会話に割り込みたくなる"); c.rate[NeedKind.Love] = 1.8f; c.loveScore = 1.6f;
            Def(StatusLine.Jealousy, StatusPolarity.Love, new[] { "ちくっ", "やきもち", "嫉妬" }, new[] { 180f, 240f, 360f }, a, b, c);

            // ---- 魔力 ----
            a = E("魔力が満ちている。勉強も少しはかどる"); a.studyEfficiency = 1.1f;
            b = E("魔力があふれそう。じっとしていられない"); b.rate[NeedKind.Bored] = 1.2f; b.funScore = 1.2f;
            c = E("魔力が暴走する！"); c.speed = 1.1f;
            Def(StatusLine.Mana, StatusPolarity.Magic, new[] { "魔力充実", "魔力過多", "魔力暴走" }, new[] { 0f, 0f, 0f }, a, b, c).decays = false;

            a = E("魔力を使い果たしてぐったり。魔法は使えない"); a.rate[NeedKind.Tired] = 1.5f; a.speed = 0.9f;
            Def(StatusLine.ManaOut, StatusPolarity.Magic, new[] { "魔力切れ" }, new[] { 360f }, a);

            // ---- 満腹 ----
            a = E("おなかいっぱいで眠くなる"); a.rate[NeedKind.Sleepy] = 1.3f; a.rate[NeedKind.Hunger] = 0.5f; a.speed = 0.95f;
            b = E("食べすぎて動けない"); b.rate[NeedKind.Sleepy] = 1.6f; b.rate[NeedKind.Tired] = 1.2f; b.rate[NeedKind.Hunger] = 0.3f; b.speed = 0.85f;
            Def(StatusLine.Full, StatusPolarity.Body, new[] { "満腹", "食べすぎ" }, new[] { 90f, 150f }, a, b);
        }
    }
}
