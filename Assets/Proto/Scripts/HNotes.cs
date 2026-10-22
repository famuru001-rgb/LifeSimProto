using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// エッチの記録に添える「どんなふうに過ごしたか」の1文（famuru 決定 2026-10-22：ひとことだけ・直接の描写はしない）。
    /// 文は Resources/Text/HNotes.txt（Grok に書いてもらい、Claude が確かめた物）。1行1件：
    ///   記号|場所|時間|状態|関係|回数|人数|誘い|文
    ///   記号：A〜K か *（だれでも）／場所：自室・篤の部屋・部屋（だれの部屋でも）・温室・図書室・天文台・中庭／時間：夜・昼／状態：発情・悶々・ふつう
    ///   関係（篤への気持ち）：恋人・好き・気になる・まだ／回数（その周の、その子と篤の）：初めて・数回・慣れた・今日また
    ///   人数：2人・3〜4人・大勢／誘い：自分から・篤から・加わった／* はどれでもよい
    ///   文の {名} はその子の名前、{ほか} はいっしょにいたほかの子の名前（3人以上のときだけ）
    /// その時の様子に合う行の中から、合う所が多い行ほど選ばれやすい。同じ子に同じ文が続かないようにする。
    /// 「#」で始まる行と空の行は読まない。文を足すときは、このファイルに行を足すだけでよい。
    /// </summary>
    public static class HNotes
    {
        class Line
        {
            public string letter, place, time, state, rel, count, size, invite, text;
        }

        static List<Line> lines;
        static readonly Dictionary<string, List<string>> recent = new Dictionary<string, List<string>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() { lines = null; recent.Clear(); }

        static void Load()
        {
            lines = new List<Line>();
            var ta = Resources.Load<TextAsset>("Text/HNotes");
            if (ta == null) return;
            foreach (var raw in ta.text.Split('\n'))
            {
                string l = raw.Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                var p = l.Split('|');
                if (p.Length < 9) continue;
                string text = string.Join("|", p, 8, p.Length - 8).Trim();
                if (text.Length == 0) continue;
                lines.Add(new Line { letter = T(p[0]), place = T(p[1]), time = T(p[2]), state = T(p[3]), rel = T(p[4]), count = T(p[5]), size = T(p[6]), invite = T(p[7]), text = text });
            }
        }

        static string T(string s) { s = s.Trim(); return s.Length == 0 ? "*" : s; }

        public static int Count { get { if (lines == null) Load(); return lines.Count; } }

        /// <summary>記録の1回分：1人ずつの文（who と同じ順。篤は空）。</summary>
        public static string[] Make(LoungeWorld w, List<SimCharacter> who, string place, SimCharacter inviter, bool night)
        {
            if (lines == null) Load();
            var res = new string[who.Count];
            var f = w.RomanceTarget;
            for (int i = 0; i < who.Count; i++)
            {
                var c = who[i];
                res[i] = "";
                if (c == null || c.IsTarget || string.IsNullOrEmpty(c.letter)) continue;
                var others = new List<string>();
                foreach (var o in who) if (o != null && o != c && !o.IsTarget) others.Add(o.displayName);
                res[i] = Pick(c, f, PlaceTag(w, c, place), night ? "夜" : "昼", StateTag(c), RelTag(w, c), CountTag(w, c),
                    who.Count <= 2 ? "2人" : (who.Count <= 4 ? "3〜4人" : "大勢"),
                    inviter == c ? "自分から" : (inviter != null && inviter.IsTarget ? "篤から" : "加わった"), others);
            }
            return res;
        }

        static string PlaceTag(LoungeWorld w, SimCharacter c, string place)
        {
            if (string.IsNullOrEmpty(place)) return "?";
            if (c.room != null && place == c.room.AreaName) return "自室";
            var f = w.RomanceTarget;
            if (f != null && f.room != null && place == f.room.AreaName) return "篤の部屋";
            if (place == LoungeWorld.Observatory) return "天文台";
            if (place == LoungeWorld.Greenhouse || place == LoungeWorld.Library || place == LoungeWorld.Courtyard) return place;
            if (place.EndsWith("の部屋")) return "ほかの部屋"; // ほかの子の部屋：場所が「部屋」か * の文を使う
            return "?";
        }

        static string StateTag(SimCharacter c)
        {
            int st = c.GetStage(StatusLine.Desire); // 1 そわそわ・2 悶々・3 発情
            return st >= 3 ? "発情" : (st == 2 ? "悶々" : "ふつう");
        }

        static string RelTag(LoungeWorld w, SimCharacter c)
        {
            int st = w.romance.Stage(c.id);
            return st == 3 ? "恋人" : (st == 2 ? "好き" : (st == 1 ? "気になる" : "まだ"));
        }

        /// <summary>その周の、その子と篤の回数（今回の分を足す前）。</summary>
        static string CountTag(LoungeWorld w, SimCharacter c)
        {
            int before = 0, today = 0;
            foreach (var e in HLog.Entries)
            {
                if (e.loop != Loop.Count || System.Array.IndexOf(e.letters, c.letter) < 0 || System.Array.IndexOf(e.letters, "F") < 0) continue;
                before++;
                if (e.day == w.Day) today++;
            }
            if (before == 0) return "初めて";
            if (today > 0) return "今日また";
            return before < 5 ? "数回" : "慣れた";
        }

        class Cand { public Line line; public int sc; public float wgt; }

        static string Pick(SimCharacter c, SimCharacter f, string place, string time, string state, string rel, string count, string size, string invite, List<string> others)
        {
            var cands = new List<Cand>();
            int best = 0;
            List<string> rc;
            recent.TryGetValue(c.letter, out rc);
            foreach (var l in lines)
            {
                int sc = 0;
                if (!Fit(l.letter, c.letter, ref sc, 3) || !Fit(l.place, place, ref sc) || !Fit(l.time, time, ref sc) || !Fit(l.state, state, ref sc)
                    || !Fit(l.rel, rel, ref sc) || !Fit(l.count, count, ref sc) || !Fit(l.size, size, ref sc) || !Fit(l.invite, invite, ref sc)) continue;
                if (l.text.Contains("{ほか}") && others.Count == 0) continue;
                float wgt = (sc + 1f) * (sc + 1f);
                if (rc != null && rc.Contains(l.text)) wgt *= 0.05f; // 同じ文が続かないように
                cands.Add(new Cand { line = l, sc = sc, wgt = wgt });
                best = Mathf.Max(best, sc);
            }
            if (cands.Count == 0) return "";
            // 合う所が多い行を中心に（いちばん合う行より 3 つ以上少ない行は使わない）
            cands.RemoveAll(x => x.sc < best - 2);
            float total = 0f;
            foreach (var x in cands) total += x.wgt;
            float r = Random.value * total;
            Line pick = cands[cands.Count - 1].line;
            foreach (var x in cands) { r -= x.wgt; if (r <= 0f) { pick = x.line; break; } }
            if (rc == null) { rc = new List<string>(); recent[c.letter] = rc; }
            rc.Add(pick.text);
            if (rc.Count > 8) rc.RemoveAt(0);
            return pick.text.Replace("{名}", c.displayName).Replace("{ほか}", string.Join("と", others.ToArray())).Replace("{篤}", f != null ? f.displayName : "篤");
        }

        /// <summary>行の値が * か、今の様子と同じなら合う（同じなら sc を足す）。</summary>
        static bool Fit(string tag, string now, ref int sc, int bonus = 1)
        {
            if (tag == "*") return true;
            if (tag == now) { sc += bonus; return true; }
            // 「部屋」はだれの部屋でもよい（自室・篤の部屋・ほかの子の部屋）
            if (tag == "部屋" && (now == "自室" || now == "篤の部屋" || now == "ほかの部屋")) { sc += bonus; return true; }
            // 「天文台（屋上）」なども同じに扱う
            if (now != null && tag.Length > 0 && (tag.StartsWith(now) || now.StartsWith(tag))) { sc += bonus; return true; }
            return false;
        }
    }
}
