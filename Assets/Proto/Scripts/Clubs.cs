using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 部活動（2026-11-01 famuru）：生徒会・剣術部・魔法術部・ほうき部・料理部・魔道具研究会の6つ。
    /// ・寮生・ほかの寮の学生・篤の全員が、どれか1つに入る（先生は入らない）。寮生とほかの寮の学生は得意なことと好みで決まる。篤は画面で選べる
    /// ・授業の日の 16:30〜18:00 に、学校のそれぞれの場所で活動する（姿が見える）。活動すると、その部の科目と能力が伸び、部の仲間と仲良くなる
    /// ・季節ごとの水の日の夕方に部の大会（生徒会は「生徒会の働き」）。寮ごとに強さを比べて、寮杯の点が入る
    /// </summary>
    public static class Clubs
    {
        public const int Council = 0, Sword = 1, Magic = 2, Broom = 3, Cooking = 4, Tools = 5, Count = 6;
        public static readonly string[] Names = { "生徒会", "剣術部", "魔法術部", "ほうき部", "料理部", "魔道具研究会" };
        public static readonly string[] Rooms = { "生徒会室", "剣術部の部室", "魔法術部の部室", "ほうき部の部室", "料理部の部室", "魔道具研究会の部室" };
        /// <summary>活動のようす（画面の「今：」に出す）。</summary>
        public static readonly string[] Doing = { "生徒会の仕事をしている", "剣術の稽古をしている", "魔法の練習をしている", "ほうきで飛ぶ練習をしている", "料理の練習をしている", "魔道具をいじっている" };
        /// <summary>動きを選ぶ言葉（MotionBook の場面）。</summary>
        public static readonly string[] Scene = { null, "剣術", "魔法", null, "料理", "工房" };
        /// <summary>伸びる科目と能力。</summary>
        public static readonly int[] Subject = { Academics.History, Academics.Sword, Academics.MagicArts, Academics.Flight, Academics.Herb, Academics.Tools };
        public static readonly int[] Ability = { Abilities.Charm, Abilities.Body, Abilities.Magic, Abilities.Body, Abilities.Dex, Abilities.Dex };

        public const double StartMin = 990, EndMin = 1080;   // 16:30〜18:00
        public const int CouncilPerDorm = 2;                 // 生徒会：寮ごとに2人（4つの寮で8人）

        /// <summary>篤が選んだ部（-1 はまだ。ループをまたいで残る）。</summary>
        public static int FChoice = -1;
        public static string LastTournament = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() { FChoice = -1; LastTournament = ""; }

        public static string NameOf(int club) { return club >= 0 && club < Count ? Names[club] : "なし"; }

        /// <summary>その部の活動の場所（学校の区画の名前で選ぶ）。</summary>
        public static System.Predicate<AreaInfo> Place(int club, bool badWeather)
        {
            switch (club)
            {
                case Council: return a => a.name.EndsWith("生徒会室");
                case Sword: return a => a.name == "体育館" && a.floor == 0;
                case Magic: return a => a.name.EndsWith("魔法実習室");
                case Broom: return badWeather ? (System.Predicate<AreaInfo>)(a => a.name == "体育館" && a.floor == 0) : (a => a.name == "本館の中庭");
                case Cooking: return a => a.name.EndsWith("料理部の部室");
                case Tools: return a => a.name.EndsWith("魔道具研究会の部室");
            }
            return a => false;
        }

        /// <summary>その部での強さ（大会・向き不向き）。</summary>
        public static float Score(SimCharacter c, int club)
        {
            switch (club)
            {
                case Council: return (c.Ab(Abilities.Charm) + c.Ab(Abilities.Mind)) * 0.5f + c.SkillOf(Academics.History) * 0.3f;
                case Sword: return c.SkillOf(Academics.Sword) * 0.7f + c.Ab(Abilities.Body) * 0.5f;
                case Magic: return c.SkillOf(Academics.MagicArts) * 0.7f + c.Ab(Abilities.Magic) * 0.5f;
                case Broom: return c.SkillOf(Academics.Flight) * 0.7f + c.Ab(Abilities.Body) * 0.3f + c.Ab(Abilities.Magic) * 0.2f;
                case Cooking: return c.Ab(Abilities.Dex) * 0.6f + c.SkillOf(Academics.Herb) * 0.4f + c.Ab(Abilities.Charm) * 0.2f;
                case Tools: return c.SkillOf(Academics.Tools) * 0.5f + c.SkillOf(Academics.Alchemy) * 0.4f + c.Ab(Abilities.Dex) * 0.4f;
            }
            return 0f;
        }

        /// <summary>入る部を決める（寮生・ほかの寮の学生・篤）。最初に1回。</summary>
        public static void Assign(LoungeWorld w)
        {
            var students = new List<SimCharacter>();
            foreach (var c in w.characters) if (c.profile != null && !c.IsTeacher) students.Add(c);
            foreach (var c in students) c.club = -1;
            // 生徒会：寮ごとに、魅力と知力が高く真面目な子から2人（篤は自分で選ぶ）
            for (int d = 0; d < HouseCup.Count; d++)
            {
                var cand = students.FindAll(c => !c.IsTarget && DormOf(c) == d);
                cand.Sort((a, b) => CouncilPick(b).CompareTo(CouncilPick(a)));
                for (int i = 0; i < Mathf.Min(CouncilPerDorm, cand.Count); i++) cand[i].club = Council;
            }
            foreach (var c in students)
            {
                if (c.club >= 0) continue;
                if (c.IsTarget) { c.club = FChoice >= 0 ? FChoice : BestFor(c); continue; }
                c.club = BestFor(c);
            }
            if (FChoice < 0) { var f = w.RomanceTarget; if (f != null) FChoice = f.club; }
            var counts = new int[Count];
            foreach (var c in students) if (c.club >= 0) counts[c.club]++;
            var parts = new List<string>();
            for (int k = 0; k < Count; k++) parts.Add(Names[k] + " " + counts[k] + "人");
            Debug.Log("部活：" + string.Join("・", parts.ToArray()));
        }

        static float CouncilPick(SimCharacter c)
        {
            var rnd = new System.Random(c.id * 131 + 7);
            return Score(c, Council) + (c.profile != null ? c.profile.diligence * 25f : 0f) + (float)rnd.NextDouble() * 20f;
        }

        /// <summary>生徒会以外で、いちばん向いている部（好みとくじも少し）。</summary>
        public static int BestFor(SimCharacter c)
        {
            var p = c.profile;
            // 寮生の好み（設定に合わせて）
            if (p != null && !p.mob)
            {
                if (p.letter == "I") return Cooking;                     // リア：料理を覚えたい
                if (p.letter == "G" || p.letter == "J") return Tools;     // ロッテ・キッカ：魔道具
            }
            int best = Sword; float bv = float.MinValue;
            for (int k = 1; k < Count; k++)
            {
                var rnd = new System.Random(c.id * 7919 + k * 31);
                float v = Score(c, k) + (float)rnd.NextDouble() * 18f;
                if (p != null)
                {
                    if (p.mob && p.mobDorm == 1 && k == Sword) v += 12f;   // 騎士寮
                    if (p.mob && p.mobDorm == 2 && k == Magic) v += 12f;   // 魔法師寮
                    if (p.mob && p.mobDorm == 0 && (k == Broom || k == Sword)) v += 6f; // 冒険者寮
                    if (k == Cooking && (p.Likes("料理") || p.hobby == SimCharacter.HobbyKind.Tea)) v += 10f;
                    if (k == Magic && p.hobby == SimCharacter.HobbyKind.Magic) v += 8f;
                    if (k == Broom && p.hobby == SimCharacter.HobbyKind.Walk) v += 6f;
                    if (p.affiliation == Affiliation.Knight && k == Sword) v += 8f;
                    if (p.affiliation == Affiliation.Mage && k == Magic) v += 8f;
                }
                if (v > bv) { bv = v; best = k; }
            }
            return best;
        }

        /// <summary>篤の部を変える（画面から）。</summary>
        public static void SetF(LoungeWorld w, int club)
        {
            var f = w.RomanceTarget;
            if (f == null || club < 0 || club >= Count) return;
            FChoice = club;
            f.club = club;
            w.Log("篤は" + Names[club] + "に入った");
        }

        public static List<SimCharacter> Members(LoungeWorld w, int club)
        {
            var list = new List<SimCharacter>();
            foreach (var c in w.characters) if (c.club == club) list.Add(c);
            return list;
        }

        /// <summary>今日は部活の日か（授業の日。試験の日は休み）。</summary>
        public static bool ActiveDay(LoungeWorld w, int day)
        {
            // 行事の日でも、夕方の行事（歓迎会など）なら部活はある。学校の行事のある日（競技会など）は休み
            if (!Calendar.IsClassDay(day, w.holidayWeekdays) || Calendar.IsExamDay(day, w.holidayWeekdays)) return false;
            foreach (var e in YearEvents.All) if (e.invited == null && e.school && e.season == Calendar.Season(day) && e.weekday == Calendar.Weekday(day)) return false;
            return true;
        }

        /// <summary>季節ごとの部の大会（水の日の 17:00 に結果）。</summary>
        public static void Tournament(LoungeWorld w)
        {
            var lines = new List<string>();
            int season = Calendar.Season(w.Day);
            for (int k = 1; k < Count; k++)
            {
                var scores = new float[HouseCup.Count];
                SimCharacter mvp = null; float mvpV = float.MinValue;
                for (int d = 0; d < HouseCup.Count; d++)
                {
                    var vals = new List<float>();
                    foreach (var c in w.characters)
                    {
                        if (c.club != k || c.IsTeacher) continue;
                        if (DormOf(c) != d) continue;
                        float v = Score(c, k) + Random.Range(0f, 15f);
                        vals.Add(v);
                        if (d == 0 && v > mvpV) { mvpV = v; mvp = c; }
                    }
                    vals.Sort((a, b) => b.CompareTo(a));
                    float sum = 0f; int n = Mathf.Min(3, vals.Count);
                    for (int i = 0; i < n; i++) sum += vals[i];
                    scores[d] = n > 0 ? sum / 3f : 0f;   // 人数が少ない寮は弱い（3人に満たない分は0）
                }
                int place = HouseCup.AddRanked(w, scores, new[] { 20, 12, 6, 2 }, Names[k] + "の大会");
                lines.Add(Names[k] + "：" + HouseCup.OurName + " " + place + "位" + (place == 1 && mvp != null ? "（" + mvp.displayName + "が活躍）" : ""));
                foreach (var c in w.residents) if (c.club == k) { if (place == 1) c.PositiveEvent(0.8f); else if (place == 4) c.NegativeEvent(0.3f); }
                var f = w.RomanceTarget;
                if (f != null && f.club == k && place == 1) f.PositiveEvent(0.8f);
            }
            // 生徒会：学園の行事の運営。生徒会に入っている学生の寮に点
            var council = new int[HouseCup.Count];
            foreach (var c in w.characters) if (c.club == Council && !c.IsTeacher) council[DormOf(c)]++;
            for (int d = 0; d < HouseCup.Count; d++) if (council[d] > 0) HouseCup.Add(w, d, council[d] * 5, "生徒会の働き（" + council[d] + "人）", d == 0);
            LastTournament = Calendar.SeasonNames[season] + "の部の大会：" + string.Join("　", lines.ToArray());
            w.Log("★ " + LastTournament);
            Album.Add(w, LastTournament);
        }

        /// <summary>学生の寮（0 古時計寮・1〜3 ほかの寮）。</summary>
        public static int DormOf(SimCharacter c)
        {
            if (c.IsMob) return c.profile.mobDorm >= 0 && c.profile.mobDorm <= 2 ? c.profile.mobDorm + 1 : -1;
            return 0;
        }
    }
}
