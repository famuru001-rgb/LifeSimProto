using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>欲求を変えるもとの種類（表示や、あとで整理するときの目印）。</summary>
    public enum ModSource { Race, Trait, Outfit, Accessory, Furniture, Item, Gift, Event, Other }

    /// <summary>
    /// 欲求の増え方・下がり方・たまりやすさを変える「補正」1つ分。
    /// 種族・衣装・装飾品・家具・アイテム（プレゼント含む）は、どれもこの形で書く。
    /// 倍率は 1 がふつう。書いていない欲求は変わらない。
    /// </summary>
    public class NeedMod
    {
        public string id;          // 見分けるための名前（例 "race:銀狼"、"item:安眠のお香"）
        public string name;        // 画面に出す名前
        public ModSource source;
        public string desc = "";
        public readonly Dictionary<NeedKind, float> rate = new Dictionary<NeedKind, float>();   // ゲージの増え方
        public readonly Dictionary<NeedKind, float> relief = new Dictionary<NeedKind, float>(); // ゲージの下がり方（満たしたとき）
        public readonly Dictionary<NeedKind, float> build = new Dictionary<NeedKind, float>();  // たまりのたまりやすさ
        public readonly Dictionary<NeedKind, float> drain = new Dictionary<NeedKind, float>();  // たまりの減りやすさ

        public NeedMod(string id, string name, ModSource src) { this.id = id; this.name = name; source = src; }

        public NeedMod Rate(NeedKind n, float v) { rate[n] = v; return this; }
        public NeedMod Relief(NeedKind n, float v) { relief[n] = v; return this; }
        public NeedMod Build(NeedKind n, float v) { build[n] = v; return this; }
        public NeedMod Drain(NeedKind n, float v) { drain[n] = v; return this; }
        public NeedMod Desc(string d) { desc = d; return this; }

        public static float Get(Dictionary<NeedKind, float> d, NeedKind n) { float v; return d.TryGetValue(n, out v) ? v : 1f; }
    }

    /// <summary>キャラについている補正（期限つきもある。衣装を着る・アイテムを使う・家具に座る などで足す）。</summary>
    public class ActiveMod
    {
        public NeedMod mod;
        public double until; // ゲーム内の通算分（0 以下なら外すまで続く）
    }

    /// <summary>
    /// 補正の一覧表。数字を変えるときはここだけ直す（説明は 企画\欲求の決まり.md）。
    /// 種族の値は試作用の仮の値。衣装・装飾品・家具・アイテムは、物を作るときにここへ足す。
    /// </summary>
    public static class NeedBook
    {
        static Dictionary<string, NeedMod> races;
        static Dictionary<string, NeedMod> items;

        const NeedKind Lonely = NeedKind.Lonely, Tired = NeedKind.Tired, Bored = NeedKind.Bored, Hunger = NeedKind.Hunger,
            Study = NeedKind.Study, Hobby = NeedKind.Hobby, Love = NeedKind.Love, Sleepy = NeedKind.Sleepy, Desire = NeedKind.Desire;

        static NeedMod R(string race) { var m = new NeedMod("race:" + race, race, ModSource.Race); races[race] = m; return m; }

        static void Build()
        {
            races = new Dictionary<string, NeedMod>();
            items = new Dictionary<string, NeedMod>();

            // ---- 種族（少しだけ差をつける。仮の値）----
            R("竜人").Rate(Tired, 0.9f).Rate(Hunger, 1.2f).Rate(Desire, 0.9f).Build(Desire, 1.5f).Desc("体が丈夫でよく食べる。落ち着いているが、ため込みやすい");
            R("エルフ").Rate(Hunger, 0.8f).Rate(Bored, 0.85f).Rate(Study, 1.1f).Rate(Desire, 0.8f).Build(Desire, 1.2f).Desc("少食で気長。欲は薄いが、ため込みやすい");
            R("銀狼").Rate(Lonely, 1.15f).Rate(Bored, 1.2f).Rate(Hunger, 1.3f).Rate(Tired, 0.85f).Rate(Desire, 1.1f).Build(Desire, 1.1f).Desc("群れの生き物。よく動き、よく食べる");
            R("羊").Rate(Sleepy, 1.15f).Rate(Lonely, 1.1f).Rate(Desire, 0.9f).Desc("おっとり。眠くなりやすい");
            R("天使族").Rate(Lonely, 1.1f).Rate(Hunger, 0.9f).Rate(Desire, 0.8f).Build(Desire, 1.3f).Desc("清らかで欲は薄いが、ため込みやすい");
            R("ドワーフ").Rate(Tired, 0.8f).Rate(Hunger, 1.2f).Rate(Hobby, 1.2f).Desc("疲れ知らずで、作りたい気持ちが強い");
            R("うさぎ").Rate(Lonely, 1.1f).Rate(Hunger, 1.1f).Rate(Desire, 1.2f).Build(Desire, 1.15f).Desc("甘えたがり。気持ちが高ぶりやすい");
            R("リス").Rate(Hunger, 1.3f).Rate(Bored, 1.2f).Rate(Tired, 1.1f).Desc("ちょこまか動き、よく食べる");
            R("人間"); // 基準（すべて 1）
            R("人間（転生者）");

            // ---- アイテム（使ってからしばらく効く。続く時間は PlayerControl.cs の ItemBook）----
            I("item:やすらぎのハーブティー", "やすらぎのハーブティー", ModSource.Item).Rate(Lonely, 0.7f).Rate(Bored, 0.7f).Desc("さみしさと退屈がたまりにくい");
            I("item:集中のキャンドル", "集中のキャンドル", ModSource.Item).Rate(Study, 1.4f).Relief(Study, 1.3f).Desc("勉強したい気持ちが高まり、勉強がはかどる");
            I("item:疲れ知らずの飴", "疲れ知らずの飴", ModSource.Item).Rate(Tired, 0.7f).Rate(Hunger, 0.8f).Desc("疲れと空腹がたまりにくい");
            I("item:落ち着きのお茶", "落ち着きのお茶", ModSource.Item).Build(Desire, 0.5f).Drain(Desire, 1.5f).Relief(Desire, 1.3f).Desc("気持ちが静まる（性欲のたまりがたまりにくく、減りやすい）");
            I("item:ときめきの香水", "ときめきの香水", ModSource.Item).Rate(Love, 1.3f).Rate(Lonely, 0.9f).Desc("恋心が育ちやすい");
            I("item:ぐっすりの枕香", "ぐっすりの枕香", ModSource.Item).Relief(Sleepy, 1.4f).Relief(Tired, 1.2f).Desc("眠ると眠気と疲れがよく取れる");

            // ---- プレゼント（もらってしばらく効く）----
            I("gift:花束", "花束", ModSource.Gift).Rate(Lonely, 0.8f).Desc("さみしさがたまりにくい");
            I("gift:小さなプレゼント", "小さなプレゼント", ModSource.Gift).Rate(Lonely, 0.8f).Rate(Bored, 0.9f).Desc("さみしさと退屈がたまりにくい");

            // ---- 装飾品（身につけている間ずっと効く。1人1つまで。新しく渡すと付け替える）----
            I("acc:星のペンダント", "星のペンダント", ModSource.Accessory).Rate(Lonely, 0.85f).Desc("さみしさがたまりにくい");
            I("acc:花のリボン", "花のリボン", ModSource.Accessory).Rate(Bored, 0.85f).Rate(Hobby, 1.1f).Desc("退屈しにくく、趣味を楽しみたくなる");
            I("acc:魔石の腕輪", "魔石の腕輪", ModSource.Accessory).Rate(Tired, 0.85f).Rate(Sleepy, 0.9f).Desc("疲れと眠気がたまりにくい");
            I("acc:物知りの眼鏡飾り", "物知りの眼鏡飾り", ModSource.Accessory).Rate(Study, 1.2f).Relief(Study, 1.15f).Desc("勉強したい気持ちが高まる");

            // ---- 衣装・家具（これから足す）----
            // 例）I("outfit:浴衣", "浴衣", ModSource.Outfit).Rate(Tired, 0.9f);
            //     I("furniture:暖炉の椅子", "暖炉の前の椅子", ModSource.Furniture).Relief(Tired, 1.3f);
            // ※ 性欲や発情を上げる物は作らない（ほかの子に使えてしまうため）
        }

        /// <summary>補正を一覧に登録する（衣装・装飾品・家具・アイテム用）。</summary>
        public static NeedMod I(string id, string name, ModSource src)
        {
            if (items == null) Build();
            var m = new NeedMod(id, name, src);
            items[id] = m;
            return m;
        }

        public static NeedMod Race(string race)
        {
            if (races == null) Build();
            if (string.IsNullOrEmpty(race)) return null;
            NeedMod m;
            if (races.TryGetValue(race, out m)) return m;
            foreach (var kv in races) if (race.StartsWith(kv.Key)) return kv.Value; // 「人間（転生者）」など
            return null;
        }

        public static NeedMod Item(string id)
        {
            if (items == null) Build();
            NeedMod m;
            return items.TryGetValue(id, out m) ? m : null;
        }

        /// <summary>
        /// ゲージの変動の大きさ（1 がふつう。小さいほど、増えるのも減るのもゆっくり）。
        /// さみしさ・退屈・勉強・趣味は 2026-10-21 に半分にした（famuru の希望：変動を減らす）。
        /// </summary>
        public static float Swing(NeedKind n)
        {
            switch (n)
            {
                case NeedKind.Lonely: return 0.5f;
                case NeedKind.Bored: return 0.5f;
                case NeedKind.Study: return 0.5f;
                case NeedKind.Hobby: return 0.5f;
                default: return 1f;
            }
        }

        public static IEnumerable<NeedMod> AllRaces { get { if (races == null) Build(); return races.Values; } }
        public static IEnumerable<NeedMod> AllItems { get { if (items == null) Build(); return items.Values; } }
    }
}
