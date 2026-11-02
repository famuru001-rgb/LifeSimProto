using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 生活のできごと（2026-11-02 famuru「夜の行動・生活の間のちょっとしたハプニング・朝のルーチン・体調不良・共同作業と、それに関わるエッチ要素で生活感を出したい。
    /// 性格・パラメーター・関係で変わる部分は後から変えられるように、建物の形も最終決定ではないので対応できるように」）。
    /// ・できごとは表（Resources\Text\LifeEvents\できごと.csv。Excel で開いて直せる）に1行1件。条件・結果・せりふは言葉で書く（書き方は プロジェクトの claude/生活のできごと_表の書き方.md）
    /// ・場所は「役割」（浴場・台所・談話室 など）で指定し、役割と建物の区画の名前の対応は 場所の役割.csv に書く（建物が変わったらここだけ直す）
    /// ・せりふは せりふ.csv（ふつうの場面は Claude が下書き・famuru が直す。エッチ要素のせりふは famuru が Grok で作る）
    /// ・動かすのは LifeEventDirector.cs、キャラの側は SimCharacter.Life.cs、篤の選択肢の画面は SimHud.Life.cs
    /// </summary>
    public static class LifeBook
    {
        public const string Folder = "Text/LifeEvents/";
        public static readonly List<LifeEventDef> Events = new List<LifeEventDef>();
        public static readonly List<LifeLine> Lines = new List<LifeLine>();
        public static readonly List<KeyValuePair<string, string[]>> Roles = new List<KeyValuePair<string, string[]>>();
        public static readonly List<string> Errors = new List<string>();
        /// <summary>キャラの型（キャラの型.csv：記号 → 型。条件「型:世話焼き」で使う。キャラデザで決める）。</summary>
        public static readonly Dictionary<string, string[]> Types = new Dictionary<string, string[]>();
        /// <summary>周期（周期.csv：名前・対象の条件・何日ごと・何日間）。条件「周期:発情期」で使う。子ごとに始まる日がずれる。</summary>
        public class Cycle { public string name; public LifeCond who; public int every, days; }
        public static readonly List<Cycle> Cycles = new List<Cycle>();
        public static bool InCycle(SimCharacter c, string name, LoungeWorld w)
        {
            foreach (var cy in Cycles)
            {
                if (cy.name != name) continue;
                if (!cy.who.Test(c, null, w)) return false;
                int off = 0; foreach (char ch in (c.letter ?? "") + "|" + name) off = (off * 31 + ch) & 0xFFFF;
                return ((w.Day + off) % Mathf.Max(1, cy.every)) < cy.days;
            }
            return false;
        }
        public static bool Loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() { Loaded = false; Events.Clear(); Lines.Clear(); Roles.Clear(); Errors.Clear(); Types.Clear(); }

        /// <summary>表を読む（Unity の中では、ファイルを直接読むので Unity に取り込み直さなくても「life reload」で反映される）。</summary>
        public static void Load(bool force = false)
        {
            if (Loaded && !force) return;
            Loaded = true;
            Events.Clear(); Lines.Clear(); Roles.Clear(); Errors.Clear(); Types.Clear(); Cycles.Clear();
            LoadRoles(ReadTable("場所の役割"));
            LoadTypes(ReadTable("キャラの型"));
            LoadCycles(ReadTable("周期"));
            LoadEvents(ReadTable("できごと"));
            LoadLines(ReadTable("せりふ"));
            if (Errors.Count > 0) Debug.LogWarning("生活のできごとの表：直したほうがよい所が " + Errors.Count + " か所（Logs\\life_check.txt / 命令 life check）");
        }

        // ================= 表を読む =================
        static List<string[]> ReadTable(string name)
        {
            byte[] bytes = null;
#if UNITY_EDITOR
            string path = Path.Combine(Application.dataPath, "Proto/Resources/" + Folder + name + ".csv");
            if (File.Exists(path)) { try { bytes = File.ReadAllBytes(path); } catch (System.Exception e) { Errors.Add(name + ".csv：読めない（" + e.Message + "。Excel で開いたままかも）"); } }
#endif
            if (bytes == null)
            {
                var ta = Resources.Load<TextAsset>(Folder + name);
                if (ta != null) bytes = ta.bytes;
            }
            if (bytes == null) { Errors.Add(name + ".csv がない"); return new List<string[]>(); }
            return ParseCsv(Decode(bytes, name));
        }

        /// <summary>UTF-8（Excel の「CSV UTF-8」）で読む。違えば Shift-JIS（Excel のふつうの「CSV」）として読む。</summary>
        static string Decode(byte[] b, string name)
        {
            int start = b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF ? 3 : 0;
            try { return new UTF8Encoding(false, true).GetString(b, start, b.Length - start); }
            catch { }
            try { return Encoding.GetEncoding(932).GetString(b); }
            catch { }
            Errors.Add(name + ".csv：文字の形が読めない。Excel で「CSV UTF-8（コンマ区切り）」で保存しなおして");
            return Encoding.UTF8.GetString(b, start, b.Length - start);
        }

        /// <summary>CSV を行と列に分ける（"…" で囲んだ列の中のコンマ・改行・"" にも対応）。</summary>
        public static List<string[]> ParseCsv(string text)
        {
            var rows = new List<string[]>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool q = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (q)
                {
                    if (c == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else q = false; }
                    else cell.Append(c);
                    continue;
                }
                if (c == '"') q = true;
                else if (c == ',') { row.Add(cell.ToString().Trim()); cell.Length = 0; }
                else if (c == '\r') { }
                else if (c == '\n') { row.Add(cell.ToString().Trim()); cell.Length = 0; rows.Add(row.ToArray()); row.Clear(); }
                else cell.Append(c);
            }
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString().Trim()); rows.Add(row.ToArray()); }
            return rows;
        }

        /// <summary>1行目（見出し）の名前で列を引く。</summary>
        class Header
        {
            readonly Dictionary<string, int> idx = new Dictionary<string, int>();
            public Header(string[] h) { for (int i = 0; i < h.Length; i++) if (!idx.ContainsKey(h[i])) idx[h[i]] = i; }
            public string Get(string[] r, string col) { int i; return idx.TryGetValue(col, out i) && i < r.Length ? r[i].Trim() : ""; }
            public bool Has(string col) { return idx.ContainsKey(col); }
        }

        static bool Blank(string[] r) { foreach (var s in r) if (!string.IsNullOrEmpty(s.Trim())) return false; return true; }

        static void LoadRoles(List<string[]> t)
        {
            if (t.Count == 0) return;
            var h = new Header(t[0]);
            for (int i = 1; i < t.Count; i++)
            {
                var r = t[i];
                if (Blank(r)) continue;
                string role = h.Get(r, "役割");
                string words = h.Get(r, "区画の名前（含む言葉）");
                if (role.Length == 0 || role.StartsWith("#")) continue;
                if (words.Length == 0) { Errors.Add("場所の役割.csv " + (i + 1) + "行目：「" + role + "」の区画の名前が空"); continue; }
                Roles.Add(new KeyValuePair<string, string[]>(role, SplitList(words)));
            }
        }

        static void LoadTypes(List<string[]> t)
        {
            if (t.Count == 0) return;
            var h = new Header(t[0]);
            for (int i = 1; i < t.Count; i++)
            {
                var r = t[i];
                if (Blank(r)) continue;
                string l = h.Get(r, "記号").ToUpperInvariant();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                Types[l] = SplitList(h.Get(r, "型"));
            }
        }

        static void LoadCycles(List<string[]> t)
        {
            if (t.Count == 0) return;
            var h = new Header(t[0]);
            for (int i = 1; i < t.Count; i++)
            {
                var r = t[i];
                if (Blank(r)) continue;
                string n = h.Get(r, "名前");
                if (n.Length == 0 || n.StartsWith("#")) continue;
                string where = "周期.csv " + (i + 1) + "行目";
                Cycles.Add(new Cycle { name = n, who = LifeCond.Parse(h.Get(r, "対象の条件"), where, false), every = Mathf.Max(1, Mathf.RoundToInt(ParseF(h.Get(r, "何日ごと"), 30f, where))), days = Mathf.Max(1, Mathf.RoundToInt(ParseF(h.Get(r, "何日間"), 3f, where))) });
            }
        }

        public static bool HasType(string letter, string type)
        {
            string[] ts;
            return letter != null && Types.TryGetValue(letter, out ts) && System.Array.IndexOf(ts, type) >= 0;
        }

        /// <summary>せりふを書く所の一覧（命令 life lines → Logs\せりふ_書く所.csv）。せりふ.csv と同じ列で、せりふの列を書いて貼りつければよい。</summary>
        public static string LineSlots()
        {
            var sb = new StringBuilder("グループ,場面,言う人,記号,条件,せりふ,メモ\r\n");
            var seen = new HashSet<string>();
            foreach (var d in Events)
            {
                if (!seen.Add(d.group)) continue;
                var rows = Events.FindAll(x => x.group == d.group);
                var scenes = new List<KeyValuePair<string, string>>();   // 場面・言う人
                bool hasPartner = rows.Exists(x => x.partnerKind != "なし");
                bool withF = rows.Exists(x => x.partnerKind == "篤" || x.partnerKind == "寮生か篤");
                bool withGirl = rows.Exists(x => x.partnerKind == "寮生" || x.partnerKind == "寮生か篤");
                foreach (var sc in new[] { "始まり", "最中", "終わり" })
                {
                    if (sc == "最中" && rows.TrueForAll(x => x.Instant)) continue;
                    scenes.Add(new KeyValuePair<string, string>(sc, "主役"));
                    if (withGirl) scenes.Add(new KeyValuePair<string, string>(sc, "相手"));
                    if (rows.Exists(x => x.extraMax > 0)) scenes.Add(new KeyValuePair<string, string>(sc, "ほか"));
                    if (withF && sc != "最中") scenes.Add(new KeyValuePair<string, string>(sc, "篤"));
                }
                var labels = new List<string>();
                foreach (var x in rows) foreach (var c in x.choices) if (!labels.Contains(c)) labels.Add(c);
                foreach (var c in labels) { scenes.Add(new KeyValuePair<string, string>(c, "篤")); scenes.Add(new KeyValuePair<string, string>(c, "主役")); if (withGirl) scenes.Add(new KeyValuePair<string, string>(c, "相手")); }
                string kinds = string.Join("・", rows.ConvertAll(x => x.id).ToArray());
                foreach (var kv in scenes)
                {
                    int have = 0;
                    foreach (var l in Lines) if (l.group == d.group && l.speaker == kv.Value && (l.scene == kv.Key || (labels.Contains(kv.Key) && l.scene == "選択" + (labels.IndexOf(kv.Key) + 1)))) have++;
                    string memo = d.name + "（" + d.kind + "）／行：" + kinds + "／今 " + have + "本" + (hasPartner ? "" : "／ひとり");
                    for (int k = 0; k < 2; k++) sb.Append(Csv(d.group) + "," + Csv(kv.Key) + "," + kv.Value + ",*,,," + Csv(memo) + "\r\n");
                }
            }
            foreach (var sc in new[] { "夜に起こす|起こす|篤", "夜に起こす|起こされた|主役", "夜に起こす|受ける|主役", "夜に起こす|断る|主役", "夜に起こす|記録|主役", "夜に起こす|記録_断る|主役", "寝たふり|声かけ|篤", "寝たふり|受ける|主役", "寝たふり|記録|主役" })
            {
                var a = sc.Split('|');
                int have = 0; foreach (var l in Lines) if (l.group == a[0] && l.scene == a[1] && l.speaker == a[2]) have++;
                string memo = a[0] == "寝たふり" ? "寝たふりで待っている子に篤が声をかけて、同意のひとことをもらうとき（本人は起きている）" : "篤が寝ている子を起こして誘うとき（どのできごとでも・右クリックの誘いでも）";
                if (a[1].StartsWith("記録")) memo = "記録に出る文（せりふではない）。{主役}{篤} が使える。生活感のある言い方で／" + memo;
                for (int k = 0; k < 2; k++) sb.Append(a[0] + "," + a[1] + "," + a[2] + ",*,,," + Csv(memo + "／今 " + have + "本") + "\r\n");
            }
            return sb.ToString();
        }

        static string Csv(string s) { return s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s; }

        static void LoadEvents(List<string[]> t)
        {
            if (t.Count == 0) return;
            var h = new Header(t[0]);
            foreach (var col in new[] { "ID", "使う", "種類", "名前", "時間", "場所", "相手", "主役の条件", "起こりやすさ", "長さ（分）", "結果", "記録" })
                if (!h.Has(col)) Errors.Add("できごと.csv：見出し「" + col + "」がない");
            for (int i = 1; i < t.Count; i++)
            {
                var r = t[i];
                if (Blank(r)) continue;
                string id = h.Get(r, "ID");
                if (id.Length == 0 || id.StartsWith("#")) continue;
                string where = "できごと.csv " + (i + 1) + "行目（" + id + "）";
                var d = new LifeEventDef { id = id.ToLowerInvariant(), row = i + 1 };
                d.enabled = h.Get(r, "使う") != "0";
                d.group = h.Get(r, "グループ"); if (d.group.Length == 0) d.group = d.id; d.group = d.group.ToLowerInvariant();
                d.kind = h.Get(r, "種類");
                d.name = h.Get(r, "名前"); if (d.name.Length == 0) d.name = d.id;
                d.trigger = h.Get(r, "きっかけ"); if (d.trigger.Length == 0) d.trigger = "時間";
                if (System.Array.IndexOf(new[] { "時間", "篤が来た", "篤が入浴", "二人きりのあと" }, d.trigger) < 0) Errors.Add(where + "：きっかけは「時間・篤が来た・篤が入浴・二人きりのあと」");
                if (d.trigger == "二人きりのあと") d.length = 0f;
                if (!ParseTime(h.Get(r, "時間"), out d.fromMin, out d.toMin)) Errors.Add(where + "：時間の書き方（例 22:00-25:00）");
                d.days = h.Get(r, "日"); if (d.days.Length == 0) d.days = "全";
                d.weather = h.Get(r, "天気"); if (d.weather.Length == 0) d.weather = "全";
                d.place = h.Get(r, "場所"); if (d.place.Length == 0) d.place = "その場";
                d.partnerKind = h.Get(r, "相手"); if (d.partnerKind.Length == 0) d.partnerKind = "なし";
                if (System.Array.IndexOf(new[] { "なし", "寮生", "篤", "寮生か篤" }, d.partnerKind) < 0) Errors.Add(where + "：相手は「なし・寮生・篤・寮生か篤」");
                d.mainCond = LifeCond.Parse(h.Get(r, "主役の条件"), where + " 主役の条件", false);
                d.partnerCond = LifeCond.Parse(h.Get(r, "相手の条件"), where + " 相手の条件", false);
                d.pairCond = LifeCond.Parse(h.Get(r, "ふたりの条件"), where + " ふたりの条件", true);
                d.extraMax = Mathf.Clamp(Mathf.RoundToInt(ParseF(h.Get(r, "ほかの人数"), 0f, where)), 0, 4);
                d.extraCond = LifeCond.Parse(h.Get(r, "ほかの条件"), where + " ほかの条件", false);
                d.rate = ParseF(h.Get(r, "起こりやすさ"), 0.5f, where);
                d.coolDays = ParseF(h.Get(r, "間隔（日）"), 2f, where);
                if (d.trigger != "二人きりのあと") d.length = ParseF(h.Get(r, "長さ（分）"), 0f, where);
                d.pose = h.Get(r, "姿勢"); if (d.pose.Length == 0) d.pose = "立つ";
                if (System.Array.IndexOf(new[] { "立つ", "座る", "そのまま" }, d.pose) < 0) Errors.Add(where + "：姿勢は「立つ・座る・そのまま」");
                d.scene = h.Get(r, "動き");
                d.gesture = h.Get(r, "身ぶり");
                d.result = LifeEffect.Parse(h.Get(r, "結果"), where + " 結果");
                d.logText = h.Get(r, "記録");
                string ch = h.Get(r, "篤の選択肢");
                d.choices = ch.Length > 0 ? SplitList(ch) : new string[0];
                if (d.choices.Length > 3) { Errors.Add(where + "：篤の選択肢は3つまで"); System.Array.Resize(ref d.choices, 3); }
                d.choiceResults = new LifeEffect[d.choices.Length][];
                for (int k = 0; k < d.choices.Length; k++) d.choiceResults[k] = LifeEffect.Parse(h.Get(r, "選択" + (k + 1) + "の結果"), where + " 選択" + (k + 1) + "の結果");
                if (d.place != "その場" && d.place != "主役の部屋" && d.place != "相手の部屋" && d.place != "篤の部屋" && d.place != "篤のいる所" && FindRole(d.place) == null)
                    Errors.Add(where + "：場所「" + d.place + "」が 場所の役割.csv にない");
                Events.Add(d);
            }
        }

        static void LoadLines(List<string[]> t)
        {
            if (t.Count == 0) return;
            var h = new Header(t[0]);
            for (int i = 1; i < t.Count; i++)
            {
                var r = t[i];
                if (Blank(r)) continue;
                string g = h.Get(r, "グループ").ToLowerInvariant();
                string text = h.Get(r, "せりふ");
                if (g.Length == 0 || g.StartsWith("#") || text.Length == 0) continue;
                string where = "せりふ.csv " + (i + 1) + "行目";
                var l = new LifeLine { group = g, scene = h.Get(r, "場面"), speaker = h.Get(r, "言う人"), text = text };
                if (l.scene.Length == 0) l.scene = "始まり";
                if (l.speaker.Length == 0) l.speaker = "主役";
                l.letter = h.Get(r, "記号").ToUpperInvariant(); if (l.letter.Length == 0) l.letter = "*";
                l.cond = LifeCond.Parse(h.Get(r, "条件"), where + " 条件", false);
                Lines.Add(l);
            }
        }

        public static string[] SplitList(string s)
        {
            var parts = s.Split('/', '／');
            var res = new List<string>();
            foreach (var p in parts) if (p.Trim().Length > 0) res.Add(p.Trim());
            return res.ToArray();
        }

        static float ParseF(string s, float def, string where)
        {
            if (s.Length == 0) return def;
            float v;
            if (float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) return v;
            Errors.Add(where + "：数字でない「" + s + "」");
            return def;
        }

        /// <summary>「22:00-25:00」（25時＝次の日の1時）・「全日」・空。</summary>
        static bool ParseTime(string s, out int from, out int to)
        {
            from = 0; to = 1440;
            s = s.Replace("～", "-").Replace("〜", "-").Replace("－", "-").Replace("：", ":").Trim();
            if (s.Length == 0 || s == "全日") return true;
            var a = s.Split('-');
            if (a.Length != 2) return false;
            int f, t;
            if (!Clock(a[0], out f) || !Clock(a[1], out t)) return false;
            if (t <= f) t += 1440;
            from = f; to = t;
            return true;
        }

        static bool Clock(string s, out int min)
        {
            min = 0;
            var a = s.Trim().Split(':');
            int h, m = 0;
            if (!int.TryParse(a[0], out h)) return false;
            if (a.Length > 1 && !int.TryParse(a[1], out m)) return false;
            min = h * 60 + m;
            return true;
        }

        public static string[] FindRole(string role)
        {
            foreach (var kv in Roles) if (kv.Key == role) return kv.Value;
            return null;
        }

        /// <summary>試験用：読んだ表の中身と直したほうがよい所を書き出す。</summary>
        public static string Report(LoungeWorld w)
        {
            var sb = new StringBuilder();
            sb.AppendLine("== 生活のできごと　" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine("できごと " + Events.Count + "件・せりふ " + Lines.Count + "本・場所の役割 " + Roles.Count + "件・キャラの型 " + Types.Count + "人");
            sb.AppendLine();
            sb.AppendLine("■ 直したほうがよい所（" + Errors.Count + "）");
            foreach (var e in Errors) sb.AppendLine("・" + e);
            if (w != null)
            {
                sb.AppendLine();
                sb.AppendLine("■ 場所の役割 → 今の建物の区画");
                foreach (var kv in Roles)
                {
                    var areas = w.LifeRoleAreas(kv.Key, false);
                    var names = new List<string>();
                    foreach (var a in areas) names.Add(a.name + "（" + (a.floor + 1) + "階）");
                    sb.AppendLine("・" + kv.Key + "：" + (names.Count > 0 ? string.Join("・", names.ToArray()) : "なし（この役割のできごとは起きない）"));
                }
            }
            sb.AppendLine();
            sb.AppendLine("■ できごと");
            foreach (var d in Events)
                sb.AppendLine("・" + d.id + "（" + d.kind + "・" + d.name + "）" + (d.enabled ? "" : "［使わない］") + "　グループ " + d.group + "・場所 " + d.place + "・相手 " + d.partnerKind + "・せりふ " + LineCount(d.group) + "本");
            return sb.ToString();
        }

        static int LineCount(string g) { int n = 0; foreach (var l in Lines) if (l.group == g) n++; return n; }

        /// <summary>せりふを1本選ぶ（グループ・場面・言う人・記号・条件が合う物から。なければ null）。</summary>
        public static string PickLine(string group, string scene, string speakerRole, SimCharacter who, SimCharacter other, LoungeWorld w)
        {
            if (who == null) return null;
            var cands = new List<LifeLine>();
            foreach (var l in Lines)
            {
                if (l.group != group || l.scene != scene || l.speaker != speakerRole) continue;
                if (l.letter != "*" && l.letter != who.letter) continue;
                if (!l.cond.Test(who, other, w)) continue;
                cands.Add(l);
            }
            if (cands.Count == 0) return null;
            // 記号つきの行があれば、そちらを先に
            var own = cands.FindAll(x => x.letter != "*");
            var pool = own.Count > 0 ? own : cands;
            return pool[Random.Range(0, pool.Count)].text;
        }
    }

    public class LifeEventDef
    {
        public string id, group, kind, name, trigger, days, weather, place, partnerKind, pose, scene, gesture, logText;
        public bool enabled;
        public int fromMin, toMin, row;
        public LifeCond mainCond, partnerCond, pairCond, extraCond;
        public int extraMax;   // ほかに加わる寮生の人数（0〜4。複数人のできごと）
        public float rate, coolDays, length;
        public LifeEffect[] result;
        public string[] choices;
        public LifeEffect[][] choiceResults;
        public bool Instant { get { return length <= 0f; } }

        public bool InTime(float minuteOfDay)
        {
            float m = minuteOfDay;
            return (m >= fromMin && m < toMin) || (m + 1440f >= fromMin && m + 1440f < toMin);
        }

        public bool DayOk(LoungeWorld w)
        {
            // 日：全・授業日・休日・春・夏・秋・冬・試験の前日・行事の日・満月（「/」でどれか）
            if (days != "全" && days.Length > 0)
            {
                bool any = false;
                foreach (var dd in LifeBook.SplitList(days)) if (DayMatch(w, dd)) { any = true; break; }
                if (!any) return false;
            }
            var we = Calendar.WeatherOf(w.Day);
            switch (weather)
            {
                case "晴れ": return we == Weather.Sunny;
                case "くもり": return we == Weather.Cloudy;
                case "雨": return we == Weather.Rain;
                case "雪": return we == Weather.Snow;
                case "雨か雪": return we == Weather.Rain || we == Weather.Snow;
                case "晴れかくもり": return we == Weather.Sunny || we == Weather.Cloudy;
            }
            return true;
        }

        static bool DayMatch(LoungeWorld w, string d)
        {
            bool hol = w.IsHolidayToday;
            switch (d)
            {
                case "全": return true;
                case "休日": return hol;
                case "授業日": case "平日": return !hol;
                case "試験の前日": return w.ExamEveToday;
                case "行事の日": return Calendar.IsFestival(w.Day);
                case "満月": return Calendar.IsFullMoon(w.Day);
            }
            int s = System.Array.IndexOf(Calendar.SeasonNames, d);
            if (s >= 0) return Calendar.Season(w.Day) == s;
            return false;
        }

        public bool HasEffect(string kind)
        {
            foreach (var e in result) if (e.kind == kind) return true;
            if (choiceResults != null) foreach (var r in choiceResults) foreach (var e in r) if (e.kind == kind) return true;
            return false;
        }
    }

    public class LifeLine { public string group, scene, speaker, letter, text; public LifeCond cond; }

    // ================= 条件 =================
    /// <summary>
    /// 条件：「;」でつなぐと全部、「|」でつなぐとどれか。頭に「!」で「ではない」。
    /// 欲求（疲れ・空腹・退屈・さみしさ・眠気・性欲・恋・勉強・趣味）・性格（社交・気まぐれ・真面目）・能力（体力・魅力・器用さ・魔力・知力・性技）は 0〜100 の数と比べる（>= <= > < =）。
    /// 状態:ご機嫌（その段より上）・好き:料理・嫌い:虫・趣味:読書・記号:A・種族:エルフ・気持ち>=好き（篤への気持ち：なし・気になる・好き・恋人）・恋人・しるし:名前・
    /// 寝ている・起きている・寝込んでいる・入浴中・部屋にいる・篤の近く。ふたりの条件：仲>=40（0〜100）・関係>=友達（知り合い・友達・親友）・近く・同じ場所
    /// </summary>
    public class LifeCond
    {
        class Atom { public bool not; public string key, op, text; public float value; }
        readonly List<List<Atom>> all = new List<List<Atom>>();
        public bool Empty { get { return all.Count == 0; } }
        public bool WantsSleeping, WantsBathing;

        static readonly string[] Needs = { "疲れ", "空腹", "退屈", "さみしさ", "眠気", "性欲", "恋", "勉強", "趣味" };
        static readonly string[] Persona = { "社交", "気まぐれ", "真面目" };
        static readonly string[] LoveNames = { "なし", "気になる", "好き", "恋人" };
        static readonly string[] RelNames = { "知り合い", "友達", "親友" };
        static readonly string[] Flags = { "恋人", "寝ている", "起きている", "寝込んでいる", "入浴中", "部屋にいる", "篤の近く", "近く", "同じ場所" };
        static readonly string[] Ops = { ">=", "<=", "!=", ">", "<", "=" };

        public static LifeCond Parse(string s, string where, bool pair)
        {
            var c = new LifeCond();
            s = s.Replace("；", ";").Replace("｜", "|").Replace("＞", ">").Replace("＜", "<").Replace("＝", "=").Replace("！", "!").Replace("：", ":");
            foreach (var part in s.Split(';'))
            {
                if (part.Trim().Length == 0) continue;
                var ors = new List<Atom>();
                foreach (var alt in part.Split('|'))
                {
                    string a = alt.Trim();
                    if (a.Length == 0) continue;
                    var at = ParseAtom(a, where, pair);
                    if (at == null) continue;
                    if (!at.not && (at.key == "寝ている" || at.key == "寝込んでいる")) c.WantsSleeping = true;
                    if (!at.not && at.key == "入浴中") c.WantsBathing = true;
                    ors.Add(at);
                }
                if (ors.Count > 0) c.all.Add(ors);
            }
            return c;
        }

        static Atom ParseAtom(string a, string where, bool pair)
        {
            var at = new Atom();
            if (a.StartsWith("!")) { at.not = true; a = a.Substring(1).Trim(); }
            int colon = a.IndexOf(':');
            if (colon > 0)
            {
                at.key = a.Substring(0, colon).Trim();
                at.text = a.Substring(colon + 1).Trim();
                if (System.Array.IndexOf(new[] { "状態", "好き", "嫌い", "趣味", "記号", "種族", "しるし", "型", "周期" }, at.key) < 0) { LifeBook.Errors.Add(where + "：「" + a + "」の「" + at.key + ":」が分からない"); return null; }
                StatusLine dl; int ds;
                if (at.key == "状態" && !LifeStatus.Find(at.text, out dl, out ds)) { LifeBook.Errors.Add(where + "：状態「" + at.text + "」がない"); return null; }
                return at;
            }
            foreach (var op in Ops)
            {
                int k = a.IndexOf(op);
                if (k <= 0) continue;
                at.key = a.Substring(0, k).Trim();
                at.op = op;
                at.text = a.Substring(k + op.Length).Trim();
                if (at.key == "気持ち") { int i = System.Array.IndexOf(LoveNames, at.text); if (i < 0) { LifeBook.Errors.Add(where + "：気持ちは「なし・気になる・好き・恋人」"); return null; } at.value = i; return at; }
                if (at.key == "関係") { int i = System.Array.IndexOf(RelNames, at.text); if (i < 0) { LifeBook.Errors.Add(where + "：関係は「知り合い・友達・親友」"); return null; } at.value = i; return at; }
                bool known = System.Array.IndexOf(Needs, at.key) >= 0 || System.Array.IndexOf(Persona, at.key) >= 0 || System.Array.IndexOf(Abilities.Names, at.key) >= 0 || at.key == "仲";
                if (!known) { LifeBook.Errors.Add(where + "：「" + at.key + "」が分からない"); return null; }
                if (!float.TryParse(at.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out at.value)) { LifeBook.Errors.Add(where + "：「" + a + "」の数字"); return null; }
                return at;
            }
            if (System.Array.IndexOf(Flags, a) >= 0) { at.key = a; return at; }
            LifeBook.Errors.Add(where + "：「" + a + "」が分からない");
            return null;
        }

        /// <summary>c が条件に合うか（other はふたりの条件・近くなどに使う相手）。</summary>
        public bool Test(SimCharacter c, SimCharacter other, LoungeWorld w)
        {
            foreach (var ors in all)
            {
                bool any = false;
                foreach (var at in ors) { if (Eval(at, c, other, w) != at.not) { any = true; break; } }
                if (!any) return false;
            }
            return true;
        }

        static bool Cmp(float v, string op, float x)
        {
            switch (op)
            {
                case ">=": return v >= x;
                case "<=": return v <= x;
                case ">": return v > x;
                case "<": return v < x;
                case "=": return Mathf.Abs(v - x) < 0.01f;
                case "!=": return Mathf.Abs(v - x) >= 0.01f;
            }
            return false;
        }

        static bool Eval(Atom at, SimCharacter c, SimCharacter o, LoungeWorld w)
        {
            if (c == null) return false;
            var p = c.profile;
            switch (at.key)
            {
                case "状態": { StatusLine l; int st; LifeStatus.Find(at.text, out l, out st); return c.GetStage(l) >= st; }
                case "好き": return p != null && AnyHas(p.likes, at.text);      // 「料理」なら「辛い料理」も（言葉を含む）
                case "嫌い": return p != null && AnyHas(p.dislikes, at.text);
                case "趣味": return p != null && (p.hobbyText ?? "").Contains(at.text);
                case "記号": return System.Array.IndexOf(LifeBook.SplitList(at.text), c.letter) >= 0;
                case "種族": { if (p == null) return false; foreach (var t in LifeBook.SplitList(at.text)) if ((p.race ?? "").Contains(t)) return true; return false; }
                case "周期": return LifeBook.InCycle(c, at.text, w);
                case "しるし": return c.LifeHasFlag(at.text);
                case "型": { foreach (var t in LifeBook.SplitList(at.text)) if (LifeBook.HasType(c.letter, t)) return true; return false; }
                case "恋人": return !c.IsTarget && c.IsLover;
                case "寝ている": return c.state == SimCharacter.State.Sleeping;
                case "起きている": return c.state != SimCharacter.State.Sleeping;
                case "寝込んでいる": return c.LifeSickInBed;
                case "入浴中": return c.state == SimCharacter.State.Bathing;
                case "部屋にいる": return c.LifeInOwnRoom;
                case "篤の近く": { var f = w.RomanceTarget; return f != null && f != c && LifeEventDirector.Near(c, f, 5f); }
                case "近く": return o != null && LifeEventDirector.Near(c, o, 5f);
                case "同じ場所": { if (o == null) return false; var a = w.LifeAreaAt(c.transform.position); return a != null && a == w.LifeAreaAt(o.transform.position); }
                case "気持ち": return !c.IsTarget && Cmp(c.LoveStage, at.op, at.value);
                case "関係": return o != null && Cmp(w.rel.Stage(c.id, o.id), at.op, at.value);
                case "仲": return o != null && Cmp(w.rel.Get(c.id, o.id), at.op, at.value);
            }
            int k = System.Array.IndexOf(Needs, at.key);
            if (k >= 0) return Cmp(c.LifeNeed(k), at.op, at.value);
            k = System.Array.IndexOf(Persona, at.key);
            if (k >= 0)
            {
                if (p == null) return false;
                float v = k == 0 ? p.social : (k == 1 ? p.whim : p.diligence);
                return Cmp(v * 100f, at.op, at.value);
            }
            k = System.Array.IndexOf(Abilities.Names, at.key);
            if (k >= 0) return Cmp(c.Ab(k), at.op, at.value);
            return false;
        }

        public static int NeedIndex(string name) { return System.Array.IndexOf(Needs, name); }

        static bool AnyHas(string[] list, string word)
        {
            if (list == null) return false;
            foreach (var s in list) if (s != null && s.Contains(word)) return true;
            return false;
        }
    }

    /// <summary>状態の名前（ご機嫌・風邪 など）→ 状態の列と段。</summary>
    public static class LifeStatus
    {
        public static bool Find(string name, out StatusLine line, out int stage)
        {
            foreach (var d in StatusBook.All)
                for (int i = 0; i < d.names.Length; i++)
                    if (d.names[i] == name) { line = d.line; stage = i + 1; return true; }
            line = StatusLine.MoodUp; stage = 0;
            return false;
        }
    }

    // ================= 結果 =================
    /// <summary>
    /// 結果：「;」でつなぐ。頭に「主役:」「相手:」「ふたり:」「篤:」を付けるとその人に（付けないと主役）。
    /// 欲求±数・状態:ご機嫌（その段にする）・治す:風邪・気持ち±数（篤への気持ち）・能力:体力+0.5・しるし:名前（3日）／しるし:名前:7・
    /// 仲±数（主役と相手の仲）・寝坊+分・起こす・寝る（終わったら寝る）・評判±数・寮杯+数・銀貨+数・
    /// エッチへ（主役が篤を二人きりの時間に誘いに行く）・誘う（篤が主役を誘う）。エッチへ・誘うは、どちらも相手が望むときだけ始まる（今の決まりのまま）
    /// </summary>
    public class LifeEffect
    {
        public string who = "主役", kind, key;
        public float value;
        public string text;

        public static LifeEffect[] Parse(string s, string where)
        {
            var res = new List<LifeEffect>();
            s = s.Replace("；", ";").Replace("：", ":").Replace("＋", "+").Replace("－", "-").Replace("−", "-");
            foreach (var raw in s.Split(';'))
            {
                string a = raw.Trim();
                if (a.Length == 0) continue;
                var e = new LifeEffect();
                foreach (var w in new[] { "主役", "相手", "ふたり", "篤", "みんな", "ほか" })
                    if (a.StartsWith(w + ":")) { e.who = w; a = a.Substring(w.Length + 1).Trim(); break; }
                if (a == "起こす" || a == "寝る" || a == "エッチへ" || a == "誘う" || a == "起こして誘う") { e.kind = a; res.Add(e); continue; }
                int colon = a.IndexOf(':');
                if (colon > 0)
                {
                    e.kind = a.Substring(0, colon);
                    string rest = a.Substring(colon + 1);
                    if (e.kind == "状態" || e.kind == "治す")
                    {
                        StatusLine l; int st;
                        if (!LifeStatus.Find(rest, out l, out st)) { LifeBook.Errors.Add(where + "：状態「" + rest + "」がない"); continue; }
                        e.text = rest; res.Add(e); continue;
                    }
                    if (e.kind == "しるし")
                    {
                        var b = rest.Split(':');
                        e.text = b[0]; e.value = 3f;
                        if (b.Length > 1) float.TryParse(b[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out e.value);
                        res.Add(e); continue;
                    }
                    if (e.kind == "出かける")
                    {
                        if (!float.TryParse(rest, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out e.value)) { LifeBook.Errors.Add(where + "：「" + a + "」（出かける:90 の形）"); continue; }
                        res.Add(e); continue;
                    }
                    if (e.kind == "能力")
                    {
                        int k = FindNum(rest, out e.key, out e.value);
                        if (k < 0 || System.Array.IndexOf(Abilities.Names, e.key) < 0) { LifeBook.Errors.Add(where + "：「" + a + "」（能力:体力+0.5 の形）"); continue; }
                        res.Add(e); continue;
                    }
                    LifeBook.Errors.Add(where + "：「" + a + "」が分からない");
                    continue;
                }
                if (FindNum(a, out e.key, out e.value) < 0) { LifeBook.Errors.Add(where + "：「" + a + "」が分からない"); continue; }
                if (LifeCond.NeedIndex(e.key) >= 0) e.kind = "欲求";
                else if (System.Array.IndexOf(new[] { "気持ち", "仲", "寝坊", "評判", "寮杯", "銀貨" }, e.key) >= 0) e.kind = e.key;
                else { LifeBook.Errors.Add(where + "：「" + e.key + "」が分からない"); continue; }
                res.Add(e);
            }
            return res.ToArray();
        }

        /// <summary>「疲れ+10」→ 疲れ・10。数の位置を返す（なければ -1）。</summary>
        static int FindNum(string a, out string key, out float v)
        {
            key = a; v = 0f;
            int k = a.IndexOfAny(new[] { '+', '-' });
            if (k <= 0) return -1;
            key = a.Substring(0, k).Trim();
            if (!float.TryParse(a.Substring(k), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) return -1;
            return k;
        }
    }
}
