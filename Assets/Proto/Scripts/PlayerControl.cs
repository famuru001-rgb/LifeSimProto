using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    public enum ItemKind
    {
        Sweets, Bouquet, Present, CheerPotion, WakePotion, SleepIncense, ColdMedicine, LoveCharm,
        // 欲求を変えるアイテム（しばらく効く）
        HerbTea, FocusCandle, StaminaCandy, CalmTea, Perfume, PillowScent,
        // 装飾品（身につけている間ずっと効く）
        StarPendant, FlowerRibbon, ManaBracelet, GlassesCharm,
        // 装飾品：よい所と困った所がある
        SunBrooch, NightEarring, SwiftAnklet, HeartyRing, AdventFeather, ScholarBookmark, CrafterThimble, MoonVeil, ClearBeads,
        // 装飾品：能力値・学力を伸ばす
        LionBracelet, RoseChoker, ArtisanRing, ManaCrystal, SageMonocle, StudyBadge,
        // お守り（その人を意識しやすくなる。記号 A〜J）
        CharmA, CharmB, CharmC, CharmD, CharmE, CharmF, CharmG, CharmH, CharmI, CharmJ,
        // 寮生の私物（寮生が自分で買って使う。篤は買えない・渡せない）
        PassionAroma, NightCandle, LongingOil
    }

    /// <summary>購買部の棚（表示の分け方）。</summary>
    public enum ItemShelf { Basic, Needs, Gift, Accessory, Growth, Charm, Personal }
    public enum RequestKind { Meet, Study, Rest, Teach }

    public class ItemDef
    {
        public ItemKind kind;
        public string name;
        public int price;
        public string desc;
        public bool selfUse;     // Fが自分に使える
        public bool oncePerDay;  // 1日1回まで
        public string modId;     // 欲求の補正（NeedMods.cs の NeedBook。なければ補正なし）
        public float modMinutes; // 補正が続く時間（ゲーム内の分。0 なら身につけている間ずっと）
        public bool accessory;   // 装飾品（1人2つまで）・お守り（1人1つまで）。新しく渡すと付け替える
        public ItemShelf shelf;  // 購買部の棚
        public bool npcOnly;     // 寮生の私物：寮生が自分で買って使うだけ（篤は買えない・渡せない・使えない）
        public NeedMod Mod { get { return string.IsNullOrEmpty(modId) ? null : NeedBook.Item(modId); } }
    }

    /// <summary>購買部の品物。数値は試作用の仮の値。（あとで実習の探索でも手に入るようにする予定）</summary>
    public static class ItemBook
    {
        static List<ItemDef> list;

        public static List<ItemDef> All { get { if (list == null) Build(); return list; } }

        public static ItemDef Get(ItemKind k)
        {
            foreach (var d in All) if (d.kind == k) return d;
            return null;
        }

        static void Build()
        {
            list = new List<ItemDef>
            {
                new ItemDef { kind = ItemKind.Sweets, name = "お菓子", price = 10, desc = "好感度＋3・空腹−30", selfUse = true , shelf = ItemShelf.Basic },
                new ItemDef { kind = ItemKind.Bouquet, name = "花束", price = 40, desc = "好感度＋6・篤への気持ち＋5・ときどきご機嫌・半日さみしさがたまりにくい", modId = "gift:花束", modMinutes = 720f , shelf = ItemShelf.Gift },
                new ItemDef { kind = ItemKind.Present, name = "小さなプレゼント", price = 80, desc = "好感度＋10・篤への気持ち＋8・気分が1段階上がる・1日さみしさと退屈がたまりにくい", modId = "gift:小さなプレゼント", modMinutes = 1440f , shelf = ItemShelf.Gift },
                new ItemDef { kind = ItemKind.CheerPotion, name = "元気の薬", price = 50, desc = "落ち込み系を1段階回復（なければご機嫌）・疲れ−30", selfUse = true , shelf = ItemShelf.Basic },
                new ItemDef { kind = ItemKind.WakePotion, name = "目覚めの薬", price = 40, desc = "寝不足系を治す・眠気−50", selfUse = true , shelf = ItemShelf.Basic },
                new ItemDef { kind = ItemKind.SleepIncense, name = "安眠のお香", price = 30, desc = "今夜はぐっすり眠れる（翌朝の寝不足を防ぐ）", selfUse = true , shelf = ItemShelf.Basic },
                new ItemDef { kind = ItemKind.ColdMedicine, name = "風邪薬", price = 30, desc = "風邪を1段階治す", selfUse = true , shelf = ItemShelf.Basic },
                new ItemDef { kind = ItemKind.LoveCharm, name = "恋のおまじない", price = 120, desc = "篤を「気になる」以上の子だけに効く。恋心＋30・恋わずらい（1日1回）", oncePerDay = true , shelf = ItemShelf.Gift },
                // 欲求を変えるアイテム
                new ItemDef { kind = ItemKind.HerbTea, name = "やすらぎのハーブティー", price = 25, desc = "半日、さみしさと退屈がたまりにくい", selfUse = true, modId = "item:やすらぎのハーブティー", modMinutes = 720f , shelf = ItemShelf.Needs },
                new ItemDef { kind = ItemKind.FocusCandle, name = "集中のキャンドル", price = 30, desc = "半日、勉強したい気持ちが高まり、勉強がはかどる", selfUse = true, modId = "item:集中のキャンドル", modMinutes = 720f , shelf = ItemShelf.Needs },
                new ItemDef { kind = ItemKind.StaminaCandy, name = "疲れ知らずの飴", price = 20, desc = "半日、疲れと空腹がたまりにくい", selfUse = true, modId = "item:疲れ知らずの飴", modMinutes = 720f , shelf = ItemShelf.Needs },
                new ItemDef { kind = ItemKind.CalmTea, name = "落ち着きのお茶", price = 30, desc = "1日、気持ちが静まる（性欲のたまりがたまりにくく減りやすい）", selfUse = true, modId = "item:落ち着きのお茶", modMinutes = 1440f , shelf = ItemShelf.Needs },
                new ItemDef { kind = ItemKind.Perfume, name = "ときめきの香水", price = 60, desc = "1日、恋心が育ちやすい（篤を「気になる」以上の子に）", modId = "item:ときめきの香水", modMinutes = 1440f , shelf = ItemShelf.Gift },
                new ItemDef { kind = ItemKind.PillowScent, name = "ぐっすりの枕香", price = 35, desc = "1日、眠ると眠気と疲れがよく取れる", selfUse = true, modId = "item:ぐっすりの枕香", modMinutes = 1440f , shelf = ItemShelf.Needs },
                // 装飾品（プレゼント。身につけている間ずっと効く）
                new ItemDef { kind = ItemKind.StarPendant, name = "星のペンダント", price = 150, desc = "装飾品：さみしさがたまりにくい・好感度＋8", selfUse = true, accessory = true, modId = "acc:星のペンダント" , shelf = ItemShelf.Accessory },
                new ItemDef { kind = ItemKind.FlowerRibbon, name = "花のリボン", price = 120, desc = "装飾品：退屈しにくく、趣味を楽しみたくなる・好感度＋8", selfUse = true, accessory = true, modId = "acc:花のリボン" , shelf = ItemShelf.Accessory },
                new ItemDef { kind = ItemKind.ManaBracelet, name = "魔石の腕輪", price = 180, desc = "装飾品：疲れと眠気がたまりにくい・好感度＋8", selfUse = true, accessory = true, modId = "acc:魔石の腕輪" , shelf = ItemShelf.Accessory },
                new ItemDef { kind = ItemKind.GlassesCharm, name = "物知りの眼鏡飾り", price = 140, desc = "装飾品：勉強したい気持ちが高まる・好感度＋8", selfUse = true, accessory = true, modId = "acc:物知りの眼鏡飾り" , shelf = ItemShelf.Accessory },
                // 装飾品：よい所と困った所がある（バフ 0.75・デバフ 1.2）
                new ItemDef { kind = ItemKind.SunBrooch, name = "陽だまりのブローチ", price = 100, desc = "装飾品：さみしさがたまりにくいが、眠くなりやすい", selfUse = true, accessory = true, modId = "acc:陽だまりのブローチ", shelf = ItemShelf.Accessory },
                new ItemDef { kind = ItemKind.NightEarring, name = "夜ふかしのイヤリング", price = 100, desc = "装飾品：眠くなりにくいが、疲れやすい", selfUse = true, accessory = true, modId = "acc:夜ふかしのイヤリング", shelf = ItemShelf.Accessory },
                new ItemDef { kind = ItemKind.SwiftAnklet, name = "韋駄天のアンクレット", price = 100, desc = "装飾品：疲れにくいが、おなかがすきやすい", selfUse = true, accessory = true, modId = "acc:韋駄天のアンクレット", shelf = ItemShelf.Accessory },
                new ItemDef { kind = ItemKind.HeartyRing, name = "腹持ちの指輪", price = 90, desc = "装飾品：おなかがすきにくいが、退屈しやすい", selfUse = true, accessory = true, modId = "acc:腹持ちの指輪", shelf = ItemShelf.Accessory },
                new ItemDef { kind = ItemKind.AdventFeather, name = "冒険家の羽飾り", price = 100, desc = "装飾品：退屈しにくいが、勉強する気になりにくい", selfUse = true, accessory = true, modId = "acc:冒険家の羽飾り", shelf = ItemShelf.Accessory },
                new ItemDef { kind = ItemKind.ScholarBookmark, name = "学者のしおり飾り", price = 110, desc = "装飾品：勉強したい気持ちが高まるが、趣味がしたくて落ち着かない", selfUse = true, accessory = true, modId = "acc:学者のしおり飾り", shelf = ItemShelf.Accessory },
                new ItemDef { kind = ItemKind.CrafterThimble, name = "職人の指ぬき", price = 90, desc = "装飾品：趣味で満たされやすいが、恋心が募りやすい", selfUse = true, accessory = true, modId = "acc:職人の指ぬき", shelf = ItemShelf.Accessory },
                new ItemDef { kind = ItemKind.MoonVeil, name = "月影のヴェール", price = 110, desc = "装飾品：恋心が落ち着くが、人恋しくなりやすい", selfUse = true, accessory = true, modId = "acc:月影のヴェール", shelf = ItemShelf.Accessory },
                new ItemDef { kind = ItemKind.ClearBeads, name = "清めの数珠", price = 100, desc = "装飾品：心が静まる（性欲がたまりにくい）が、退屈しやすい", selfUse = true, accessory = true, modId = "acc:清めの数珠", shelf = ItemShelf.Accessory },
                // 装飾品：能力値・学力を伸ばす（増やすだけ）
                new ItemDef { kind = ItemKind.LionBracelet, name = "獅子の腕輪", price = 220, desc = "装飾品：体力が伸びやすい・体力＋5", selfUse = true, accessory = true, modId = "acc:獅子の腕輪", shelf = ItemShelf.Growth },
                new ItemDef { kind = ItemKind.RoseChoker, name = "薔薇のチョーカー", price = 220, desc = "装飾品：魅力が伸びやすい・魅力＋5", selfUse = true, accessory = true, modId = "acc:薔薇のチョーカー", shelf = ItemShelf.Growth },
                new ItemDef { kind = ItemKind.ArtisanRing, name = "細工師の指輪", price = 220, desc = "装飾品：器用さが伸びやすい・器用さ＋5", selfUse = true, accessory = true, modId = "acc:細工師の指輪", shelf = ItemShelf.Growth },
                new ItemDef { kind = ItemKind.ManaCrystal, name = "魔晶のペンダント", price = 220, desc = "装飾品：魔力が伸びやすい・魔力＋5", selfUse = true, accessory = true, modId = "acc:魔晶のペンダント", shelf = ItemShelf.Growth },
                new ItemDef { kind = ItemKind.SageMonocle, name = "賢者の片眼鏡", price = 220, desc = "装飾品：知力が伸びやすい・知力＋5", selfUse = true, accessory = true, modId = "acc:賢者の片眼鏡", shelf = ItemShelf.Growth },
                new ItemDef { kind = ItemKind.StudyBadge, name = "学びのバッジ", price = 220, desc = "装飾品：学力（科目）が伸びやすい", selfUse = true, accessory = true, modId = "acc:学びのバッジ", shelf = ItemShelf.Growth },
                // お守り（装飾品とは別に1つまで）
                new ItemDef { kind = ItemKind.CharmA, name = "ヴィエッタのお守り", price = 80, desc = "お守り：ヴィエッタを意識しやすくなる（会いに行きやすく、近くに座りやすく、話すと仲よくなりやすい）", selfUse = true, accessory = true, modId = "charm:A", shelf = ItemShelf.Charm },
                new ItemDef { kind = ItemKind.CharmB, name = "シルヴィエルのお守り", price = 80, desc = "お守り：シルヴィエルを意識しやすくなる（会いに行きやすく、近くに座りやすく、話すと仲よくなりやすい）", selfUse = true, accessory = true, modId = "charm:B", shelf = ItemShelf.Charm },
                new ItemDef { kind = ItemKind.CharmC, name = "ルピナのお守り", price = 80, desc = "お守り：ルピナを意識しやすくなる（会いに行きやすく、近くに座りやすく、話すと仲よくなりやすい）", selfUse = true, accessory = true, modId = "charm:C", shelf = ItemShelf.Charm },
                new ItemDef { kind = ItemKind.CharmD, name = "ミレイユのお守り", price = 80, desc = "お守り：ミレイユを意識しやすくなる（会いに行きやすく、近くに座りやすく、話すと仲よくなりやすい）", selfUse = true, accessory = true, modId = "charm:D", shelf = ItemShelf.Charm },
                new ItemDef { kind = ItemKind.CharmE, name = "ラフィーナのお守り", price = 80, desc = "お守り：ラフィーナを意識しやすくなる（会いに行きやすく、近くに座りやすく、話すと仲よくなりやすい）", selfUse = true, accessory = true, modId = "charm:E", shelf = ItemShelf.Charm },
                new ItemDef { kind = ItemKind.CharmF, name = "篤のお守り", price = 80, desc = "お守り：篤を意識しやすくなる（会いに行きやすく、近くに座りやすく、話すと仲よくなりやすい）", selfUse = false, accessory = true, modId = "charm:F", shelf = ItemShelf.Charm },
                new ItemDef { kind = ItemKind.CharmG, name = "ロッテのお守り", price = 80, desc = "お守り：ロッテを意識しやすくなる（会いに行きやすく、近くに座りやすく、話すと仲よくなりやすい）", selfUse = true, accessory = true, modId = "charm:G", shelf = ItemShelf.Charm },
                new ItemDef { kind = ItemKind.CharmH, name = "ニナのお守り", price = 80, desc = "お守り：ニナを意識しやすくなる（会いに行きやすく、近くに座りやすく、話すと仲よくなりやすい）", selfUse = true, accessory = true, modId = "charm:H", shelf = ItemShelf.Charm },
                new ItemDef { kind = ItemKind.CharmI, name = "リアのお守り", price = 80, desc = "お守り：リアを意識しやすくなる（会いに行きやすく、近くに座りやすく、話すと仲よくなりやすい）", selfUse = true, accessory = true, modId = "charm:I", shelf = ItemShelf.Charm },
                new ItemDef { kind = ItemKind.CharmJ, name = "キッカのお守り", price = 80, desc = "お守り：キッカを意識しやすくなる（会いに行きやすく、近くに座りやすく、話すと仲よくなりやすい）", selfUse = true, accessory = true, modId = "charm:J", shelf = ItemShelf.Charm },
                // 寮生の私物（寮生が自分で買って使う。プレゼントにはできない）
                new ItemDef { kind = ItemKind.PassionAroma, name = "情熱のアロマ", price = 40, desc = "私物：半日、性欲が高まりやすい", npcOnly = true, modId = "own:情熱のアロマ", modMinutes = 720f, shelf = ItemShelf.Personal },
                new ItemDef { kind = ItemKind.NightCandle, name = "夜想のキャンドル", price = 40, desc = "私物：1日、性欲のたまりがたまりやすい・眠気がたまりにくい", npcOnly = true, modId = "own:夜想のキャンドル", modMinutes = 1440f, shelf = ItemShelf.Personal },
                new ItemDef { kind = ItemKind.LongingOil, name = "恋慕の香油", price = 50, desc = "私物：1日、恋心と性欲が高まりやすい", npcOnly = true, modId = "own:恋慕の香油", modMinutes = 1440f, shelf = ItemShelf.Personal },
            };
        }
    }

    /// <summary>
    /// プレイヤー（F）のお金・持ち物・お願いの記録。Fへの操作は SimHud から呼ばれる。
    /// </summary>
    public class PlayerControl : MonoBehaviour
    {
        public int money = 50;
        public readonly Dictionary<ItemKind, int> inventory = new Dictionary<ItemKind, int>();
        readonly Dictionary<ItemKind, int> usedDay = new Dictionary<ItemKind, int>();
        readonly Dictionary<SimCharacter, List<double>> requestLog = new Dictionary<SimCharacter, List<double>>();
        LoungeWorld world;

        public SimCharacter F { get { return world != null ? world.RomanceTarget : null; } }

        void Start()
        {
            world = GetComponent<LoungeWorld>();
        }

        public void Earn(int amount, string reason)
        {
            if (amount <= 0) return;
            money += amount;
            world.Log("銀貨＋" + amount + "（" + reason + "）　所持：" + money);
        }

        public int Count(ItemKind k) { int n; return inventory.TryGetValue(k, out n) ? n : 0; }

        public bool CanUseToday(ItemKind k)
        {
            var d = ItemBook.Get(k);
            if (d == null || !d.oncePerDay) return true;
            int day;
            return !usedDay.TryGetValue(k, out day) || day != world.Day;
        }

        public bool Buy(ItemKind k)
        {
            var d = ItemBook.Get(k);
            if (d == null || money < d.price || d.npcOnly) return false; // 寮生の私物は篤は買えない
            money -= d.price;
            inventory[k] = Count(k) + 1;
            world.Log("購買部で「" + d.name + "」を買った（銀貨−" + d.price + "）");
            return true;
        }

        public bool Take(ItemKind k)
        {
            if (Count(k) <= 0 || !CanUseToday(k)) return false;
            inventory[k] = Count(k) - 1;
            usedDay[k] = world.Day;
            return true;
        }

        public void Refund(ItemKind k) { inventory[k] = Count(k) + 1; }

        /// <summary>最近（ゲーム内1日以内）にお願いした回数。</summary>
        public int RecentRequests(SimCharacter c)
        {
            List<double> l;
            if (!requestLog.TryGetValue(c, out l)) return 0;
            l.RemoveAll(t => world.Now - t > 1440);
            return l.Count;
        }

        public void NoteRequest(SimCharacter c)
        {
            List<double> l;
            if (!requestLog.TryGetValue(c, out l)) { l = new List<double>(); requestLog[c] = l; }
            l.Add(world.Now);
        }

        public static string RequestName(RequestKind k, SimCharacter target)
        {
            switch (k)
            {
                case RequestKind.Meet: return (target != null ? target.displayName : "だれか") + "に会いに行って";
                case RequestKind.Study: return "勉強して";
                case RequestKind.Teach: return "勉強を教えて";
                default: return "休んで";
            }
        }
    }
}
