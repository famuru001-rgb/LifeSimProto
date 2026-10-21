using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    public enum ItemKind { Sweets, Bouquet, Present, CheerPotion, WakePotion, SleepIncense, ColdMedicine, LoveCharm }
    public enum RequestKind { Meet, Study, Rest, Teach }

    public class ItemDef
    {
        public ItemKind kind;
        public string name;
        public int price;
        public string desc;
        public bool selfUse;     // Fが自分に使える
        public bool oncePerDay;  // 1日1回まで
    }

    /// <summary>購買部の品物。数値は試作用の仮の値。</summary>
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
                new ItemDef { kind = ItemKind.Sweets, name = "お菓子", price = 10, desc = "好感度＋3・空腹−30", selfUse = true },
                new ItemDef { kind = ItemKind.Bouquet, name = "花束", price = 40, desc = "好感度＋6・篤への気持ち＋5・ときどきご機嫌" },
                new ItemDef { kind = ItemKind.Present, name = "小さなプレゼント", price = 80, desc = "好感度＋10・篤への気持ち＋8・気分が1段階上がる" },
                new ItemDef { kind = ItemKind.CheerPotion, name = "元気の薬", price = 50, desc = "落ち込み系を1段階回復（なければご機嫌）・疲れ−30", selfUse = true },
                new ItemDef { kind = ItemKind.WakePotion, name = "目覚めの薬", price = 40, desc = "寝不足系を治す・眠気−50", selfUse = true },
                new ItemDef { kind = ItemKind.SleepIncense, name = "安眠のお香", price = 30, desc = "今夜はぐっすり眠れる（翌朝の寝不足を防ぐ）", selfUse = true },
                new ItemDef { kind = ItemKind.ColdMedicine, name = "風邪薬", price = 30, desc = "風邪を1段階治す", selfUse = true },
                new ItemDef { kind = ItemKind.LoveCharm, name = "恋のおまじない", price = 120, desc = "篤を「気になる」以上の子だけに効く。恋心＋30・恋わずらい（1日1回）", oncePerDay = true },
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
            if (d == null || money < d.price) return false;
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
