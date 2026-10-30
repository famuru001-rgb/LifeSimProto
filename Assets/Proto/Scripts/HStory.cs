using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// エッチの場面の流れを文で残す（記録の行を押すと開く）。2026-10-25 famuru「記録にエッチの履歴を追加、エッチの内容を文章で表現できるようにする」。
    /// できごとごとに1文を時刻つきで残す。文は Resources/Text/HStory.txt から選ぶ（なければ下の決まった文）。
    /// HStory.txt の書き方（1行1件）：
    ///   できごと|記号|文
    ///   できごと：start（始まり）・mid1（前半）・mid2（中盤）・mid3（後半）・finish（フィニッシュ）・finish_out（外に出したフィニッシュ）・climax（ヒロインの絶頂）・turn（直接の相手の交代）・join（途中参加）・
    ///             rest（ひと休み）・back（休憩から戻る）・after（終わったあと）・end（終わり）
    ///   記号：その文の中心の子（A〜K）か *（だれでも）。合う記号の行があればそちらを選ぶ
    ///   文の {名} はその子、{篤} は篤、{場所}、{みんな}（いっしょにいる子の名前）、{回}（フィニッシュの回数）、{ml}（その回の量）、{合計}（量の合計）
    /// 「#」で始まる行と空の行は読まない。同じできごとに文が何行もあれば、その中から選ぶ（続けて同じ文にならないように）。
    /// </summary>
    public static class HStory
    {
        class Line { public string ev, letter, text; }

        static List<Line> lines;
        static readonly Dictionary<string, string> lastUsed = new Dictionary<string, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() { lines = null; lastUsed.Clear(); }

        static readonly Dictionary<string, string[]> Defaults = new Dictionary<string, string[]>
        {
            { "start", new[] { "{場所}で、{みんな}と{篤}の時間が始まった", "{場所}で、{みんな}が{篤}に寄り添った" } },
            { "mid1", new[] { "少しずつ気持ちが高まっていく", "照れながらも、距離が近づいていく" } },
            { "mid2", new[] { "{名}の息が少しずつ上がっていく", "{名}が{篤}に甘えるように身を寄せた" } },
            { "mid3", new[] { "いちばん高まったところ", "{名}がぎゅっと{篤}にしがみついた" } },
            { "finish", new[] { "{回}回目のフィニッシュ（{名}・{ml}ml）" } },
            { "finish_out", new[] { "{回}回目のフィニッシュ（外に・{名}・{ml}ml）" } },
            { "turn", new[] { "{名}の番になった", "{名}が{篤}のとなりへ" } },
            { "climax", new[] { "{名}が絶頂を迎えた" } },
            { "join", new[] { "{名}が加わった", "{名}がそっと入ってきた" } },
            { "rest", new[] { "{名}はひと休み", "{名}は少し横になって休んだ" } },
            { "back", new[] { "{名}が戻ってきた" } },
            { "after", new[] { "みんなで寄り添って、余韻にひたった", "{篤}のそばで、しばらくまどろんだ" } },
            { "end", new[] { "終わった（フィニッシュ{回}回・合計{合計}ml）" } },
        };

        static void Load()
        {
            lines = new List<Line>();
            var ta = Resources.Load<TextAsset>("Text/HStory");
            if (ta == null) return;
            foreach (var raw in ta.text.Split('\n'))
            {
                string l = raw.Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                var p = l.Split(new[] { '|' }, 3);
                if (p.Length < 3) continue;
                lines.Add(new Line { ev = p[0].Trim(), letter = p[1].Trim(), text = p[2].Trim() });
            }
        }

        /// <summary>できごとの文を作る（vars は {名} などの置きかえ）。</summary>
        public static string Make(string ev, string letter, Dictionary<string, string> vars)
        {
            if (lines == null) Load();
            var cands = new List<string>();
            foreach (var l in lines) if (l.ev == ev && letter != null && l.letter == letter) cands.Add(l.text);
            if (cands.Count == 0) foreach (var l in lines) if (l.ev == ev && l.letter == "*") cands.Add(l.text);
            string[] def;
            if (cands.Count == 0 && Defaults.TryGetValue(ev, out def)) cands.AddRange(def);
            if (cands.Count == 0) return null;
            string key = ev + "|" + letter;
            string last;
            lastUsed.TryGetValue(key, out last);
            if (cands.Count > 1 && last != null) cands.Remove(last);
            string t = cands[Random.Range(0, cands.Count)];
            lastUsed[key] = t;
            if (vars != null) foreach (var kv in vars) t = t.Replace("{" + kv.Key + "}", kv.Value ?? "");
            return t;
        }

        /// <summary>決まった文をそのまま記録に足す（時刻つき）。</summary>
        public static void AddText(LoungeWorld w, HLog.Entry e, string text)
        {
            if (e == null || w == null || string.IsNullOrEmpty(text)) return;
            if (e.story == null) e.story = new List<string>();
            if (e.story.Count >= 80) return;
            e.story.Add(ScheduleMaker.Clock(w.MinuteOfDay) + "　" + text);
        }

        /// <summary>記録に1文を足す（時刻つき）。</summary>
        public static void Add(LoungeWorld w, HLog.Entry e, string ev, string letter, Dictionary<string, string> vars)
        {
            if (e == null || w == null) return;
            string t = Make(ev, letter, vars);
            if (string.IsNullOrEmpty(t)) return;
            if (e.story == null) e.story = new List<string>();
            if (e.story.Count >= 80) return; // 長くなりすぎないように
            e.story.Add(ScheduleMaker.Clock(w.MinuteOfDay) + "　" + t);
        }
    }
}
