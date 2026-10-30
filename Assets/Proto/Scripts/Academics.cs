using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 科目と時間割と試験の結果（2026-10-25 作り直し。famuru 決定）。
    /// 座学（6）：魔法理論・古代語・魔道具学・薬草学・王国史・魔物学
    /// 実技（6）：剣術・魔法術・ダンジョン実習・錬成・飛行術・魔法医学（治癒と状態異常をまとめて学ぶ）
    /// 2026-10-30 追加（famuru「科目もゲームに取り入れておいて」：追加のヒロインの設定の科目を4つとも）：
    ///   座学 占星術（もとの座学の予備の枠）・実技 体術（もとの実技の予備の枠）・元素魔法・回復魔法。座学7・実技9の16科目
    ///   並びは後ろに足しただけなので、前のセーブの学力はそのまま使える（足した科目は最初の値）
    /// 1人が取るのは座学4・実技4の8科目（篤は座学のみ）。騎士団・魔法師団の子は優先科目を必ず取る。
    /// 授業は光・火・風・月の日（1日4コマ）。試験は土の日の午前（取っている科目ごと）。
    /// </summary>
    public static class Academics
    {
        // 科目の番号（セーブや時間割に使う。並びを変えないこと）
        public const int Theory = 0, Ancient = 1, Tools = 2, Herb = 3, History = 4, Monster = 5;           // 座学
        public const int Sword = 6, MagicArts = 7, Dungeon = 8, Alchemy = 9, Flight = 10, Medicine = 11;  // 実技
        public const int Astrology = 12, BodyArts = 13, Elemental = 14, HealMagic = 15;                     // 2026-10-30 追加：占星術（座学）・体術・元素魔法・回復魔法（実技）
        public const int SpareStudy = Astrology, SparePractical = BodyArts;                                 // もとの予備の枠（使った）

        public static readonly string[] Names = { "魔法理論", "古代語", "魔道具学", "薬草学", "王国史", "魔物学", "剣術", "魔法術", "ダンジョン実習", "錬成", "飛行術", "魔法医学", "占星術", "体術", "元素魔法", "回復魔法" };
        public const int Count = 16;
        /// <summary>セーブの科目の並びの版（並びを変えたら上げる）。前の版のセーブの学力は使わない。</summary>
        public const int Version = 2;
        public const int Healing = Medicine; // 実技の失敗の手当て・風邪を軽くする魔法

        static readonly bool[] Practical = { false, false, false, false, false, false, true, true, true, true, true, true, false, true, true, true };
        public static bool IsPractical(int s) { return s >= 0 && s < Count && Practical[s]; }
        /// <summary>使っている科目か（予備の枠は名前を入れるまで使わない）。</summary>
        public static bool IsActive(int s) { return s >= 0 && s < Count && !string.IsNullOrEmpty(Names[s]); }
        /// <summary>使っている科目の数（成績表の行の数）。</summary>
        public static int ActiveCount { get { int n = 0; for (int s = 0; s < Count; s++) if (IsActive(s)) n++; return n; } }

        // 騎士団・魔法師団の優先科目（所属の子は必ず取る。famuru 決定 2026-10-25）
        public static readonly int[] KnightPriority = { Sword, Dungeon, Monster };
        public static readonly int[] MagePriority = { MagicArts, Theory, Ancient };

        /// <summary>授業のある曜日（0始まり：光・火・風・月）。</summary>
        public static readonly int[] ClassWeekdays = { 0, 1, 3, 5 };

        /// <summary>時間割［授業の曜日の順番, コマ］＝科目。</summary>
        public static readonly int[,] Timetable =
        {
            { Theory, Sword, Ancient, Medicine },      // 光の日：魔法理論・剣術・古代語・魔法医学
            { Tools, MagicArts, Herb, Flight },        // 火の日：魔道具学・魔法術・薬草学・飛行術
            { History, Alchemy, Monster, Dungeon },    // 風の日：王国史・錬成・魔物学・ダンジョン実習
            { Astrology, Elemental, HealMagic, BodyArts }, // 月の日：占星術・元素魔法・回復魔法・体術（2026-10-30。前は魔法理論・魔法術・剣術・魔法医学の2回目）
        };

        /// <summary>その曜日の時間割の行（授業のない曜日は -1）。</summary>
        public static int TimetableRow(int weekday) { return System.Array.IndexOf(ClassWeekdays, weekday); }

        /// <summary>実技の授業の、ちょっとした失敗（外での授業は結果だけ伝わる）。</summary>
        public static string MishapText(int s)
        {
            switch (s)
            {
                case Sword: return "木剣をすっぽ抜かせた";
                case MagicArts: return "小さな爆発を起こした";
                case Dungeon: return "練習用の罠にひっかかった";
                case Alchemy: return "鍋から煙をもくもく出した";
                case Flight: return "ほうきから落ちた";
                case Medicine: return "眠りの魔法を自分にかけてしまった";
                case BodyArts: return "受け身をとりそこねた";
                case Elemental: return "小さなつむじ風で髪がぼさぼさになった";
                case HealMagic: return "治すつもりが、くすぐったくしてしまった";
                default: return "ちょっと失敗した";
            }
        }

        public static string MishapSound(int s)
        {
            switch (s)
            {
                case Sword: return "カランッ！";
                case MagicArts: return "ボンッ！";
                case Dungeon: return "ひゃっ！";
                case Alchemy: return "もくもく…";
                case Flight: return "わわっ…どてっ";
                case Medicine: return "すや…";
                case BodyArts: return "どすん！";
                case Elemental: return "ひゅるる…";
                case HealMagic: return "きゃはは…";
                default: return "あっ";
            }
        }

        public static string Mark(float score) { return score >= 60f ? "◎" : (score >= 40f ? "○" : (score >= 25f ? "△" : "×")); }

        // ---- 試験の結果（いちばん新しい回）----
        public class ExamEntry { public SimCharacter who; public float score; public string mark; }
        public static int lastExamDay = -1;
        /// <summary>いちばん新しい試験の、寮ごとの平均点の順位（表示用。2026-10-30）。</summary>
        public static string LastDormRanking = "";
        public static readonly List<ExamEntry>[] LastExam = NewTable();
        public static readonly List<KeyValuePair<SimCharacter, float>> LastOverall = new List<KeyValuePair<SimCharacter, float>>();

        static List<ExamEntry>[] NewTable()
        {
            var t = new List<ExamEntry>[Count];
            for (int i = 0; i < Count; i++) t[i] = new List<ExamEntry>();
            return t;
        }

        public static void ClearExam()
        {
            foreach (var l in LastExam) l.Clear();
            LastOverall.Clear();
        }
    }
}
