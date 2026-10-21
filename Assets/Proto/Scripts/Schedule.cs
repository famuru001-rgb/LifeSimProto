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
        public readonly List<int> subjects = new List<int>(); // 授業の科目（コマの順）
        public readonly List<int> slots = new List<int>();    // 授業のコマ（日×10＋コマ）。同じコマのキャラは一緒に出かける

        public string Label
        {
            get
            {
                if (!string.IsNullOrEmpty(title)) return title;
                switch (kind)
                {
                    case TaskKind.Breakfast: return "朝食";
                    case TaskKind.Class:
                        if (subjects.Count == 0) return "授業";
                        var n = new List<string>();
                        foreach (var x in subjects) n.Add(Academics.Names[x]);
                        return "授業（" + string.Join("・", n.ToArray()) + "）";
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
    /// 授業の日（光・火・風・月）：起床 6:30〜7:15 → 朝食 → 取っている科目の授業（時間割どおり）→ 昼食 → 夕食 → お風呂 → 就寝 22:45〜23:30
    /// 休日：起床 7:15〜8:15、授業なし。試験の日（土）：午前は全員が試験（9:00〜12:15）。
    /// </summary>
    public static class ScheduleMaker
    {
        static readonly int[,] Blocks = { { 540, 630 }, { 645, 735 }, { 780, 870 }, { 885, 975 } };

        public static double DayStart(int day) { return (day - 1) * 1440.0; }

        public static double WakeTime(int charId, int day, bool[] holidays)
        {
            var rnd = new System.Random(charId * 7919 + day * 104729 + 1);
            bool holiday = Calendar.IsHoliday(day, holidays);
            var prof = CharProfiles.ById(charId);
            int late = prof != null && (prof.traits & CharTrait.DeepSleeper) != 0 ? 15 : 0; // 眠りが深い
            return DayStart(day) + late + (holiday ? 435 + rnd.Next(0, 61) : 390 + rnd.Next(0, 46));
        }

        public static List<ScheduleItem> Make(int charId, int day, bool[] holidays)
        {
            var rnd = new System.Random(charId * 7919 + day * 104729);
            double d0 = DayStart(day);
            double wake = WakeTime(charId, day, holidays);
            bool exam = Calendar.IsExamDay(day, holidays);
            var list = new List<ScheduleItem>();

            double bf = System.Math.Max(wake + 10, d0 + 420) + rnd.Next(0, 30);
            list.Add(new ScheduleItem { kind = TaskKind.Breakfast, start = bf, end = bf + 25 });

            if (exam)
            {
                list.Add(new ScheduleItem { kind = TaskKind.Class, title = "試験", start = d0 + Blocks[0, 0] - 15, end = d0 + Blocks[1, 1] + 15 });
            }
            else if (Calendar.IsClassDay(day, holidays))
            {
                // 時間割のうち、取っている科目のコマに出る（続いているコマはまとめて出かける）
                int row = Academics.TimetableRow(Calendar.Weekday(day));
                var prof = CharProfiles.ById(charId);
                var chosen = new List<int>();
                for (int b = 0; b < 4; b++)
                {
                    int subj = Academics.Timetable[row, b];
                    if (prof != null && System.Array.IndexOf(prof.enrolled, subj) >= 0) chosen.Add(b);
                }
                int k = 0;
                while (k < chosen.Count)
                {
                    int first = chosen[k];
                    int last = first;
                    while (k + 1 < chosen.Count && chosen[k + 1] == last + 1) { k++; last = chosen[k]; }
                    var item = new ScheduleItem { kind = TaskKind.Class, start = d0 + Blocks[first, 0] - 15, end = d0 + Blocks[last, 1] + 15 };
                    for (int b = first; b <= last; b++) { item.subjects.Add(Academics.Timetable[row, b]); item.slots.Add(day * 10 + b); }
                    list.Add(item);
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
