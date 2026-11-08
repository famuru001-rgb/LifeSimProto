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
        // 歩きの active は使わない（famuru 2026-10-31）→ happy に置きかえる。おじぎ・手を振るは active のまま
        public static string Walk(string style, string mood)
        {
            string s = string.IsNullOrEmpty(mood) ? style : mood;
            if (s == "active") s = "happy";
            return "BN_walk_" + s;
        }

        /// <summary>
        /// 歩く動き（Mixamo の歩きも混ぜる。2026-11-04 famuru「入れた動きを全部使う」）。キャラごとに決まった1つ。
        /// 沈んでいる→BN_walk_sad か M_SadWalk／女らしい→BN_walk_feminine か M_FemaleWalk／自信家→BN_walk_proud か M_Catwalk／ふつう→BN_walk_normal か M_Walking
        /// </summary>
        public static string WalkFor(string style, string mood, string letter, Func<string, AnimationClip> find)
        {
            string bn = Walk(style, mood);
            string[] alt = null;
            string key = string.IsNullOrEmpty(mood) ? style : mood;
            switch (key)
            {
                case "sad": alt = new[] { bn, "M_SadWalk" }; break;
                case "feminine": alt = new[] { bn, "M_FemaleWalk" }; break;
                case "proud": alt = new[] { bn, "M_CatwalkWalk", "M_CatwalkWalking" }; break;
                case "normal": alt = new[] { bn, "M_Walking", "M_Walking_1" }; break;
            }
            if (alt == null) return bn;
            string pick = alt[Pick(letter, "walk_" + key, alt.Length)];
            return find(pick) != null ? pick : bn;
        }

        // ---- 気持ちの身ぶりの言いかえ（2026-11-04 famuru「入れた動きを全部使う」）。同じ身ぶりでも、毎回この中からどれかを選ぶ ----
        static readonly Dictionary<string, string[]> GesturePool = new Dictionary<string, string[]>
        {
            { "Nod", new[] { "Nod", "HY_Nod_a", "HY_Nod_b", "M_Acknowledging" } },
            { "HeadShake", new[] { "HeadShake", "HY_HeadShake_a", "HY_HeadShake_b", "M_No", "M_ShakingHeadNo", "M_AnnoyedHeadShake", "M_ThoughtfulHeadShake" } },
            { "Surprised", new[] { "Surprised", "HY_Surprised_a", "HY_Surprised_b", "M_Surprised", "M_Reacting", "R_DoubleTakes" } },
            { "Troubled", new[] { "Troubled", "HY_Troubled_a", "HY_Troubled_b" } },
            { "Shy", new[] { "Shy", "HY_Shy_a", "HY_Shy_b", "M_Bashful" } },
            { "Angry", new[] { "Angry", "M_Angry", "M_AngryGesture", "M_AngryPoint", "HY_Annoyed_a", "HY_Annoyed_b", "M_StandingArguing" } },
            { "Cry", new[] { "Cry", "M_Crying" } },
            { "Thanks", new[] { "Thanks", "M_Thankful" } },
            { "Think", new[] { "Think", "M_Thinking" } },
        };
        // 座っているときの身ぶり（上半身に重ねる）
        static readonly Dictionary<string, string[]> SeatedGesturePool = new Dictionary<string, string[]>
        {
            { "Angry", new[] { "M_SittingAngry", "M_SittingAngry_1", "M_SittingAngry_2" } },
            { "Surprised", new[] { "M_SittingDisbelief" } },
            { "Surprised2", new[] { "M_SittingDisbelief" } },
        };

        static string FromPool(Dictionary<string, string[]> pool, string g, Func<string, AnimationClip> find)
        {
            string[] list;
            if (string.IsNullOrEmpty(g) || !pool.TryGetValue(g, out list)) return g;
            int start = UnityEngine.Random.Range(0, list.Length);
            for (int i = 0; i < list.Length; i++) { var n = list[(start + i) % list.Length]; if (find(n) != null) return n; }
            return g;
        }
        /// <summary>身ぶりの名前を、言いかえの中からどれか1つに（無ければそのまま）。</summary>
        public static string GestureVariant(string g, Func<string, AnimationClip> find) { return FromPool(GesturePool, g, find); }
        /// <summary>座っているときの身ぶり（無ければそのまま）。</summary>
        public static string SeatedGesture(string g, Func<string, AnimationClip> find) { return FromPool(SeatedGesturePool, g, find); }

        /// <summary>立って話すときの身ぶり（会話ごとに1つ）。</summary>
        public static readonly string[] TalkPool = { "Talk", "Talk2", "Talk3", "Talk4", "HY_Talk1_a", "HY_Talk1_b", "HY_Talk2_a", "HY_Talk2_b", "M_Talking", "M_Talking_1", "M_Talking_2", "M_Talking_3", "M_TalkingAtWatercooler", "M_TellingASecret" };
        /// <summary>相手の話を聞いているとき（立って）。</summary>
        public static readonly string[] ListenPool = { "HY_Listen_a", "HY_Listen_b" };
        /// <summary>座って話すとき。</summary>
        public static readonly string[] SitTalkPool = { "SitTalk", "M_SittingTalking" };
        /// <summary>気分が沈んでいるときの立ち方（キャラごとに1つ）。</summary>
        static readonly string[] SadPool = { "SadIdle", "HY_Sad_a", "HY_Sad_b", "M_SadIdle", "M_SadIdle_1", "M_Disappointed", "M_Rejected" };
        public static AnimationClip SadIdle(string letter, Func<string, AnimationClip> find)
        {
            int start = Pick(letter, "sad", SadPool.Length);
            for (int i = 0; i < SadPool.Length; i++) { var c = find(SadPool[(start + i) % SadPool.Length]); if (c != null) return c; }
            return null;
        }
        public static AnimationClip PickFrom(string[] pool, Func<string, AnimationClip> find)
        {
            int start = UnityEngine.Random.Range(0, pool.Length);
            for (int i = 0; i < pool.Length; i++) { var c = find(pool[(start + i) % pool.Length]); if (c != null) return c; }
            return null;
        }
        public static string Bow(string style) { return "BN_bow_" + style; }
        public static string Bye(string style) { return "BN_bye_" + style; }

        // ---- 座り方（2026-11-08 famuru「HY版（1本目）」。椅子：とじる・ふつう・ひらく／床：正座・横座り・女の子座り・あぐら）----
        // キャラのらしさで決め、らしさが決まっていない子はキャラごとにくじ（同じ子はいつも同じ）。仮の割り当て。famuru が見て直す
        static readonly Dictionary<string, string> ChairByStyle = new Dictionary<string, string>
        { { "feminine", "Sit_Closed" }, { "proud", "Sit_Closed" }, { "normal", "Sit_Normal" }, { "happy", "Sit_Normal" }, { "childish", "Sit_Open" }, { "active", "Sit_Open" }, { "masculinity", "Sit_Open" } };
        static readonly Dictionary<string, string> FloorByStyle = new Dictionary<string, string>
        { { "feminine", "SitFloor_Yoko" }, { "proud", "SitFloor" }, { "normal", "SitFloor" }, { "happy", "SitFloor_Girl" }, { "childish", "SitFloor_Girl" }, { "active", "SitFloor_Agura" }, { "masculinity", "SitFloor_Agura" } };
        static readonly string[] ChairAll = { "Sit_Closed", "Sit_Normal", "Sit_Open" };
        static readonly string[] FloorAll = { "SitFloor", "SitFloor_Yoko", "SitFloor_Girl", "SitFloor_Agura" };
        /// <summary>椅子に座る動きの名前（Sit_〇〇。_Down＝座る・_Up＝立つ）</summary>
        public static string ChairSit(string letter, bool male)
        {
            string s;
            if (!string.IsNullOrEmpty(letter) && StyleByLetter.TryGetValue(letter, out s) && ChairByStyle.TryGetValue(s, out s)) return s;
            return male ? "Sit_Open" : ChairAll[Pick(letter, "chair", ChairAll.Length)];
        }
        /// <summary>床に座る動きの名前（SitFloor は正座）</summary>
        public static string FloorSit(string letter, bool male)
        {
            string s;
            if (!string.IsNullOrEmpty(letter) && StyleByLetter.TryGetValue(letter, out s) && FloorByStyle.TryGetValue(s, out s)) return s;
            return male ? "SitFloor_Agura" : FloorAll[Pick(letter, "floor", FloorAll.Length)];
        }
        /// <summary>座る動き（_Down）の始めの腰の位置（座ったときの腰から。Unity の体の向きで x＝右・z＝前、m）。見た目を前の位置からつなぐのに使う</summary>
        public static readonly Dictionary<string, Vector2> SitStart = new Dictionary<string, Vector2>
        {
            { "Sit_Closed", new Vector2(-0.008f, 0.209f) }, { "Sit_Normal", new Vector2(-0.016f, 0.239f) }, { "Sit_Open", new Vector2(-0.002f, 0.136f) },
            { "SitFloor_Yoko", new Vector2(-0.062f, 0.097f) }, { "SitFloor_Girl", new Vector2(0.011f, 0.248f) }, { "SitFloor_Agura", new Vector2(-0.233f, 0.155f) },
        };

        // ---- 待機（キャラごとに1つ）----
        static readonly string[] Idles = { "Idle", "R_RegularLight", "R_RegularMedium", "R_Light", "R_Medium", "R_HeadTilts" };
        // ほかの立ち方：待っているとき（足でとんとん）・絶好調（踊る）・好きな人のそば（ちょっと気を引く）。R_RegularHeavyCHECK は名前に CHECK（要確認）とあるので使わない
        public const string WaitClip = "R_FootTapping", HappyClip = "R_HappyDance", FlirtClip = "R_Flirty";
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
            P("歌の練習", "R_Singer"),                                  // 娯楽室（2026-11-04）
            P("楽器", "R_PlayingGuitar", "R_PlayingPiano"),            // 娯楽室（楽器の模型はまだ無い）
            P("職員室で仕事", "R_ClipboardLeverComputer"),            // 先生
            P("授業をして", "Talk", "Talk2", "Talk3", "Talk4", "M_Talking", "M_Talking_1", "M_Talking_2", "M_Talking_3"), // 教室の前で教える（2026-11-04）
            // ---- 2026-11-08 モーション（famuru「名前は動きがあっているか確かめて合わせて」「新しい動きを作って」）----
            // 名前の合わせ：浜辺のたき火は暖炉と同じ手をかざす動き／ひらがなの「ながめ」／新しい寮の「本を探している」は本棚の動き
            P("たき火", "HY_WarmHands_a", "HY_WarmHands_b"),
            P("ながめ", "HY_Gaze_a", "HY_Gaze_b", "M_LookAround"),
            P("本を探", "HY_BrowseBooks_a", "HY_BrowseBooks_b"),
            P("散歩", "HY_Gaze_a", "HY_Gaze_b", "M_LookAround"),          // 砂浜の散歩・中庭のぶらぶら：立ち止まってながめる
            P("ぶらぶら", "HY_Gaze_a", "HY_Gaze_b", "M_LookAround"),
            P("生徒会", "R_ClipboardLeverComputer"),                    // 生徒会の仕事（書類を見る）
            // 新しい動き（N_。HY-Motion で作った）
            P("ほうき", "N_Broom"),                                     // ほうきで飛ぶ練習・ほうき部・飛行術の授業・飛行レース
            P("飛行", "N_Broom"),
            P("体を鍛え", "N_Squat", "N_JumpingJack", "N_PushUp", "N_SitUp", "N_StretchWarm"),   // 腕立て・腹筋は「立つ→床に伏せる／寝転ぶ」の始める動きを作って戻した（2026-11-08）   // 種目ごとの動き（20秒ごとに次の種目へ。2026-11-08 famuru「動き毎に分けて」）
            P("洗濯物を干", "N_LaundryHang"),
            P("洗濯物をたた", "N_LaundryFold"),
            P("洗濯", "N_LaundryWash"),
            P("アイロン", "N_Iron"),
            P("おやつ", "N_SnackTake"),
            P("水ぎわ", "N_WaterPlay"),
            P("水遊び", "N_WaterPlay"),
            P("シャワー", "N_Shower"),
            P("歯みがき", "N_BrushTeeth"),
            P("顔を洗", "N_WashFace"),
            P("戸をたたく", "N_Knock"),
            P("物音", "N_ListenNoise"),
            P("渡す", "N_GiveItem"),                                   // 物を渡す（タオル・毛布・編み物・落とし物）
            P("のび", "N_WakeStretch"),
            P("雪", "N_SnowPlay"),
            // 行事（集まりの名前）
            P("花火", "N_Fireworks"),
            P("ごちそう", "N_EatStand"),
            P("歓迎会", "N_Toast", "N_EatStand"),
            P("送別会", "N_Toast", "N_EatStand"),
            P("苗を植え", "M_plantaplant", "M_digandplantseeds", "M_watering"),
            P("野外実習", "HY_Herbs_a", "HY_Herbs_b", "M_pickfruit"),
            P("魔道具", "HY_Tinker_a", "HY_Tinker_b"),
            P("出し物", "R_Singer", "R_PlayingGuitar", "R_HappyDance"),
            P("星を見", "HY_Gaze_a", "HY_Gaze_b"),
            // ダンジョン
            P("忍び足", "N_ListenNoise"),   // 前へ進む忍び足は足がすべるので、身をすくめて耳をすます動きにした（2026-11-08）
            P("駆け抜け", "L_Jog_Jog_F"),
            P("宝箱", "N_OpenChest"),
            P("勝った", "N_Victory"),
            // 生活のできごと（表の「動き」の欄。L_ は 2026-11-04 に作った生活の動き）
            P("掃除", "L_Sweep_Sweep_F", "L_Sweep_Wipe_F"),
            P("運動", "L_Jog_Jog_F", "N_Squat", "N_JumpingJack", "N_StretchWarm"),
            P("ばらまく", "L_DropPick_Pick_F"),
            P("荷物", "L_CarryBox_Hold_F"),
            P("ずぶ濡れ", "L_Shiver_Shiver_F", "L_Shiver_Wring_F"),
        };

        // ---- 座って行う場面（上半身だけ重ねる）----
        static readonly KeyValuePair<string, string[]>[] Seated =
        {
            P("#食べる", "HY_Eat_a", "HY_Eat_b", "R_SomethingBehindMeEating"),
            P("#お茶", "HY_Tea_a", "HY_Tea_b", "M_Drinking"),
            P("#読書", "HY_Read_a", "HY_Read_b"),
            P("#勉強", "HY_Write_a", "HY_Write_b", "R_TypingUsingMouse"),
            P("机で", "HY_Write_a", "HY_Write_b", "R_SitDownUseLaptop"),
            // 生活のできごと（2026-11-08 モーション。表の「動き」の欄）
            P("宝物", "L_Polish_Polish_F", "L_Polish_Admire_F"),
            P("晩酌", "L_Drink_Sip_F", "L_Drink_Tipsy_F"),
            P("編み物", "L_Knit_Knit_F"),
        };

        /// <summary>同じ場面の中で動きを順に変える場面（体を鍛える・朝の運動）と、変える間かく（秒）</summary>
        static readonly string[] RotateKeys = { "体を鍛え", "運動" };
        public static float RotateSeconds = 20f;   // 腕立て・腹筋は始める・やめるに時間がかかるので 20秒
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
                if (System.Array.IndexOf(RotateKeys, kv.Key) >= 0) start = (start + (int)(Time.time / RotateSeconds)) % kv.Value.Length;   // 種目を順に変える
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
