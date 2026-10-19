using System.Collections.Generic;

namespace LifeSimProto
{
    public enum StatusKind { SleepDeprived, Cheerful, Down, Lovesick, Cold, ManaDepleted, Assignment, ExamEve, VigorPotion, Birthday }

    public enum NeedKind { Lonely, Tired, Bored, Hunger, Study, Hobby, Love, Sleepy, Desire }

    /// <summary>状態1つ分の効果（欲求の増え方の倍率・歩く速さ・会話の楽しさ・勉強のはかどり）。</summary>
    public class StatusDef
    {
        public string name;
        public string desc;
        public readonly Dictionary<NeedKind, float> rate = new Dictionary<NeedKind, float>();
        public float speed = 1f;
        public float talkBonus;
        public float studyEfficiency = 1f;
        public float socialScore = 1f;
        public float studyScore = 1f;
        public float funScore = 1f;
    }

    public class ActiveStatus
    {
        public StatusKind kind;
        public double until;  // ゲーム内の通算分
    }

    /// <summary>
    /// キャラの状態の一覧。数値は試作用の仮の値。
    /// </summary>
    public static class Statuses
    {
        static Dictionary<StatusKind, StatusDef> defs;

        public static StatusDef Get(StatusKind k)
        {
            if (defs == null) Build();
            return defs[k];
        }

        static StatusDef D(StatusKind k, string name, string desc)
        {
            var d = new StatusDef { name = name, desc = desc };
            defs[k] = d;
            return d;
        }

        static void Build()
        {
            defs = new Dictionary<StatusKind, StatusDef>();

            var s = D(StatusKind.SleepDeprived, "寝不足", "眠気がたまりやすく、動きが遅い。勉強がはかどらない");
            s.rate[NeedKind.Sleepy] = 1.6f; s.rate[NeedKind.Tired] = 1.3f; s.speed = 0.85f; s.studyEfficiency = 0.7f;

            s = D(StatusKind.Cheerful, "ご機嫌", "会話が楽しくなりやすく、さみしさ・退屈がたまりにくい");
            s.rate[NeedKind.Lonely] = 0.7f; s.rate[NeedKind.Bored] = 0.7f; s.talkBonus = 0.12f; s.speed = 1.05f;

            s = D(StatusKind.Down, "落ち込み", "人と話す気になれず、退屈しやすい。会話も楽しくなりにくい");
            s.rate[NeedKind.Bored] = 1.4f; s.rate[NeedKind.Lonely] = 1.2f; s.talkBonus = -0.15f; s.socialScore = 0.5f; s.speed = 0.9f;

            s = D(StatusKind.Lovesick, "恋わずらい", "Fのことで頭がいっぱい。勉強が手につかない");
            s.rate[NeedKind.Love] = 1.3f; s.studyEfficiency = 0.5f; s.studyScore = 0.6f;

            s = D(StatusKind.Cold, "風邪", "部屋で寝込んでいる。仲のいい子がお見舞いに来る");
            s.rate[NeedKind.Tired] = 1.5f; s.speed = 0.8f;

            s = D(StatusKind.ManaDepleted, "魔力切れ", "魔法を使いすぎてぐったり。魔法の練習はできない");
            s.rate[NeedKind.Tired] = 1.5f; s.speed = 0.9f;

            s = D(StatusKind.Assignment, "課題に追われている", "勉強したい気持ちがどんどん増える");
            s.rate[NeedKind.Study] = 2.5f; s.studyScore = 1.5f;

            s = D(StatusKind.ExamEve, "試験前", "明日は試験。勉強を優先し、遊びは控えめ");
            s.rate[NeedKind.Study] = 1.8f; s.studyScore = 1.5f; s.funScore = 0.7f; s.studyEfficiency = 1.3f;

            s = D(StatusKind.VigorPotion, "元気の魔法薬", "差し入れの魔法薬入りのお菓子で元気いっぱい");
            s.rate[NeedKind.Tired] = 0.3f; s.rate[NeedKind.Sleepy] = 0.6f; s.speed = 1.2f;

            s = D(StatusKind.Birthday, "誕生日", "今日は誕生日。みんなに祝ってもらえる");
            s.rate[NeedKind.Lonely] = 0.5f; s.talkBonus = 0.1f;
        }
    }
}
