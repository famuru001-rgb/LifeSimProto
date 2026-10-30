using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LifeSimProto
{
    /// <summary>
    /// ループ（1年＝28日で春の朝に戻る）。篤だけが覚えている（だれにも知られていない）。
    /// 残るもの：篤の記憶・キャラ図鑑・思い出のアルバム・篤の学力（と使える魔法）・開いた場所。
    /// 戻るもの：銀貨・持ち物・みんなとの関係（知り合いに戻る）。
    /// 戻る瞬間：毎周、冬至の祭りが終わったあと（夜の時刻は周ごとに少し変わる）。
    /// 物語でループを抜けたら、終わりのない日常を続けるか、もう一度周回するかを選べる（物語はあとで入れる）。
    /// </summary>
    public static class Loop
    {
        public static int Count = 1;                          // 何周目か
        public static float[] fSkills;                        // 篤の学力（持ち越し）
        public static float[] fAbilities;                     // 篤の能力値（持ち越し）
        public static readonly HashSet<string> Zukan = new HashSet<string>();      // 図鑑でわかったこと（"A:好き:本" など）
        public static readonly List<string> OpenedPlaces = new List<string>();     // 開いた場所（場所ができたら使う）
        public static readonly HashSet<string> NightTogether = new HashSet<string>(); // その周で篤と夜をいっしょに過ごした子（記号）。周が変わると消える
        public static bool Escaped;                           // 物語でループを抜けた
        public static bool EndlessDays;                       // 抜けたあと、日常を続けることを選んだ
        public static bool AskChoice;                         // 年の終わりに「続ける／もう一周」をたずねている

        /// <summary>この周で戻る日（1年の何日目か）と時刻（時）。</summary>
        public static int RewindDayOfYear = 28;
        public static float RewindHour = 23.5f;

        // 再生を始めるたびに最初の状態にする（Unity の設定で、再生のたびに読み込み直さない場合のため）
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            Count = 1; fSkills = null; fAbilities = null; NightTogether.Clear(); Zukan.Clear(); OpenedPlaces.Clear();
            Escaped = false; EndlessDays = false; AskChoice = false;
            Album.Entries.Clear();
            FMagic.Unlocked.Clear();
            foreach (var p in CharProfiles.All) p.ResetEnrolled();
            PickRewind();
        }

        static void PickRewind()
        {
            // 毎周、冬至の祭り（冬・星の日 17:30〜19:30）が終わったら戻る。戻る時刻は周ごとに少し変わる（famuru 決定 2026-10-21）
            var rnd = new System.Random(Count * 977 + 3);
            RewindDayOfYear = 28;
            RewindHour = Count == 1 ? 22.5f : 20.5f + (float)rnd.NextDouble() * 3f;
        }

        public static string Key(SimCharacter c, string kind, string what) { return c.letter + ":" + kind + ":" + what; }
        public static bool Known(SimCharacter c, string kind, string what) { return Zukan.Contains(Key(c, kind, what)); }

        /// <summary>図鑑に書く。新しくわかったら true。</summary>
        public static bool Learn(LoungeWorld w, SimCharacter c, string kind, string what)
        {
            if (c == null || c.IsTarget) return false;
            if (!Zukan.Add(Key(c, kind, what))) return false;
            w.Log("（図鑑：" + c.displayName + "は「" + what + "」が" + kind + "）");
            return true;
        }

        /// <summary>年の終わり：春の朝に戻る。篤の学力などを持ち越して、寮を最初から作り直す。</summary>
        public static void Rewind(LoungeWorld w)
        {
            var f = w.RomanceTarget;
            if (f != null) { fSkills = (float[])f.skill.Clone(); fAbilities = (float[])f.ability.Clone(); }
            Album.Add(w, "…気がつくと、また春の朝だった（" + Count + "周目が終わった）");
            Count++;
            NightTogether.Clear();
            Story.OnRewind();         // 物語の気配・その周の行事の結果は消える（進みと針は残る）
            RoomDecor.Placed.Clear(); // 家具は持ち物と同じく戻る
            PickRewind();
            ReloadScene();
        }

        /// <summary>寮を最初から作り直す（ループ・セーブの読み込み）。</summary>
        public static void ReloadScene()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
#endif
        }

        /// <summary>周の数から、戻る時刻を決め直す（セーブを読み込んだとき）。</summary>
        public static void RefreshRewind() { PickRewind(); }
    }

    /// <summary>
    /// 場所の開き方：篤の能力値＋縁のあるキャラの物語の第1話を見た（2026-10-22 から。前は「友達」）。
    /// </summary>
    public static class PlaceGates
    {
        public class Gate { public string place, ability; public int abilityIndex; public string[] friends; }

        public static readonly Gate[] All =
        {
            new Gate { place = "中庭", abilityIndex = Abilities.Body, ability = "C", friends = new[] { "C", "E", "H" } },
            new Gate { place = "温室", abilityIndex = Abilities.Mind, ability = "C", friends = new[] { "B" } },
            new Gate { place = "図書室", abilityIndex = Abilities.Mind, ability = "B", friends = new[] { "A", "I" } },
            new Gate { place = "工房", abilityIndex = Abilities.Dex, ability = "C", friends = new[] { "G", "J" } },
            new Gate { place = "屋上の天文台", abilityIndex = Abilities.Magic, ability = "D", friends = new[] { "D", "H", "K" } },
            // 台所は最初から開いている（famuru 2026-10-26。前は 器用さD以上・リアの物語の第1話）
        };

        public static string Text(Gate g)
        {
            var n = new System.Collections.Generic.List<string>();
            foreach (var l in g.friends) { var p = CharProfiles.All.Find(x => x.letter == l); n.Add(p != null ? p.name : l); }
            return "篤の" + Abilities.Names[g.abilityIndex] + " " + g.ability + "以上・" + string.Join("か", n.ToArray()) + "の物語の第1話";
        }

        /// <summary>条件を満たした場所を開く（1日1回くらい呼ぶ）。</summary>
        public static void Check(LoungeWorld w)
        {
            var f = w.RomanceTarget;
            if (f == null) return;
            foreach (var g in All)
            {
                if (Loop.OpenedPlaces.Contains(g.place)) continue;
                if (f.Ab(g.abilityIndex) < Abilities.RankMin(g.ability)) continue;
                bool friend = false;
                foreach (var l in g.friends) if (Story.DoneOf(l) >= 1) friend = true;
                if (!friend) continue;
                Loop.OpenedPlaces.Add(g.place);
                w.Log("★ 「" + g.place + "」が開いた（場所はこれから作る。ループしても開いたまま）");
                Album.Add(w, "「" + g.place + "」に入れるようになった");
            }
        }
    }

    /// <summary>
    /// 篤の魔法。はじめは勉学だけ。勉学の学力が上がると、実技の魔法が使えるようになり、実技の授業にも出る。
    /// 学力はループで持ち越すので、周を重ねるほど使える魔法が増える。
    /// </summary>
    public static class FMagic
    {
        public class Spell
        {
            public int subject;       // 実技の科目
            public int needSubject;   // 必要な勉学
            public float need;        // 必要な学力
            public string name, desc;
        }

        public static readonly Spell[] All =
        {
            // 2026-10-25 科目の作り直しに合わせて、実技・必要な座学をつなぎ直した
            new Spell { subject = Academics.MagicArts, needSubject = Academics.Theory, need = 40f, name = "暖炉に火をつける", desc = "談話室があたたかくなり、みんなの気分が少し上がる" },
            new Spell { subject = Academics.Medicine, needSubject = Academics.Herb, need = 40f, name = "風邪を軽くする", desc = "選んだ子の風邪を1段階軽くする" },
            new Spell { subject = Academics.Alchemy, needSubject = Academics.Tools, need = 40f, name = "壊れた物を直す", desc = "寮の古い物を直す（寮の評判＋1）" },
            new Spell { subject = Academics.Flight, needSubject = Academics.History, need = 45f, name = "一緒に飛ぶ", desc = "選んだ子と夜空を少し飛ぶ（仲よく・気持ちが育つ）" },
            new Spell { subject = Academics.Sword, needSubject = Academics.Monster, need = 45f, name = "身のこなし", desc = "剣術の基本の呼吸。疲れにくくなる" },
        };

        public static readonly HashSet<int> Unlocked = new HashSet<int>();
        static readonly Dictionary<int, int> usedDay = new Dictionary<int, int>();

        public static bool Has(int subject) { return Unlocked.Contains(subject); }
        public static bool UsedToday(int subject, int day) { int d; return usedDay.TryGetValue(subject, out d) && d == day; }

        /// <summary>学力を見て、新しく使える魔法を開く。開いた実技は篤の時間割にも入る。</summary>
        public static void Check(LoungeWorld w, SimCharacter f)
        {
            if (f == null || f.profile == null) return;
            foreach (var s in All)
            {
                if (Unlocked.Contains(s.subject) || f.SkillOf(s.needSubject) < s.need) continue;
                Unlocked.Add(s.subject);
                if (System.Array.IndexOf(f.profile.enrolled, s.subject) < 0)
                {
                    var list = new List<int>(f.profile.enrolled) { s.subject };
                    f.profile.enrolled = list.ToArray();
                }
                if (f.skill[s.subject] < 10f) f.skill[s.subject] = 10f;
                w.Log("★ 篤が" + Academics.Names[s.subject] + "の魔法を少し使えるようになった（「" + s.name + "」）");
                Album.Add(w, "篤がはじめて" + Academics.Names[s.subject] + "の魔法を使えた");
            }
        }

        /// <summary>魔法を使う（1日1回ずつ）。target は選んだ子（いらない魔法は null）。</summary>
        public static bool Use(LoungeWorld w, SimCharacter f, int subject, SimCharacter target)
        {
            if (!Has(subject) || UsedToday(subject, w.Day) || f == null) return false;
            usedDay[subject] = w.Day;
            f.AddSkill(subject, 1.5f);
            f.GrowAbility(Abilities.Magic, 1f);
            switch (subject)
            {
                case Academics.MagicArts:
                    f.ShowBubble("（指先に火を灯す）", 2f);
                    foreach (var c in w.residents)
                        if (c != f && c.Present && w.AreaName(c.transform.position) == "談話室") { c.bored = Mathf.Max(0f, c.bored - 15f); c.PositiveEvent(0.3f); }
                    w.Log("篤が魔法で暖炉に火をつけた。談話室がぽかぽかになった");
                    break;
                case Academics.Medicine:
                    if (target == null) return false;
                    f.ShowBubble("（手をかざす）", 2f);
                    target.HealCold();
                    w.rel.Add(f.id, target.id, 3f, w.simTime);
                    w.ChangeLove(target, 2f);
                    w.Log("篤が魔法医学で" + target.displayName + "の具合を軽くした");
                    break;
                case Academics.Alchemy:
                    f.ShowBubble("（直れ…）", 2f);
                    w.dormReputation += 1;
                    w.Log("篤が錬成の魔法で寮の古い椅子を直した（寮の評判 " + w.dormReputation + "）");
                    break;
                case Academics.Flight:
                    if (target == null) return false;
                    f.ShowBubble("ちょっと飛んでみる？", 2f);
                    target.ShowBubble(target.Say("like", "わあ…！"), 2f);
                    w.rel.Add(f.id, target.id, 5f, w.simTime);
                    w.ChangeLove(target, 3f);
                    target.PositiveEvent(0.6f);
                    w.Log("篤が" + target.displayName + "と一緒に少しだけ空を飛んだ");
                    Album.Add(w, "篤と" + target.displayName + "が一緒に空を飛んだ");
                    break;
                case Academics.Sword:
                    f.tired = Mathf.Max(0f, f.tired - 30f);
                    f.ShowBubble("（呼吸を整える）", 2f);
                    w.Log("篤が剣術の呼吸で疲れを取った");
                    break;
            }
            return true;
        }
    }
}
