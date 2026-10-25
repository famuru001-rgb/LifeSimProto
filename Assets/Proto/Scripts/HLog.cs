using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// エッチの記録（いつ・どこで・だれと・だれが誘ったか）。描写はしない。
    /// 一覧は周ごとに見られる（全部の周の分を残す）。キャラごとの通算の回数はループしても残る（famuru 決定 2026-10-22）。
    /// </summary>
    public static class HLog
    {
        [System.Serializable]
        public class Entry
        {
            public int loop, day;          // 何周目・何日目
            public float minute;           // その日の時刻（分）
            public string place;           // 場所（「左1の部屋」「図書室」など）
            public string[] letters;       // いっしょにいた人（記号）
            public string inviter;         // 誘った人（記号）
            public bool night;             // 夜（21時〜朝）だった
            public string[] notes;         // 1人ずつの「どんなふうに過ごしたか」の1文（letters と同じ順。篤は空）。HNotes.cs
            public int[] direct;           // 1人ずつ：フィニッシュのとき篤が直接相手をしていた回数（letters と同じ順）
            public float[] ml;             // 1人ずつ：量（ml）の合計（letters と同じ順）
            public int finishes;           // フィニッシュの回数
            public float mlTotal;          // 量（ml）の合計
        }

        public static readonly List<Entry> Entries = new List<Entry>();
        /// <summary>キャラごとの通算の回数（記号 → 回数）。</summary>
        public static readonly Dictionary<string, int> Total = new Dictionary<string, int>();
        const int MaxEntries = 3000;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() { Entries.Clear(); Total.Clear(); }

        public static Entry Record(LoungeWorld w, List<SimCharacter> who, string place, SimCharacter inviter)
        {
            if (w == null || who == null || who.Count == 0) return null;
            var e = new Entry
            {
                loop = Loop.Count,
                day = w.Day,
                minute = w.MinuteOfDay,
                place = place,
                inviter = inviter != null ? inviter.letter : null,
            };
            float m = w.MinuteOfDay;
            e.night = m >= 21f * 60f || LoungeWorld.IsNight(m);
            e.notes = HNotes.Make(w, who, place, inviter, e.night); // 記録に加える前に作る（回数を数えるため）
            var ls = new List<string>();
            var ns = new List<string>();
            for (int i = 0; i < who.Count; i++)
            {
                var c = who[i];
                if (c == null || string.IsNullOrEmpty(c.letter)) continue;
                ls.Add(c.letter);
                ns.Add(e.notes != null && i < e.notes.Length ? e.notes[i] : "");
                int n;
                Total[c.letter] = (Total.TryGetValue(c.letter, out n) ? n : 0) + 1;
            }
            e.letters = ls.ToArray();
            e.notes = ns.ToArray();
            e.direct = new int[e.letters.Length];
            e.ml = new float[e.letters.Length];
            Entries.Add(e);
            if (Entries.Count > MaxEntries) Entries.RemoveAt(0);
            return e;
        }

        /// <summary>途中で加わった子を記録に足す（2026-10-23 サブ連携のチャット）。</summary>
        public static void AddMember(Entry e, SimCharacter c)
        {
            if (e == null || c == null || string.IsNullOrEmpty(c.letter) || System.Array.IndexOf(e.letters, c.letter) >= 0) return;
            int n = e.letters.Length;
            System.Array.Resize(ref e.letters, n + 1); e.letters[n] = c.letter;
            if (e.notes == null) e.notes = new string[n];
            System.Array.Resize(ref e.notes, n + 1); e.notes[n] = "";
            if (e.direct == null) e.direct = new int[n];
            System.Array.Resize(ref e.direct, n + 1);
            if (e.ml == null) e.ml = new float[n];
            System.Array.Resize(ref e.ml, n + 1);
            int t;
            Total[c.letter] = (Total.TryGetValue(c.letter, out t) ? t : 0) + 1;
        }

        /// <summary>フィニッシュを記録する（直接相手をしていた子に、回数と量）。</summary>
        public static void AddFinish(Entry e, string letter, float ml)
        {
            if (e == null) return;
            e.finishes++;
            e.mlTotal += ml;
            int i = letter != null ? System.Array.IndexOf(e.letters, letter) : -1;
            if (i < 0) return;
            if (e.direct == null || e.direct.Length != e.letters.Length) System.Array.Resize(ref e.direct, e.letters.Length);
            if (e.ml == null || e.ml.Length != e.letters.Length) System.Array.Resize(ref e.ml, e.letters.Length);
            e.direct[i]++;
            e.ml[i] += ml;
        }

        /// <summary>その子の、直接の回数と量（loop が 0 なら全部の周）。</summary>
        public static void DirectOf(string letter, int loop, out int count, out float ml)
        {
            count = 0; ml = 0f;
            foreach (var e in Entries)
            {
                if (loop > 0 && e.loop != loop) continue;
                int i = System.Array.IndexOf(e.letters, letter);
                if (i < 0) continue;
                if (e.direct != null && i < e.direct.Length) count += e.direct[i];
                if (e.ml != null && i < e.ml.Length) ml += e.ml[i];
            }
        }

        public static int TotalOf(string letter) { int n; return letter != null && Total.TryGetValue(letter, out n) ? n : 0; }

        /// <summary>この周の、その子の回数。</summary>
        public static int LoopCountOf(string letter, int loop)
        {
            int n = 0;
            foreach (var e in Entries) if (e.loop == loop && System.Array.IndexOf(e.letters, letter) >= 0) n++;
            return n;
        }

        /// <summary>その子のいちばん新しい記録（なければ null）。</summary>
        public static Entry LastOf(string letter)
        {
            for (int i = Entries.Count - 1; i >= 0; i--) if (System.Array.IndexOf(Entries[i].letters, letter) >= 0) return Entries[i];
            return null;
        }

        public static List<Entry> OfLoop(int loop) { return Entries.FindAll(e => e.loop == loop); }

        /// <summary>記録の中の、いちばん大きい周の数。</summary>
        public static int MaxLoop()
        {
            int m = Loop.Count;
            foreach (var e in Entries) if (e.loop > m) m = e.loop;
            return m;
        }

        public static string NameOf(string letter)
        {
            if (string.IsNullOrEmpty(letter)) return "";
            var p = CharProfiles.All.Find(x => x.letter == letter);
            return p != null ? p.name : letter;
        }

        public static string Names(Entry e)
        {
            var n = new List<string>();
            foreach (var l in e.letters) n.Add(NameOf(l));
            return string.Join("・", n.ToArray());
        }

        public static string WhenText(Entry e)
        {
            return e.loop + "周目 " + e.day + "日目（" + Calendar.SeasonNames[Calendar.Season(e.day)] + "・" + Calendar.WeekdayNames[Calendar.Weekday(e.day)] + "）" + ScheduleMaker.Clock(e.minute);
        }
    }
}
