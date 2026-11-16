using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 信頼と悩みの段（2026-11-16 famuru「進めてみて」。設計：プロジェクトの claude/信頼と悩みの段_設計.md）。
    /// famuru の決定：まずヒロインだけ（モブはあと）・寮は今は分けない・段と恋人（告白）は別々・数字は言葉とゲージの両方で見せる。
    /// ・ヒロイン1人ごと、篤に対して：信頼・警戒・関心（0〜100）と段（0 知らない・1 貸しがある・2 関係がある・3 信じている）
    /// ・悩み：Resources/Text/悩み.csv。1人が同時に1つ。右クリックの「悩みを聞く」で分かり、「（篤がすること）」を回数ぶんすると解決
    /// ・解決 → 段1（貸し・二人きりの約束）→ 約束の二人きりの時間 → 段2 → 信頼 70 以上・気持ち「好き」以上 → 段3
    /// ・周が変わると 0 から（篤への気持ちと同じ）。セーブに入る（SaveSystem.cs）
    /// </summary>
    public class BondBook
    {
        public readonly float[] trust, wary, interest;
        public readonly int[] stage;
        public readonly string[] worry;
        public readonly double[] worryUntil, lastAct;
        public readonly int[] worryProg, lastAskDay;
        public readonly bool[] worryKnown, promise;
        public bool initialized;
        int lastDay = -1;
        float hourTimer;
        double lastNow = -1;

        public BondBook(int n)
        {
            trust = new float[n]; wary = new float[n]; interest = new float[n];
            stage = new int[n]; worry = new string[n]; worryUntil = new double[n]; lastAct = new double[n];
            worryProg = new int[n]; lastAskDay = new int[n]; worryKnown = new bool[n]; promise = new bool[n];
            for (int i = 0; i < n; i++) { lastAct[i] = -1e9; lastAskDay[i] = -1; }
        }

        public static readonly string[] StageNames = { "知らない", "貸しがある", "関係がある", "信じている" };

        public static string TrustWord(float v) { return v >= 70 ? "信じきっている" : v >= 40 ? "頼っている" : v >= 20 ? "少し頼る" : "よく知らない"; }
        public static string WaryWord(float v) { return v >= 60 ? "近づきにくい" : v >= 30 ? "まだ距離がある" : v >= 10 ? "少し気を許している" : "気を許している"; }
        public static string InterestWord(float v) { return v >= 70 ? "目が離せない" : v >= 40 ? "気になっている" : v >= 15 ? "少し気になる" : "気にしていない"; }

        static bool Heroine(SimCharacter c) { return c != null && !c.IsTarget && !c.IsMob; }

        /// <summary>はじめの値（キャラの型から。周のはじめ・新しいゲーム）。</summary>
        public void InitValues(LoungeWorld w)
        {
            initialized = true;
            foreach (var c in w.characters)
            {
                if (!Heroine(c)) continue;
                int i = c.id;
                var rnd = new System.Random(i * 7919 + 13);
                float t = 15f, wa = 25f;
                if (LifeBook.HasType(c.letter, "世話焼き")) t += 8;
                if (LifeBook.HasType(c.letter, "素直")) t += 6;
                if (LifeBook.HasType(c.letter, "甘えんぼう")) { t += 5; wa -= 10; }
                if (LifeBook.HasType(c.letter, "強がり")) wa += 15;
                if (LifeBook.HasType(c.letter, "人見知り")) wa += 18;
                if (LifeBook.HasType(c.letter, "ツンデレ")) wa += 12;
                if (c.profile != null && c.profile.diligence > 0.7f) wa += 8;
                if (c.profile != null && c.profile.social > 0.7f) wa -= 8;
                trust[i] = Mathf.Clamp(t + (float)(rnd.NextDouble() * 10 - 5), 0, 100);
                wary[i] = Mathf.Clamp(wa + (float)(rnd.NextDouble() * 10 - 5), 0, 100);
                interest[i] = (float)(rnd.NextDouble() * 10);
            }
        }

        public void AddTrust(int i, float d) { trust[i] = Mathf.Clamp(trust[i] + d, 0, 100); }
        public void AddWary(int i, float d) { wary[i] = Mathf.Clamp(wary[i] + d, 0, 100); }
        public void AddInterest(int i, float d) { interest[i] = Mathf.Clamp(interest[i] + d, 0, 100); }

        public WorryBook.Def WorryOf(int i) { return string.IsNullOrEmpty(worry[i]) ? null : WorryBook.Get(worry[i]); }

        /// <summary>毎コマ（LoungeWorld.Update）：日がかわったら関心を少し下げる・1時間ごとに悩みが生まれる／期限・段の進みを見る。</summary>
        public void Update(LoungeWorld w)
        {
            if (!initialized) InitValues(w);
            if (w.Day != lastDay)
            {
                if (lastDay >= 0) for (int i = 0; i < interest.Length; i++) interest[i] = Mathf.Max(0f, interest[i] - 1f);
                lastDay = w.Day;
            }
            hourTimer -= Time.unscaledDeltaTime;
            if (hourTimer > 0f) return;
            hourTimer = 1f;
            double now = w.Now;
            float dtMin = lastNow < 0 || now < lastNow ? 0f : Mathf.Min(60f, (float)(now - lastNow));
            lastNow = now;
            var f = w.RomanceTarget;
            foreach (var c in w.characters)
            {
                if (!Heroine(c)) continue;
                int i = c.id;
                // 悩みの期限
                var wd = WorryOf(i);
                if (wd != null && w.Now > worryUntil[i])
                {
                    w.Log("○ " + c.displayName + "の悩み「" + wd.name + "」は、そのままになった" + (worryKnown[i] ? "（信頼が下がった）" : ""));
                    if (worryKnown[i]) { AddTrust(i, -5); AddWary(i, 5); }
                    worry[i] = null; worryKnown[i] = false; worryProg[i] = 0;
                }
                // 悩みが生まれる（段3 の子は持たない。1日に 20% くらい）
                if (string.IsNullOrEmpty(worry[i]) && stage[i] < 3 && c.Present && dtMin > 0f && Random.value < WorryBook.PerDay * dtMin / 1440f)
                    TryGiveWorry(w, c);
                // 段1 → 段2：約束の二人きりの時間
                if (stage[i] == 1 && promise[i] && f != null && c.InPrivate && f.InPrivate && Vector3.Distance(c.transform.position, f.transform.position) < 3f)
                {
                    stage[i] = 2; promise[i] = false;
                    AddTrust(i, 10);
                    w.Log("♦ " + c.displayName + "：約束の二人きりの時間（段2「関係がある」になった）");
                }
                // 段2 → 段3：信頼 70 以上・気持ち「好き」以上
                if (stage[i] == 2 && trust[i] >= 70f && c.LoveStage >= 2)
                {
                    stage[i] = 3;
                    w.Log("♦ " + c.displayName + "は篤を信じきっている（段3「信じている」になった）");
                }
            }
        }

        /// <summary>この子に合う悩みを1つ持たせる（表の「持ちやすい子」と「起こりやすさ」）。</summary>
        public bool TryGiveWorry(LoungeWorld w, SimCharacter c, string name = null)
        {
            var list = new List<WorryBook.Def>();
            float sum = 0f;
            foreach (var d in WorryBook.All)
            {
                if (name != null && d.name != name) continue;
                if (name == null && d.cond != null && !d.cond.Test(c, null, w)) continue;
                list.Add(d); sum += Mathf.Max(0.01f, d.weight);
            }
            if (list.Count == 0) return false;
            float r = Random.value * sum;
            WorryBook.Def pick = list[list.Count - 1];
            foreach (var d in list) { r -= Mathf.Max(0.01f, d.weight); if (r <= 0f) { pick = d; break; } }
            int i = c.id;
            worry[i] = pick.name; worryKnown[i] = false; worryProg[i] = 0;
            worryUntil[i] = w.Now + pick.days * 1440.0;
            return true;
        }

        /// <summary>右クリック「悩みを聞く」。</summary>
        public void Ask(LoungeWorld w, SimCharacter c)
        {
            int i = c.id;
            var wd = WorryOf(i);
            if (lastAskDay[i] == w.Day && (wd == null || worryKnown[i])) { c.ShowBubble("さっきも聞いてくれたよね。ありがと", 2.2f); return; }
            lastAskDay[i] = w.Day;
            if (wd == null) { c.ShowBubble("ううん、今は特にないかな", 2.2f); AddInterest(i, 1); AddTrust(i, 1); return; }
            if (worryKnown[i]) { c.ShowBubble(wd.askLine, 3f); return; }
            if (trust[i] < WorryBook.AskTrust && wary[i] >= 30f)
            {
                c.ShowBubble("……なんでもないよ", 2.2f);
                AddInterest(i, 2);
                w.Log(c.displayName + "は何か悩んでいるようだが、まだ話してくれない（信頼「" + TrustWord(trust[i]) + "」）");
                return;
            }
            worryKnown[i] = true;
            AddTrust(i, 2);
            c.ShowBubble(wd.askLine, 3.2f);
            w.Log("○ " + c.displayName + "の悩み「" + wd.name + "」を聞いた（" + wd.action + "を " + wd.count + "回。あと " + Mathf.Max(0, Mathf.CeilToInt((float)((worryUntil[i] - w.Now) / 1440.0))) + "日）");
        }

        /// <summary>悩みのための行動ができるか（できなければ理由）。</summary>
        public string CanAct(LoungeWorld w, SimCharacter c, SimCharacter f)
        {
            int i = c.id;
            var wd = WorryOf(i);
            if (wd == null || !worryKnown[i]) return "悩みを知らない";
            if (f == null || Vector3.Distance(c.transform.position, f.transform.position) > 3f || Mathf.Abs(c.transform.position.y - f.transform.position.y) > 0.8f) return "そばへ行ってから";
            if (w.Now - lastAct[i] < 60.0) return "少し時間をおいて";
            if (!wd.InTime(w.MinuteOfDay)) return wd.when + "に";
            if (wd.alone)
                foreach (var o in w.characters)
                    if (o != null && o != c && o != f && o.Present && !o.IsMob && Vector3.Distance(o.transform.position, c.transform.position) < 5f && Mathf.Abs(o.transform.position.y - c.transform.position.y) < 2f) return "人目がある";
            if (wd.silver > 0 && (w.player == null || w.player.money < wd.silver)) return "銀貨が " + wd.silver + " いる";
            if (wd.item != null && (w.player == null || w.player.Count(wd.item.kind) <= 0)) return "「" + wd.item.name + "」がいる";
            return null;
        }

        /// <summary>右クリック「（篤がすること）」。</summary>
        public void Act(LoungeWorld w, SimCharacter c, SimCharacter f)
        {
            string why = CanAct(w, c, f);
            if (why != null) { w.Log("今はできない：" + why); return; }
            DoAct(w, c, true);
        }

        /// <summary>試験用（worry act）：条件を見ずに1回する（悩みを知らなければ知ったことにする）。</summary>
        public void ForceAct(LoungeWorld w, SimCharacter c)
        {
            if (WorryOf(c.id) == null) return;
            worryKnown[c.id] = true;
            DoAct(w, c, false);
        }

        void DoAct(LoungeWorld w, SimCharacter c, bool pay)
        {
            int i = c.id;
            var wd = WorryOf(i);
            if (wd == null) return;
            if (pay && wd.silver > 0) w.player.money -= wd.silver;
            if (pay && wd.item != null) w.player.Take(wd.item.kind);
            lastAct[i] = w.Now;
            worryProg[i]++;
            AddTrust(i, 3); AddInterest(i, 3);
            w.romance.Add(i, 1f, w.simTime);
            if (worryProg[i] < wd.count)
            {
                c.ShowBubble("ありがとう、助かる…", 2.2f);
                w.Log("○ 篤が" + c.displayName + "のために「" + wd.action + "」（" + worryProg[i] + "/" + wd.count + "）");
                return;
            }
            // 解決
            worry[i] = null; worryKnown[i] = false; worryProg[i] = 0;
            AddTrust(i, wd.trustGain); AddWary(i, -20f); AddInterest(i, 10f);
            c.ShowBubble(wd.thanksLine, 3.2f);
            if (stage[i] == 0) { stage[i] = 1; promise[i] = true; w.Log("♦ " + c.displayName + "の悩み「" + wd.name + "」を解決した（段1「貸しがある」：二人きりでお礼をする約束をした）"); }
            else { if (stage[i] == 1) promise[i] = true; w.Log("♦ " + c.displayName + "の悩み「" + wd.name + "」を解決した（信頼が上がった）"); }
        }

        /// <summary>話したとき（LoungeWorld.ReportTalk）：楽しい会話は関心＋・警戒−。</summary>
        public void OnTalk(SimCharacter a, SimCharacter b, TalkResult r)
        {
            SimCharacter g = a.IsTarget ? b : (b.IsTarget ? a : null);
            if (!Heroine(g)) return;
            if (r == TalkResult.Fun) { AddInterest(g.id, 1.5f); AddWary(g.id, -1.5f); AddTrust(g.id, 0.5f); }
            else if (r == TalkResult.Normal) { AddInterest(g.id, 0.5f); AddWary(g.id, -0.5f); }
        }
    }

    /// <summary>悩みの表（Resources/Text/悩み.csv）。</summary>
    public static class WorryBook
    {
        public static float PerDay = 0.2f;     // 悩みのない子が1日に悩みを持つ見こみ
        public static float AskTrust = 20f;    // これより信頼が低く、警戒が 30 以上だと話してくれない

        public class Def
        {
            public string name, kind, condText, action, when = "いつでも", askLine, thanksLine;
            public float weight = 1f, trustGain = 20f;
            public int days = 7, count = 1, silver;
            public bool alone;
            public ItemDef item;
            public LifeCond cond;
            public bool InTime(float m)
            {
                switch (when)
                {
                    case "放課後": return m >= 930f && m < 1320f;            // 15:30〜22:00
                    case "夜": return m >= 1200f || m < 120f;                // 20:00〜2:00
                    case "朝": return m >= 360f && m < 540f;                 // 6:00〜9:00
                    default: return true;
                }
            }
        }

        static List<Def> defs;
        public static readonly List<string> Errors = new List<string>();
        public static List<Def> All { get { if (defs == null) Load(); return defs; } }
        public static Def Get(string n) { foreach (var d in All) if (d.name == n) return d; return null; }
        public static void Reload() { defs = null; }

        static void Load()
        {
            defs = new List<Def>();
            Errors.Clear();
            byte[] bytes = null;
#if UNITY_EDITOR
            string path = Path.Combine(Application.dataPath, "Proto/Resources/Text/悩み.csv");
            if (File.Exists(path)) { try { bytes = File.ReadAllBytes(path); } catch (System.Exception e) { Errors.Add("悩み.csv：読めない（" + e.Message + "）"); } }
#endif
            if (bytes == null) { var ta = Resources.Load<TextAsset>("Text/悩み"); if (ta != null) bytes = ta.bytes; }
            if (bytes == null) { Errors.Add("悩み.csv が無い"); return; }
            int st = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            string text = new UTF8Encoding(false).GetString(bytes, st, bytes.Length - st);
            foreach (var r in LifeBook.ParseCsv(text))
            {
                if (r.Length < 6 || string.IsNullOrEmpty(r[0]) || r[0] == "名前" || r[0].StartsWith("#")) continue;
                var d = new Def { name = r[0].Trim(), kind = C(r, 1, ""), condText = C(r, 2, ""), action = C(r, 5, "話を聞く"), when = C(r, 7, "いつでも"), askLine = C(r, 11, "実は、ちょっと悩んでて…"), thanksLine = C(r, 12, "ありがとう…お礼、させてね") };
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                float.TryParse(C(r, 3, "1"), System.Globalization.NumberStyles.Float, ci, out d.weight);
                int.TryParse(C(r, 4, "7"), out d.days);
                int.TryParse(C(r, 6, "1"), out d.count);
                d.alone = C(r, 8, "気にしない") == "二人だけ";
                string need = C(r, 9, "");
                if (need.StartsWith("銀貨")) int.TryParse(need.Substring(2), out d.silver);
                else if (need.Length > 0) { foreach (var it in ItemBook.All) if (it.name == need) d.item = it; if (d.item == null) Errors.Add(d.name + "：必要な物「" + need + "」が持ち物に無い"); }
                float.TryParse(C(r, 10, "20"), System.Globalization.NumberStyles.Float, ci, out d.trustGain);
                if (d.condText.Length > 0 && d.condText != "だれでも")
                {
                    int before = LifeBook.Errors.Count;
                    d.cond = LifeCond.Parse(d.condText, "悩み.csv「" + d.name + "」", false);
                    for (int k = before; k < LifeBook.Errors.Count; k++) Errors.Add(LifeBook.Errors[k]);
                }
                if (d.count < 1) d.count = 1;
                defs.Add(d);
            }
        }

        static string C(string[] r, int i, string def) { return i < r.Length && !string.IsNullOrEmpty(r[i].Trim()) ? r[i].Trim() : def; }
    }

    public partial class LoungeWorld
    {
        BondBook bondBook;
        /// <summary>信頼と悩みの段（Bond.cs）。はじめて使うときに作る。</summary>
        public BondBook bond { get { if (bondBook == null) bondBook = new BondBook(romance.love.Length); return bondBook; } }
    }
}
