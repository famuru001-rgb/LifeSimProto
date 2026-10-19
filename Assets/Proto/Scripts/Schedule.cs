using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    public enum TaskKind { Breakfast, Class, Lunch, Dinner, Bath, Bed }

    /// <summary>1日の予定の1項目。時刻は「1日目0時からの通算分」。</summary>
    public class ScheduleItem
    {
        public TaskKind kind;
        public double start;
        public double end;
        public bool done;
        public string title;  // 「試験」など、名前を変えたいとき

        public string Label
        {
            get
            {
                if (!string.IsNullOrEmpty(title)) return title;
                switch (kind)
                {
                    case TaskKind.Breakfast: return "朝食";
                    case TaskKind.Class: return "授業";
                    case TaskKind.Lunch: return "昼食";
                    case TaskKind.Dinner: return "夕食";
                    case TaskKind.Bath: return "お風呂";
                    default: return "就寝";
                }
            }
        }
    }

    /// <summary>
    /// キャラごとに違う1日の予定を作る。
    /// 平日：起床 6:30〜7:15 → 朝食 → 授業（4コマから1〜3コマ）→ 昼食 → 夕食 → お風呂 → 就寝 22:45〜23:30
    /// 休日：起床 7:15〜8:15、授業なし。試験の日：午前は全員が試験（9:00〜12:15）。
    /// </summary>
    public static class ScheduleMaker
    {
        static readonly int[,] Blocks = { { 540, 630 }, { 645, 735 }, { 780, 870 }, { 885, 975 } };

        public static double DayStart(int day) { return (day - 1) * 1440.0; }

        public static double WakeTime(int charId, int day, bool[] holidays)
        {
            var rnd = new System.Random(charId * 7919 + day * 104729 + 1);
            bool holiday = Calendar.IsHoliday(day, holidays);
            return DayStart(day) + (holiday ? 435 + rnd.Next(0, 61) : 390 + rnd.Next(0, 46));
        }

        public static List<ScheduleItem> Make(int charId, int day, bool[] holidays)
        {
            var rnd = new System.Random(charId * 7919 + day * 104729);
            double d0 = DayStart(day);
            double wake = WakeTime(charId, day, holidays);
            bool holiday = Calendar.IsHoliday(day, holidays);
            bool exam = Calendar.IsExamDay(day, holidays);
            var list = new List<ScheduleItem>();

            double bf = System.Math.Max(wake + 10, d0 + 420) + rnd.Next(0, 30);
            list.Add(new ScheduleItem { kind = TaskKind.Breakfast, start = bf, end = bf + 25 });

            if (exam)
            {
                list.Add(new ScheduleItem { kind = TaskKind.Class, title = "試験", start = d0 + Blocks[0, 0] - 15, end = d0 + Blocks[1, 1] + 15 });
                if (rnd.Next(0, 2) == 0)
                {
                    int b = 2 + rnd.Next(0, 2);
                    list.Add(new ScheduleItem { kind = TaskKind.Class, start = d0 + Blocks[b, 0] - 15, end = d0 + Blocks[b, 1] + 15 });
                }
            }
            else if (!holiday)
            {
                int count = 1 + rnd.Next(0, 3);
                var chosen = new List<int> { 0, 1, 2, 3 };
                for (int i = 0; i < 4; i++) { int j = rnd.Next(i, 4); int t = chosen[i]; chosen[i] = chosen[j]; chosen[j] = t; }
                chosen = chosen.GetRange(0, count);
                chosen.Sort();
                int k = 0;
                while (k < chosen.Count)
                {
                    int first = chosen[k];
                    int last = first;
                    while (k + 1 < chosen.Count && chosen[k + 1] == last + 1) { k++; last = chosen[k]; }
                    list.Add(new ScheduleItem { kind = TaskKind.Class, start = d0 + Blocks[first, 0] - 15, end = d0 + Blocks[last, 1] + 15 });
                    k++;
                }
            }

            double lunch = d0 + 740 + rnd.Next(0, 30);
            if (!Overlaps(list, lunch, lunch + 25)) list.Add(new ScheduleItem { kind = TaskKind.Lunch, start = lunch, end = lunch + 25 });

            double dinner = d0 + 1100 + rnd.Next(0, 50);
            list.Add(new ScheduleItem { kind = TaskKind.Dinner, start = dinner, end = dinner + 30 });

            double bath = d0 + 1200 + rnd.Next(0, 120);
            list.Add(new ScheduleItem { kind = TaskKind.Bath, start = bath, end = bath + 30 });

            double bed = d0 + 1365 + rnd.Next(0, 46);
            list.Add(new ScheduleItem { kind = TaskKind.Bed, start = bed, end = WakeTime(charId, day + 1, holidays) });

            list.Sort((a, b) => a.start.CompareTo(b.start));
            return list;
        }

        static bool Overlaps(List<ScheduleItem> list, double s, double e)
        {
            foreach (var it in list) if (s < it.end && e > it.start) return true;
            return false;
        }

        public static string Clock(double absMinutes)
        {
            int m = Mathf.FloorToInt((float)(absMinutes % 1440.0));
            return string.Format("{0}:{1:00}", m / 60, m % 60);
        }
    }
}
