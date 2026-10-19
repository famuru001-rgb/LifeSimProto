using UnityEngine;

namespace LifeSimProto
{
    public enum Weather { Sunny, Cloudy, Rain, Snow }

    /// <summary>
    /// 暦。1週間は7日（光・火・水・風・土・月・星の日）、季節は2週間ごと（春・夏・秋・冬）。
    /// 休日は LoungeWorld の Inspector で変更できる（初期値は水の日と星の日）。
    /// 土の日の午前は試験（風の日は試験前）。季節の最後の日はお祭り。
    /// </summary>
    public static class Calendar
    {
        public static readonly string[] WeekdayNames = { "光の日", "火の日", "水の日", "風の日", "土の日", "月の日", "星の日" };
        public static readonly string[] SeasonNames = { "春", "夏", "秋", "冬" };
        public static readonly string[] FestivalNames = { "春の花祭り", "夏の星祭り", "収穫祭", "冬至祭" };
        public const int DaysPerSeason = 14;
        public const int ExamWeekday = 4;   // 土の日（0始まり）
        public const int YearDays = DaysPerSeason * 4;

        public static int Weekday(int day) { return (day - 1) % 7; }
        public static int Week(int day) { return (day - 1) / 7 + 1; }
        public static int Season(int day) { return ((day - 1) / DaysPerSeason) % 4; }
        public static bool IsFestival(int day) { return day % DaysPerSeason == 0; }

        public static bool IsHoliday(int day, bool[] holidays)
        {
            int w = Weekday(day);
            return holidays != null && w < holidays.Length && holidays[w];
        }

        public static bool IsExamDay(int day, bool[] holidays) { return Weekday(day) == ExamWeekday && !IsHoliday(day, holidays); }
        public static bool IsExamEve(int day, bool[] holidays) { return IsExamDay(day + 1, holidays); }

        public static Weather WeatherOf(int day)
        {
            var rnd = new System.Random(day * 7307 + 11);
            double r = rnd.NextDouble();
            switch (Season(day))
            {
                case 0: return r < 0.6 ? Weather.Sunny : (r < 0.85 ? Weather.Cloudy : Weather.Rain);
                case 1: return r < 0.6 ? Weather.Sunny : (r < 0.75 ? Weather.Cloudy : Weather.Rain);
                case 2: return r < 0.5 ? Weather.Sunny : (r < 0.8 ? Weather.Cloudy : Weather.Rain);
                default: return r < 0.4 ? Weather.Sunny : (r < 0.7 ? Weather.Cloudy : Weather.Snow);
            }
        }

        public static string WeatherName(Weather w)
        {
            switch (w)
            {
                case Weather.Sunny: return "晴れ";
                case Weather.Cloudy: return "くもり";
                case Weather.Rain: return "雨";
                default: return "雪";
            }
        }

        public static bool BadWeather(int day) { var w = WeatherOf(day); return w == Weather.Rain || w == Weather.Snow; }

        /// <summary>誕生日（1年＝56日の何日目か）。試作用の仮の日付。</summary>
        static readonly int[] Birthdays = { 10, 24, 5, 38, 17, 31, 45, 52, 3, 28 };

        public static bool IsBirthday(int charId, int day)
        {
            if (charId < 0 || charId >= Birthdays.Length) return false;
            return ((day - 1) % YearDays) + 1 == Birthdays[charId];
        }

        public static string DateText(int day, bool[] holidays)
        {
            string s = Week(day) + "週目 " + WeekdayNames[Weekday(day)];
            if (IsHoliday(day, holidays)) s += "（休日）";
            return s + "・" + SeasonNames[Season(day)] + "・" + WeatherName(WeatherOf(day));
        }
    }
}
