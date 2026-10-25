using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// エッチの場面の吹き出し（2026-10-23 サブ連携のチャット。構成はプロジェクトの claude/二人きりの時間_構成.md）。
    /// せりふは famuru が Strata で作り、Claude が Resources/Text/HLines/&lt;記号&gt;.txt に取り込む（Claude はせりふを書かない）。
    /// 1行1本：場面|型|部品|人数|条件|せりふ
    ///   場面：invite・move・start・mid1・mid2・mid3・finish1〜3・resume・after・join_find・join_ask・join_ok・join_no・join_in・join_atsushi・wait・turn・pet・rest_in・rest・rest_back・rest_see
    ///         頭・しめ・条件つきの本文は、場面の群（pre・mid・fin・post）を書く
    ///   型：talk・act・feel・reply・self・others　部品：本文・頭・しめ　人数：2・3・4（4人以上）・*（どれでも）
    ///   条件：-（なし）・rel0・rel1・lover・first・soul・used・again・other・lust・love・jealous・lonely・sleepy・up・down・shy・mana・sick
    ///   せりふの {ほか} はいっしょにいるほかの子の名前、{名} は話す子の名前（モブ用）
    /// 1本のせりふ＝［頭］＋本文＋［しめ］。頭・しめは同じ型の本文にだけ付く。「#」で始まる行と空の行は読まない。
    /// </summary>
    public static class HLines
    {
        public class Line { public string scene, type, part, size, cond, text; }

        static readonly Dictionary<string, List<Line>> cache = new Dictionary<string, List<Line>>();
        static readonly Dictionary<string, List<string>> recent = new Dictionary<string, List<string>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() { cache.Clear(); recent.Clear(); }

        /// <summary>その子（記号）のせりふ。ファイルがなければ空。</summary>
        public static List<Line> Of(string key)
        {
            if (string.IsNullOrEmpty(key)) return new List<Line>();
            List<Line> list;
            if (cache.TryGetValue(key, out list)) return list;
            list = new List<Line>();
            var ta = Resources.Load<TextAsset>("Text/HLines/" + key);
            if (ta != null)
            {
                foreach (var raw in ta.text.Split('\n'))
                {
                    string s = raw.Trim();
                    if (s.Length == 0 || s.StartsWith("#")) continue;
                    var f = s.Split('|');
                    if (f.Length < 6) continue;
                    string text = string.Join("|", f, 5, f.Length - 5).Trim();
                    if (text.Length == 0) continue;
                    list.Add(new Line { scene = f[0].Trim(), type = f[1].Trim(), part = f[2].Trim(), size = f[3].Trim(), cond = f[4].Trim(), text = text });
                }
            }
            cache[key] = list;
            return list;
        }

        public static bool Has(string key) { return Of(key).Count > 0; }

        // ---------- 新しい演出（2026-10-25）：最中は「声」が中心、区切りの場面だけせりふ ----------
        static readonly HashSet<string> MidScenes = new HashSet<string> { "mid1", "mid2", "mid3", "resume", "pet" };

        /// <summary>最中の場面か（声の演出を使う場面）。</summary>
        public static bool IsMid(string scene) { return MidScenes.Contains(scene); }

        /// <summary>最中の本文（共通または条件つき）を持っているか。持っていなければ、その子は最中を声で出す。</summary>
        public static bool HasMidBody(string key)
        {
            foreach (var l in Of(key))
                if (l.part == "本文" && l.cond != "sick" && (MidScenes.Contains(l.scene) || l.scene == "mid")) return true;
            return false;
        }

        /// <summary>声（act の頭）を1つか2つ。なければ null。</summary>
        public static string Voice(string key, string scene)
        {
            string grp = Group(scene);
            var c = new List<Line>(); var w = new List<float>();
            foreach (var l in Of(key)) if (l.part == "頭" && l.type == "act" && l.scene == grp) { c.Add(l); w.Add(1f); }
            if (c.Count == 0) return null;
            var a = PickWeighted(key, c, w); Remember(key, a.text);
            if (Random.value < 0.3f && c.Count > 1)
            {
                var b = PickWeighted(key, c, w); Remember(key, b.text);
                if (b.text != a.text) return a.text + "　" + b.text;
            }
            return a.text;
        }

        /// <summary>場面の群。頭・しめと条件つきの本文はこの群で選ぶ。</summary>
        public static string Group(string scene)
        {
            switch (scene)
            {
                case "invite": case "move": case "start": case "join_find": case "join_ask": return "pre";
                case "finish1": case "finish2": case "finish3": return "fin";
                case "after": return "post";
                default: return "mid";
            }
        }

        // 条件つきの本文を混ぜる場面（途中参加・順番・休憩の場面は、その場面の本文だけ）
        static readonly HashSet<string> CondScenes = new HashSet<string> { "invite", "move", "start", "mid1", "mid2", "mid3", "finish1", "finish2", "finish3", "resume", "after", "pet" };

        static string SizeKey(int size) { return size >= 4 ? "4" : (size == 3 ? "3" : "2"); }

        static bool SizeOk(string s, string want, bool part)
        {
            if (s == "*" || s == want) return true;
            if (part && s == "3" && want == "4") return true; // 3人以上の部品は4人以上でも使う
            return false;
        }

        /// <summary>
        /// 1本のせりふを組み立てる。見つからなければ null。
        /// conds：今当てはまる条件。other：{ほか} に入る名前（いなければ null）。call：その子の篤の呼び方（呼びかけで始まる本文には頭を付けない）。
        /// maxDots：「…」の上限（0 未満なら上限なし）。sparse：句読点を少なめにする子（頭と本文の間に「、」を入れない）。
        /// </summary>
        public static string Build(string key, string scene, int size, ICollection<string> conds, string other, string call, int maxDots, bool sparse, string selfName = null)
        {
            var all = Of(key);
            if (all.Count == 0) return null;
            string sz = SizeKey(size);
            string grp = Group(scene);
            bool sick = conds != null && conds.Contains("sick") && size <= 2;
            var bodies = new List<Line>();
            var weights = new List<float>();
            Collect(all, scene, grp, sz, conds, sick, other != null, bodies, weights);
            if (bodies.Count == 0 && sz != "2") Collect(all, scene, grp, "2", conds, sick, other != null, bodies, weights); // 人数の分がまだなければ2人の分
            if (bodies.Count == 0 && sick) Collect(all, scene, grp, "2", conds, false, false, bodies, weights);           // 風邪の分がまだなければふだんの分
            if (bodies.Count == 0) return null;

            var body = PickWeighted(key, bodies, weights);
            string text = body.text;

            // 頭・しめ（同じ型・同じ群だけ。4割くらい）
            string head = null, tail = null;
            if (Random.value < 0.4f && !StartsWithCall(text, call)) head = PickPart(key, all, "頭", body.type, grp, sz);
            if (Random.value < 0.4f && !EndsWithQuestion(text)) tail = PickPart(key, all, "しめ", body.type, grp, sz);

            string result = Join(head, text, tail, sparse);
            if (!Fits(result, text, maxDots)) { result = Join(null, text, tail, sparse); if (!Fits(result, text, maxDots)) { result = Join(head, text, null, sparse); if (!Fits(result, text, maxDots)) result = text; } }

            Remember(key, body.text);
            if (head != null && result.Contains(head)) Remember(key, head);
            if (tail != null && result.Contains(tail)) Remember(key, tail);
            if (other != null) result = result.Replace("{ほか}", other);
            if (selfName != null) result = result.Replace("{名}", selfName);
            return result;
        }

        static void Collect(List<Line> all, string scene, string grp, string sz, ICollection<string> conds, bool sick, bool hasOther, List<Line> bodies, List<float> weights)
        {
            bool condScene = CondScenes.Contains(scene);
            foreach (var l in all)
            {
                if (l.part != "本文") continue;
                if (!hasOther && (l.type == "others" || l.text.Contains("{ほか}"))) continue;
                if (sick)
                {
                    if (l.cond == "sick" && l.scene == scene) { bodies.Add(l); weights.Add(1f); }
                    continue;
                }
                if (l.cond == "sick") continue;
                if (l.cond == "-" || l.cond == "")
                {
                    if (l.scene == scene && SizeOk(l.size, sz, false)) { bodies.Add(l); weights.Add(1f); }
                }
                else if (condScene && conds != null && conds.Contains(l.cond) && (l.scene == grp || l.scene == scene) && SizeOk(l.size, sz, false))
                {
                    bodies.Add(l); weights.Add(1.6f); // 条件のせりふは少し出やすく
                }
            }
        }

        static string PickPart(string key, List<Line> all, string part, string type, string grp, string sz)
        {
            var c = new List<Line>();
            var w = new List<float>();
            foreach (var l in all)
                if (l.part == part && l.type == type && (l.scene == grp || l.scene == "*") && SizeOk(l.size, sz, true)) { c.Add(l); w.Add(1f); }
            if (c.Count == 0) return null;
            return PickWeighted(key, c, w).text;
        }

        static Line PickWeighted(string key, List<Line> c, List<float> w)
        {
            List<string> rec;
            recent.TryGetValue(key, out rec);
            float total = 0f;
            var ws = new float[c.Count];
            for (int i = 0; i < c.Count; i++)
            {
                float x = w[i];
                if (rec != null && rec.Contains(c[i].text)) x *= 0.05f; // 少し前に使ったものは出にくく
                ws[i] = x; total += x;
            }
            float r = Random.value * total;
            for (int i = 0; i < c.Count; i++) { r -= ws[i]; if (r <= 0f) return c[i]; }
            return c[c.Count - 1];
        }

        static void Remember(string key, string text)
        {
            List<string> rec;
            if (!recent.TryGetValue(key, out rec)) { rec = new List<string>(); recent[key] = rec; }
            rec.Add(text);
            if (rec.Count > 16) rec.RemoveAt(0);
        }

        static bool StartsWithCall(string t, string call)
        {
            if (t.StartsWith("ねえ") || t.StartsWith("{ほか}") || t.StartsWith("{名}")) return true;
            return !string.IsNullOrEmpty(call) && t.StartsWith(call);
        }

        static bool EndsWithQuestion(string t) { return t.EndsWith("？") || t.EndsWith("?"); }

        static bool IsPunct(char ch) { return "…、。！？!?ー〜♡♪」）".IndexOf(ch) >= 0; }

        static string Join(string head, string body, string tail, bool sparse)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(head))
            {
                sb.Append(head);
                if (!IsPunct(head[head.Length - 1]) && !sparse) sb.Append('、');
            }
            sb.Append(body);
            if (!string.IsNullOrEmpty(tail))
            {
                if (!IsPunct(body[body.Length - 1])) sb.Append('。');
                sb.Append(tail);
            }
            return sb.ToString();
        }

        static int DotRuns(string s)
        {
            int n = 0; bool inRun = false;
            foreach (var ch in s) { bool d = ch == '…'; if (d && !inRun) n++; inRun = d; }
            return n;
        }

        static bool Fits(string result, string body, int maxDots)
        {
            if (result.Length > 30 && result != body) return false;
            if (maxDots >= 0 && DotRuns(result) > Mathf.Max(maxDots, DotRuns(body))) return false;
            return true;
        }
    }
}
