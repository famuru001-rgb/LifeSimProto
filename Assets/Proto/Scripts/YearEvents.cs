using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>1年の行事の1つ（季節と曜日で決まる）。</summary>
    public class YearEvent
    {
        public string key, name, label;
        public int season, weekday;     // 季節（0春〜3冬）と曜日（0光〜6星）
        public float startHour, hours;
        public bool school;             // 学校の行事（授業は休み）
        public bool outing;             // 出かける行事（会場ができるまでは「外出中」）
        public string area;             // 寮の中で集まる区画
        public Vector3 focus;
        public string[] invited;        // 記号（A〜J）。null ならみんな
        public bool honorF;             // 篤が主役（歓迎会）
        public string note;             // 今日の予定に出す説明
    }

    /// <summary>
    /// 1年（28日）の行事：学校の行事・寮のイベント・個人のイベント。
    /// 会場（大講堂・湖・中庭など）はまだないので、学校の行事はみんなが出かけて、帰ってきたら結果と思い出が残る。
    /// 寮の評判は行事の成功や試験で上がる（新しい場所や家具を開く目安）。
    /// </summary>
    public class YearEvents : MonoBehaviour
    {
        const int Spring = 0, Summer = 1, Autumn = 2, Winter = 3;
        const int Light = 0, Fire = 1, Water = 2, Wind = 3, Earth = 4, Moon = 5, Star = 6;
        static readonly Vector3 Lounge = LoungeWorld.LoungeCenter, Fireplace = LoungeWorld.FireplaceSpot, Dining = LoungeWorld.DiningCenter; // 2026-10-25 寮の作り直し

        public static readonly List<YearEvent> All = new List<YearEvent>
        {
            // ---- 学校の行事 ----
            new YearEvent { key = "entrance", name = "入学式と魔力の鑑定の儀式", label = "入学式と魔力の鑑定に出ている", season = Spring, weekday = Light, startHour = 9f, hours = 3.5f, school = true, outing = true, note = "大講堂" },
            new YearEvent { key = "planting", name = "植樹祭", label = "植樹祭で苗を植えている", season = Spring, weekday = Water, startHour = 10f, hours = 2f, school = true, outing = true, note = "学園じゅうで魔法の苗を植える" },
            new YearEvent { key = "lake", name = "湖での野外実習", label = "湖で野外実習をしている", season = Summer, weekday = Fire, startHour = 9f, hours = 6f, school = true, outing = true, note = "水の元素魔法・薬草の採集" },
            new YearEvent { key = "race", name = "夏の飛行レース", label = "夏の飛行レースに出ている", season = Summer, weekday = Moon, startHour = 10f, hours = 2f, school = true, outing = true, note = "湖のまわり" },
            new YearEvent { key = "schoolfest", name = "学園祭", label = "学園祭で寮の出し物をしている", season = Autumn, weekday = Water, startHour = 10f, hours = 6f, school = true, outing = true, note = "寮で出し物" },
            new YearEvent { key = "contest", name = "魔法競技会", label = "魔法競技会（寮対抗）に出ている", season = Autumn, weekday = Wind, startHour = 9f, hours = 6f, school = true, outing = true, note = "寮対抗・実技で勝負" },
            // ---- 寮のイベント ----
            new YearEvent { key = "welcome", name = "歓迎会", label = "歓迎会に参加している", season = Spring, weekday = Fire, startHour = 19.5f, hours = 1f, area = "談話室", focus = Lounge, honorF = true, note = "篤の歓迎も兼ねる" },
            new YearEvent { key = "stars", name = "屋上での星見の夜", label = "屋上で星を見ている", season = Spring, weekday = Star, startHour = 21f, hours = 1.5f, outing = true, note = "ミレイユとニナが張り切る" },
            new YearEvent { key = "waterplay", name = "湖での水遊び", label = "湖で水遊びをしている", season = Summer, weekday = Water, startHour = 13f, hours = 3f, outing = true },
            new YearEvent { key = "cooking", name = "料理対決", label = "料理対決に参加している", season = Summer, weekday = Moon, startHour = 17f, hours = 1.5f, area = "食堂", focus = Dining, note = "一人一品" },
            new YearEvent { key = "summerfest", name = "夏祭りと花火", label = "夏祭りで花火を見ている", season = Summer, weekday = Star, startHour = 18.5f, hours = 2.5f, outing = true, note = "元素魔法の花火" },
            new YearEvent { key = "harvest", name = "収穫祭", label = "収穫祭のごちそうを食べている", season = Autumn, weekday = Star, startHour = 17.5f, hours = 1.5f, area = "食堂", focus = Dining, note = "実りをみんなで食べる" },
            new YearEvent { key = "farewell", name = "送別会", label = "送別会に参加している", season = Winter, weekday = Moon, startHour = 19.5f, hours = 1f, area = "談話室", focus = Lounge, note = "卒業する先輩たちを送る" },
            new YearEvent { key = "solstice", name = "冬至の祭り", label = "冬至の祭りで暖炉を囲んでいる", season = Winter, weekday = Star, startHour = 17.5f, hours = 2f, area = "談話室", focus = Fireplace, note = "ごちそうと贈り物・年の終わりの夜" },
            // ---- 個人のイベント（挑戦）----
            new YearEvent { key = "riacook", name = "リアの料理の練習", label = "台所で料理の練習をしている", season = Spring, weekday = Star, startHour = 15f, hours = 1f, area = "食堂", focus = Dining, invited = new[] { "I" }, note = "初めての成功なるか" },
            new YearEvent { key = "riacook2", name = "リアの料理の練習", label = "台所で料理の練習をしている", season = Summer, weekday = Water, startHour = 10f, hours = 1f, area = "食堂", focus = Dining, invited = new[] { "I" } },
            new YearEvent { key = "tools", name = "ロッテの魔道具コンテスト", label = "魔道具コンテストに出ている", season = Autumn, weekday = Moon, startHour = 13f, hours = 2f, outing = true, invited = new[] { "G" }, note = "篤の応援で力が出る" },
        };

        LoungeWorld world;
        int lastDay = -1;
        readonly Dictionary<string, Gathering> running = new Dictionary<string, Gathering>();
        readonly Dictionary<string, YearEvent> eventOf = new Dictionary<string, YearEvent>();
        readonly HashSet<string> started = new HashSet<string>(), finished = new HashSet<string>();
        bool planted, riaCooked;
        float thunderAt = -1f, coldAt = -1f;

        /// <summary>その日に授業を休みにする学校の行事があるか。</summary>
        public static bool NoClassDay(int day)
        {
            foreach (var e in All) if (e.school && e.season == Calendar.Season(day) && e.weekday == Calendar.Weekday(day)) return true;
            return false;
        }

        /// <summary>その日の大きな行事（学校の行事・寮のイベント）。デートの日と重ならないように使う。</summary>
        public static bool IsBigDay(int day)
        {
            foreach (var e in All) if (e.invited == null && e.season == Calendar.Season(day) && e.weekday == Calendar.Weekday(day)) return true;
            return false;
        }

        void Start() { world = GetComponent<LoungeWorld>(); }

        void Update()
        {
            if (world == null || world.characters.Count == 0) return;
            if (world.Day != lastDay) NewDay();
            float m = world.MinuteOfDay;
            int day = world.Day;
            foreach (var e in All)
            {
                if (e.season != Calendar.Season(day) || e.weekday != Calendar.Weekday(day)) continue;
                string k = e.key + day;
                if (!started.Contains(k) && m >= e.startHour * 60f && m < (e.startHour + e.hours) * 60f) Begin(e, k);
            }
            // 終わった行事の結果（日をまたいでも必ず出す）
            foreach (var kv in running)
            {
                if (finished.Contains(kv.Key) || world.Now < kv.Value.end) continue;
                finished.Add(kv.Key);
                End(eventOf[kv.Key], kv.Value);
                break; // 一度に1つ（辞書を回している間に書きかえないため）
            }
            RaceEvents(m);
            FMagic.Check(world, world.RomanceTarget);
            if (Time.frameCount % 120 == 0) PlaceGates.Check(world);
            // 年の終わり：春の朝に戻る（ループを抜けていたら、続けるかもう一周するかを選ぶ）
            if (!Loop.EndlessDays && Calendar.Year(day) == 1 && Calendar.DayOfYear(day) == Loop.RewindDayOfYear && m >= Loop.RewindHour * 60f && !rewinding)
            {
                if (Loop.Escaped) { if (!Loop.AskChoice) { Loop.AskChoice = true; world.SetUserSpeed(0f); world.Log("★ ループを抜けた。このまま日常を続けるか、もう一度最初から周回するかを選べる"); } }
                else { rewinding = true; Loop.Rewind(world); }
            }
        }

        bool rewinding;

        void NewDay()
        {
            lastDay = world.Day;
            int day = world.Day;
            int season = Calendar.Season(day);
            if (Calendar.DayOfYear(day) == 1) planted = false;
            foreach (var e in All)
            {
                if (e.season != season || e.weekday != Calendar.Weekday(day)) continue;
                if (e.key.StartsWith("riacook") && riaCooked) continue;
                world.todayNotes.Add(e.name + " " + Clock(e.startHour) + (string.IsNullOrEmpty(e.note) ? "" : "（" + e.note + "）"));
                if (e.invited == null) world.Log("今日は" + e.name + "！" + (e.school && e.outing ? "（会場はまだないので、みんなが出かけて結果を持ち帰る）" : ""));
            }
            // 種族の出来事の予定
            var rnd = new System.Random(day * 31337 + 7);
            thunderAt = season == Summer && Calendar.WeatherOf(day) == Weather.Rain && rnd.NextDouble() < 0.7 ? 21.5f * 60f : -1f;
            coldAt = season == Winter && Calendar.WeatherOf(day) == Weather.Snow ? 18f * 60f : -1f;
            if (thunderAt > 0) world.todayNotes.Add("夜は雷になりそう");
            if (Calendar.IsFullMoon(day)) world.Log("今夜は満月。" + NameOf("C") + "が朝からそわそわしている");
            if (season == Autumn && Calendar.Weekday(day) == Light) world.Log(NameOf("J") + "の部屋が、木の実と光る物でいっぱいになってきた（秋はため込む季節）");
        }

        // ================= 始まり =================
        void Begin(YearEvent e, string k)
        {
            started.Add(k);
            if (e.key.StartsWith("riacook") && riaCooked) return;
            var g = new Gathering
            {
                key = "year_" + e.key, label = e.label, area = e.area, focus = e.focus, outing = e.outing,
                start = world.Now, end = ScheduleMaker.DayStart(world.Day) + (e.startHour + e.hours) * 60.0,
                joinChance = e.school ? 1f : 0.9f,
            };
            if (e.school || e.honorF) g.invited = new List<SimCharacter>(world.characters);
            if (e.invited != null)
            {
                g.invited = new List<SimCharacter>();
                foreach (var l in e.invited) { var c = ByLetter(l); if (c != null) g.invited.Add(c); }
            }
            if (e.honorF) g.honoree = world.RomanceTarget;
            // 会場の場所が開いていれば、そこで行う（中庭：競技会・学園祭、天文台：星見の夜、台所：リアの料理）
            string venue = VenueOf(e.key);
            if (venue != null && world.PlaceOpen(venue) && !(venue == LoungeWorld.Courtyard && world.BadWeatherToday))
            {
                g.outing = false;
                g.area = venue;
                g.focus = LoungeWorld.FocusOf(venue);
                world.Log("（会場：" + venue + "）");
            }
            world.gatherings.Add(g);
            running[k] = g;
            eventOf[k] = e;
            world.Log("★ " + e.name + "が始まった");
            if (e.key == "stars") { Say("D", "like", "星、きれいですよ！"); Say("H", "like", "星見、行こっ！"); }
            if (e.key == "race") { Say("E", "accept", "わたくしに任せなさい！"); Say("H", "accept", "がんばるよっ！"); }
        }

        // ================= 終わりと結果 =================
        void End(YearEvent e, Gathering g)
        {
            var members = new List<SimCharacter>(g.members);
            var f = world.RomanceTarget;
            string memo = null;
            switch (e.key)
            {
                case "entrance":
                {
                    var parts = new List<string>();
                    foreach (var c in members)
                    {
                        if (c.IsTarget) continue;
                        int best = c.profile != null ? c.profile.strongPractical : -1;
                        parts.Add(c.displayName + "＝" + (best >= 0 ? Academics.Names[best] : "魔力") + "の才");
                        if (f != null) world.rel.Add(c.id, f.id, 3f, world.simTime);
                    }
                    world.Log("魔力の鑑定：" + string.Join("・", parts.ToArray()));
                    world.Log("★ 篤だけ「魔力なし」と出て、大講堂がざわついた。みんな篤のことが気になっている");
                    memo = "入学式と魔力の鑑定。篤だけ「魔力なし」と出て大騒ぎに";
                    break;
                }
                case "planting":
                    planted = members.Count > 0;
                    world.dormReputation += 2;
                    memo = "植樹祭でみんなと魔法の苗を植えた（秋に実る）";
                    break;
                case "lake":
                {
                    SimCharacter wet = null;
                    foreach (var c in members)
                    {
                        c.AddSkill(5, 2f); c.AddSkill(1, 1.5f);
                        c.GrowAbility(Abilities.Body, 0.5f); c.GrowAbility(Abilities.Magic, 0.5f);
                        if (wet == null && c.profile != null && c.profile.IsWeak(5)) wet = c;
                    }
                    world.Log("湖での野外実習：みんなの元素魔法と薬草学が少し伸びた" + (wet != null ? "。" + wet.displayName + "は水の魔法で頭から水をかぶったらしい" : ""));
                    memo = "湖での野外実習" + (wet != null ? "（" + wet.displayName + "がずぶぬれ）" : "");
                    break;
                }
                case "race":
                {
                    var rank = Rank(members, c => c.SkillOf(7) + c.Ab(Abilities.Body) * 0.3f + c.Ab(Abilities.Magic) * 0.2f + Random.Range(0f, 15f) + (FMagic.Has(7) || !c.IsTarget ? 0f : -30f));
                    foreach (var c in members) c.GrowAbility(Abilities.Body, 1f);
                    if (rank.Count > 0)
                    {
                        var w = rank[0];
                        w.PositiveEvent(1f);
                        w.ShowBubble(w.Say("rank1", "一番！"), 2.5f);
                        world.Log("★ 夏の飛行レース：1位 " + w.displayName + Tail(rank, 1, "2位 ") + Tail(rank, 2, "3位 "));
                        world.dormReputation += 3;
                        memo = "夏の飛行レースで" + w.displayName + "が1位";
                    }
                    break;
                }
                case "contest":
                {
                    float sum = 0f; SimCharacter mvp = null; float mvpV = -1f;
                    foreach (var c in members)
                    {
                        float best = 0f;
                        for (int s = 5; s < Academics.Count; s++) best = Mathf.Max(best, c.SkillOf(s));
                        best += c.Ab(Abilities.Magic) * 0.2f + c.Ab(Abilities.Body) * 0.1f - 15f;
                        c.GrowAbility(Abilities.Magic, 1f);
                        sum += best;
                        if (best > mvpV && !c.IsTarget) { mvpV = best; mvp = c; }
                    }
                    float ours = members.Count > 0 ? sum / members.Count + Random.Range(0f, 20f) : 0f;
                    int place = 1;
                    for (int i = 0; i < 3; i++) if (Random.Range(45f, 75f) > ours) place++;
                    world.dormReputation += place == 1 ? 10 : (place == 2 ? 5 : 2);
                    foreach (var c in members) { if (place == 1) c.PositiveEvent(0.8f); else if (place == 4) c.NegativeEvent(0.5f); }
                    world.Log("★ 魔法競技会（寮対抗）：この寮は " + place + "位！" + (mvp != null ? "　活躍：" + mvp.displayName : "") + "（寮の評判 " + world.dormReputation + "）");
                    memo = "魔法競技会で寮が" + place + "位" + (mvp != null ? "（" + mvp.displayName + "が大活躍）" : "");
                    break;
                }
                case "schoolfest":
                {
                    string shop = ShopIdea(members);
                    float mood = 0f;
                    foreach (var a in members) foreach (var b in members) if (a.id < b.id) mood += world.rel.Get(a.id, b.id);
                    int pairs = members.Count * (members.Count - 1) / 2;
                    float charm = 0f;
                    foreach (var c in members) { charm += c.Ab(Abilities.Charm); c.GrowAbility(Abilities.Charm, 1f); }
                    charm = members.Count > 0 ? charm / members.Count : 50f;
                    bool hit = pairs > 0 && mood / pairs + Random.Range(0f, 30f) + (charm - 55f) * 0.5f > 45f;
                    world.dormReputation += hit ? 6 : 3;
                    foreach (var c in members) c.PositiveEvent(hit ? 0.8f : 0.4f);
                    world.Log("★ 学園祭：寮の出し物は「" + shop + "」" + (hit ? "。大盛況！" : "。まずまずのにぎわい"));
                    memo = "学園祭で「" + shop + "」" + (hit ? "が大盛況" : "を出した");
                    break;
                }
                case "welcome":
                    if (f != null && members.Contains(f)) foreach (var c in members) if (c != f) world.rel.Add(c.id, f.id, 2f, world.simTime);
                    memo = "寮の歓迎会。篤もみんなに迎えてもらった";
                    break;
                case "stars":
                    foreach (var c in members) { c.PositiveEvent(0.5f); c.GrowAbility(Abilities.Mind, 0.3f); }
                    memo = "屋上での星見の夜（" + Names(members) + "）";
                    break;
                case "waterplay":
                    foreach (var c in members) { c.PositiveEvent(0.6f); c.bored = 0f; c.GrowAbility(Abilities.Body, 0.5f); }
                    memo = "湖での水遊び（" + Names(members) + "）";
                    break;
                case "cooking":
                {
                    var rank = Rank(members, c =>
                    {
                        float v = Random.Range(0f, 30f) + c.Ab(Abilities.Dex) * 0.4f;
                        c.GrowAbility(Abilities.Dex, 1f);
                        if (c.profile != null && c.profile.Likes("料理")) v += 25f;
                        if (c.IsTarget) v += 30f; // 転生前の料理
                        return v;
                    });
                    if (rank.Count > 0)
                    {
                        var w = rank[0];
                        w.PositiveEvent(1f);
                        w.ShowBubble(w.Say("rank1", "やった！"), 2.5f);
                        world.Log("★ 料理対決：優勝は " + w.displayName + (w.IsTarget ? "（見たことのない異世界の料理に、みんなびっくり）" : ""));
                        if (w.IsTarget) foreach (var c in members) if (c != w) world.rel.Add(c.id, w.id, 2f, world.simTime);
                        memo = "料理対決で" + w.displayName + "が優勝";
                        if (w.IsTarget || w.letter == "I") Story.Flags.Add("cookwin"); // リアの物語 第3話の鍵
                    }
                    break;
                }
                case "summerfest":
                {
                    SimCharacter fire = null; float best = -1f;
                    foreach (var c in members) if (c.SkillOf(5) + c.Ab(Abilities.Magic) * 0.5f > best && !c.IsTarget) { best = c.SkillOf(5) + c.Ab(Abilities.Magic) * 0.5f; fire = c; }
                    foreach (var c in members) c.PositiveEvent(0.8f);
                    world.Log("夏祭り：" + (fire != null ? fire.displayName + "の元素魔法の花火が夜空に咲いた" : "花火がきれいだった"));
                    memo = "夏祭りと花火" + (fire != null ? "（" + fire.displayName + "の花火）" : "");
                    break;
                }
                case "harvest":
                    foreach (var c in members) { c.PositiveEvent(planted ? 0.9f : 0.5f); c.hunger = 0f; }
                    world.Log("収穫祭：" + (planted ? "春に植えた苗が実って、ごちそうになった" : "みんなでごちそうを食べた"));
                    if (planted) world.dormReputation += 2;
                    memo = "収穫祭" + (planted ? "（春の苗が実った）" : "");
                    break;
                case "farewell":
                    memo = "卒業する先輩たちの送別会";
                    break;
                case "solstice":
                    foreach (var c in members)
                    {
                        c.PositiveEvent(1f);
                        SimCharacter friend = null; float fv = -1f;
                        foreach (var o in members) if (o != c && world.rel.Get(c.id, o.id) > fv) { fv = world.rel.Get(c.id, o.id); friend = o; }
                        if (friend != null) world.rel.Add(c.id, friend.id, 4f, world.simTime);
                    }
                    world.Log("冬至の祭り：暖炉の前でごちそうと贈り物。今年もあと少し");
                    memo = "冬至の祭り。暖炉の前で贈り物を交換した";
                    break;
                case "riacook":
                case "riacook2":
                {
                    var ria = ByLetter("I");
                    if (ria == null || !members.Contains(ria)) break;
                    bool cheer = f != null && world.Now - ria.LastFContact < 120;
                    float p = 0.05f + ria.Ab(Abilities.Dex) / 250f + world.rel.Get(ria.id, f != null ? f.id : 0) / 200f + (cheer ? 0.25f : 0f) + (e.key == "riacook2" ? 0.2f : 0f);
                    ria.GrowAbility(Abilities.Dex, 1.5f);
                    if (Random.value < p)
                    {
                        riaCooked = true;
                        Story.Flags.Add("riacooked"); // リアの物語 第1話のきっかけ
                        ria.PositiveEvent(1f);
                        ria.ShowBubble("できた…！ はじめて成功した！", 2.5f);
                        world.Log("★ リアの料理が初めて成功した！" + (cheer ? "（篤の応援が効いた）" : ""));
                        memo = "リアの料理の初成功";
                    }
                    else { ria.ShowBubble("あっ…焦げちゃった", 2f); world.Log("リアの料理の練習：今日は焦がしてしまった（またの機会に）"); }
                    break;
                }
                case "tools":
                {
                    var g7 = ByLetter("G");
                    if (g7 == null || !members.Contains(g7)) break;
                    bool cheer = f != null && world.Now - g7.LastFContact < 240;
                    float p = 0.05f + g7.SkillOf(2) / 200f + g7.Ab(Abilities.Dex) / 300f + (cheer ? 0.2f : 0f);
                    g7.GrowAbility(Abilities.Dex, 1.5f);
                    if (Random.value < p)
                    {
                        g7.PositiveEvent(1f);
                        world.dormReputation += 3;
                        world.Log("★ ロッテの魔道具コンテスト：入賞！" + (cheer ? "（篤の応援が効いた）" : ""));
                        Story.Flags.Add("toolswin"); // ロッテの物語 第3話の鍵
                        memo = "ロッテが魔道具コンテストで入賞";
                    }
                    else { g7.NegativeEvent(0.5f); world.Log("ロッテの魔道具コンテスト：今回は入賞ならず。次の改良案を考えているらしい"); }
                    break;
                }
            }
            if (memo != null) Album.Add(world, memo);
        }

        static string VenueOf(string key)
        {
            switch (key)
            {
                case "contest": case "schoolfest": return LoungeWorld.Courtyard;
                case "stars": return LoungeWorld.Observatory;
                case "riacook": case "riacook2": return LoungeWorld.Kitchen;
                default: return null;
            }
        }

        // ================= 種族の出来事 =================
        void RaceEvents(float m)
        {
            int day = world.Day;
            if (thunderAt > 0 && m >= thunderAt && Once("thunder" + day))
            {
                var g = new Gathering
                {
                    key = "thunder", label = "雷がこわくて寄り添っている", area = "談話室", focus = Fireplace,
                    start = world.Now, end = world.Now + 45, joinChance = 1f, invited = new List<SimCharacter>()
                };
                foreach (var l in new[] { "H", "D" }) { var c = ByLetter(l); if (c != null && c.Present) g.invited.Add(c); }
                if (g.invited.Count > 0)
                {
                    world.gatherings.Add(g);
                    world.Log("ゴロゴロ…雷の夜。" + Names(g.invited) + "が暖炉の前で寄り添っている");
                }
            }
            if (coldAt > 0 && m >= coldAt && Once("cold" + day))
            {
                var a = ByLetter("A");
                if (a != null && a.Present)
                {
                    world.gatherings.Add(new Gathering
                    {
                        key = "cold", label = "暖炉の前から動かない", area = "談話室", focus = Fireplace,
                        start = world.Now, end = world.Now + 90, joinChance = 1f, invited = new List<SimCharacter> { a }
                    });
                    a.ShowBubble(a.Say("cold", "…寒い"), 1.8f);
                    world.Log("雪の日。" + a.displayName + "が暖炉の前から動かない");
                }
            }
        }

        readonly HashSet<string> once = new HashSet<string>();
        bool Once(string k) { return once.Add(k); }

        // ================= 道具 =================
        SimCharacter ByLetter(string l)
        {
            foreach (var c in world.characters) if (c.letter == l) return c;
            return null;
        }

        string NameOf(string l) { var c = ByLetter(l); return c != null ? c.displayName : l; }

        void Say(string l, string key, string fallback)
        {
            var c = ByLetter(l);
            if (c != null && c.Present) c.ShowBubble(c.Say(key, fallback), 2f);
        }

        static string Clock(float h) { return string.Format("{0}:{1:00}", (int)h, Mathf.RoundToInt((h - (int)h) * 60f)); }

        static string Names(List<SimCharacter> list)
        {
            var n = new List<string>();
            foreach (var c in list) n.Add(c.displayName);
            return string.Join("・", n.ToArray());
        }

        static List<SimCharacter> Rank(List<SimCharacter> list, System.Func<SimCharacter, float> score)
        {
            var sc = new Dictionary<SimCharacter, float>();
            foreach (var c in list) sc[c] = score(c);
            var r = new List<SimCharacter>(list);
            r.Sort((a, b) => sc[b].CompareTo(sc[a]));
            return r;
        }

        static string Tail(List<SimCharacter> r, int i, string head) { return r.Count > i ? "　" + head + r[i].displayName : ""; }

        static string ShopIdea(List<SimCharacter> members)
        {
            var ideas = new List<string>();
            foreach (var c in members)
            {
                if (c.profile == null) continue;
                if (c.profile.Likes("料理")) ideas.Add("手作り料理の屋台");
                if (c.profile.Likes("魔道具")) ideas.Add("動く魔道具の展示");
                if (c.profile.Likes("お茶会")) ideas.Add("天使のお茶会カフェ");
                if (c.profile.Likes("占い")) ideas.Add("星占いの館");
                if (c.profile.Likes("光る物")) ideas.Add("きらきら宝物の展示");
            }
            if (ideas.Count == 0) ideas.Add("寮の喫茶店");
            return ideas[Random.Range(0, ideas.Count)];
        }
    }

    /// <summary>
    /// 思い出のアルバム（行事・告白・物語の場面）。ループしても残る。
    /// 今は文章だけ。あとで場面の写真も残す。
    /// </summary>
    public static class Album
    {
        public static readonly List<string> Entries = new List<string>();

        public static void Add(LoungeWorld w, string text)
        {
            string when = Loop.Count + "周目・" + Calendar.SeasonNames[Calendar.Season(w.Day)] + "・" + Calendar.WeekdayNames[Calendar.Weekday(w.Day)];
            Entries.Add(when + "：" + text);
            if (Entries.Count > 300) Entries.RemoveAt(0);
            w.Log("（アルバムに残った：" + text + "）");
        }
    }
}
