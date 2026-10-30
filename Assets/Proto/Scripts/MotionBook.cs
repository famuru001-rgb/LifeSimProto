using System;
using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// どの場面・どのキャラでどの動きを使うかの表（2026-10-30 モーションのチャット。famuru「一通り取り入れて、キャラや場面に応じて使い分ける」）。
    /// ・キャラのらしさ（歩き方・おじぎ・手を振る）：バンダイナムコ研究所のデータ（BN_…。CC BY-NC 4.0・クレジット要）
    /// ・待機：Idle と Rokoko の立ち方（R_…）から、キャラごとに1つ
    /// ・生活の場面：場所の名前（Poi の label）に含まれる言葉で選ぶ。候補が複数あるときはキャラごとに決まった1つ
    /// 動きのファイルは Resources\Motions（候補\・候補3\ を含む）。ファイルが無い名前は使わない（今までの動きに戻る）。
    /// </summary>
    public static class MotionBook
    {
        // ---- キャラのらしさ（バンダイナムコの15種類：normal feminine happy sad angry proud notconfident masculinity childish old tired active musical giant chimpira）----
        // 仮の割り当て。famuru が見て直す
        static readonly Dictionary<string, string> StyleByLetter = new Dictionary<string, string>
        {
            { "A", "normal" },      // ヴィエッタ：クールで無口
            { "B", "feminine" },    // シルヴィエル：ツンとした高貴
            { "C", "active" },      // ルピナ：人なつこい大型犬
            { "D", "feminine" },    // ミレイユ：しっかり者の世話焼き
            { "E", "proud" },       // ラフィーナ：自信家のお嬢様
            { "F", "masculinity" }, // 篤
            { "G", "normal" },      // ロッテ：理屈っぽい研究者
            { "H", "childish" },    // ニナ：甘えん坊
            { "I", "feminine" },    // リア：上品で世間知らず
            { "J", "active" },      // キッカ：ちょこまか
            { "K", "happy" },       // セレネ：ふわふわ天然
        };

        public static string StyleOf(string letter, bool male)
        {
            string s;
            if (!string.IsNullOrEmpty(letter) && StyleByLetter.TryGetValue(letter, out s)) return s;
            return male ? "masculinity" : "feminine";
        }

        /// <summary>歩く動きの名前。気分（sad・tired・happy・angry）があればそちらを使う</summary>
        public static string Walk(string style, string mood) { return "BN_walk_" + (string.IsNullOrEmpty(mood) ? style : mood); }
        public static string Bow(string style) { return "BN_bow_" + style; }
        public static string Bye(string style) { return "BN_bye_" + style; }

        // ---- 待機（キャラごとに1つ）----
        static readonly string[] Idles = { "Idle", "R_RegularLight", "R_RegularMedium", "R_Light", "R_Medium" };
        public static string Idle(string letter) { return Idles[Pick(letter, "idle", Idles.Length)]; }

        // ---- 立って行う生活の場面（場所の名前に含まれる言葉 → 候補）。上から順に見る ----
        static readonly KeyValuePair<string, string[]>[] Stand =
        {
            P("洗い物", "HY_Wash_a", "HY_Wash_b"),
            P("料理を配", "HY_Serve_a", "HY_Serve_b"),
            P("料理を受け取", "HY_Serve_a", "HY_Serve_b"),
            P("料理", "HY_Cook_a", "HY_Cook_b", "R_ChefCooking"),
            P("お菓子", "HY_Cook_a", "HY_Cook_b"),
            P("大鍋", "HY_StirPot_a", "HY_StirPot_b"),
            P("釜", "HY_StirPot_a", "HY_StirPot_b"),
            P("本棚", "HY_BrowseBooks_a", "HY_BrowseBooks_b"),
            P("本を選", "HY_BrowseBooks_a", "HY_BrowseBooks_b"),
            P("暖炉", "HY_WarmHands_a", "HY_WarmHands_b"),
            P("望遠鏡", "HY_Telescope_a", "HY_Telescope_b"),
            P("星図", "HY_Gaze_a", "HY_Gaze_b"),
            P("眺め", "HY_Gaze_a", "HY_Gaze_b", "M_LookAround"),
            P("魔法", "HY_Magic_a", "HY_Magic_b", "R_WandSpells", "R_MagicSnaps", "R_MagicShields", "R_DrStrangeMagic", "R_Fireballs"),
            P("剣術", "HY_Sword_a", "HY_Sword_b", "R_SwordIdleLight", "R_SwordGeneric", "R_SwordFight"),
            P("薬草", "HY_Herbs_a", "HY_Herbs_b", "M_watering", "M_pullplant", "M_plantaplant", "M_pickfruit", "M_digandplantseeds"),
            P("工房", "HY_Tinker_a", "HY_Tinker_b"),
            P("使い魔", "HY_PetFamiliar_a", "HY_PetFamiliar_b"),
        };

        // ---- 座って行う場面（上半身だけ重ねる）----
        static readonly KeyValuePair<string, string[]>[] Seated =
        {
            P("#食べる", "HY_Eat_a", "HY_Eat_b", "R_SomethingBehindMeEating"),
            P("#お茶", "HY_Tea_a", "HY_Tea_b", "M_Drinking"),
            P("#読書", "HY_Read_a", "HY_Read_b"),
            P("#勉強", "HY_Write_a", "HY_Write_b"),
            P("机で", "HY_Write_a", "HY_Write_b"),
        };

        static KeyValuePair<string, string[]> P(string k, params string[] v) { return new KeyValuePair<string, string[]>(k, v); }

        /// <summary>立って行う場面の動き（無ければ null）</summary>
        public static AnimationClip StandClip(string scene, string letter, Func<string, AnimationClip> find) { return Choose(Stand, scene, letter, find); }
        /// <summary>座って行う場面の上半身の動き（無ければ null）</summary>
        public static AnimationClip SeatedClip(string scene, string letter, Func<string, AnimationClip> find) { return Choose(Seated, scene, letter, find); }

        static AnimationClip Choose(KeyValuePair<string, string[]>[] table, string scene, string letter, Func<string, AnimationClip> find)
        {
            if (string.IsNullOrEmpty(scene)) return null;
            foreach (var kv in table)
            {
                if (!scene.Contains(kv.Key)) continue;
                int start = Pick(letter, kv.Key, kv.Value.Length);
                for (int i = 0; i < kv.Value.Length; i++)
                {
                    var c = find(kv.Value[(start + i) % kv.Value.Length]);
                    if (c != null) return c;
                }
                return null;
            }
            return null;
        }

        // キャラと場面ごとに決まった番号（同じキャラはいつも同じ動き）
        static int Pick(string letter, string key, int n)
        {
            if (n <= 1) return 0;
            int h = 17;
            foreach (char ch in (letter ?? "") + "|" + key) h = h * 31 + ch;
            return (h & 0x7fffffff) % n;
        }

        // ---- 吹き出しの言葉 → 気持ちの身ぶり（Rokoko の追加分）----
        public static readonly KeyValuePair<string, string>[] BubbleExtra =
        {
            new KeyValuePair<string, string>("あはは", "R_Laughs"),
            new KeyValuePair<string, string>("ははっ", "R_Laughs"),
            new KeyValuePair<string, string>("さあ…", "R_Shrugging"),
            new KeyValuePair<string, string>("わかんない", "R_Shrugging"),
            new KeyValuePair<string, string>("はぁ", "R_FacePalms"),
            new KeyValuePair<string, string>("すごい", "R_ClapSlowClap"),
            new KeyValuePair<string, string>("やった", "R_FistPumps"),
        };
    }
}
