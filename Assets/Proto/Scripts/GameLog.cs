using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// できごとの記録（画面右下の「できごと」の欄）の1行と、見出し（種類分け）の決まり。
    /// 1行ごとに「篤が出てくるか」「どのヒロインが出てくるか」の札を付けておき、見出しで絞って表示する。
    /// 見出しを足すときは、下の Tabs に1行足す（名前と、どの行を出すかの条件）。
    /// famuru 決定 2026-10-24：見出しは すべて／篤／ヒロイン／寮・行事。ヒロインは全員まとめて＋1人ずつも見られる。
    /// 篤が出てくる行は「篤」にも「ヒロイン」にも出す。さかのぼって読める。種類ごとに文字の色を変える。
    /// </summary>
    public class LogEntry
    {
        public string text;        // 画面に出す文（「[3日目 10:20] 〜」）
        public bool aboutF;        // 篤が出てくる
        public bool dayHeader;     // 日付の区切りの行（「―― 3日目 … ――」）
        public readonly List<string> heroines = new List<string>(); // 出てくるヒロイン（記号）

        public bool AboutHeroine { get { return heroines.Count > 0; } }
        /// <summary>篤もヒロインも出てこない行（寮の行事・天気・授業など）。</summary>
        public bool AboutDorm { get { return !aboutF && !AboutHeroine; } }
        public bool About(string letter) { return heroines.Contains(letter); }
        public override string ToString() { return text; }

        /// <summary>文に出てくる名前から札を付ける。「〇〇のお守り」の名前は、その子のできごとに数えない。</summary>
        public static LogEntry Make(string stamp, string message, LoungeWorld w)
        {
            var e = new LogEntry { text = stamp + message, dayHeader = message.StartsWith("――") };
            if (w == null) return e;
            var f = w.RomanceTarget;
            foreach (var c in w.residents)
            {
                if (c == null || string.IsNullOrEmpty(c.displayName)) continue;
                string m = message.Replace(c.displayName + "のお守り", "");
                if (m.IndexOf(c.displayName, System.StringComparison.Ordinal) < 0) continue;
                if (c == f) e.aboutF = true;
                else if (!string.IsNullOrEmpty(c.letter) && !e.heroines.Contains(c.letter)) e.heroines.Add(c.letter);
            }
            return e;
        }
    }

    public static class GameLog
    {
        /// <summary>残しておく行の数（古い行から消える）。</summary>
        public const int MaxLines = 80;

        public class Tab
        {
            public string name;
            public System.Func<LogEntry, bool> show;
            public Tab(string name, System.Func<LogEntry, bool> show) { this.name = name; this.show = show; }
        }

        /// <summary>見出しの一覧（左から順に並ぶ）。「ヒロイン」の見出しでは、さらに1人ずつ絞れる。</summary>
        public static readonly Tab[] Tabs =
        {
            new Tab("すべて", e => true),
            new Tab("篤", e => e.aboutF),
            new Tab("ヒロイン", e => e.AboutHeroine),
            new Tab("寮・行事", e => e.AboutDorm),
        };
        public const int HeroineTab = 2;

        // 文字の色（暗い欄の上で読みやすい色）
        public static readonly Color ColorF = new Color(0.62f, 0.84f, 1f);         // 篤：水色
        public static readonly Color ColorHeroine = new Color(1f, 0.72f, 0.84f);   // ヒロイン：ピンク
        public static readonly Color ColorDorm = new Color(0.95f, 0.92f, 0.85f);   // 寮・行事：いつもの色
        public static readonly Color ColorDay = new Color(1f, 0.85f, 0.55f);       // 日付の区切り：金色

        /// <summary>行の色。篤が出てくる行は水色、ヒロインだけの行はピンク。</summary>
        public static Color ColorOf(LogEntry e)
        {
            if (e.dayHeader) return ColorDay;
            if (e.aboutF) return ColorF;
            if (e.AboutHeroine) return ColorHeroine;
            return ColorDorm;
        }
    }
}
