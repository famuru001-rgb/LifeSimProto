using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 寮杯：古時計寮（篤たちの寮）と冒険者寮・騎士寮・魔法師寮の4つの寮が、1年を通して点数を競う（2026-11-01 famuru）。
    /// ・点は良いことでだけ入る（famuru 決定：加点のみ）：試験の寮ごとの平均点・魔法競技会・行事の成功・部の大会・ダンジョン実習
    /// ・冬至の祭りで優勝を発表。古時計寮が優勝すると、寮の評判・銀貨・みんなのご機嫌・談話室に優勝杯
    /// ・ループで春に戻ると点は0から（優勝した回数は残る）
    /// </summary>
    public static class HouseCup
    {
        public const string OurName = "古時計寮";
        public const int Count = 4;
        /// <summary>0 古時計寮・1 冒険者寮・2 騎士寮・3 魔法師寮。</summary>
        public static string Name(int dorm) { return dorm <= 0 ? OurName : CharProfiles.MobDormNames[Mathf.Clamp(dorm - 1, 0, 2)]; }

        public static readonly int[] Points = new int[Count];
        /// <summary>新しい順の点の記録（「日付｜寮｜＋点｜理由」）。</summary>
        public static readonly List<string> Recent = new List<string>();
        /// <summary>古時計寮が寮杯を取った回数（ループをまたいで残る）。</summary>
        public static int Wins;
        /// <summary>今の周で優勝を発表したか。</summary>
        public static bool Announced;
        public static string LastResult = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() { Reset(); Wins = 0; }

        /// <summary>周のはじめ（ループで春に戻る）。</summary>
        public static void Reset()
        {
            for (int i = 0; i < Count; i++) Points[i] = 0;
            Recent.Clear();
            Announced = false;
            LastResult = "";
        }

        /// <summary>寮に点を入れる。dorm：0 古時計寮、1〜3 ほかの寮（寮生以外の学生の mobDorm＋1）。</summary>
        public static void Add(LoungeWorld w, int dorm, int pts, string reason, bool log = true)
        {
            if (pts <= 0 || dorm < 0 || dorm >= Count) return;
            Points[dorm] += pts;
            Recent.Insert(0, (w != null ? w.Day + "日目 " : "") + Name(dorm) + " ＋" + pts + "（" + reason + "）");
            if (Recent.Count > 60) Recent.RemoveAt(Recent.Count - 1);
            if (log && w != null && dorm == 0) w.Log("寮杯：" + OurName + " ＋" + pts + "点（" + reason + "）　合計 " + Points[0] + "点");
        }

        /// <summary>4つの寮の成績（大きいほど上）から順位を出して、順位ごとの点を入れる。戻り値は古時計寮の順位（1〜4）。</summary>
        public static int AddRanked(LoungeWorld w, float[] scores, int[] award, string what)
        {
            var order = new List<int> { 0, 1, 2, 3 };
            order.Sort((a, b) => scores[b].CompareTo(scores[a]));
            var parts = new List<string>();
            for (int i = 0; i < order.Count; i++)
            {
                int d = order[i];
                int pts = i < award.Length ? award[i] : 0;
                Add(w, d, pts, what + " " + (i + 1) + "位", false);
                parts.Add((i + 1) + "位 " + Name(d) + "（＋" + pts + "）");
            }
            int ourPlace = order.IndexOf(0) + 1;
            if (w != null) w.Log("寮杯（" + what + "）：" + string.Join("　", parts.ToArray()));
            return ourPlace;
        }

        /// <summary>今の順位（上から寮の番号）。</summary>
        public static List<int> Order()
        {
            var order = new List<int> { 0, 1, 2, 3 };
            order.Sort((a, b) => Points[b] != Points[a] ? Points[b].CompareTo(Points[a]) : a.CompareTo(b));
            return order;
        }

        public static int OurPlace() { return Order().IndexOf(0) + 1; }

        public static string Standings()
        {
            var o = Order();
            var parts = new List<string>();
            for (int i = 0; i < o.Count; i++) parts.Add((i + 1) + "位 " + Name(o[i]) + " " + Points[o[i]] + "点");
            return string.Join("　", parts.ToArray());
        }

        /// <summary>冬至の祭りで優勝を発表する（その周に1回）。</summary>
        public static void Announce(LoungeWorld w)
        {
            if (Announced || w == null) return;
            Announced = true;
            var o = Order();
            int win = o[0];
            LastResult = "寮杯の結果：" + Standings();
            w.Log("★ " + LastResult);
            if (win == 0)
            {
                Wins++;
                w.dormReputation += 15;
                if (w.player != null) w.player.Earn(300, "寮杯の優勝");
                foreach (var c in w.residents) { c.PositiveEvent(1f); c.PositiveEvent(0.6f); }
                w.Log("★ 寮杯は" + OurName + "が優勝！談話室に優勝杯が飾られた（寮の評判 " + w.dormReputation + "）");
                Album.Add(w, "寮杯で" + OurName + "が優勝した（" + Points[0] + "点）");
                w.ShowCupTrophy(true);
            }
            else
            {
                int place = o.IndexOf(0) + 1;
                foreach (var c in w.residents) if (place >= 3) c.NegativeEvent(0.3f);
                w.Log("寮杯は" + Name(win) + "が優勝。" + OurName + "は" + place + "位だった");
                Album.Add(w, "寮杯は" + Name(win) + "が優勝。" + OurName + "は" + place + "位");
            }
        }

        /// <summary>ほかの寮の日々の活躍（寮生以外の学生の能力で、毎日少しずつ）。古時計寮は行事や実習で点を取る。</summary>
        public static void DailyRivals(LoungeWorld w)
        {
            var sums = new float[3]; var n = new int[3];
            foreach (var m in w.mobs)
            {
                if (m.IsTeacher || m.profile.mobDorm < 0 || m.profile.mobDorm > 2) continue;
                float a = 0f;
                for (int k = 0; k < Abilities.Count; k++) a += m.Ab(k);
                sums[m.profile.mobDorm] += a / Abilities.Count; n[m.profile.mobDorm]++;
            }
            for (int d = 0; d < 3; d++)
            {
                float avg = n[d] > 0 ? sums[d] / n[d] : 50f;
                int pts = Mathf.Max(0, Mathf.RoundToInt(Random.Range(0f, 4f) + (avg - 45f) * 0.08f));
                if (pts > 0) Add(w, d + 1, pts, "学生たちの活躍", false);
            }
        }
    }
}
