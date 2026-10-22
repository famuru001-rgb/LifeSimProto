using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 実績（ループしても残る）。条件は2秒ごとに見る（ProgressDirector）。数値は仮。
    /// </summary>
    public static class Achievements
    {
        public class Def
        {
            public string id, name, desc;
            public System.Func<LoungeWorld, bool> test;
        }

        /// <summary>取った実績（id → 「n周目 d日目」）。</summary>
        public static readonly Dictionary<string, string> Got = new Dictionary<string, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() { Got.Clear(); }

        static SimCharacter F(LoungeWorld w) { return w.RomanceTarget; }

        static int Girls(LoungeWorld w, System.Func<SimCharacter, bool> pred)
        {
            int n = 0;
            foreach (var c in w.characters) if (!c.IsTarget && pred(c)) n++;
            return n;
        }

        static int GirlTotal(LoungeWorld w) { return Girls(w, c => true); }

        static int ZukanLikes()
        {
            int n = 0;
            foreach (var z in Loop.Zukan) if (z.Contains(":好き:") || z.Contains(":苦手:")) n++;
            return n;
        }

        static int ZukanAll()
        {
            int n = 0;
            foreach (var p in CharProfiles.All) if (p.letter != "F") n += p.likes.Length + p.dislikes.Length;
            return n;
        }

        static int HPartners()
        {
            int n = 0;
            foreach (var kv in HLog.Total) if (kv.Key != "F" && kv.Value > 0) n++;
            return n;
        }

        static bool HBig()
        {
            foreach (var e in HLog.Entries) if (e.letters.Length >= 3) return true;
            return false;
        }

        public static readonly List<Def> All = new List<Def>
        {
            // ---- 関係 ----
            new Def { id = "friend1", name = "はじめての友達", desc = "だれかと篤が友達（好感度40）になる", test = w => F(w) != null && Girls(w, c => w.rel.Get(c.id, F(w).id) >= 40f) >= 1 },
            new Def { id = "friendAll", name = "寮のみんなと友達", desc = "全員と篤が友達になる", test = w => F(w) != null && Girls(w, c => w.rel.Get(c.id, F(w).id) >= 40f) >= GirlTotal(w) },
            new Def { id = "best1", name = "親友", desc = "だれかと篤が親友（好感度80）になる", test = w => F(w) != null && Girls(w, c => w.rel.Get(c.id, F(w).id) >= 80f) >= 1 },
            new Def { id = "lover1", name = "はじめての恋人", desc = "だれかと恋人になる", test = w => Girls(w, c => c.IsLover) >= 1 },
            new Def { id = "lover3", name = "恋人が3人", desc = "同じ周で3人と恋人になる", test = w => Girls(w, c => c.IsLover) >= 3 },
            new Def { id = "loverAll", name = "みんなの恋人", desc = "同じ周で全員と恋人になる", test = w => GirlTotal(w) > 0 && Girls(w, c => c.IsLover) >= GirlTotal(w) },
            // ---- 図鑑・アルバム ----
            new Def { id = "zukan10", name = "知りたがり", desc = "図鑑の好き・苦手を10個知る", test = w => ZukanLikes() >= 10 },
            new Def { id = "zukanAll", name = "寮の物知り", desc = "図鑑の好き・苦手を全部知る", test = w => ZukanLikes() >= ZukanAll() && ZukanAll() > 0 },
            new Def { id = "album20", name = "思い出がいっぱい", desc = "アルバムに20の思い出を残す", test = w => Album.Entries.Count >= 20 },
            // ---- 学び・魔法・場所 ----
            new Def { id = "magic1", name = "はじめての魔法", desc = "篤が魔法を1つ使えるようになる", test = w => FMagic.Unlocked.Count >= 1 },
            new Def { id = "magicAll", name = "魔力なしの魔法使い", desc = "篤が魔法を全部使えるようになる", test = w => FMagic.Unlocked.Count >= FMagic.All.Length },
            new Def { id = "top1", name = "首席", desc = "試験の総合で篤が1位になる", test = w => Academics.LastOverall.Count > 0 && Academics.LastOverall[0].Key == F(w) },
            new Def { id = "placeAll", name = "寮のすみずみまで", desc = "新しい場所を全部開く", test = w => Loop.OpenedPlaces.Count >= PlaceGates.All.Length },
            new Def { id = "rep50", name = "評判の寮", desc = "寮の評判を50にする", test = w => w.dormReputation >= 50 },
            // ---- 物語・ループ ----
            new Def { id = "story1", name = "物語のはじまり", desc = "だれかの物語の第1話を見る", test = w => Story.Done.Count >= 1 },
            new Def { id = "story1All", name = "みんなのきっかけ", desc = "全員の第1話を見る", test = w => GirlTotal(w) > 0 && Girls(w, c => Story.DoneOf(c.letter) >= 1) >= GirlTotal(w) },
            new Def { id = "wishAll", name = "みんなの願い", desc = "全員の第2話を見る（願いを知る）", test = w => GirlTotal(w) > 0 && Girls(w, c => Story.DoneOf(c.letter) >= 2) >= GirlTotal(w) },
            new Def { id = "hand1", name = "止まった針", desc = "だれかの針が止まる", test = w => Story.GirlHands(w) >= 1 },
            new Def { id = "handAll", name = "時計の針", desc = "みんなの針が止まる", test = w => GirlTotal(w) > 0 && Story.GirlHands(w) >= GirlTotal(w) },
            new Def { id = "escape", name = "2年目の春へ", desc = "篤の針が止まり、ループを抜ける", test = w => Story.Hands.Contains("F") },
            new Def { id = "loop2", name = "2周目", desc = "はじめて春の朝に戻る", test = w => Loop.Count >= 2 },
            new Def { id = "loop5", name = "くり返す1年", desc = "5周目を迎える", test = w => Loop.Count >= 5 },
            // ---- 夜 ----
            new Def { id = "h1", name = "はじめての夜", desc = "はじめてエッチする", test = w => HLog.Entries.Count >= 1 },
            new Def { id = "hBig", name = "にぎやかな夜", desc = "3人以上でエッチする", test = w => HBig() },
            new Def { id = "hAll", name = "みんなと結ばれる", desc = "全員とエッチしたことがある", test = w => GirlTotal(w) > 0 && HPartners() >= GirlTotal(w) },
            new Def { id = "h50", name = "数えきれない夜", desc = "エッチの記録が通算50回", test = w => HLog.Entries.Count >= 50 },
            // ---- 模様替え ----
            new Def { id = "decor1", name = "はじめての模様替え", desc = "だれかの部屋に家具を贈る", test = w => RoomDecor.Placed.Count >= 1 },
            new Def { id = "decorAll", name = "寮のインテリア係", desc = "同じ周で全員の部屋に家具を贈る", test = w => GirlTotal(w) > 0 && Girls(w, c => RoomDecor.CountIn(c.letter) > 0) >= GirlTotal(w) },
            new Def { id = "decorFull", name = "理想の部屋", desc = "ひとりの部屋の家具の置き場を全部うめる", test = w => Girls(w, c => RoomDecor.CountIn(c.letter) >= RoomDecor.SlotCount) >= 1 },
        };

        public static void Check(LoungeWorld w)
        {
            foreach (var d in All)
            {
                if (Got.ContainsKey(d.id)) continue;
                bool ok;
                try { ok = d.test(w); } catch { ok = false; }
                if (!ok) continue;
                Got[d.id] = Loop.Count + "周目 " + w.Day + "日目";
                w.Log("◆ 実績「" + d.name + "」（" + d.desc + "）");
            }
        }
    }
}
