using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 学園の仕組みの時計（2026-11-01）：部活の割り当て、季節ごとの部の大会、ほかの寮の日々の寮杯の点。
    /// </summary>
    public class AcademyDirector : MonoBehaviour
    {
        LoungeWorld world;
        bool assigned;
        int rivalDay = -1, tournamentDay = -1;

        void Start() { world = GetComponent<LoungeWorld>(); }

        void Update()
        {
            if (world == null || world.characters.Count == 0) return;
            if (!assigned)
            {
                assigned = true;
                Clubs.Assign(world);
                if (HouseCup.Announced && HouseCup.Order()[0] == 0) world.ShowCupTrophy(true);
            }
            float m = world.MinuteOfDay;
            int day = world.Day;
            // ほかの寮の日々の活躍（毎日18時）
            if (m >= 18f * 60f && rivalDay != day) { rivalDay = day; HouseCup.DailyRivals(world); }
            // 部の大会（各季節の水の日の17時）
            if (Calendar.Weekday(day) == 2 && m >= 17f * 60f && tournamentDay != day) { tournamentDay = day; Clubs.Tournament(world); }
        }

        public static string NextTournamentText(LoungeWorld w)
        {
            int day = w.Day;
            for (int d = day; d < day + 8; d++)
            {
                if (Calendar.Weekday(d) != 2) continue;
                if (d == day && w.MinuteOfDay >= 17f * 60f) continue;
                return Calendar.SeasonNames[Calendar.Season(d)] + "の水の日（" + d + "日目）17:00";
            }
            return "";
        }
    }
}
