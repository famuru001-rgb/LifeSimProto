using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>種族などの特徴（ゲームの中の小さな効果）。</summary>
    [System.Flags]
    public enum CharTrait
    {
        None = 0,
        WinterWeak = 1,   // 冬に弱い（冬は疲れやすい）
        FullMoon = 2,     // 満月の日はそわそわ（退屈がたまりやすい）
        DeepSleeper = 4,  // 眠りが深い（よく眠れて、起きるのが少し遅い）
        RainWings = 8,    // 雨の日は翼が重い（疲れやすい）
        Lonely = 16,      // さみしさがたまりやすい
    }

    /// <summary>
    /// キャラ1人分の決まりごと（名前・性格・好き嫌い・得意な科目・口調）。
    /// 内容は「企画\ゲーム内容_たたき台.md」のとおり。記号（A〜J）は3Dモデルや絵のファイル名に使う。
    /// </summary>
    public class CharProfile
    {
        public string letter;      // 記号（A〜J）。モデル・絵のファイル名
        public string name;        // 名前
        public string shortName;   // 関係表などの短い名前
        public string race;        // 種族
        public string personality; // 性格（ひとこと）
        public bool male;
        public Color color;
        public float social, whim, diligence;
        public SimCharacter.HobbyKind hobby;
        public string hobbyText;   // 趣味の表示
        public string[] likes = new string[0];
        public string[] dislikes = new string[0];
        public CharTrait traits;
        public string traitText = "";
        public int strongStudy = -1, weakStudy = -1, strongPractical = -1, weakPractical = -1;
        public int[] enrolled = new int[0]; // 取っている科目（6つ。篤は勉学のみ）
        public string callF = "篤くん";      // 篤の呼び方
        public readonly Dictionary<string, string[]> lines = new Dictionary<string, string[]>();

        public CharProfile L(string key, params string[] v) { lines[key] = v; return this; }

        int[] baseEnrolled;
        /// <summary>取っている科目を最初の状態に戻す（篤は魔法を覚えると実技が増えるため）。</summary>
        public void ResetEnrolled()
        {
            if (baseEnrolled == null) baseEnrolled = (int[])enrolled.Clone();
            else enrolled = (int[])baseEnrolled.Clone();
        }

        public bool Likes(string tag) { return System.Array.IndexOf(likes, tag) >= 0; }
        public bool Dislikes(string tag) { return System.Array.IndexOf(dislikes, tag) >= 0; }

        /// <summary>科目の伸びやすさ（得意 1.5倍・苦手 0.6倍）。</summary>
        public float Growth(int subject)
        {
            if (subject == strongStudy || subject == strongPractical) return 1.5f;
            if (subject == weakStudy || subject == weakPractical) return 0.6f;
            return 1f;
        }

        public bool IsStrong(int subject) { return subject >= 0 && (subject == strongStudy || subject == strongPractical); }
        public bool IsWeak(int subject) { return subject >= 0 && (subject == weakStudy || subject == weakPractical); }
    }

    /// <summary>会話の話題。tags がだれかの好き嫌いに当たると、会話が楽しく（気まずく）なりやすい。</summary>
    public class TalkTopic
    {
        public string text;
        public string[] tags;
        public TalkTopic(string t, params string[] g) { text = t; tags = g; }
    }

    public static class CharProfiles
    {
        // 科目の番号（Academics.Names と同じ並び）
        const int Theory = 0, Herb = 1, Tools = 2, Ancient = 3, Astro = 4, Element = 5, Heal = 6, Fly = 7, Body = 8, Alchemy = 9;

        public static readonly List<CharProfile> All = new List<CharProfile>();

        public static readonly TalkTopic[] Topics =
        {
            new TalkTopic("授業の話"), new TalkTopic("寮のうわさ"), new TalkTopic("週末の予定"), new TalkTopic("試験の話"),
            new TalkTopic("課題の話"), new TalkTopic("故郷の話"), new TalkTopic("使い魔の話"), new TalkTopic("図書館の話"),
            new TalkTopic("古い本の話", "本"), new TalkTopic("辛い料理の話", "辛い料理"), new TalkTopic("暖炉の話", "暖炉"),
            new TalkTopic("冬の寒さの話", "寒さ"), new TalkTopic("甘いお菓子の話", "甘いもの", "お菓子"),
            new TalkTopic("温室の植物の話", "植物", "薬草学"), new TalkTopic("ハーブティーの話", "ハーブティー", "お茶会"),
            new TalkTopic("新しい魔道具の話", "魔道具", "工具", "魔道具学"), new TalkTopic("肉料理の話", "肉", "庶民の食べ物"),
            new TalkTopic("走りに行く話", "走る", "じっとする"), new TalkTopic("満月の話", "満月"), new TalkTopic("編み物の話", "編み物"),
            new TalkTopic("星の話", "星", "占い"), new TalkTopic("飛行術の話", "高い所", "飛ぶ"), new TalkTopic("お茶会の話", "お茶会", "みんな", "注目"),
            new TalkTopic("占星術の話", "占星術", "占い"), new TalkTopic("実験の話", "実験", "大きな音"), new TalkTopic("にんじんケーキの話", "にんじんケーキ", "甘いもの"),
            new TalkTopic("料理の練習の話", "料理", "庶民の食べ物"), new TalkTopic("木の実の話", "木の実"), new TalkTopic("光る石の話", "光る物"),
            new TalkTopic("雷の話", "雷", "大きな音"), new TalkTopic("古代語の話", "古代語", "本"), new TalkTopic("元素魔法の話", "元素魔法"),
            new TalkTopic("ひとりの夜の話", "ひとり"), new TalkTopic("王都のうわさ", "正体"),
        };

        /// <summary>id（0〜9）で引く。見つからなければ null。</summary>
        public static CharProfile ById(int id) { return id >= 0 && id < All.Count ? All[id] : null; }

        static CharProfiles()
        {
            // A ヴィエッタ（竜人）
            All.Add(new CharProfile
            {
                letter = "A", name = "ヴィエッタ", shortName = "ヴィエ", race = "竜人", personality = "クールで無口、実は世話焼き",
                color = new Color(0.55f, 0.8f, 1f), social = 0.3f, whim = 0.15f, diligence = 0.75f,
                hobby = SimCharacter.HobbyKind.Reading, hobbyText = "古文書の読書",
                likes = new[] { "本", "辛い料理", "暖炉" }, dislikes = new[] { "寒さ", "甘いもの" },
                traits = CharTrait.WinterWeak, traitText = "冬に弱い",
                strongStudy = Ancient, weakStudy = Herb, strongPractical = Element, weakPractical = Heal,
                enrolled = new[] { Ancient, Element, Heal, Theory, Astro, Body }, callF = "篤",
            }
            .L("hello_m", "…おはよう").L("hello_d", "…ああ").L("hello_n", "…早く寝ろ")
            .L("close", "{0}か").L("reply", "…ああ", "…ん")
            .L("meal", "…悪くない", "辛味が足りないな", "…もう少し食べろ", "…温かいのはいい")
            .L("snack", "甘いのは…お前が食べろ", "…茶だけでいい")
            .L("bath", "…温まるな", "…ふぅ", "湯はいい")
            .L("seat", "…", "それで？", "…ふん", "…そうか")
            .L("chatter", "…ああ", "…そうか", "続けろ", "…ふむ", "…悪くない")
            .L("topic", "…{0}のことだが")
            .L("like", "…その話は嫌いじゃない").L("dislike", "…その話はいい")
            .L("sleepy", "…眠い").L("sigh", "…はぁ").L("frustr", "…苛々する").L("cold", "…寒い")
            .L("done", "…終わった").L("busy", "…課題が片付かん").L("teach", "…ここはこう読む", "…手は貸す")
            .L("thanks", "…助かる").L("accept", "…好きにしろ。…手は貸すが", "…わかった").L("refuse", "…今は無理だ")
            .L("night", "…おやすみ").L("surprise", "っ…！").L("hurt", "…っ、加減を誤った")
            .L("rank1", "…当然だ").L("examGood", "…まあまあだな").L("examBad", "…次は落とさん"));

            // B シルヴィエル（エルフ）
            All.Add(new CharProfile
            {
                letter = "B", name = "シルヴィエル", shortName = "シル", race = "エルフ", personality = "ツンとした高貴、人間の文化に興味",
                color = new Color(0.35f, 0.7f, 0.35f), social = 0.3f, whim = 0.2f, diligence = 0.8f,
                hobby = SimCharacter.HobbyKind.Tea, hobbyText = "園芸",
                likes = new[] { "植物", "ハーブティー", "お菓子" }, dislikes = new[] { "魔道具", "大きな音" },
                strongStudy = Herb, weakStudy = Tools, strongPractical = Heal, weakPractical = Body,
                enrolled = new[] { Herb, Heal, Tools, Ancient, Astro, Alchemy }, callF = "篤さん",
            }
            .L("hello_m", "ごきげんよう").L("hello_d", "あら").L("hello_n", "おやすみなさいませ")
            .L("close", "あら、{0}").L("reply", "…ふん、ごきげんよう")
            .L("meal", "人間の料理も…悪くありませんわ", "ハーブが効いていますわね", "…もう一ついただけます？")
            .L("snack", "…べつに人間のお菓子に興味など。…もう一ついただけます？", "このクッキー、内緒ですわよ")
            .L("bath", "…静かなお湯ですこと", "森の泉には及びませんけれど")
            .L("seat", "そうですの？", "ふぅん", "…それで？", "お行儀よくなさい")
            .L("chatter", "まあ", "そうですの？", "ふぅん", "…悪くありませんわ", "続けなさい")
            .L("topic", "{0}のことですけれど")
            .L("like", "あら、わかっていますのね").L("dislike", "…その話は好みませんわ")
            .L("sleepy", "…ふぁ、失礼").L("sigh", "はぁ…").L("frustr", "まったくもう…")
            .L("done", "終わりましたわ").L("busy", "課題、課題…").L("teach", "よろしくて？ ここは…")
            .L("thanks", "…礼を言いますわ").L("accept", "し、仕方ありませんわね", "よろしくてよ").L("refuse", "今は遠慮しますわ")
            .L("night", "おやすみなさいませ").L("surprise", "きゃっ！").L("hurt", "…っ、今のは見なかったことに")
            .L("heal", "じっとしていなさい、治しますわ")
            .L("rank1", "当然の結果ですわ").L("examGood", "まあ、こんなものですわ").L("examBad", "…人間の道具は難しいですわ"));

            // C ルピナ（銀狼）
            All.Add(new CharProfile
            {
                letter = "C", name = "ルピナ", shortName = "ルピ", race = "銀狼", personality = "人なつこい大型犬",
                color = new Color(0.78f, 0.8f, 0.86f), social = 0.9f, whim = 0.8f, diligence = 0.2f,
                hobby = SimCharacter.HobbyKind.Walk, hobbyText = "散歩・鍛錬",
                likes = new[] { "肉", "走る", "満月", "なでる" }, dislikes = new[] { "じっとする", "古代語" },
                traits = CharTrait.FullMoon, traitText = "満月の日はそわそわ",
                strongStudy = Herb, weakStudy = Ancient, strongPractical = Body, weakPractical = Alchemy,
                enrolled = new[] { Herb, Body, Ancient, Fly, Element, Heal }, callF = "篤",
            }
            .L("hello_m", "おっはよー！").L("hello_d", "よっ！").L("hello_n", "おやすみー！")
            .L("close", "{0}！ 遊ぼうぜ！").L("reply", "おう！")
            .L("meal", "肉！ 肉ある？", "うまっ！", "おかわり！")
            .L("snack", "これうまいな！", "もう一個！")
            .L("bath", "あっちー！", "泳いでいい？", "ぷはー！")
            .L("seat", "なあなあ", "それでさ！", "へへっ", "じっとしてんの苦手…")
            .L("chatter", "まじで！？", "へへっ", "おう！", "すげー！", "それで？ それで？")
            .L("topic", "なあ、{0}なんだけどさ！")
            .L("like", "それ最高じゃん！").L("dislike", "うへぇ、その話は…")
            .L("sleepy", "ふぁ〜あ").L("sigh", "はぁ〜…").L("frustr", "うがー！").L("moon", "満月だ…そわそわする！")
            .L("done", "終わったー！ 遊ぼうぜ！").L("busy", "課題めんどい〜").L("teach", "こうやってガッとやるんだよ！")
            .L("thanks", "サンキュ！").L("accept", "おう、まかせとけ！").L("refuse", "えー、今はやだ")
            .L("night", "おやすみー").L("surprise", "うわっ！").L("hurt", "いってて…へへ")
            .L("rank1", "よっしゃー！ 一番！").L("examGood", "やったぜ！").L("examBad", "うぅ…古代語わかんねぇ"));

            // D ミレイユ（羊）
            All.Add(new CharProfile
            {
                letter = "D", name = "ミレイユ", shortName = "ミレ", race = "羊", personality = "しっかり者の世話焼き",
                color = new Color(0.95f, 0.92f, 0.82f), social = 0.6f, whim = 0.3f, diligence = 0.75f,
                hobby = SimCharacter.HobbyKind.Reading, hobbyText = "編み物・星見",
                likes = new[] { "編み物", "甘いもの", "星" }, dislikes = new[] { "高い所", "雷", "大きな音" },
                traits = CharTrait.DeepSleeper, traitText = "眠りが深い",
                strongStudy = Astro, weakStudy = Theory, strongPractical = Heal, weakPractical = Fly,
                enrolled = new[] { Astro, Heal, Fly, Herb, Ancient, Alchemy }, callF = "篤さん",
            }
            .L("hello_m", "おはようございます").L("hello_d", "こんにちは").L("hello_n", "もう、ちゃんと寝ないとだめですよ")
            .L("close", "{0}さん、お疲れさまです").L("reply", "はい、おかげさまで")
            .L("meal", "ちゃんと野菜も食べてくださいね", "おいしいですね", "よく噛んでくださいね")
            .L("snack", "甘いもの、しあわせです…", "お茶、淹れましょうか？")
            .L("bath", "ぽかぽかですね", "肩までつかってくださいね")
            .L("seat", "ふふ", "そうなんですか？", "あ、ほつれてますよ", "今日もお疲れさまです")
            .L("chatter", "まあ", "ふふっ", "そうなんですね", "なるほど", "えらいですね")
            .L("topic", "{0}のことなんですけど…")
            .L("like", "まあ、すてきですね！").L("dislike", "あの…その話はちょっと…")
            .L("sleepy", "ふぁ…ねむねむ…").L("sigh", "はぁ…").L("frustr", "もう…")
            .L("done", "終わりました〜").L("busy", "課題、がんばらないと…").L("teach", "ここはね、こうするんですよ")
            .L("thanks", "ありがとうございます").L("accept", "はい、まかせてください").L("refuse", "ごめんなさい、今はちょっと…")
            .L("night", "おやすみなさい").L("surprise", "ひゃっ！").L("hurt", "あいたた…")
            .L("heal", "動かないで、すぐ治しますね")
            .L("rank1", "わあ、一番です…！").L("examGood", "よかったぁ").L("examBad", "飛行術、怖いです…"));

            // E ラフィーナ（天使族）
            All.Add(new CharProfile
            {
                letter = "E", name = "ラフィーナ", shortName = "ラフィ", race = "天使族", personality = "自信家のお嬢様、寂しがり",
                color = new Color(0.95f, 0.8f, 0.35f), social = 0.85f, whim = 0.5f, diligence = 0.45f,
                hobby = SimCharacter.HobbyKind.Tea, hobbyText = "お茶会",
                likes = new[] { "お茶会", "注目", "高い所" }, dislikes = new[] { "占星術", "ひとり" },
                traits = CharTrait.RainWings, traitText = "翼の手入れ・雨で翼が重い",
                strongStudy = Theory, weakStudy = Astro, strongPractical = Fly, weakPractical = Element,
                enrolled = new[] { Theory, Fly, Element, Heal, Ancient, Body }, callF = "篤",
            }
            .L("hello_m", "ごきげんよう！").L("hello_d", "あら、ごきげんよう").L("hello_n", "わたくしはまだ眠くありませんわ")
            .L("close", "{0}、わたくしとお話しなさい！").L("reply", "よろしくてよ！")
            .L("meal", "わたくしにふさわしい味ですわ", "紅茶はありまして？", "おーっほっほ！")
            .L("snack", "お茶会の始まりですわ！", "このお菓子、合格ですわ")
            .L("bath", "翼が重いですわ…", "羽を乾かすのが大変ですの")
            .L("seat", "聞いていまして？", "わたくしに任せなさい！", "ふふん", "…ひとりにしないでくださる？")
            .L("chatter", "当然ですわ！", "まあ！", "ふふん", "続けなさい", "…そうなの？")
            .L("topic", "{0}のこと、お聞きなさい！")
            .L("like", "さすが、わかっていますわね！").L("dislike", "その話、わたくし好きではありませんわ")
            .L("sleepy", "ふぁ…失礼").L("sigh", "はぁ…さみしいですわ").L("frustr", "もう、なんですの！").L("rain", "翼が重いですわ…")
            .L("done", "わたくしにかかればこの程度！").L("busy", "課題なんて…").L("teach", "わたくしに任せなさい！")
            .L("thanks", "…感謝してさしあげますわ").L("accept", "わたくしに任せなさい！").L("refuse", "今は気分ではありませんわ")
            .L("night", "おやすみなさい…").L("surprise", "きゃあっ！").L("hurt", "わ、わたくしとしたことが…")
            .L("rank1", "おーっほっほ！ 当然一番ですわ！").L("examGood", "当然の結果ですわ！").L("examBad", "占星術なんて大嫌いですわ…"));

            // F 篤（転生者）
            All.Add(new CharProfile
            {
                letter = "F", name = "篤", shortName = "篤", race = "人間（転生者）", personality = "落ち着いた元社会人",
                male = true, color = new Color(0.55f, 0.55f, 0.58f), social = 0.5f, whim = 0.35f, diligence = 0.6f,
                hobby = SimCharacter.HobbyKind.Reading, hobbyText = "読書",
                enrolled = new[] { Theory, Herb, Tools, Ancient, Astro }, callF = "",
            }
            .L("hello_m", "ああ、おはよう").L("hello_d", "お疲れ").L("hello_n", "おやすみ")
            .L("close", "{0}、お疲れ").L("reply", "ああ")
            .L("meal", "うまいな", "これ、懐かしい味だ", "いただきます")
            .L("snack", "ありがたい", "甘いな")
            .L("bath", "ふぅ…極楽", "いい湯だ")
            .L("seat", "うん", "へえ", "なるほど", "そうか")
            .L("chatter", "うん", "へえ", "なるほど", "そうなんだ", "それで？")
            .L("topic", "{0}のことなんだけど")
            .L("like", "いいな、それ").L("dislike", "…まあ、うん")
            .L("sleepy", "ふぁ…").L("sigh", "はぁ…").L("frustr", "…参ったな")
            .L("done", "よし、終わった").L("busy", "課題、片付けないと").L("teach", "ここはこう考えるといい")
            .L("thanks", "ありがとう").L("accept", "わかった", "手伝うよ").L("refuse", "悪い、今はちょっと")
            .L("night", "おやすみ").L("surprise", "うおっ").L("hurt", "いてて")
            .L("rank1", "お、一番か").L("examGood", "よし").L("examBad", "…まだまだだな"));

            // G ロッテ（ドワーフ）
            All.Add(new CharProfile
            {
                letter = "G", name = "ロッテ", shortName = "ロッテ", race = "ドワーフ", personality = "理屈っぽい研究者",
                color = new Color(0.82f, 0.84f, 0.9f), social = 0.3f, whim = 0.3f, diligence = 0.9f,
                hobby = SimCharacter.HobbyKind.Magic, hobbyText = "魔道具研究・修理",
                likes = new[] { "魔道具", "工具", "実験" }, dislikes = new[] { "占星術", "飛ぶ" },
                strongStudy = Tools, weakStudy = Astro, strongPractical = Alchemy, weakPractical = Fly,
                enrolled = new[] { Tools, Alchemy, Astro, Theory, Element, Ancient }, callF = "篤さん",
            }
            .L("hello_m", "おはようございます").L("hello_d", "どうも").L("hello_n", "睡眠は大事です")
            .L("close", "{0}さん、ちょうどいいところに").L("reply", "どうも")
            .L("meal", "栄養の配分が理想的ですね", "このスープ、塩分が…", "統計的に言えば、おかわりは二杯までです")
            .L("snack", "糖分は脳の燃料です", "実験の合間にちょうどいい")
            .L("bath", "湯温は四十一度…理想的です", "筋肉の緊張がほぐれます")
            .L("seat", "統計的に言えば、ですね…", "興味深い", "仮説ですが…", "ふむ")
            .L("chatter", "興味深い", "なるほど", "根拠は？", "ふむふむ", "統計的に言えば、ですね…")
            .L("topic", "{0}について、ですね…")
            .L("like", "その話、詳しく聞かせてください！").L("dislike", "それは非科学的です")
            .L("sleepy", "…集中力が低下しています").L("sigh", "はぁ…").L("frustr", "計算が合わない…")
            .L("done", "完了です").L("busy", "課題の進捗、六割…").L("teach", "理屈で言うと、こうです")
            .L("thanks", "助かります").L("accept", "了解です").L("refuse", "今は実験中なので")
            .L("night", "おやすみなさい").L("surprise", "わっ！？").L("hurt", "…計算外の反応です")
            .L("rank1", "予想どおりです").L("examGood", "想定内ですね").L("examBad", "占星術は再現性が…"));

            // H ニナ（うさぎ）
            All.Add(new CharProfile
            {
                letter = "H", name = "ニナ", shortName = "ニナ", race = "うさぎ", personality = "甘えん坊",
                color = new Color(1f, 0.75f, 0.82f), social = 0.95f, whim = 0.6f, diligence = 0.35f,
                hobby = SimCharacter.HobbyKind.Tea, hobbyText = "おしゃべり・占い",
                likes = new[] { "にんじんケーキ", "みんな", "占い" }, dislikes = new[] { "ひとり", "雷" },
                traits = CharTrait.Lonely, traitText = "さみしさがたまりやすい",
                strongStudy = Astro, weakStudy = Theory, strongPractical = Fly, weakPractical = Alchemy,
                enrolled = new[] { Astro, Fly, Alchemy, Herb, Heal, Body }, callF = "篤くん",
            }
            .L("hello_m", "おはよっ！").L("hello_d", "あっ、やっほー！").L("hello_n", "おやすみ…ひとりやだな")
            .L("close", "{0}！ ねえねえ、一緒に行こっ！").L("reply", "えへへ")
            .L("meal", "おいしいね〜！", "にんじんケーキないかな？", "みんなで食べるとおいしい！")
            .L("snack", "半分こしよっ！", "あまーい！")
            .L("bath", "ぽかぽか〜", "みんなで入ると楽しいね！")
            .L("seat", "ねえねえ！", "それでそれで？", "えへへ", "そばにいていい？")
            .L("chatter", "えー！", "すごーい！", "えへへ", "それでそれで？", "わかる〜！")
            .L("topic", "ねえねえ、{0}のことなんだけど！")
            .L("like", "それ大好き！").L("dislike", "うぅ…その話こわい…")
            .L("sleepy", "ふぁ…").L("sigh", "さみしいな…").L("frustr", "むぅ〜！")
            .L("done", "終わったよ〜！").L("busy", "課題たすけて〜").L("teach", "えっとね、こうだよ！")
            .L("thanks", "ありがとっ！").L("accept", "うん、いいよっ！").L("refuse", "うーん、今はごめんね")
            .L("night", "おやすみ…").L("surprise", "ぴゃっ！").L("hurt", "うぇーん、失敗した…")
            .L("rank1", "やったー！ 一番だよ！").L("examGood", "やったぁ！").L("examBad", "魔法理論むずかしいよ〜"));

            // I リア（人間・王女）
            All.Add(new CharProfile
            {
                letter = "I", name = "リア", shortName = "リア", race = "人間", personality = "上品で世間知らず、言葉は親しみやすい",
                color = new Color(1f, 0.85f, 0.5f), social = 0.6f, whim = 0.55f, diligence = 0.55f,
                hobby = SimCharacter.HobbyKind.Tea, hobbyText = "料理の練習",
                likes = new[] { "庶民の食べ物", "料理" }, dislikes = new[] { "薬草学", "正体" },
                strongStudy = Ancient, weakStudy = Herb, strongPractical = Element, weakPractical = Body,
                enrolled = new[] { Ancient, Element, Body, Theory, Heal, Astro }, callF = "篤くん",
            }
            .L("hello_m", "おはよう！").L("hello_d", "こんにちは！").L("hello_n", "おやすみなさい")
            .L("close", "{0}、こんにちは！").L("reply", "うん！")
            .L("meal", "わあ、これおいしいね！", "こういうの初めて食べた", "作り方、教えてもらえるかな？")
            .L("snack", "これ、なんていうお菓子？", "こういうお菓子っておいしいね！")
            .L("bath", "大きなお風呂って楽しいね", "みんなで入るの、新鮮")
            .L("seat", "ねえ、それってどういうこと？", "へえ、知らなかった！", "ふふっ", "…あ、なんでもないよ")
            .L("chatter", "へえ！", "知らなかった！", "ふふっ", "そうなんだ！", "すごいね")
            .L("topic", "{0}のことなんだけどね")
            .L("like", "それ、もっと聞きたい！").L("dislike", "え、えっと…その話は…")
            .L("sleepy", "ふぁ…").L("sigh", "はぁ…").L("frustr", "うまくいかないなぁ")
            .L("done", "終わった〜！").L("busy", "課題って大変なんだね").L("teach", "ここはね、こう読むの")
            .L("thanks", "ありがとう！").L("accept", "うん、やってみる！").L("refuse", "ごめんね、今は…")
            .L("night", "おやすみなさい").L("surprise", "わっ！").L("hurt", "あっ…失敗しちゃった")
            .L("rank1", "えっ、わたしが一番？").L("examGood", "やったね！").L("examBad", "薬草の名前、覚えられない…"));

            // J キッカ（リス）
            All.Add(new CharProfile
            {
                letter = "J", name = "キッカ", shortName = "キッカ", race = "リス", personality = "ちょこまかおしゃべり",
                color = new Color(0.8f, 0.5f, 0.25f), social = 0.7f, whim = 0.8f, diligence = 0.4f,
                hobby = SimCharacter.HobbyKind.Walk, hobbyText = "収集・木登り",
                likes = new[] { "木の実", "光る物" }, dislikes = new[] { "魔道具学", "元素魔法" },
                strongStudy = Theory, weakStudy = Tools, strongPractical = Alchemy, weakPractical = Element,
                enrolled = new[] { Theory, Alchemy, Element, Herb, Fly, Body }, callF = "篤さん",
            }
            .L("hello_m", "おはようなのです！").L("hello_d", "あっ、こんにちはなのです！").L("hello_n", "おやすみなさいなのです")
            .L("close", "{0}さん！ 見て見て！").L("reply", "はいなのです！")
            .L("meal", "木の実入りなのです！", "もぐもぐ…", "ほっぺに入れておくのです")
            .L("snack", "どんぐりクッキー！", "おやつは別腹なのです")
            .L("bath", "しっぽがふくらむのです", "あったかいのです〜")
            .L("seat", "見て見て！", "また見つけたのです！", "きらきらなのです", "ちょこまか…")
            .L("chatter", "なのです！", "へえ〜！", "すごいのです", "それでそれで？", "ふむふむなのです")
            .L("topic", "{0}の話なのです！")
            .L("like", "それ大好きなのです！").L("dislike", "うう、それは苦手なのです…")
            .L("sleepy", "ふぁ…").L("sigh", "はぁ…なのです").L("frustr", "むむむ…")
            .L("done", "終わったのです！").L("busy", "課題がいっぱいなのです…").L("teach", "ここはこうなのです！")
            .L("thanks", "ありがとうなのです！").L("accept", "まかせてなのです！").L("refuse", "今はだめなのです")
            .L("night", "おやすみなさいなのです").L("surprise", "ひゃっ！").L("hurt", "あわわ、失敗なのです…")
            .L("rank1", "一番なのです！").L("examGood", "やったのです！").L("examBad", "魔道具はむずかしいのです…"));
        }

        /// <summary>
        /// 最初の間柄。友達：A–I, B–D, B–C, C–H, D–H, G–J。A–E はライバルっぽい知り合い（少し高め）。ほかはみんな知り合い。
        /// 相性は好き嫌いの重なりで少し変わる。
        /// </summary>
        public static void ApplyInitialRelations(Relationships rel, int seed)
        {
            var rnd = new System.Random(seed);
            int n = Mathf.Min(rel.count, All.Count);
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var a = All[i]; var b = All[j];
                    float aff = 20f;
                    float c = (float)(rnd.NextDouble() * 0.8 - 0.3);
                    if (IsPair(a, b, "A", "I") || IsPair(a, b, "B", "D") || IsPair(a, b, "B", "C") || IsPair(a, b, "C", "H") || IsPair(a, b, "D", "H") || IsPair(a, b, "G", "J"))
                    { aff = 50f; c = Mathf.Max(c, 0.35f) + 0.15f; }
                    else if (IsPair(a, b, "A", "E")) { aff = 32f; c = 0.05f; }
                    foreach (var t in a.likes) { if (b.Likes(t)) c += 0.15f; if (b.Dislikes(t)) c -= 0.12f; }
                    foreach (var t in b.likes) { if (a.Dislikes(t)) c -= 0.12f; }
                    rel.SetInitial(i, j, aff, Mathf.Clamp(c, -0.6f, 0.8f));
                }
            }
        }

        static bool IsPair(CharProfile a, CharProfile b, string x, string y)
        {
            return (a.letter == x && b.letter == y) || (a.letter == y && b.letter == x);
        }
    }
}
