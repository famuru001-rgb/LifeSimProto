using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 科目と時間割と試験の結果。
    /// 勉学：魔法理論・薬草学・魔道具学・古代語・占星術　実技：元素魔法・回復魔法・飛行術・体術・錬成
    /// 授業は光・火・風・月の日（1日4コマ）。試験は土の日の午前（取っている科目ごと）。
    /// </summary>
    public static class Academics
    {
        public static readonly string[] Names = { "魔法理論", "薬草学", "魔道具学", "古代語", "占星術", "元素魔法", "回復魔法", "飛行術", "体術", "錬成" };
        public const int Count = 10;
        public const int Healing = 6;

        public static bool IsPractical(int s) { return s >= 5; }

        /// <summary>授業のある曜日（0始まり：光・火・風・月）。</summary>
        public static readonly int[] ClassWeekdays = { 0, 1, 3, 5 };

        /// <summary>時間割［授業の曜日の順番, コマ］＝科目。</summary>
        public static readonly int[,] Timetable =
        {
            { 0, 5, 1, 8 },   // 光の日：魔法理論・元素魔法・薬草学・体術
            { 3, 6, 2, 7 },   // 火の日：古代語・回復魔法・魔道具学・飛行術
            { 4, 9, 0, 5 },   // 風の日：占星術・錬成・魔法理論・元素魔法
            { 1, 6, 3, 9 },   // 月の日：薬草学・回復魔法・古代語・錬成
        };

        /// <summary>その曜日の時間割の行（授業のない曜日は -1）。</summary>
        public static int TimetableRow(int weekday) { return System.Array.IndexOf(ClassWeekdays, weekday); }

        /// <summary>実技の授業の、ちょっとした失敗（外での授業は結果だけ伝わる）。</summary>
        public static string MishapText(int s)
        {
            switch (s)
            {
                case 5: return "小さな爆発を起こした";
                case 6: return "光を出しすぎてまぶしくさせた";
                case 7: return "ほうきから落ちた";
                case 8: return "受け身に失敗して転んだ";
                case 9: return "鍋から煙をもくもく出した";
                default: return "ちょっと失敗した";
            }
        }

        public static string MishapSound(int s)
        {
            switch (s)
            {
                case 5: return "ボンッ！";
                case 6: return "ぴかーっ！";
                case 7: return "わわっ…どてっ";
                case 8: return "どさっ";
                case 9: return "もくもく…";
                default: return "あっ";
            }
        }

        public static string Mark(float score) { return score >= 60f ? "◎" : (score >= 40f ? "○" : (score >= 25f ? "△" : "×")); }

        // ---- 試験の結果（いちばん新しい回）----
        public class ExamEntry { public SimCharacter who; public float score; public string mark; }
        public static int lastExamDay = -1;
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
