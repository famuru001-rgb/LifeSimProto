using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// キャラごとの小さな物語（1人3話）とループの針（骨組み：企画「ループの物語_骨組み.md」。famuru 決定 2026-10-21）。
    /// ・第1話「きっかけ」：篤と友達（好感度40）＋その子の場面の時間 → 終わるとその子の場所が開く目安になる
    /// ・第2話「打ち明け」：好感度60＋季節 → 図鑑に「願い」。次の周からは篤との好感度が50で始まる
    /// ・第3話「解決」：篤と恋人＋鍵（能力・学力・場所・行事の結果）→ 図鑑に「解決」
    /// ・第3話を終えて、その周で恋人になり、夜をいっしょに過ごすと、その子の針が止まる（ループしても止まったまま）
    /// ・みんなの針が止まると、篤の最後の物語。終わるとループを抜けられる
    /// 物語の気配が出たら、篤がその子に話しかけると始まる（場面の文章が出て、時間は止まる）。
    /// 進み（済んだ話・止まった針）はループしても残る。気配と行事の結果はその周だけ。
    /// </summary>
    public static class Story
    {
        public static readonly Dictionary<string, int> Done = new Dictionary<string, int>(); // 済んだ話の数（0〜3）
        public static readonly HashSet<string> Hands = new HashSet<string>();               // 止まった針（記号。篤は "F"）
        public static readonly HashSet<string> Flags = new HashSet<string>();               // その周の出来事（cookwin・toolswin・riacooked）
        public static readonly HashSet<string> Pending = new HashSet<string>();             // 気配が出ている子（その周だけ）

        // 今見ている場面
        public static string ActiveLetter;
        public static int ActiveEp = -1;
        public static int ActiveLine;
        public static bool ActiveFinal;
        static float resumeSpeed = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            Done.Clear(); Hands.Clear(); Flags.Clear(); Pending.Clear();
            ActiveLetter = null; ActiveEp = -1; ActiveLine = 0; ActiveFinal = false;
        }

        /// <summary>春の朝に戻るとき：その周だけの物を消す。</summary>
        public static void OnRewind()
        {
            Flags.Clear(); Pending.Clear();
            ActiveLetter = null; ActiveEp = -1; ActiveFinal = false;
        }

        public static int DoneOf(string letter) { int n; return letter != null && Done.TryGetValue(letter, out n) ? n : 0; }
        public static bool IsActive { get { return ActiveLetter != null || ActiveFinal; } }

        // ================= 条件 =================
        static readonly Dictionary<string, int> Ep2Season = new Dictionary<string, int>
        {
            { "A", 3 }, { "B", 0 }, { "C", 0 }, { "D", 2 }, { "E", 1 }, { "G", 1 }, { "H", 3 }, { "I", 1 }, { "J", 2 }, { "K", 2 },
        };

        static bool Between(float m, float fromHour, float toHour)
        {
            float a = fromHour * 60f, b = toHour * 60f;
            if (b <= 1440f) return m >= a && m < b;
            return m >= a || m < b - 1440f; // 日をまたぐ
        }

        static bool Ep1Time(LoungeWorld w, string l)
        {
            float m = w.MinuteOfDay;
            int day = w.Day;
            switch (l)
            {
                case "A": return Between(m, 19f, 23f);
                case "B": return Between(m, 13f, 17f);
                case "C": return Calendar.IsFullMoon(day) && Between(m, 21f, 28f);
                case "D": return Between(m, 22f, 26f);
                case "E": return Between(m, 21f, 24f);
                case "G": return Between(m, 15f, 19f);
                case "H": return (Calendar.BadWeather(day) || Calendar.Weekday(day) == 6) && Between(m, 20f, 24f);
                case "I": return Flags.Contains("riacooked") || (Calendar.Season(day) >= 1 && Between(m, 17f, 20f));
                case "J": return Between(m, 14f, 18f);
                case "K": return Between(m, 22f, 27f);
            }
            return Between(m, 18f, 22f);
        }

        static SimCharacter ByLetter(LoungeWorld w, string l) { return w.characters.Find(x => x.letter == l); }

        static bool Ep3Key(LoungeWorld w, SimCharacter c, SimCharacter f)
        {
            switch (c.letter)
            {
                case "A": return f.SkillOf(Academics.Ancient) >= Abilities.RankMin("B");
                case "B": return w.PlaceOpen(LoungeWorld.Greenhouse) && f.SkillOf(Academics.Herb) >= Abilities.RankMin("C");
                case "C": return w.dormReputation >= 30 && f.Ab(Abilities.Body) >= Abilities.RankMin("C");
                case "D":
                {
                    var g = ByLetter(w, "G");
                    return f.Ab(Abilities.Dex) >= Abilities.RankMin("B") && g != null && w.rel.Get(c.id, g.id) >= 40f;
                }
                case "E": return w.PlaceOpen(LoungeWorld.Library) && f.SkillOf(Academics.Ancient) >= Abilities.RankMin("C");
                case "G": return Flags.Contains("toolswin");
                case "H": return w.PlaceOpen(LoungeWorld.Observatory) && f.Ab(Abilities.Magic) >= Abilities.RankMin("D");
                case "I": return w.dormReputation >= 40 && Flags.Contains("cookwin");
                case "J": return w.PlaceOpen(LoungeWorld.Workshop) && FMagic.Has(Academics.Alchemy);
                case "K": return w.PlaceOpen(LoungeWorld.Observatory) && f.SkillOf(Academics.Theory) >= Abilities.RankMin("C");
            }
            return true;
        }

        static bool Ready(LoungeWorld w, SimCharacter c, SimCharacter f, int done)
        {
            float rel = w.rel.Get(c.id, f.id);
            switch (done)
            {
                case 0: return rel >= 40f && Ep1Time(w, c.letter);
                case 1: { int s; return rel >= 60f && Ep2Season.TryGetValue(c.letter, out s) && Calendar.Season(w.Day) == s; }
                case 2: return c.IsLover && Ep3Key(w, c, f);
            }
            return false;
        }

        /// <summary>話の始まる条件の説明（画面用）。</summary>
        public static string Hint(string l, int ep)
        {
            switch (ep)
            {
                case 0:
                {
                    string t;
                    switch (l)
                    {
                        case "A": t = "19〜23時"; break;
                        case "B": t = "13〜17時"; break;
                        case "C": t = "満月（風の日）の21時〜朝4時"; break;
                        case "D": t = "22〜2時"; break;
                        case "E": t = "21〜24時"; break;
                        case "G": t = "15〜19時"; break;
                        case "H": t = "雨か雪の日・星の日の20〜24時"; break;
                        case "I": t = "リアの料理が成功したあと（夏からは17〜20時でも）"; break;
                        case "J": t = "14〜18時"; break;
                        case "K": t = "22〜3時"; break;
                        default: t = "18〜22時"; break;
                    }
                    return "篤と友達（好感度40）・" + t;
                }
                case 1:
                {
                    int s;
                    return "好感度60・" + (Ep2Season.TryGetValue(l, out s) ? Calendar.SeasonNames[s] : "") + "のあいだ";
                }
                case 2:
                {
                    string k;
                    switch (l)
                    {
                        case "A": k = "篤の古代語 B 以上"; break;
                        case "B": k = "温室が開いている・篤の薬草学 C 以上"; break;
                        case "C": k = "寮の評判30以上・篤の体力 C 以上"; break;
                        case "D": k = "篤の器用さ B 以上・ミレイユとロッテが友達"; break;
                        case "E": k = "図書室が開いている・篤の古代語 C 以上"; break;
                        case "G": k = "その周の魔道具コンテストで入賞"; break;
                        case "H": k = "屋上の天文台が開いている・篤の魔力 D 以上"; break;
                        case "I": k = "寮の評判40以上・その周の料理対決で篤かリアが優勝"; break;
                        case "J": k = "工房が開いている・篤が錬成の魔法を使える"; break;
                        case "K": k = "屋上の天文台が開いている・篤の魔法理論 C 以上"; break;
                        default: k = ""; break;
                    }
                    return "篤と恋人・" + k;
                }
            }
            return "第3話を終えて、その周で恋人になり、夜をいっしょに過ごす";
        }

        // ================= 進める =================
        /// <summary>ときどき呼ぶ（ProgressDirector から）：気配・針・最後の物語。</summary>
        public static void Tick(LoungeWorld w)
        {
            var f = w.RomanceTarget;
            if (f == null || IsActive) return;
            foreach (var c in w.characters)
            {
                if (c.IsTarget || StoryText.Get(c.letter) == null) continue;
                int done = DoneOf(c.letter);
                if (done < 3 && !Pending.Contains(c.letter) && Ready(w, c, f, done))
                {
                    Pending.Add(c.letter);
                    w.Log("★ 物語の気配：" + c.displayName + "の様子がいつもと違う（篤が話しかけると 第" + (done + 1) + "話「" + StoryText.Get(c.letter).titles[done] + "」）");
                }
                if (done >= 3 && !Hands.Contains(c.letter) && c.IsLover && Loop.NightTogether.Contains(c.letter))
                {
                    Hands.Add(c.letter);
                    w.Log("★ " + c.displayName + "の針が止まった（止まった針 " + GirlHands(w) + "/" + GirlCount(w) + "。ループしても止まったまま）");
                    Album.Add(w, c.displayName + "の針が止まった。「来年も」と言ってくれた");
                }
            }
            if (!Hands.Contains("F") && GirlCount(w) > 0 && GirlHands(w) >= GirlCount(w))
            {
                float m = w.MinuteOfDay;
                if (m >= 21f * 60f || LoungeWorld.IsNight(m)) BeginFinal(w);
            }
        }

        public static int GirlCount(LoungeWorld w)
        {
            int n = 0;
            foreach (var c in w.characters) if (!c.IsTarget && StoryText.Get(c.letter) != null) n++;
            return n;
        }

        public static int GirlHands(LoungeWorld w)
        {
            int n = 0;
            foreach (var c in w.characters) if (!c.IsTarget && Hands.Contains(c.letter)) n++;
            return n;
        }

        /// <summary>篤がその子と話したとき：気配が出ていれば物語が始まる。</summary>
        public static void OnTalk(LoungeWorld w, SimCharacter girl)
        {
            if (girl == null || girl.IsTarget || IsActive || !Pending.Contains(girl.letter)) return;
            int ep = DoneOf(girl.letter);
            if (ep >= 3) { Pending.Remove(girl.letter); return; }
            ActiveLetter = girl.letter;
            ActiveEp = ep;
            ActiveLine = 0;
            Pause(w);
            w.Log("★ " + girl.displayName + "の物語 第" + (ep + 1) + "話「" + StoryText.Get(girl.letter).titles[ep] + "」");
        }

        static void BeginFinal(LoungeWorld w)
        {
            ActiveFinal = true;
            ActiveLine = 0;
            Pause(w);
            w.Log("★ 篤の物語「" + StoryText.FinalTitle + "」");
        }

        static void Pause(LoungeWorld w)
        {
            resumeSpeed = w.userSpeed; // 止めていたなら止めたまま戻す
            w.SetUserSpeed(0f);
        }

        public static string[] ActiveLines
        {
            get
            {
                if (ActiveFinal) return StoryText.FinalLines;
                var d = StoryText.Get(ActiveLetter);
                return d != null && ActiveEp >= 0 && ActiveEp < 3 ? d.lines[ActiveEp] : new string[0];
            }
        }

        public static string ActiveTitle
        {
            get
            {
                if (ActiveFinal) return "篤の物語「" + StoryText.FinalTitle + "」";
                var d = StoryText.Get(ActiveLetter);
                return d == null ? "" : HLog.NameOf(ActiveLetter) + "の物語　第" + (ActiveEp + 1) + "話「" + d.titles[ActiveEp] + "」";
            }
        }

        /// <summary>次の行へ。最後まで行ったら終わる。</summary>
        public static void Next(LoungeWorld w)
        {
            if (!IsActive) return;
            ActiveLine++;
            if (ActiveLine >= ActiveLines.Length) Complete(w);
        }

        static void Complete(LoungeWorld w)
        {
            var f = w.RomanceTarget;
            if (ActiveFinal)
            {
                ActiveFinal = false;
                Hands.Add("F");
                Loop.Escaped = true;
                w.Log("★ 最後の針が止まった。今年の冬至の夜が明けると、ループを抜けて2年目の春が来る");
                Album.Add(w, "篤の針が止まった。ここで生きると決めた");
                w.SetUserSpeed(resumeSpeed);
                return;
            }
            string l = ActiveLetter;
            int ep = ActiveEp;
            ActiveLetter = null;
            ActiveEp = -1;
            Pending.Remove(l);
            Done[l] = Mathf.Max(DoneOf(l), ep + 1);
            var c = ByLetter(w, l);
            var d = StoryText.Get(l);
            if (c != null && f != null)
            {
                switch (ep)
                {
                    case 0:
                        w.rel.Add(c.id, f.id, 5f, w.simTime);
                        PlaceGates.Check(w);
                        break;
                    case 1:
                        w.ChangeLove(c, 5f);
                        w.rel.Add(c.id, f.id, 3f, w.simTime);
                        Loop.Zukan.Add(Loop.Key(c, "願い", "わかった"));
                        w.Log("（図鑑：" + c.displayName + "の願いがわかった）");
                        break;
                    case 2:
                        w.ChangeLove(c, 8f);
                        Loop.Zukan.Add(Loop.Key(c, "解決", "わかった"));
                        w.Log("（図鑑：" + c.displayName + "の願いが解けた。あとは、この周で夜をいっしょに過ごすと針が止まる）");
                        break;
                }
                c.PositiveEvent(0.8f);
            }
            if (d != null) Album.Add(w, HLog.NameOf(l) + "の物語 第" + (ep + 1) + "話「" + d.titles[ep] + "」");
            w.SetUserSpeed(resumeSpeed);
        }

        /// <summary>周のはじめ（2周目から）：第2話まで済んだ子は、篤との好感度50から始まる（篤が覚えているので）。</summary>
        public static void ApplyLoopStart(LoungeWorld w)
        {
            if (Loop.Count <= 1) return;
            var f = w.RomanceTarget;
            if (f == null) return;
            foreach (var c in w.characters)
            {
                if (c.IsTarget || DoneOf(c.letter) < 2) continue;
                float v = w.rel.Get(c.id, f.id);
                if (v < 50f) w.rel.Add(c.id, f.id, 50f - v, -100f);
            }
        }

        /// <summary>画面用：その子の物語の進み。</summary>
        public static string StatusText(SimCharacter c)
        {
            int done = DoneOf(c.letter);
            if (Hands.Contains(c.letter)) return "針が止まった";
            if (Pending.Contains(c.letter)) return "第" + (done + 1) + "話の気配（話しかけると始まる）";
            if (done >= 3) return "第3話まで済み・針はまだ動いている";
            if (done == 0) return "まだ始まっていない";
            return "第" + done + "話まで済み";
        }
    }

    /// <summary>
    /// 物語・実績・自動セーブを、ときどき見る部品（LoungeWorld が足す）。
    /// </summary>
    public class ProgressDirector : MonoBehaviour
    {
        LoungeWorld world;
        float storyTimer, achieveTimer;
        int autoSaveDay = -1;

        void Start()
        {
            world = GetComponent<LoungeWorld>();
            if (world != null) autoSaveDay = world.Day; // 始めた日（読み込んだ日）は自動セーブしない
        }

        void Update()
        {
            if (world == null || world.characters.Count == 0) return;
            storyTimer -= Time.unscaledDeltaTime;
            achieveTimer -= Time.unscaledDeltaTime;
            if (storyTimer <= 0f) { storyTimer = 0.5f; Story.Tick(world); }
            if (achieveTimer <= 0f) { achieveTimer = 2f; Achievements.Check(world); }
            // 自動セーブ：毎朝 6:00（その日のはじめに1回）
            if (world.Day != autoSaveDay && world.MinuteOfDay >= 360f && world.MinuteOfDay < 720f && !Story.IsActive && !Loop.AskChoice)
            {
                autoSaveDay = world.Day;
                if (SaveSystem.Save(world, 0)) world.Log("（自動セーブ：" + world.Day + "日目の朝）");
            }
        }
    }
}
