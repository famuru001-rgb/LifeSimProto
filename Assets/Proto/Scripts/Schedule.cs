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

        public string Label
        {
            get
            {
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
    /// 起床 6:30〜7:15 → 朝食 → 授業（午前・午後の4コマから1〜3コマ）→ 昼食 → 夕食 → お風呂 → 就寝 22:45〜23:30
    /// </summary>
    public static class ScheduleMaker
    {
        // 授業のコマ（分）
        static readonly int[,] Blocks = { { 540, 630 }, { 645, 735 }, { 780, 870 }, { 885, 975 } };

        public static double DayStart(int day) { return (day - 1) * 1440.0; }

        public static double WakeTime(int charId, int day)
        {
            var rnd = new System.Random(charId * 7919 + day * 104729 + 1);
            return DayStart(day) + 390 + rnd.Next(0, 46);
        }

        public static List<ScheduleItem> Make(int charId, int day)
        {
            var rnd = new System.Random(charId * 7919 + day * 104729);
            double d0 = DayStart(day);
            double wake = WakeTime(charId, day);
            var list = new List<ScheduleItem>();

            // 朝食
            double bf = System.Math.Max(wake + 10, d0 + 420) + rnd.Next(0, 30);
            list.Add(new ScheduleItem { kind = TaskKind.Breakfast, start = bf, end = bf + 25 });

            // 授業：4コマから1〜3コマ（キャラと日によって違う）
            int count = 1 + rnd.Next(0, 3);
            var chosen = new List<int> { 0, 1, 2, 3 };
            for (int i = 0; i < 4; i++) { int j = rnd.Next(i, 4); int t = chosen[i]; chosen[i] = chosen[j]; chosen[j] = t; }
            chosen = chosen.GetRange(0, count);
            chosen.Sort();
            // 続けて受けるコマは1回の外出にまとめる
            int k = 0;
            while (k < chosen.Count)
            {
                int first = chosen[k];
                int last = first;
                while (k + 1 < chosen.Count && chosen[k + 1] == last + 1) { k++; last = chosen[k]; }
                list.Add(new ScheduleItem { kind = TaskKind.Class, start = d0 + Blocks[first, 0] - 15, end = d0 + Blocks[last, 1] + 15 });
                k++;
            }

            // 昼食（12:20〜13:00 の間に始める。授業で出かけていれば外で食べる＝予定なし）
            double lunch = d0 + 740 + rnd.Next(0, 30);
            if (!Overlaps(list, lunch, lunch + 25)) list.Add(new ScheduleItem { kind = TaskKind.Lunch, start = lunch, end = lunch + 25 });

            // 夕食（18:20〜19:10）
            double dinner = d0 + 1100 + rnd.Next(0, 50);
            list.Add(new ScheduleItem { kind = TaskKind.Dinner, start = dinner, end = dinner + 30 });

            // お風呂（20:00〜22:00 の間に30分）
            double bath = d0 + 1200 + rnd.Next(0, 120);
            list.Add(new ScheduleItem { kind = TaskKind.Bath, start = bath, end = bath + 30 });

            // 就寝（22:45〜23:30）。寝ている間は翌日の起床まで
            double bed = d0 + 1365 + rnd.Next(0, 46);
            list.Add(new ScheduleItem { kind = TaskKind.Bed, start = bed, end = WakeTime(charId, day + 1) });

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
