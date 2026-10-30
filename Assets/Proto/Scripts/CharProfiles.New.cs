using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 追加のヒロイン23人（L〜AH）と、寮生以外の学生90人・先生7人（2026-10-30 famuru「ヒロインとNPCをゲームに追加して」）。
    /// ・L〜U：騎士課程（寮の2階）、V〜AE：魔法師課程（寮の3階）、AF〜AH：先生（1階の寮母さんの部屋を先生の部屋に）
    /// ・設定は企画\キャラ○_名前_設定.md（プロジェクトの文書）。科目は今の科目に合わせて置きかえた：
    ///   占星術→王国史、体術→剣術、元素魔法→魔法術、回復魔法→魔法医学（famuru があとで直せる）
    /// ・能力のランク（S〜E）は、S 88・A 75・B 62・C 48・D 33・E 18 を最初の値にした
    /// ・寮生以外（famuru 決定）：名前は番号だけ。欲求・関係・会話なし、エッチはできる。寮ごとの評価のため、ひとりずつ学力と能力がある
    ///   見た目（キャラデザ_決定事項.md）：全員女性・人間・黒髪・制服。体は 小152cm・中158cm・大164cm（±3%）。髪はショート・セミロング・ポニーテール
    /// </summary>
    public static partial class CharProfiles
    {
        /// <summary>寮に住む人の数（篤とヒロイン。寮生以外の学生・先生はこのあと）。</summary>
        public static int ResidentCount { get; private set; }

        static CharLook HeroLook(Color hair, params string[] parts)
        {
            return new CharLook { hair = hair, parts = parts };
        }

        static void AddNewHeroines()
        {
            // L オルテンシア（人間（侯爵家）・騎士課程3年）：規律に厳しい完璧主義の優等生（企画\キャラL_オルテンシア_設定.md）
            All.Add(new CharProfile
            {
                letter = "L", name = "オルテンシア", shortName = "オルテ", race = "人間（侯爵家）", personality = "規律に厳しい完璧主義の優等生",
                color = new Color(0.878f, 0.690f, 0.188f), social = 0.45f, whim = 0.15f, diligence = 0.95f,
                hobby = SimCharacter.HobbyKind.Walk, hobbyText = "剣の型の稽古", hobbyPlace = "中庭",
                likes = new[] { "剣の稽古", "整理整頓", "紅茶", "かわいい小物" }, dislikes = new[] { "だらしないこと", "遅刻", "まっすぐにほめられること" },
                traits = CharTrait.None, traitText = "",
                strongStudy = Theory, weakStudy = Herb, strongPractical = Sword, weakPractical = Alchemy,
                enrolled = new[] { Monster, Theory, Herb, Ancient, Sword, Dungeon, Alchemy, MagicArts }, affiliation = Affiliation.Knight, callF = "篤殿",
                course = "騎士", grade = 3, teacher = false, heightCm = 170f,
                initAbility = new float[] { 75, 62, 62, 48, 75 }, initSex = 3f,
                look = HeroLook(new Color(0.878f, 0.690f, 0.188f), "Hair_Pony"),
            }
            .L("hello_m", "おはようございます、篤殿。今日も時間どおりですね", "おはようございます。身だしなみ、よろしい")
            .L("hello_d", "ごきげんよう", "…何か御用ですか")
            .L("hello_n", "消灯の時間を守るように", "おやすみなさいませ。夜ふかしは禁止です")
            .L("close", "{0}、ちょうどよいところに")
            .L("reply", "ええ、問題ありません", "…はい")
            .L("meal", "栄養の均衡は大切です", "よく噛んで食べましょう", "…この紅茶、悪くありません")
            .L("snack", "間食は規則の範囲内で", "…この焼き菓子、形がかわいらしいですね")
            .L("bath", "…湯加減、ちょうどよいですね", "長湯は規則違反です。…あと少しだけ")
            .L("seat", "姿勢を正しなさい", "それで？", "なるほど", "…ふむ")
            .L("chatter", "なるほど", "規則どおりですね", "ええ", "それは感心です", "…そうですか")
            .L("topic", "{0}について、少し考えていたのですが")
            .L("like", "その話は好ましいですね")
            .L("dislike", "…その話は、感心しません")
            .L("sleepy", "…眠くなど、ありません")
            .L("sigh", "…はぁ")
            .L("frustr", "まったく、だらしない…")
            .L("done", "予定どおり、終わりました")
            .L("busy", "課題は計画的に…")
            .L("teach", "よいですか、ここは順番に考えます")
            .L("thanks", "感謝します")
            .L("accept", "承知しました", "よろしいでしょう")
            .L("refuse", "今は規則上、できません")
            .L("night", "おやすみなさいませ。明日も時間どおりに")
            .L("surprise", "っ…！")
            .L("hurt", "…っ、今のは稽古不足です")
            .L("rank1", "当然の結果です")
            .L("examGood", "予定どおりです")
            .L("examBad", "…次は必ず取り返します")
            .L("cute", "…このぬいぐるみは落とし物です。私の物ではありません", "規則は守るためにあるのです"));
            CharLines.Nick["L"] = "オルテ"; CharLines.Honor["L"] = "さん";
            // M エルヴィラ（竜・騎士課程3年）：のんびり・時間の感覚がずれている（企画\キャラM_エルヴィラ_設定.md）
            All.Add(new CharProfile
            {
                letter = "M", name = "エルヴィラ", shortName = "エルヴィ", race = "竜", personality = "のんびり・時間の感覚がずれている",
                color = new Color(0.122f, 0.541f, 0.357f), social = 0.4f, whim = 0.6f, diligence = 0.3f,
                hobby = SimCharacter.HobbyKind.Walk, hobbyText = "宝物を磨くこと・長い昼寝", hobbyPlace = "工房",
                likes = new[] { "日なたぼっこ", "光る物", "甘い果物" }, dislikes = new[] { "寒さ", "急かされること", "時計" },
                traits = CharTrait.WinterWeak | CharTrait.DeepSleeper, traitText = "寒さに弱く、眠りが深い",
                strongStudy = Ancient, weakStudy = Tools, strongPractical = Fly, weakPractical = Medicine,
                enrolled = new[] { Monster, Ancient, Tools, Theory, Sword, Dungeon, Fly, Medicine }, affiliation = Affiliation.Knight, callF = "篤",
                course = "騎士", grade = 3, teacher = false, heightCm = 177f,
                initAbility = new float[] { 88, 62, 33, 75, 62 }, initSex = 5f,
                look = HeroLook(new Color(0.122f, 0.541f, 0.357f), "Hair_LongBack", "Acc_Horns"),
            }
            .L("hello_m", "ふあ…もう朝かの。ついさっき寝たばかりじゃが", "おはよう、篤。よう眠れたか")
            .L("hello_d", "おお、篤か", "のんびりしていくがよい")
            .L("hello_n", "夜はまだ長いのう", "おやすみ。われはもう少し月を浴びる")
            .L("close", "{0}、こっちへ来い")
            .L("reply", "うむ、元気じゃ", "…ふあ、なんじゃ")
            .L("meal", "うむ、うまいのう", "甘い果物はないのか？", "ゆっくり食べるがよい")
            .L("snack", "甘い物はよいのう", "もうひとつ、もらってもよいか")
            .L("bath", "ふぅ…極楽じゃ", "湯は百年たっても飽きぬのう")
            .L("seat", "ふあ…", "のんびりいこう", "そうかそうか", "うむ")
            .L("chatter", "ほう", "うむうむ", "なるほどのう", "よいよい", "ふふ")
            .L("topic", "{0}のことじゃが")
            .L("like", "おお、その話は好きじゃ")
            .L("dislike", "…その話は、急かされるようで好かん")
            .L("sleepy", "ふあ…ひと眠りするか")
            .L("sigh", "やれやれ")
            .L("frustr", "急かすでない…")
            .L("done", "終わったぞ。百年ぶりにがんばった")
            .L("busy", "課題…まだ百日はあるじゃろ？")
            .L("teach", "ここはの、ゆっくり読めばわかる")
            .L("thanks", "うむ、礼を言う")
            .L("accept", "よいぞ", "まかせよ")
            .L("refuse", "今はひと眠りしたいのう")
            .L("night", "おやすみ。…起きたら百年後、ということはないぞ")
            .L("surprise", "おお？")
            .L("hurt", "おっと、力が入りすぎたのう")
            .L("rank1", "われが一番か。当然じゃな")
            .L("examGood", "ふふん、まあこんなものじゃ")
            .L("examBad", "寝ておったら終わっておった…")
            .L("cute", "百年なぞ、ほんのひと眠りよ", "この光る石、きれいじゃろ？"));
            CharLines.Nick["M"] = "エルヴィ"; CharLines.Honor["M"] = "";
            // N モニカ（牛・騎士課程3年）：おっとりした癒やし系（企画\キャラN_モニカ_設定.md）
            All.Add(new CharProfile
            {
                letter = "N", name = "モニカ", shortName = "モニカ", race = "牛", personality = "おっとりした癒やし系",
                color = new Color(0.957f, 0.957f, 0.957f), social = 0.75f, whim = 0.4f, diligence = 0.45f,
                hobby = SimCharacter.HobbyKind.Tea, hobbyText = "お菓子作り・花壇の世話", hobbyPlace = "台所",
                likes = new[] { "食べること", "焼き菓子", "昼寝", "動物の世話" }, dislikes = new[] { "食べ物を粗末にされること", "早起き" },
                traits = CharTrait.DeepSleeper, traitText = "眠りが深い",
                strongStudy = Herb, weakStudy = Theory, strongPractical = Sword, weakPractical = Fly,
                enrolled = new[] { Monster, Herb, Theory, Ancient, Sword, Dungeon, Fly, MagicArts }, affiliation = Affiliation.Knight, callF = "篤くん",
                course = "騎士", grade = 3, teacher = false, heightCm = 168f,
                initAbility = new float[] { 75, 75, 48, 33, 48 }, initSex = 6f,
                look = HeroLook(new Color(0.957f, 0.957f, 0.957f), "Hair_LongBack", "Acc_Horns"),
            }
            .L("hello_m", "ふぁ…おはようございますぅ", "おはようございますぅ。朝ごはん、楽しみですねぇ")
            .L("hello_d", "あらあら、篤くん", "こんにちはぁ")
            .L("hello_n", "おやすみなさいねぇ", "あったかくして寝てくださいねぇ")
            .L("close", "{0}、こっちですよぉ")
            .L("reply", "はぁい、元気ですよぉ")
            .L("meal", "おいしいですねぇ", "おかわり、いいですかぁ？", "残さず食べましょうねぇ", "しあわせですぅ")
            .L("snack", "焼き菓子、ありますよぉ", "甘いの、半分こしましょうかぁ")
            .L("bath", "ぽかぽかですぅ", "ふわぁ…眠くなっちゃいますねぇ")
            .L("seat", "あらあら", "うふふ", "そうなんですかぁ", "のんびりしましょうねぇ")
            .L("chatter", "あらあら", "うふふ", "そうなんですねぇ", "まあ", "いいですねぇ")
            .L("topic", "{0}のお話、しましょうかぁ")
            .L("like", "あら、それ大好きですぅ")
            .L("dislike", "……今、なんて言いました？")
            .L("sleepy", "ふぁ…ねむねむですぅ")
            .L("sigh", "ふぅ…")
            .L("frustr", "もぉ…")
            .L("done", "終わりましたぁ。おやつにしましょ")
            .L("busy", "課題、ゆっくりやりますねぇ")
            .L("teach", "大丈夫ですよぉ、ここはこうですぅ")
            .L("thanks", "ありがとうございますぅ")
            .L("accept", "はぁい、いいですよぉ")
            .L("refuse", "今はちょっと…ごめんなさいねぇ")
            .L("night", "おやすみなさいぃ")
            .L("surprise", "あらっ？")
            .L("hurt", "あらあら、失敗しちゃいましたぁ")
            .L("rank1", "あら、私が一番ですかぁ")
            .L("examGood", "よかったですぅ")
            .L("examBad", "うぅ…おなかすいちゃいました")
            .L("cute", "あらあら、おなかすいてませんかぁ？ 焼き菓子ありますよぉ"));
            CharLines.Nick["N"] = "モニちゃん"; CharLines.Honor["N"] = "さん";
            // O アイリス（剣の精霊・騎士課程2年）：無口でまっすぐ・主に一途（企画\キャラO_アイリス_設定.md）
            All.Add(new CharProfile
            {
                letter = "O", name = "アイリス", shortName = "アイリス", race = "剣の精霊", personality = "無口でまっすぐ・主に一途",
                color = new Color(0.431f, 0.482f, 0.557f), social = 0.2f, whim = 0.1f, diligence = 0.85f,
                hobby = SimCharacter.HobbyKind.Walk, hobbyText = "剣を磨くこと・型の稽古", hobbyPlace = "中庭",
                likes = new[] { "剣の手入れ", "朝の素振り", "甘い物" }, dislikes = new[] { "雑談", "湿気", "回復魔法" },
                traits = CharTrait.None, traitText = "",
                strongStudy = Tools, weakStudy = History, strongPractical = Sword, weakPractical = Medicine,
                enrolled = new[] { Monster, Tools, History, Theory, Sword, Dungeon, Medicine, MagicArts }, affiliation = Affiliation.Knight, callF = "篤", callFClose = "主", callFCloseStage = 2,
                course = "騎士", grade = 2, teacher = false, heightCm = 172f,
                initAbility = new float[] { 75, 62, 75, 48, 48 }, initSex = 2f,
                look = HeroLook(new Color(0.431f, 0.482f, 0.557f), "Hair_Bob"),
            }
            .L("hello_m", "…おはよう", "…朝の素振り、終わった")
            .L("hello_d", "…ん", "…何か")
            .L("hello_n", "…おやすみ", "…見回りは、自分がする")
            .L("close", "…{0}")
            .L("reply", "…問題ない", "…了解")
            .L("meal", "…いただく", "…悪くない", "…食事は、大事")
            .L("snack", "…甘い物は、嫌いではない", "…もう一つ、いいか")
            .L("bath", "…湿気は苦手だが、湯はいい", "…ふぅ")
            .L("seat", "…", "…ん", "…そうか", "…了解")
            .L("chatter", "…ん", "…そうか", "…了解", "…なるほど", "…")
            .L("topic", "…{0}のことだが")
            .L("like", "…それは、いい")
            .L("dislike", "…雑談は、苦手だ")
            .L("sleepy", "…少し、眠い")
            .L("sigh", "…")
            .L("frustr", "…湿気が、まとわりつく")
            .L("done", "…完了")
            .L("busy", "…課題、遂行中")
            .L("teach", "…ここは、こう斬る。…じゃなくて、こう解く")
            .L("thanks", "…感謝する")
            .L("accept", "…了解", "主の命なら")
            .L("refuse", "…今は、できない")
            .L("night", "…おやすみ")
            .L("surprise", "…っ")
            .L("hurt", "…刃筋が、ぶれた")
            .L("rank1", "…当然")
            .L("examGood", "…合格")
            .L("examBad", "…鍛え直す")
            .L("cute", "…甘い物は、嫌いではない"));
            CharLines.Nick["O"] = "アイ"; CharLines.Honor["O"] = "";
            // P ノクティア（堕天使・騎士課程2年）：皮肉屋だが根はまじめで優しい（企画\キャラP_ノクティア_設定.md）
            All.Add(new CharProfile
            {
                letter = "P", name = "ノクティア", shortName = "ノクト", race = "堕天使", personality = "皮肉屋だが根はまじめで優しい",
                color = new Color(0.941f, 0.941f, 0.949f), social = 0.35f, whim = 0.35f, diligence = 0.7f,
                hobby = SimCharacter.HobbyKind.Reading, hobbyText = "星を見ること・翼の手入れ", hobbyPlace = "屋上の天文台",
                likes = new[] { "夜空", "苦いコーヒー", "古い星の本" }, dislikes = new[] { "祈りの言葉", "まっすぐ感謝されること" },
                traits = CharTrait.RainWings, traitText = "雨の日は翼が重い",
                strongStudy = History, weakStudy = Ancient, strongPractical = Fly, weakPractical = Medicine,
                enrolled = new[] { Monster, History, Ancient, Theory, Sword, Dungeon, Fly, Medicine }, affiliation = Affiliation.Knight, callF = "後輩くん",
                course = "騎士", grade = 2, teacher = false, heightCm = 174f,
                initAbility = new float[] { 62, 75, 62, 75, 62 }, initSex = 4f,
                look = HeroLook(new Color(0.941f, 0.941f, 0.949f), "Hair_LongBack"),
            }
            .L("hello_m", "…朝か。眩しいね", "おはよ、後輩くん")
            .L("hello_d", "やあ", "何か用？ ボクに？")
            .L("hello_n", "いい夜だね", "夜はボクの時間だよ")
            .L("close", "{0}、ちょっといい？")
            .L("reply", "ま、悪くないよ")
            .L("meal", "…ま、食べられなくはない", "コーヒー、苦いのある？", "ちゃんと食べなよ")
            .L("snack", "甘いのは…ま、ひとつだけ", "コーヒーに合うなら")
            .L("bath", "…翼が湿るのは苦手なんだけど", "ふぅ…悪くないね")
            .L("seat", "ふーん", "で？", "…なるほどね", "ま、いいけど")
            .L("chatter", "ふーん", "へえ", "…で？", "まあね", "ふふ、物好きだね")
            .L("topic", "{0}のことなんだけどさ")
            .L("like", "…へえ、わかってるじゃん")
            .L("dislike", "…その手の話は勘弁して")
            .L("sleepy", "…昼は眠いんだよ")
            .L("sigh", "…はぁ")
            .L("frustr", "…やれやれ")
            .L("done", "終わったよ。…ほめなくていいから")
            .L("busy", "課題？ …ちゃんとやってるよ")
            .L("teach", "…ま、ボクでよければ手伝うけど")
            .L("thanks", "…べつに。…ありがと")
            .L("accept", "…ま、いいよ")
            .L("refuse", "堕ちた天使に何を期待してるんだか。…今は無理")
            .L("night", "おやすみ、後輩くん")
            .L("surprise", "っ、なに？")
            .L("hurt", "…ちっ、羽がずれた")
            .L("rank1", "…ま、当然でしょ")
            .L("examGood", "…悪くないね")
            .L("examBad", "…次は本気出す")
            .L("cute", "堕ちた天使に何を期待してるんだか", "…ま、ボクでよければ手伝うけど"));
            CharLines.Nick["P"] = "ノクト"; CharLines.Honor["P"] = "";
            // Q ティナ（人間（騎士爵家・双子の姉）・騎士課程2年）：元気ないたずら好き（企画\キャラQ_ティナ_設定.md）
            All.Add(new CharProfile
            {
                letter = "Q", name = "ティナ", shortName = "ティナ", race = "人間（騎士爵家・双子の姉）", personality = "元気ないたずら好き",
                color = new Color(0.965f, 0.757f, 0.812f), social = 0.85f, whim = 0.85f, diligence = 0.25f,
                hobby = SimCharacter.HobbyKind.Walk, hobbyText = "空中の連携の練習・落書き", hobbyPlace = "中庭",
                likes = new[] { "いたずら", "甘いお菓子", "曲芸飛行" }, dislikes = new[] { "座学", "じっとしていること" },
                traits = CharTrait.None, traitText = "",
                strongStudy = Tools, weakStudy = History, strongPractical = Fly, weakPractical = Alchemy,
                enrolled = new[] { Monster, Tools, History, Theory, Sword, Dungeon, Fly, Alchemy }, affiliation = Affiliation.Knight, callF = "お兄ちゃん",
                course = "騎士", grade = 2, teacher = false, heightCm = 145f,
                initAbility = new float[] { 75, 62, 48, 48, 33 }, initSex = 4f,
                look = HeroLook(new Color(0.965f, 0.757f, 0.812f)),
            }
            .L("hello_m", "おはよ、お兄ちゃん！", "おはよー！ 今日はなにして遊ぶ？")
            .L("hello_d", "お兄ちゃーん！", "やっほー！")
            .L("hello_n", "まだ寝ないもんねー", "おやすみ…じゃなくて、もうちょっと遊ぼ？")
            .L("close", "{0}、こっちこっち！")
            .L("reply", "元気いっぱいだよ！")
            .L("meal", "いっただっきまーす！", "ティア、それちょうだい♪", "おいしー！")
            .L("snack", "お菓子だー！", "甘いの、もっともっと！")
            .L("bath", "ばしゃーん！", "潜水勝負しよ！")
            .L("seat", "えへへ", "ねえねえ", "それでそれで？", "ふふーん")
            .L("chatter", "へー！", "すごーい", "えへへ", "ほんとに？", "やったね")
            .L("topic", "ねえ、{0}の話しよ！")
            .L("like", "それ大好き！")
            .L("dislike", "えー、座学の話はやだー")
            .L("sleepy", "ふぁ…眠くないもん…")
            .L("sigh", "ちぇー")
            .L("frustr", "むーっ！")
            .L("done", "終わったー！ 遊びに行こ！")
            .L("busy", "課題…ティアに見せてもらお")
            .L("teach", "こうやって、こう！ わかった？")
            .L("thanks", "ありがと、お兄ちゃん！")
            .L("accept", "いいよー！", "まかせて！")
            .L("refuse", "今はだーめ")
            .L("night", "おやすみ、お兄ちゃん。ティアには内緒ね？")
            .L("surprise", "わわっ！")
            .L("hurt", "いたた…失敗しちゃった")
            .L("rank1", "一番だー！")
            .L("examGood", "やったー！")
            .L("examBad", "座学なんてきらーい…")
            .L("cute", "お兄ちゃん、ティアには内緒ね？"));
            CharLines.Nick["Q"] = "ティナ"; CharLines.Honor["Q"] = "ちゃん";
            // R ティア（人間（騎士爵家・双子の妹）・騎士課程2年）：冷静なつっこみ役（企画\キャラR_ティア_設定.md）
            All.Add(new CharProfile
            {
                letter = "R", name = "ティア", shortName = "ティア", race = "人間（騎士爵家・双子の妹）", personality = "冷静なつっこみ役",
                color = new Color(0.965f, 0.757f, 0.812f), social = 0.45f, whim = 0.2f, diligence = 0.85f,
                hobby = SimCharacter.HobbyKind.Reading, hobbyText = "空中の連携の練習・刺しゅう", hobbyPlace = "図書室",
                likes = new[] { "読書", "静かな時間", "姉の世話" }, dislikes = new[] { "大きな音" },
                traits = CharTrait.None, traitText = "",
                strongStudy = Theory, weakStudy = Herb, strongPractical = Fly, weakPractical = MagicArts,
                enrolled = new[] { Monster, Theory, Herb, Ancient, Sword, Dungeon, Fly, MagicArts }, affiliation = Affiliation.Knight, callF = "お兄さん",
                course = "騎士", grade = 2, teacher = false, heightCm = 145f,
                initAbility = new float[] { 62, 62, 62, 48, 75 }, initSex = 3f,
                look = HeroLook(new Color(0.965f, 0.757f, 0.812f)),
            }
            .L("hello_m", "おはようございます、お兄さん", "おはようございます。姉さん、まだ寝てます")
            .L("hello_d", "こんにちは", "…姉さん、見ませんでした？")
            .L("hello_n", "おやすみなさい", "静かな夜ですね")
            .L("close", "{0}、少しいいですか")
            .L("reply", "はい、問題ありません")
            .L("meal", "いただきます", "姉さん、取らないで", "…おいしいです")
            .L("snack", "甘いのは、少しだけ", "姉さんの分も取っておきます")
            .L("bath", "…静かでいいですね", "姉さん、潜らないで")
            .L("seat", "そうですか", "…なるほど", "それで？", "…ふう")
            .L("chatter", "はい", "なるほど", "そうですね", "…それ三回目です", "ふふ")
            .L("topic", "{0}のことですけど")
            .L("like", "それは、好きです")
            .L("dislike", "大きな音は…苦手です")
            .L("sleepy", "…少し、眠いです")
            .L("sigh", "はぁ…")
            .L("frustr", "姉さん…")
            .L("done", "終わりました")
            .L("busy", "課題は早めに終わらせます")
            .L("teach", "ここは、順番に見れば簡単です")
            .L("thanks", "ありがとうございます")
            .L("accept", "わかりました", "いいですよ")
            .L("refuse", "今は遠慮します")
            .L("night", "おやすみなさい、お兄さん")
            .L("surprise", "きゃっ")
            .L("hurt", "…失敗しました。姉さんには内緒で")
            .L("rank1", "…よかったです")
            .L("examGood", "予習のおかげです")
            .L("examBad", "…姉さんに勉強を教えていたら")
            .L("cute", "姉さん、それ三回目。…お兄さんも止めてください"));
            CharLines.Nick["R"] = "ティア"; CharLines.Honor["R"] = "さん";
            // S ブリギッタ（鬼・騎士課程1年）：豪快でおおらか（企画\キャラS_ブリギッタ_設定.md）
            All.Add(new CharProfile
            {
                letter = "S", name = "ブリギッタ", shortName = "ブリギ", race = "鬼", personality = "豪快でおおらか",
                color = new Color(0.176f, 0.357f, 0.816f), social = 0.8f, whim = 0.6f, diligence = 0.35f,
                hobby = SimCharacter.HobbyKind.Walk, hobbyText = "食べ歩き・薪割り", hobbyPlace = "中庭",
                likes = new[] { "肉料理", "力くらべ", "お祭り" }, dislikes = new[] { "細かい作業", "高い所", "魔法理論" },
                traits = CharTrait.None, traitText = "",
                strongStudy = Herb, weakStudy = Theory, strongPractical = Sword, weakPractical = Fly,
                enrolled = new[] { Monster, Herb, Theory, Ancient, Sword, Dungeon, Fly, MagicArts }, affiliation = Affiliation.Knight, callF = "篤",
                course = "騎士", grade = 1, teacher = false, heightCm = 182f,
                initAbility = new float[] { 88, 62, 33, 48, 33 }, initSex = 5f,
                look = HeroLook(new Color(0.176f, 0.357f, 0.816f), "Hair_Pony", "Acc_Horns"),
            }
            .L("hello_m", "おう、おはよう！", "朝飯だ朝飯！")
            .L("hello_d", "よう、篤！", "おっ、元気か？")
            .L("hello_n", "もう寝るのか？ はえーな", "おやすみ！ 腹出して寝るなよ")
            .L("close", "おーい、{0}！")
            .L("reply", "ははっ、元気だぜ！")
            .L("meal", "うめえ！", "肉はねえのか、肉は！", "おかわり！")
            .L("snack", "おっ、うまそうだな！", "ひと口でいけるぜ")
            .L("bath", "ぷはーっ、最高だな！", "でっかい風呂はいいな！")
            .L("seat", "ははっ", "で？", "おう", "細けえことは気にすんな")
            .L("chatter", "ははっ", "おう！", "いいじゃねえか", "まじか！", "だよな！")
            .L("topic", "なあ、{0}の話なんだけどよ")
            .L("like", "お、その話いいな！")
            .L("dislike", "うげ、その話は勘弁")
            .L("sleepy", "ふあ…ねみい")
            .L("sigh", "ふう")
            .L("frustr", "あーもう、ちまちましてんな！")
            .L("done", "よっしゃ、終わり！")
            .L("busy", "課題…細けえのは苦手だ…")
            .L("teach", "こうやって、ぐっとやるんだよ！")
            .L("thanks", "ありがとな！")
            .L("accept", "おう、まかせとけ！")
            .L("refuse", "わりい、今は無理だ")
            .L("night", "おやすみ！")
            .L("surprise", "うおっ！？")
            .L("hurt", "いてて…力入れすぎた")
            .L("rank1", "一番だ！ ははっ！")
            .L("examGood", "やったぜ！")
            .L("examBad", "魔法理論とか、わかんねえよ…")
            .L("cute", "お、オレを子ども扱いすんなよ…いや、別にいやじゃねえけど", "ははっ、細けえことは気にすんな！"));
            CharLines.Nick["S"] = "ブリ"; CharLines.Honor["S"] = "";
            // T ハンナ（犬・騎士課程1年）：礼儀正しく元気な後輩（企画\キャラT_ハンナ_設定.md）
            All.Add(new CharProfile
            {
                letter = "T", name = "ハンナ", shortName = "ハンナ", race = "犬", personality = "礼儀正しく元気な後輩",
                color = new Color(0.478f, 0.290f, 0.180f), social = 0.85f, whim = 0.4f, diligence = 0.7f,
                hobby = SimCharacter.HobbyKind.Walk, hobbyText = "朝の散歩", hobbyPlace = "中庭",
                likes = new[] { "朝の散歩", "骨付き肉", "役に立つこと" }, dislikes = new[] { "雷", "ひとりでのお留守番", "元素魔法" },
                traits = CharTrait.Lonely, traitText = "ひとりは苦手（さみしさがたまりやすい）",
                strongStudy = Tools, weakStudy = Ancient, strongPractical = Sword, weakPractical = MagicArts,
                enrolled = new[] { Monster, Tools, Ancient, Theory, Sword, Dungeon, MagicArts, Alchemy }, affiliation = Affiliation.Knight, callF = "篤さん",
                course = "騎士", grade = 1, teacher = false, heightCm = 165f,
                initAbility = new float[] { 75, 62, 62, 33, 48 }, initSex = 4f,
                look = HeroLook(new Color(0.478f, 0.290f, 0.180f), "Hair_Bob", "Acc_CatEars", "Acc_CatTail"),
            }
            .L("hello_m", "おはようございます、先輩！", "朝のお散歩、行きませんか？")
            .L("hello_d", "はいっ、ハンナです！", "先輩、こんにちは！")
            .L("hello_n", "おやすみなさいです！", "夜も見回り、がんばります！")
            .L("close", "{0}、お供します！")
            .L("reply", "はいっ、元気です！")
            .L("meal", "おいしいです！", "骨付き肉…ありませんか？", "いただきます！")
            .L("snack", "わあ、いいんですか？", "しっぽが勝手に…えへへ")
            .L("bath", "あったかいです〜", "先輩、お背中流しましょうか？")
            .L("seat", "はいっ", "えへへ", "そうなんですね！", "なるほどです")
            .L("chatter", "はいっ！", "すごいです！", "えへへ", "そうなんですね", "さすがです！")
            .L("topic", "{0}のお話、聞いてください！")
            .L("like", "それ、大好きです！")
            .L("dislike", "うう…雷は苦手です…")
            .L("sleepy", "ふぁ…ねむいです")
            .L("sigh", "くぅん…")
            .L("frustr", "うう〜")
            .L("done", "終わりました！ ほめてくださいっ")
            .L("busy", "課題、がんばってます！")
            .L("teach", "ハンナにわかることなら！")
            .L("thanks", "ありがとうございます！")
            .L("accept", "はいっ、ハンナがお供します！")
            .L("refuse", "ごめんなさい、今は…")
            .L("night", "おやすみなさい、先輩！")
            .L("surprise", "きゃうっ！？")
            .L("hurt", "うう…失敗しちゃいました")
            .L("rank1", "えへへ…ほめられちゃいました")
            .L("examGood", "やりましたっ！")
            .L("examBad", "元素魔法、むずかしいです…")
            .L("cute", "はいっ！ 先輩、ハンナがお供します！", "えへへ…ほめられちゃいました"));
            CharLines.Nick["T"] = "ハンナちゃん"; CharLines.Honor["T"] = "さん";
            // U ネフィラ（魔王の娘・騎士課程1年）：強がりで負けずぎらい（企画\キャラU_ネフィラ_設定.md）
            All.Add(new CharProfile
            {
                letter = "U", name = "ネフィラ", shortName = "ネフィ", race = "魔王の娘", personality = "強がりで負けずぎらい",
                color = new Color(0.357f, 0.173f, 0.514f), social = 0.55f, whim = 0.55f, diligence = 0.55f,
                hobby = SimCharacter.HobbyKind.Reading, hobbyText = "騎士物語を読むこと", hobbyPlace = "図書室",
                likes = new[] { "騎士の物語", "甘いお菓子", "勝負ごと" }, dislikes = new[] { "回復魔法", "負けること", "正体を聞かれること" },
                traits = CharTrait.None, traitText = "",
                strongStudy = Ancient, weakStudy = Tools, strongPractical = Sword, weakPractical = Medicine,
                enrolled = new[] { Monster, Ancient, Tools, Theory, Sword, Dungeon, Medicine, MagicArts }, affiliation = Affiliation.Knight, callF = "篤", callFClose = "お兄ちゃん", callFCloseStage = 2,
                course = "騎士", grade = 1, teacher = false, heightCm = 140f,
                initAbility = new float[] { 62, 75, 48, 88, 62 }, initSex = 3f,
                look = HeroLook(new Color(0.357f, 0.173f, 0.514f), "Hair_Twin", "Acc_Horns"),
            }
            .L("hello_m", "お、おはよう！ べつに待ってないし", "ふん、早起きは騎士の基本よ")
            .L("hello_d", "なによ", "ふ、ふん！")
            .L("hello_n", "まだ眠くないし！", "お、おやすみ…")
            .L("close", "{0}！ ちょっと来なさい！")
            .L("reply", "ふん、元気に決まってるでしょ")
            .L("meal", "ま、まあまあね", "もっと食べて大きくなるの！", "…おいしい")
            .L("snack", "甘いの！ …べ、べつにうれしくないし")
            .L("bath", "ふぅ…", "角、ぶつけないでよね")
            .L("seat", "ふん", "で？", "べ、べつに", "そうなの？")
            .L("chatter", "ふん", "へえ", "当然よ", "…そうなの？", "ま、まあね")
            .L("topic", "{0}のことなんだけど！")
            .L("like", "そ、それは好きかも…")
            .L("dislike", "その話はやめて！")
            .L("sleepy", "ねむくないし…")
            .L("sigh", "…はぁ")
            .L("frustr", "むきーっ！")
            .L("done", "終わったわ！ 当然よ！")
            .L("busy", "課題くらい、すぐ終わるし！")
            .L("teach", "わたしに任せなさい！")
            .L("thanks", "…あ、ありがと")
            .L("accept", "我に…じゃなくて、わたしに任せなさい！")
            .L("refuse", "い、今はだめ！")
            .L("night", "おやすみ…。べつに、さみしくないし")
            .L("surprise", "ひゃっ！？")
            .L("hurt", "い、今のは練習よ！")
            .L("rank1", "当然よ！ 一番なんだから！")
            .L("examGood", "ふふん、見た？")
            .L("examBad", "ま、負けてないし…")
            .L("cute", "ふ、ふん！ 魔王の娘なんかじゃないし！"));
            CharLines.Nick["U"] = "ネフィ"; CharLines.Honor["U"] = "";
            // V エリス（ハイエルフ・魔法師課程3年）：浮世離れして穏やか・好奇心旺盛（企画\キャラV_エリス_設定.md）
            All.Add(new CharProfile
            {
                letter = "V", name = "エリス", shortName = "エリス", race = "ハイエルフ", personality = "浮世離れして穏やか・好奇心旺盛",
                color = new Color(0.812f, 0.910f, 0.875f), social = 0.4f, whim = 0.7f, diligence = 0.6f,
                hobby = SimCharacter.HobbyKind.Reading, hobbyText = "珍しい物集め・古代語の研究", hobbyPlace = "図書室",
                likes = new[] { "新しい道具", "古い書物", "月夜の散歩" }, dislikes = new[] { "体術", "急かされること", "騒がしい場所" },
                traits = CharTrait.None, traitText = "",
                strongStudy = Ancient, weakStudy = Tools, strongPractical = Medicine, weakPractical = Sword,
                enrolled = new[] { Theory, Ancient, Tools, Herb, MagicArts, Medicine, Sword, Dungeon }, affiliation = Affiliation.Mage, callF = "人の子", callFClose = "篤", callFCloseStage = 2,
                course = "魔法師", grade = 3, teacher = false, heightCm = 176f,
                initAbility = new float[] { 33, 75, 62, 88, 75 }, initSex = 3f,
                look = HeroLook(new Color(0.812f, 0.910f, 0.875f), "Hair_LongBack", "Acc_ElfEars"),
            }
            .L("hello_m", "あら…もう朝？", "おはよう、人の子")
            .L("hello_d", "ふふ、ごきげんよう", "あら、人の子")
            .L("hello_n", "月がきれいね", "夜の散歩、ご一緒にいかが？")
            .L("close", "{0}、こちらへいらして")
            .L("reply", "ええ、変わりないわ")
            .L("meal", "ふふ、人の子の料理は面白いわね", "これは何かしら？", "ゆっくりいただくわ")
            .L("snack", "まあ、かわいらしいお菓子")
            .L("bath", "ふふ、温かいわね", "百年ぶりの湯浴みかしら")
            .L("seat", "ふふ", "まあ", "それは何かしら？", "あら")
            .L("chatter", "まあ", "ふふ", "面白いわね", "それで？", "あら")
            .L("topic", "{0}のこと、教えてくださる？")
            .L("like", "まあ、それは素敵ね")
            .L("dislike", "…騒がしいのは、少し苦手なの")
            .L("sleepy", "ふぁ…あら、三日ほど寝ていないかしら")
            .L("sigh", "ふぅ")
            .L("frustr", "急かさないでくださる？")
            .L("done", "終わったわ。思ったより早かったかしら")
            .L("busy", "課題…あら、期限はいつだったかしら")
            .L("teach", "ここはね、千年前の書き方なの")
            .L("thanks", "ありがとう、人の子")
            .L("accept", "ええ、いいわよ", "ふふ、楽しそうね")
            .L("refuse", "今は遠慮しておくわ")
            .L("night", "おやすみなさい。よい夢を")
            .L("surprise", "あら？")
            .L("hurt", "あらあら、体術は苦手なの")
            .L("rank1", "ふふ、当然ね")
            .L("examGood", "面白い問題だったわ")
            .L("examBad", "体術は、どうにも…")
            .L("cute", "ふふ、それは何かしら。わらわにも見せてくださる？", "あら…もう朝？ 本を読んでいたら三日たっていたみたい"));
            CharLines.Nick["V"] = "エリ"; CharLines.Honor["V"] = "";
            // W ヘレナ（人間（聖女）・魔法師課程3年）：表は完璧な聖女・素顔はだらしない（企画\キャラW_ヘレナ_設定.md）
            All.Add(new CharProfile
            {
                letter = "W", name = "ヘレナ", shortName = "ヘレナ", race = "人間（聖女）", personality = "表は完璧な聖女・素顔はだらしない",
                color = new Color(0.725f, 0.639f, 0.890f), social = 0.6f, whim = 0.5f, diligence = 0.35f,
                hobby = SimCharacter.HobbyKind.Reading, hobbyText = "恋愛小説を読むこと", hobbyPlace = "図書室",
                likes = new[] { "昼寝", "甘いお菓子", "決めた人のそば" }, dislikes = new[] { "片付け", "長いお祈り" },
                traits = CharTrait.DeepSleeper, traitText = "眠りが深い（昼寝が好き）",
                strongStudy = Herb, weakStudy = Theory, strongPractical = Medicine, weakPractical = Sword,
                enrolled = new[] { Theory, Ancient, Herb, Tools, MagicArts, Medicine, Sword, Dungeon }, affiliation = Affiliation.Mage, callF = "篤様", callFClose = "あっちゃん", callFCloseStage = 3,
                course = "魔法師", grade = 3, teacher = false, heightCm = 153f,
                initAbility = new float[] { 33, 88, 33, 75, 62 }, initSex = 5f,
                look = HeroLook(new Color(0.725f, 0.639f, 0.890f), "Hair_LongBack"),
            }
            .L("hello_m", "おはようございます。よい朝ですね", "ふぁ…あ、おはようございます")
            .L("hello_d", "ごきげんよう", "あなたに神のご加護がありますように")
            .L("hello_n", "おやすみなさいませ", "よい夢を")
            .L("close", "{0}、こちらへどうぞ")
            .L("reply", "ええ、おかげさまで")
            .L("meal", "恵みに感謝を", "…もう少しいただいても？", "おいしいですね")
            .L("snack", "甘いもの…ではなく、お供えですね", "…もうひとつ")
            .L("bath", "ふぅ…極楽〜…あ、いえ、清めの湯ですね")
            .L("seat", "ええ", "まあ", "そうですか", "ふふ")
            .L("chatter", "まあ", "ええ", "それは素敵ですね", "ふふ", "神のお導きですね")
            .L("topic", "{0}のお話をしましょうか")
            .L("like", "まあ、それは好きです")
            .L("dislike", "…片付けの話は、またにしましょう")
            .L("sleepy", "ふぁ…お昼寝の時間ですね")
            .L("sigh", "はぁ〜…めんどくさぁい…あ、いえ")
            .L("frustr", "…もう、お祈り長すぎ")
            .L("done", "終わりました。神のご加護ですね")
            .L("busy", "課題…だれかにお願いを…")
            .L("teach", "落ち着いて、ここはこう考えます")
            .L("thanks", "ありがとうございます。感謝を")
            .L("accept", "ええ、喜んで")
            .L("refuse", "今は…お祈りの時間ですので")
            .L("night", "おやすみなさいませ。よい夢を")
            .L("surprise", "きゃっ…あ、失礼")
            .L("hurt", "…あらあら、聖女も失敗するんですよ")
            .L("rank1", "神のお導きです")
            .L("examGood", "祈りが届きました")
            .L("examBad", "…体術は神の領分ではありません")
            .L("cute", "え〜、めんどくさぁい…あっちゃん、やっといて〜"));
            CharLines.Nick["W"] = "ヘレナ"; CharLines.Honor["W"] = "さん";
            // X デイジー（ホビット・魔法師課程3年）：陽気な宴会好きのムードメーカー（企画\キャラX_デイジー_設定.md）
            All.Add(new CharProfile
            {
                letter = "X", name = "デイジー", shortName = "デイジー", race = "ホビット", personality = "陽気な宴会好きのムードメーカー",
                color = new Color(0.796f, 0.690f, 0.541f), social = 0.95f, whim = 0.6f, diligence = 0.45f,
                hobby = SimCharacter.HobbyKind.Tea, hobbyText = "料理・歌", hobbyPlace = "台所",
                likes = new[] { "宴会", "料理", "歌" }, dislikes = new[] { "ひとりで食べるごはん", "元素魔法", "高い棚" },
                traits = CharTrait.Lonely, traitText = "ひとりのごはんはさみしい",
                strongStudy = Ancient, weakStudy = Theory, strongPractical = Medicine, weakPractical = MagicArts,
                enrolled = new[] { Theory, Ancient, Tools, Herb, MagicArts, Medicine, Sword, Dungeon }, affiliation = Affiliation.Mage, callF = "篤っち",
                course = "魔法師", grade = 3, teacher = false, heightCm = 135f,
                initAbility = new float[] { 62, 75, 75, 62, 48 }, initSex = 6f,
                look = HeroLook(new Color(0.796f, 0.690f, 0.541f), "Hair_Bob"),
            }
            .L("hello_m", "おっはよー！ 朝ごはん作ろっか！", "おはよ、篤っち！")
            .L("hello_d", "やっほー！", "篤っち、元気？")
            .L("hello_n", "夜はこれからだよ！", "おやすみー！ また明日ね！")
            .L("close", "{0}、おいでおいで！")
            .L("reply", "うちはいつでも元気だよ！")
            .L("meal", "うまっ！", "みんなで食べるとおいしいね！", "うちのシチューも食べてって！")
            .L("snack", "お菓子パーティーだ！", "はい、半分こ！")
            .L("bath", "ふぃ〜、いい湯だね〜", "お風呂で歌うと響くんだよ〜♪")
            .L("seat", "あはは", "でしょ？", "いいねいいね", "ふんふ〜ん♪")
            .L("chatter", "あはは！", "いいね！", "わかる〜", "ほんと？", "最高！")
            .L("topic", "ねえねえ、{0}の話しよ！")
            .L("like", "それ、うちも大好き！")
            .L("dislike", "ひとりのごはんは、さみしいよ…")
            .L("sleepy", "ふぁ…ちょっと眠いかも")
            .L("sigh", "ふぅ〜")
            .L("frustr", "むむ〜")
            .L("done", "終わったー！ 宴会だー！")
            .L("busy", "課題…歌いながらやろっと")
            .L("teach", "いい？ こうやって混ぜるの！")
            .L("thanks", "ありがと！ 篤っち、いい人！")
            .L("accept", "まかせて！", "よーし、やろう！")
            .L("refuse", "ごめん、今はちょっと！")
            .L("night", "おやすみー！ また明日ね！")
            .L("surprise", "わっ！？")
            .L("hurt", "あちゃー、失敗失敗")
            .L("rank1", "うちが一番！ 宴会だー！")
            .L("examGood", "やったね！")
            .L("examBad", "元素魔法、むずかしいよ〜")
            .L("cute", "さあさあ、今夜は宴会だよ！ うちの特製シチュー、食べてって！"));
            CharLines.Nick["X"] = "デイ"; CharLines.Honor["X"] = "ちゃん";
            // Y サラ（ダークエルフ・魔法師課程2年）：大人の余裕・聞き上手で包容力がある（企画\キャラY_サラ_設定.md）
            All.Add(new CharProfile
            {
                letter = "Y", name = "サラ", shortName = "サラ", race = "ダークエルフ", personality = "大人の余裕・聞き上手で包容力がある",
                color = new Color(0.788f, 0.745f, 0.863f), social = 0.7f, whim = 0.3f, diligence = 0.6f,
                hobby = SimCharacter.HobbyKind.Reading, hobbyText = "星占い・お香", hobbyPlace = "屋上の天文台",
                likes = new[] { "夜空", "香りのいいお茶", "人の話を聞くこと" }, dislikes = new[] { "飛行術", "強い日差し", "細かい魔道具いじり" },
                traits = CharTrait.None, traitText = "",
                strongStudy = History, weakStudy = Tools, strongPractical = MagicArts, weakPractical = Fly,
                enrolled = new[] { Theory, Ancient, History, Tools, MagicArts, Fly, Sword, Dungeon }, affiliation = Affiliation.Mage, callF = "坊や", callFClose = "篤", callFCloseStage = 2,
                course = "魔法師", grade = 2, teacher = false, heightCm = 178f,
                initAbility = new float[] { 62, 88, 62, 75, 75 }, initSex = 8f,
                look = HeroLook(new Color(0.788f, 0.745f, 0.863f), "Hair_LongBack", "Acc_ElfEars"),
            }
            .L("hello_m", "おはよう、坊や。よく眠れた？", "ふふ、おはよう")
            .L("hello_d", "あら、坊や", "どうしたの？")
            .L("hello_n", "夜空がきれいね", "おやすみなさい。いい夢を")
            .L("close", "{0}、いらっしゃい")
            .L("reply", "ええ、元気よ")
            .L("meal", "おいしいわね", "ゆっくり食べなさいな", "お茶もいかが？")
            .L("snack", "ふふ、甘いものもたまにはいいわね")
            .L("bath", "ふぅ…いいお湯ね", "ゆっくりしましょう")
            .L("seat", "ふふ", "それで？", "話してごらんなさい", "ええ")
            .L("chatter", "ふふ", "ええ", "そうなのね", "それで？", "大丈夫よ")
            .L("topic", "{0}のこと、聞かせて")
            .L("like", "あら、いいわね")
            .L("dislike", "強い日差しは、少し苦手なの")
            .L("sleepy", "…少し、眠いわね")
            .L("sigh", "ふぅ")
            .L("frustr", "…困ったわね")
            .L("done", "終わったわ")
            .L("busy", "課題、片付けておかないとね")
            .L("teach", "大丈夫。あなたなら、ちゃんとできるわ")
            .L("thanks", "ありがとう")
            .L("accept", "ええ、いいわよ")
            .L("refuse", "今はごめんなさいね")
            .L("night", "おやすみなさい、坊や")
            .L("surprise", "あら")
            .L("hurt", "あら…失敗しちゃった")
            .L("rank1", "ふふ、よかった")
            .L("examGood", "思ったとおりね")
            .L("examBad", "飛行術は…ね")
            .L("cute", "ふふ、どうしたの坊や。話してごらんなさい"));
            CharLines.Nick["Y"] = "サラ姉"; CharLines.Honor["Y"] = "";
            // Z フェリシア（サキュバス・魔法師課程2年）：色っぽく見えて実は恋に不慣れで純情（企画\キャラZ_フェリシア_設定.md）
            All.Add(new CharProfile
            {
                letter = "Z", name = "フェリシア", shortName = "フェリ", race = "サキュバス", personality = "色っぽく見えて実は恋に不慣れで純情",
                color = new Color(0.878f, 0.251f, 0.604f), social = 0.7f, whim = 0.5f, diligence = 0.45f,
                hobby = SimCharacter.HobbyKind.Tea, hobbyText = "恋愛小説・香水作り", hobbyPlace = "温室",
                likes = new[] { "恋愛小説", "ハーブティー", "夜の散歩" }, dislikes = new[] { "本気の告白", "回復魔法", "古代語" },
                traits = CharTrait.None, traitText = "",
                strongStudy = Herb, weakStudy = Ancient, strongPractical = MagicArts, weakPractical = Medicine,
                enrolled = new[] { Theory, Ancient, Herb, Tools, MagicArts, Medicine, Sword, Dungeon }, affiliation = Affiliation.Mage, callF = "ダーリン", callFClose = "篤くん", callFCloseStage = 2,
                course = "魔法師", grade = 2, teacher = false, heightCm = 175f,
                initAbility = new float[] { 48, 88, 62, 75, 62 }, initSex = 4f,
                look = HeroLook(new Color(0.878f, 0.251f, 0.604f), "Hair_LongBack", "Acc_Horns"),
            }
            .L("hello_m", "おはよ、ダーリン♡", "ふぁ…朝は苦手なのよね")
            .L("hello_d", "ふふ、ダーリン♡", "あら、来てくれたの？")
            .L("hello_n", "夜はこれからよ♡", "お、おやすみ…")
            .L("close", "{0}♡ こっちよ")
            .L("reply", "ふふ、元気よ♡")
            .L("meal", "おいしいわね♡", "あーんしてあげよっか？…じょ、冗談よ！", "ハーブティーある？")
            .L("snack", "甘いの、好きよ♡")
            .L("bath", "ふぅ…いいお湯", "み、見ないでよ？")
            .L("seat", "ふふ", "あら", "そう？", "♡")
            .L("chatter", "ふふ♡", "あら", "そうなの？", "ふーん", "もう♡")
            .L("topic", "ねえ、{0}の話しない？")
            .L("like", "あら、それ好きよ♡")
            .L("dislike", "…その話は、やめましょ")
            .L("sleepy", "ふぁ…")
            .L("sigh", "はぁ…")
            .L("frustr", "もう…！")
            .L("done", "終わったわ♡")
            .L("busy", "課題…古代語、むずかしいのよね")
            .L("teach", "ふふ、教えてあげる♡")
            .L("thanks", "あ、ありがと…")
            .L("accept", "ふふ、いいわよ♡")
            .L("refuse", "い、今はだめよ")
            .L("night", "おやすみ、ダーリン♡")
            .L("surprise", "きゃっ！")
            .L("hurt", "…っ、失敗しちゃった")
            .L("rank1", "ふふ、当然よ♡")
            .L("examGood", "やったわ♡")
            .L("examBad", "古代語なんて…")
            .L("cute", "え、ほ、本当に手…つなぐの！？"));
            CharLines.Nick["Z"] = "フェリ"; CharLines.Honor["Z"] = "ちゃん";
            // AA コーデリア（人間（伯爵家）・魔法師課程2年）：笑顔で毒舌の策士・身内には甘い（企画\キャラAA_コーデリア_設定.md）
            All.Add(new CharProfile
            {
                letter = "AA", name = "コーデリア", shortName = "コーデ", race = "人間（伯爵家）", personality = "笑顔で毒舌の策士・身内には甘い",
                color = new Color(0.557f, 0.141f, 0.251f), social = 0.6f, whim = 0.3f, diligence = 0.7f,
                hobby = SimCharacter.HobbyKind.Tea, hobbyText = "盤の遊び・噂の帳面", hobbyPlace = null,
                likes = new[] { "盤の陣取り遊び", "噂話", "苦いお茶" }, dislikes = new[] { "思いどおりにならないこと", "汗をかく運動" },
                traits = CharTrait.None, traitText = "",
                strongStudy = Theory, weakStudy = Herb, strongPractical = MagicArts, weakPractical = Sword,
                enrolled = new[] { Theory, Ancient, Herb, Tools, MagicArts, Sword, Dungeon, Alchemy }, affiliation = Affiliation.Mage, callF = "あなた", callFClose = "篤", callFCloseStage = 2,
                course = "魔法師", grade = 2, teacher = false, heightCm = 160f,
                initAbility = new float[] { 33, 75, 62, 62, 88 }, initSex = 4f,
                look = HeroLook(new Color(0.557f, 0.141f, 0.251f), "Hair_LongBack", "Hair_HimeSide"),
            }
            .L("hello_m", "おはようございます。素敵な寝ぐせですこと", "あら、早いのね")
            .L("hello_d", "ごきげんよう", "まあ、あなた")
            .L("hello_n", "おやすみなさいませ。夜ふかしは肌に悪くてよ")
            .L("close", "{0}、少しよろしくて？")
            .L("reply", "ええ、相変わらずですわ")
            .L("meal", "まあまあですわね", "苦いお茶はあるかしら", "あら、食べるのが早いこと")
            .L("snack", "甘いものは、ほどほどに")
            .L("bath", "ふぅ…いいお湯ですわね", "ここだけの話、ですけれど")
            .L("seat", "あら", "まあ", "それで？", "ふふ")
            .L("chatter", "まあ", "あら", "ふふ", "そうですの？", "面白いこと")
            .L("topic", "{0}の噂、ご存じ？")
            .L("like", "あら、わかっていらっしゃるのね")
            .L("dislike", "…汗をかく話はやめてくださる？")
            .L("sleepy", "…ふぁ、失礼")
            .L("sigh", "はぁ")
            .L("frustr", "思いどおりにいきませんわね")
            .L("done", "終わりましたわ。当然ですけれど")
            .L("busy", "課題…段取りが大事ですわ")
            .L("teach", "よろしくて？ 先を読むのです")
            .L("thanks", "…礼を言いますわ")
            .L("accept", "よろしくてよ", "ふふ、面白そう")
            .L("refuse", "今はご遠慮しますわ")
            .L("night", "おやすみなさいませ")
            .L("surprise", "まあ！")
            .L("hurt", "…今のは、見なかったことに")
            .L("rank1", "計算どおりですわ")
            .L("examGood", "ふふ、当然ですわね")
            .L("examBad", "…体術なんて、淑女のすることでは")
            .L("cute", "まあ、素敵な寝ぐせですこと。わざとですわよね？"));
            CharLines.Nick["AA"] = "コーデ"; CharLines.Honor["AA"] = "さん";
            // AB ナイア（湖の精霊・魔法師課程2年）：静かでもの思いにふける・甘え上手（企画\キャラAB_ナイア_設定.md）
            All.Add(new CharProfile
            {
                letter = "AB", name = "ナイア", shortName = "ナイア", race = "湖の精霊", personality = "静かでもの思いにふける・甘え上手",
                color = new Color(0.169f, 0.639f, 0.627f), social = 0.4f, whim = 0.45f, diligence = 0.5f,
                hobby = SimCharacter.HobbyKind.Walk, hobbyText = "水辺でぼんやり・星をながめる", hobbyPlace = "中庭",
                likes = new[] { "雨の音", "静かな湖", "月の夜", "ミルクティー" }, dislikes = new[] { "元素魔法", "乾いた場所", "大きな音" },
                traits = CharTrait.None, traitText = "",
                strongStudy = History, weakStudy = Herb, strongPractical = Medicine, weakPractical = MagicArts,
                enrolled = new[] { Theory, Ancient, History, Herb, MagicArts, Medicine, Sword, Dungeon }, affiliation = Affiliation.Mage, callF = "篤",
                course = "魔法師", grade = 2, teacher = false, heightCm = 162f,
                initAbility = new float[] { 48, 75, 62, 75, 62 }, initSex = 4f,
                look = HeroLook(new Color(0.169f, 0.639f, 0.627f), "Hair_LongBack"),
            }
            .L("hello_m", "…おはよう", "…雨の音で、目が覚めた")
            .L("hello_d", "…ん、篤", "…こんにちは")
            .L("hello_n", "…月がきれい", "…おやすみ")
            .L("close", "…{0}、こっち")
            .L("reply", "…うん、元気")
            .L("meal", "…おいしい", "…ミルクティー、ある？", "…ゆっくり、食べる")
            .L("snack", "…甘いの、すき")
            .L("bath", "…お湯、きもちいい", "…ここ、湖みたい")
            .L("seat", "…うん", "…そう", "…ふふ", "…")
            .L("chatter", "…うん", "…そうなんだ", "…わたしも、そう思う", "…ふふ", "…")
            .L("topic", "…{0}のこと、考えてた")
            .L("like", "…それ、すき")
            .L("dislike", "…大きな音は、いや")
            .L("sleepy", "…ねむい")
            .L("sigh", "…ふぅ")
            .L("frustr", "…かわいてる")
            .L("done", "…おわった")
            .L("busy", "…課題、すこしずつ")
            .L("teach", "…ここは、こう")
            .L("thanks", "…ありがと")
            .L("accept", "…うん、いいよ")
            .L("refuse", "…いまは、ちょっと")
            .L("night", "…おやすみ、篤")
            .L("surprise", "…っ")
            .L("hurt", "…しっぱい")
            .L("rank1", "…いちばん？")
            .L("examGood", "…よかった")
            .L("examBad", "…元素魔法、にがて")
            .L("cute", "……篤、今日は少し、寂しそう", "…うん。わたしも、そう思う"));
            CharLines.Nick["AB"] = "ナイ"; CharLines.Honor["AB"] = "";
            // AC タマモ（狐・魔法師課程1年）：からかい上手の小悪魔（企画\キャラAC_タマモ_設定.md）
            All.Add(new CharProfile
            {
                letter = "AC", name = "タマモ", shortName = "タマモ", race = "狐", personality = "からかい上手の小悪魔",
                color = new Color(0.910f, 0.510f, 0.227f), social = 0.75f, whim = 0.75f, diligence = 0.35f,
                hobby = SimCharacter.HobbyKind.Magic, hobbyText = "魔道具いじり・狐火の手品", hobbyPlace = "工房",
                likes = new[] { "いたずら", "いなり寿司", "人をからかうこと" }, dislikes = new[] { "まっすぐな好意", "錬成", "古代語" },
                traits = CharTrait.None, traitText = "",
                strongStudy = Tools, weakStudy = Ancient, strongPractical = MagicArts, weakPractical = Alchemy,
                enrolled = new[] { Theory, Ancient, Tools, Herb, MagicArts, Alchemy, Sword, Dungeon }, affiliation = Affiliation.Mage, callF = "篤ちゃん",
                course = "魔法師", grade = 1, teacher = false, heightCm = 157f,
                initAbility = new float[] { 48, 75, 75, 75, 62 }, initSex = 6f,
                look = HeroLook(new Color(0.910f, 0.510f, 0.227f), "Hair_LongBack", "Acc_FoxEars", "Acc_FoxTail"),
            }
            .L("hello_m", "おはよ、篤ちゃん♪", "ふふっ、寝ぐせついてるよ？")
            .L("hello_d", "やっほー、篤ちゃん", "なになに？ わっちに用？")
            .L("hello_n", "夜はいたずらの時間だよ♪", "おやすみ…なーんてね")
            .L("close", "{0}、こっちおいで〜")
            .L("reply", "ふふっ、元気だよ♪")
            .L("meal", "いなり寿司ないの〜？", "おいしー♪", "それ、ひと口ちょうだい")
            .L("snack", "甘いのいただき♪")
            .L("bath", "ふぁ〜…しっぽがふくらむ〜", "のぞいたら狐火だよ？")
            .L("seat", "ふふっ", "へー", "それで？", "くすっ")
            .L("chatter", "ふふっ", "へー♪", "そうなの？", "くすくす", "いいじゃん")
            .L("topic", "ねえねえ、{0}の話知ってる？")
            .L("like", "それ、好き〜♪")
            .L("dislike", "……べ、別にその話、気にしてないし")
            .L("sleepy", "ふぁ…")
            .L("sigh", "ちぇ〜")
            .L("frustr", "むぅ〜")
            .L("done", "終わったよ〜♪ ほめて？")
            .L("busy", "課題…だれかに化けてやってもらお")
            .L("teach", "ここはね、こうすると…ほら♪")
            .L("thanks", "……べ、別にうれしくないし。ありがと")
            .L("accept", "いいよ〜♪", "ふふっ、まかせて")
            .L("refuse", "今はだ〜め")
            .L("night", "おやすみ、篤ちゃん♪")
            .L("surprise", "わっ！？")
            .L("hurt", "いたた…しっぽ焦げた")
            .L("rank1", "わっちが一番♪")
            .L("examGood", "ふふん、余裕♪")
            .L("examBad", "錬成なんて、きらい…")
            .L("cute", "ふふっ、篤ちゃんってば顔赤〜い♪", "……べ、別に本気で心配したわけじゃないし"));
            CharLines.Nick["AC"] = "タマちゃん"; CharLines.Honor["AC"] = "ちゃん";
            // AD スカーレット（炎の精霊・魔法師課程1年）：熱血で負けずぎらい（企画\キャラAD_スカーレット_設定.md）
            All.Add(new CharProfile
            {
                letter = "AD", name = "スカーレット", shortName = "スカー", race = "炎の精霊", personality = "熱血で負けずぎらい",
                color = new Color(0.941f, 0.486f, 0.424f), social = 0.7f, whim = 0.55f, diligence = 0.6f,
                hobby = SimCharacter.HobbyKind.Magic, hobbyText = "魔道具の修理・朝のランニング", hobbyPlace = "工房",
                likes = new[] { "勝負ごと", "辛い料理", "たき火", "晴れた日" }, dislikes = new[] { "雨の日", "飛行術", "占星術" },
                traits = CharTrait.None, traitText = "",
                strongStudy = Tools, weakStudy = History, strongPractical = MagicArts, weakPractical = Fly,
                enrolled = new[] { Theory, Ancient, Tools, History, MagicArts, Fly, Sword, Dungeon }, affiliation = Affiliation.Mage, callF = "あんた", callFClose = "篤", callFCloseStage = 2,
                course = "魔法師", grade = 1, teacher = false, heightCm = 150f,
                initAbility = new float[] { 75, 62, 62, 75, 48 }, initSex = 3f,
                look = HeroLook(new Color(0.941f, 0.486f, 0.424f), "Hair_Pony"),
            }
            .L("hello_m", "おはよ！ 走りに行くわよ！", "よーし、今日も燃えるわよ！")
            .L("hello_d", "あんた！ 勝負よ！", "なによ、元気？")
            .L("hello_n", "まだ燃え足りないわ！", "おやすみ！ 明日も勝負よ！")
            .L("close", "{0}！ こっち来なさい！")
            .L("reply", "元気に決まってるでしょ！")
            .L("meal", "辛いのない？", "おかわり！", "燃料補給よ！")
            .L("snack", "ひと口でいけるわ！")
            .L("bath", "あっつい湯、最高！", "もっと熱くしてもいいわよ！")
            .L("seat", "で？", "ふん", "いいじゃない", "燃えてきた！")
            .L("chatter", "いいわね！", "でしょ！", "まけないわよ", "ほんと？", "燃えるわ！")
            .L("topic", "ねえ、{0}の話よ！")
            .L("like", "それ、燃えるわね！")
            .L("dislike", "雨の話はやめて！")
            .L("sleepy", "…ねむくないし")
            .L("sigh", "ふん")
            .L("frustr", "くぅ〜、くやしい！")
            .L("done", "終わったわ！ 次！")
            .L("busy", "課題…一気に片付けるわ！")
            .L("teach", "こうよ！ 気合いでいくの！")
            .L("thanks", "…ありがと")
            .L("accept", "いいわよ！ 勝負ね！")
            .L("refuse", "今は無理！")
            .L("night", "おやすみ！ 明日も燃えるわよ！")
            .L("surprise", "わっ！？")
            .L("hurt", "ま、負けてないし！ 今のは練習！")
            .L("rank1", "あたしが一番！")
            .L("examGood", "よっしゃ！")
            .L("examBad", "飛行術なんて…次は勝つ！")
            .L("cute", "よーし、燃えてきたっ！ あんたも勝負しなさい！"));
            CharLines.Nick["AD"] = "スカー"; CharLines.Honor["AD"] = "";
            // AE メリッサ（人間（宮廷魔導師の娘）・魔法師課程1年）：努力家だが自信がない（企画\キャラAE_メリッサ_設定.md）
            All.Add(new CharProfile
            {
                letter = "AE", name = "メリッサ", shortName = "メリッサ", race = "人間（宮廷魔導師の娘）", personality = "努力家だが自信がない",
                color = new Color(0.949f, 0.831f, 0.227f), social = 0.3f, whim = 0.2f, diligence = 0.95f,
                hobby = SimCharacter.HobbyKind.Magic, hobbyText = "夜の魔法の自習", hobbyPlace = "図書室",
                likes = new[] { "魔法書", "図書室のすみの席", "ほめられること" }, dislikes = new[] { "人前で魔法を使うこと", "父からの手紙" },
                traits = CharTrait.None, traitText = "",
                strongStudy = Theory, weakStudy = History, strongPractical = MagicArts, weakPractical = Alchemy,
                enrolled = new[] { Theory, Ancient, History, Tools, MagicArts, Alchemy, Sword, Dungeon }, affiliation = Affiliation.Mage, callF = "篤さん",
                course = "魔法師", grade = 1, teacher = false, heightCm = 148f,
                initAbility = new float[] { 18, 48, 48, 88, 75 }, initSex = 2f,
                look = HeroLook(new Color(0.949f, 0.831f, 0.227f), "Hair_Twin", "Acc_Glasses"),
            }
            .L("hello_m", "お、おはようございます…", "おはようございます。…今日もがんばります")
            .L("hello_d", "あ、篤さん…", "こ、こんにちは")
            .L("hello_n", "もう少しだけ、自習を…", "おやすみなさい…")
            .L("close", "{0}、あの…少しいいですか")
            .L("reply", "は、はい、元気です")
            .L("meal", "おいしいです…", "いただきます", "…食べながら本、だめですよね")
            .L("snack", "い、いいんですか？")
            .L("bath", "ふぅ…落ち着きます", "…今日の失敗、流れていけばいいのに")
            .L("seat", "そ、そうなんですね", "はい", "…すごいです", "えっと")
            .L("chatter", "は、はい", "すごいです", "そうなんですね", "…なるほど", "えへへ")
            .L("topic", "あの、{0}のことなんですけど…")
            .L("like", "そ、それ、好きです")
            .L("dislike", "…人前で魔法を使うのは、ちょっと…")
            .L("sleepy", "…ふぁ、夜ふかししすぎました")
            .L("sigh", "はぁ…")
            .L("frustr", "うう…")
            .L("done", "お、終わりました…たぶん")
            .L("busy", "課題、もう一回見直します…")
            .L("teach", "で、でも、この術式なら…たぶん…")
            .L("thanks", "あ、ありがとうございます…！")
            .L("accept", "は、はい！ がんばります")
            .L("refuse", "す、すみません、今は…")
            .L("night", "おやすみなさい、篤さん")
            .L("surprise", "ひゃっ！")
            .L("hurt", "す、すみません…わたし、また失敗して…")
            .L("rank1", "え、わたしが…一番？")
            .L("examGood", "よ、よかった…")
            .L("examBad", "やっぱり、わたしなんて…")
            .L("cute", "で、でも、この術式なら…たぶん…"));
            CharLines.Nick["AE"] = "メリ"; CharLines.Honor["AE"] = "さん";
            // AF ウィスティ（妖精・先生）：気まぐれでマイペースな先生（企画\キャラAF_ウィスティ_設定.md）
            All.Add(new CharProfile
            {
                letter = "AF", name = "ウィスティ", shortName = "ウィス", race = "妖精", personality = "気まぐれでマイペースな先生",
                color = new Color(0.549f, 0.620f, 0.922f), social = 0.65f, whim = 0.9f, diligence = 0.3f,
                hobby = SimCharacter.HobbyKind.Tea, hobbyText = "庭の花の世話", hobbyPlace = "温室",
                likes = new[] { "甘い花の蜜", "昼寝", "生徒をびっくりさせること" }, dislikes = new[] { "会議", "早起き", "鉄" },
                traits = CharTrait.DeepSleeper, traitText = "早起きが苦手",
                strongStudy = Theory, weakStudy = -1, strongPractical = MagicArts, weakPractical = -1,
                enrolled = new[] { MagicArts, Theory, Ancient, Medicine }, affiliation = Affiliation.None, callF = "あっくん",
                course = "先生", grade = 0, teacher = true, heightCm = 142f,
                initAbility = new float[] { 33, 75, 62, 88, 75 }, initSex = 6f,
                look = HeroLook(new Color(0.549f, 0.620f, 0.922f), "Hair_Bob", "Acc_ElfEars"),
            }
            .L("hello_m", "ふぁ…おはよ、あっくん", "早起きはきらーい")
            .L("hello_d", "やっほー、あっくん", "ん〜、今日も気分しだい♪")
            .L("hello_n", "夜のお花、きれいだよ〜", "おやすみ〜")
            .L("close", "{0}、こっちこっち")
            .L("reply", "おねーさんは今日も元気〜")
            .L("meal", "花の蜜はないの〜？", "おいし〜", "ちょっとだけでいいの、おねーさん小さいから")
            .L("snack", "甘いの大好き〜♪")
            .L("bath", "ふわぁ…羽がしっとり〜", "おねーさん、浮いちゃいそう")
            .L("seat", "ん〜", "ふふ", "そうなの？", "気分しだい♪")
            .L("chatter", "ん〜♪", "ふふっ", "へ〜", "いいね〜", "おねーさん感心")
            .L("topic", "{0}のこと、おねーさん知ってるよ〜")
            .L("like", "それ好き〜♪")
            .L("dislike", "会議の話はやめよ〜")
            .L("sleepy", "ふぁ…お昼寝の時間〜")
            .L("sigh", "ふぅ〜")
            .L("frustr", "もぉ〜")
            .L("done", "おわり〜♪")
            .L("busy", "採点…あとでいっか")
            .L("teach", "ここはね〜、魔力をふわっとさせるの")
            .L("thanks", "ありがと〜")
            .L("accept", "いいよ〜、気分だし♪")
            .L("refuse", "今は気分じゃないの〜")
            .L("night", "おやすみ〜。いい子は寝る時間だよ")
            .L("surprise", "わわっ！？")
            .L("hurt", "あれ〜？ 失敗しちゃった")
            .L("rank1", "おねーさんが一番〜♪")
            .L("examGood", "いい問題作ったでしょ〜")
            .L("examBad", "採点、おわんない…")
            .L("cute", "ん〜、今日の授業は外でやろっか。気分だし♪", "おねーさんね、これでも学園でいちばん長生きなんだよ？")
            .L("c_go", "授業いってくるね〜", "今日は外でやろっかな〜")
            .L("c_exam", "試験の監督〜、ねむい〜"));
            CharLines.Nick["AF"] = "ウィス先生"; CharLines.Honor["AF"] = "ちゃん";
            // AG ジゼル（ハーフエルフ・先生）：さっぱりした姉御肌の先生（企画\キャラAG_ジゼル_設定.md）
            All.Add(new CharProfile
            {
                letter = "AG", name = "ジゼル", shortName = "ジゼル", race = "ハーフエルフ", personality = "さっぱりした姉御肌の先生",
                color = new Color(0.910f, 0.706f, 0.627f), social = 0.75f, whim = 0.5f, diligence = 0.55f,
                hobby = SimCharacter.HobbyKind.Reading, hobbyText = "古い地図集め", hobbyPlace = "図書室",
                likes = new[] { "旅の話", "焚き火の料理", "地図" }, dislikes = new[] { "書類仕事", "じっと座っていること" },
                traits = CharTrait.None, traitText = "",
                strongStudy = -1, weakStudy = -1, strongPractical = Sword, weakPractical = -1,
                enrolled = new[] { Dungeon, Monster, Herb, Fly }, affiliation = Affiliation.None, callF = "篤",
                course = "先生", grade = 0, teacher = true, heightCm = 168f,
                initAbility = new float[] { 75, 75, 75, 62, 62 }, initSex = 6f,
                look = HeroLook(new Color(0.910f, 0.706f, 0.627f), "Hair_Pony", "Acc_ElfEars"),
            }
            .L("hello_m", "よっ、篤！ 朝飯食ったか？", "おはよう！ 今日も外は気持ちいいぞ")
            .L("hello_d", "よっ！", "おう、篤")
            .L("hello_n", "夜更かしすんなよ", "おやすみ！ 明日は実習だ")
            .L("close", "{0}！ ちょっと来な")
            .L("reply", "おう、元気だよ")
            .L("meal", "うまい！", "焚き火で作ると、もっとうまいんだけどな", "しっかり食えよ")
            .L("snack", "お、いいね")
            .L("bath", "ふぃ〜、生き返る", "旅の途中じゃ、こんな風呂はなかったな")
            .L("seat", "おう", "で？", "はは", "昔、北の雪山でさ…")
            .L("chatter", "はは", "おう", "いいね", "そりゃいい", "へえ")
            .L("topic", "{0}の話、聞いたことあるか？")
            .L("like", "おっ、その話いいな")
            .L("dislike", "書類の話は勘弁してくれ")
            .L("sleepy", "ふあ…")
            .L("sigh", "やれやれ")
            .L("frustr", "あ〜、書類が終わらねえ")
            .L("done", "よし、片付いた")
            .L("busy", "書類仕事…苦手なんだよな")
            .L("teach", "いいか、まず地図を見るんだ")
            .L("thanks", "ありがとな")
            .L("accept", "おう、まかせな！")
            .L("refuse", "悪い、今はちょっとな")
            .L("night", "おやすみ！")
            .L("surprise", "おっと！")
            .L("hurt", "いてて…腕がなまったかな")
            .L("rank1", "はは、当然だ")
            .L("examGood", "いい出来だな")
            .L("examBad", "採点がきつかったかな…")
            .L("cute", "よっ、篤！ 今日は外で実習だ、気合い入れていきな！", "昔、北の雪山でさ…あ、この話もう3回目？")
            .L("c_go", "実習いってくる！", "よし、授業だ")
            .L("c_exam", "試験の監督だ。カンニングすんなよ"));
            CharLines.Nick["AG"] = "ジゼル姉"; CharLines.Honor["AG"] = "";
            // AH クラリス（人間・先生）：まじめで張り切りすぎの新任教官（企画\キャラAH_クラリス_設定.md）
            All.Add(new CharProfile
            {
                letter = "AH", name = "クラリス", shortName = "クラリス", race = "人間", personality = "まじめで張り切りすぎの新任教官",
                color = new Color(0.541f, 0.604f, 0.294f), social = 0.6f, whim = 0.25f, diligence = 0.95f,
                hobby = SimCharacter.HobbyKind.Walk, hobbyText = "剣の手入れ", hobbyPlace = "中庭",
                likes = new[] { "騎士の物語", "朝の走りこみ", "生徒にたよられること" }, dislikes = new[] { "書類仕事", "人前での失敗" },
                traits = CharTrait.None, traitText = "",
                strongStudy = -1, weakStudy = -1, strongPractical = Sword, weakPractical = -1,
                enrolled = new[] { Sword, Monster, History, Fly }, affiliation = Affiliation.None, callF = "篤くん",
                course = "先生", grade = 0, teacher = true, heightCm = 166f,
                initAbility = new float[] { 75, 62, 33, 48, 62 }, initSex = 3f,
                look = HeroLook(new Color(0.541f, 0.604f, 0.294f), "Hair_Bob"),
            }
            .L("hello_m", "おはようございます！ 本日もよろしくお願いします！", "朝の走りこみ、終わりました！")
            .L("hello_d", "はいっ！ 何でしょうか！", "篤くん、ご苦労さまです！")
            .L("hello_n", "消灯時間です！ …あ、自分もでした", "おやすみなさいませ！")
            .L("close", "{0}！ 少しよろしいでしょうか！")
            .L("reply", "はい、問題ありません！")
            .L("meal", "いただきます！", "栄養補給、完了しました！", "おいしいです！")
            .L("snack", "よ、よろしいのですか！")
            .L("bath", "ふぅ…気が引きしまります", "…はっ、長湯はいけませんね")
            .L("seat", "はいっ", "なるほど", "勉強になります", "そうでしたか")
            .L("chatter", "はいっ！", "なるほど！", "勉強になります", "さすがです", "そうでしたか")
            .L("topic", "{0}について、ご意見をうかがいたく！")
            .L("like", "それは大好きです！")
            .L("dislike", "…書類仕事の話は、ちょっと")
            .L("sleepy", "ね、眠くありません！")
            .L("sigh", "…ふぅ")
            .L("frustr", "うう、書類が…")
            .L("done", "完了しました！")
            .L("busy", "書類、書類…")
            .L("teach", "自分が説明します！ …あっ、教科書が逆さでした")
            .L("thanks", "ありがとうございます！")
            .L("accept", "はいっ、喜んで！")
            .L("refuse", "も、申しわけありません、今は…")
            .L("night", "おやすみなさいませ！")
            .L("surprise", "はわっ！？")
            .L("hurt", "し、失礼しました！")
            .L("rank1", "光栄です！")
            .L("examGood", "よかった…！")
            .L("examBad", "採点を間違えました…")
            .L("cute", "自分、本日より着任いたしました！…あっ、教科書が逆さでした")
            .L("c_go", "授業に行ってまいります！", "本日も全力で教えます！")
            .L("c_exam", "試験監督、務めます！"));
            CharLines.Nick["AH"] = "クラリス先生"; CharLines.Honor["AH"] = "さん";
        }

        // ---- 追加のヒロインの最初の間柄（設定の「友達」「ライバル」「親友」）----
        static readonly HashSet<string> NewFriends = new HashSet<string> { "AA|AB", "AA|Y", "AA|Z", "AB|Y", "AB|Z", "AC|AD", "AC|AE", "AD|AE", "L|M", "L|N", "M|N", "O|P", "O|Q", "O|R", "P|Q", "P|R", "S|T", "S|U", "T|U", "V|W", "V|X", "W|X", "Y|Z" };
        static readonly HashSet<string> NewRivals = new HashSet<string> { "A|L", "AA|L", "P|W" };
        static readonly HashSet<string> NewBest = new HashSet<string> { "Q|R" };

        static bool IsNewHeroine(CharProfile p) { return p.letter.Length >= 2 || p.letter[0] >= 'L'; }

        static string PairKey(string x, string y) { return string.CompareOrdinal(x, y) < 0 ? x + "|" + y : y + "|" + x; }

        /// <summary>追加のヒロインがかかわる2人の、最初の好感度と相性。どちらも今までの11人なら false。</summary>
        static bool NewPair(CharProfile a, CharProfile b, out float aff, out float c)
        {
            aff = 20f; c = 0f;
            if (!IsNewHeroine(a) && !IsNewHeroine(b)) return false;
            string k = PairKey(a.letter, b.letter);
            int h = 17;
            foreach (char ch in k) h = h * 31 + ch;
            var rnd = new System.Random(h);
            c = (float)(rnd.NextDouble() * 0.8 - 0.3);
            if (NewBest.Contains(k)) { aff = 85f; c = Mathf.Max(c, 0.5f) + 0.2f; }
            else if (NewFriends.Contains(k)) { aff = 50f; c = Mathf.Max(c, 0.35f) + 0.15f; }
            else if (NewRivals.Contains(k)) { aff = 32f; c = 0.05f; }
            else if (a.teacher || b.teacher) aff = 30f;               // 先生はみんなと知り合い
            else if (a.course == b.course && a.grade == b.grade) aff = 26f; // 同じ課程・同じ学年
            return true;
        }

        // ================= 寮生以外の学生・先生 =================
        public static readonly string[] MobDormNames = { "冒険者寮", "騎士寮", "魔法師寮", "先生" };
        static readonly string[] MobCourse = { "冒険者", "騎士", "魔法師", "先生" };
        static readonly string[] MobLetterHead = { "mobA", "mobK", "mobM", "mobT" };
        static readonly string[] MobShortHead = { "冒", "騎", "魔", "先" };
        public const int MobPerDorm = 30, MobTeachers = 7;

        static void AddMobs()
        {
            ResidentCount = All.Count;
            var rnd = new System.Random(4242);
            System.Func<float, float, float> R = (a, b) => a + (float)rnd.NextDouble() * (b - a);
            Color black = new Color(0.07f, 0.07f, 0.09f), eye = new Color(0.25f, 0.16f, 0.1f);
            Color[] tint = { new Color(0.62f, 0.45f, 0.3f), new Color(0.45f, 0.55f, 0.72f), new Color(0.6f, 0.45f, 0.75f), new Color(0.5f, 0.5f, 0.52f) };
            float[][] baseAb =
            {
                new float[] { 55, 50, 55, 48, 50 }, // 冒険者：なんでも少しずつ
                new float[] { 64, 48, 45, 44, 48 }, // 騎士：体力
                new float[] { 42, 50, 52, 64, 58 }, // 魔法師：魔力と知力
                new float[] { 52, 55, 58, 66, 70 }, // 先生
            };
            int[] study = { Academics.Theory, Academics.Ancient, Academics.Tools, Academics.Herb, Academics.History, Academics.Monster };
            int[] prac = { Academics.Sword, Academics.MagicArts, Academics.Dungeon, Academics.Alchemy, Academics.Flight, Academics.Medicine };
            string[] hairs = { "Hair_Bob", "Hair_HimeSide", "Hair_Pony" }; // ショート・セミロング・ポニーテール
            float[] heights = { 152f, 158f, 164f };
            for (int g = 0; g < 4; g++)
            {
                int n = g < 3 ? MobPerDorm : MobTeachers;
                for (int i = 1; i <= n; i++)
                {
                    bool teacher = g == 3;
                    int grade = teacher ? 0 : 1 + (i - 1) / 10;
                    var p = new CharProfile
                    {
                        letter = MobLetterHead[g] + i.ToString("00"),
                        name = teacher ? "先生" + i : MobDormNames[g] + "の学生" + i,
                        shortName = MobShortHead[g] + i,
                        race = "人間",
                        personality = teacher ? "先生" : MobCourse[g] + "課程" + grade + "年",
                        color = tint[g],
                        social = R(0.2f, 0.8f), whim = R(0.2f, 0.8f), diligence = R(0.2f, 0.9f),
                        hobby = SimCharacter.HobbyKind.Walk, hobbyText = "",
                        course = MobCourse[g], grade = grade, teacher = teacher,
                        mob = true, mobDorm = g, mobNo = i,
                        affiliation = g == 1 ? Affiliation.Knight : (g == 2 ? Affiliation.Mage : Affiliation.None),
                        callF = "篤くん",
                        heightCm = heights[rnd.Next(3)] * R(0.97f, 1.03f),
                        look = new CharLook { hair = black, eye = eye, parts = new[] { teacher ? "Hair_LongBack" : hairs[rnd.Next(3)] } },
                        initSex = R(0f, 8f),
                    };
                    var ab = new float[5];
                    for (int k = 0; k < 5; k++) ab[k] = Mathf.Clamp(baseAb[g][k] + R(-12f, 12f), 5f, 95f);
                    p.initAbility = ab;
                    // 科目：座学4・実技4（騎士・魔法師は優先科目を必ず）。得意と苦手はばらばら
                    var sl = new List<int>(study); var pl = new List<int>(prac);
                    Shuffle(sl, rnd); Shuffle(pl, rnd);
                    if (g == 0) { pl.Remove(Academics.Dungeon); pl.Insert(0, Academics.Dungeon); } // 冒険者はダンジョン実習
                    if (teacher)
                    {
                        // 先生は2つの科目を教える（授業の時間は学校へ）
                        p.enrolled = new[] { sl[0], pl[0] };
                    }
                    else
                    {
                        var en = new List<int>();
                        en.AddRange(sl.GetRange(0, 4)); en.AddRange(pl.GetRange(0, 4));
                        p.enrolled = CharProfile.WithPriority(en.ToArray(), p.affiliation);
                        p.strongStudy = sl[rnd.Next(4)]; p.weakStudy = sl[(System.Array.IndexOf(sl.ToArray(), p.strongStudy) + 1 + rnd.Next(3)) % 4];
                        p.strongPractical = pl[rnd.Next(4)]; p.weakPractical = pl[(pl.IndexOf(p.strongPractical) + 1 + rnd.Next(3)) % 4];
                    }
                    All.Add(p);
                }
            }
        }

        static void Shuffle(List<int> l, System.Random rnd)
        {
            for (int i = l.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); int t = l[i]; l[i] = l[j]; l[j] = t; }
        }
    }
}
