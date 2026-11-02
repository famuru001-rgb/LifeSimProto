using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>動いている生活のできごと1つ分。</summary>
    public class LifeRun
    {
        public enum Phase { Gather, Active, Done }
        public LifeEventDef def;
        public SimCharacter main, partner;
        public bool partnerIsF, fHeld, fWatching;   // fWatching：篤が近くで見ていて、選択肢が出ている間その場に止めている
        public AreaInfo area;
        public Vector3 pMain, pPartner;
        public bool mainMoves, partnerMoves;
        public Phase phase;
        public double startAt, until, nextTalk;
        public bool mainWasAsleep, partnerWasAsleep, bedMain, bedPartner;
        public int chosen = -1;
        public bool choiceOpen;
        public string pendingH;   // エッチへ・誘う（終わってから）
        public readonly List<SimCharacter> extras = new List<SimCharacter>();   // ほかに加わった寮生（複数人のできごと）
        public readonly List<KeyValuePair<SimCharacter, float>> goOut = new List<KeyValuePair<SimCharacter, float>>();   // 終わったら出かける人と分
        public string Names { get { string s = main.displayName + (partner != null ? "・" + partner.displayName : ""); foreach (var c in extras) s += "・" + c.displayName; return s; } }
    }

    /// <summary>
    /// 生活のできごとを起こして進める（2026-11-02。表は LifeEvents.cs）。
    /// ・「時間」のできごと：5分ごとに、時間・日・天気が合うグループの起こりやすさ（1日あたりの回数）でくじを引き、条件の合う子を選んで始める
    /// ・「篤が来た」のできごと：篤が区画に入ったとき、その区画にいる条件の合う子で、起こりやすさ（0〜1）のくじ
    /// ・同じグループの行は上から順に条件を見て、最初に合った行を使う（性格・関係で変わる分は行を足す）
    /// ・動く人は場所へ歩き、そろったら始まる。篤は動かさない（篤のいる所に来る）。篤が加わる・近くにいるときは画面の下に選択肢
    /// </summary>
    public class LifeEventDirector : MonoBehaviour
    {
        public static LifeEventDirector Instance;
        /// <summary>セーブから読んだ「最後に起きた日」（Start で使う）。</summary>
        public static List<string> PendingCool;
        public const int MaxRuns = 3;
        const double CheckEvery = 5.0, GatherLimit = 120.0;   // 集まるのを待つ長さ（ゲームの分。歩く速さは1分＝約1秒で進むので、3階から台所まで30分ほどかかる）

        LoungeWorld w;
        public readonly List<LifeRun> runs = new List<LifeRun>();
        readonly Dictionary<string, int> lastDay = new Dictionary<string, int>();
        readonly Dictionary<string, int> owed = new Dictionary<string, int>();   // くじに当たったが、まだ起きていない回数（時間帯のあいだ持ち越す）
        double nextCheck, nextAreaCheck;
        AreaInfo fLastArea;
        public readonly List<string> recent = new List<string>();

        void Start()
        {
            Instance = this;
            w = GetComponent<LoungeWorld>();
            LifeBook.Load();
            if (PendingCool != null)
            {
                foreach (var s in PendingCool) { var a = s.Split('|'); int d; if (a.Length == 3 && int.TryParse(a[2], out d)) lastDay[a[0] + "|" + a[1]] = d; }
                PendingCool = null;
            }
            nextCheck = w.Now + 1;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public List<string> CoolSave()
        {
            var res = new List<string>();
            foreach (var kv in lastDay) res.Add(kv.Key + "|" + kv.Value);
            return res;
        }

        void Update()
        {
            if (w == null || w.romance == null) return;
            double now = w.Now;
            for (int i = runs.Count - 1; i >= 0; i--) Step(runs[i], now);
            runs.RemoveAll(r => r.phase == LifeRun.Phase.Done);
            if (now >= nextCheck) { nextCheck = now + CheckEvery; CheckTimed(now); }
            if (now >= nextAreaCheck) { nextAreaCheck = now + 0.5; CheckFArea(); CheckFBath(); CheckAfterPrivate(); }
        }

        // ================= きっかけ：篤が入浴・二人きりのあと =================
        bool fWasBathing;
        void CheckFBath()
        {
            var f = w.RomanceTarget;
            bool bath = f != null && f.state == SimCharacter.State.Bathing;
            bool started = bath && !fWasBathing;
            fWasBathing = bath;
            if (!started || runs.Count >= MaxRuns) return;
            float m = w.MinuteOfDay;
            foreach (var g in Groups("篤が入浴"))
            {
                var head = g[0];
                if (!head.InTime(m) || !head.DayOk(w) || GroupBusy(head.group)) continue;
                if (Random.value >= Mathf.Clamp01(head.rate)) continue;
                string why;
                if (TryStart(g, null, f, false, null, out why)) return;   // 1回の入浴に1つまで
            }
        }

        readonly HashSet<SimCharacter> wasPrivate = new HashSet<SimCharacter>();
        void CheckAfterPrivate()
        {
            foreach (var c in w.residents)
            {
                if (c.IsTarget || c.IsMob) continue;
                bool now = c.InPrivate || c.InHSession;
                if (now) { wasPrivate.Add(c); continue; }
                if (!wasPrivate.Remove(c)) continue;
                // 二人きりの時間が終わった子：「二人きりのあと」のできごと（その場で一瞬。結果だけ）
                // 寝に行く途中などでも起きるように、集めずに結果だけをその場で足す
                foreach (var g in Groups("二人きりのあと"))
                {
                    var head = g[0];
                    if (!head.InTime(w.MinuteOfDay) || !head.DayOk(w)) continue;
                    if (Random.value >= Mathf.Clamp01(head.rate)) continue;
                    foreach (var d in g)
                    {
                        if (!d.mainCond.Test(c, null, w)) continue;
                        var r = new LifeRun { def = d, main = c, startAt = w.Now };
                        Apply(r, d.result);
                        string text = Fill(d.logText.Length > 0 ? d.logText : "{主役}：" + d.name, r, null);
                        w.Log(text);
                        AddRecent(text);
                        break;   // 同じグループは、合った最初の行だけ
                    }
                }
            }
        }

        // ================= グループ =================
        List<List<LifeEventDef>> Groups(string trigger)
        {
            var res = new List<List<LifeEventDef>>();
            var map = new Dictionary<string, List<LifeEventDef>>();
            foreach (var d in LifeBook.Events)
            {
                if (!d.enabled) continue;
                List<LifeEventDef> g;
                if (!map.TryGetValue(d.group, out g)) { g = new List<LifeEventDef>(); map[d.group] = g; res.Add(g); }
                g.Add(d);
            }
            return res.FindAll(g => g[0].trigger == trigger);
        }

        bool GroupBusy(string group) { foreach (var r in runs) if (r.def.group == group) return true; return false; }

        void CheckTimed(double now)
        {
            if (runs.Count >= MaxRuns) return;
            float m = w.MinuteOfDay;
            foreach (var g in Groups("時間"))
            {
                var head = g[0];
                int n;
                owed.TryGetValue(head.group, out n);
                if (!head.InTime(m) || !head.DayOk(w)) { owed[head.group] = 0; continue; }   // 時間帯が終わったら持ち越しも消える
                float win = Mathf.Max(10f, head.toMin - head.fromMin);
                float p = head.rate * (float)CheckEvery / win;
                // 1日に何回も起きるもの（起こりやすさ 1 より大きい）は、1回のくじで2回分以上当たることもある
                while (p > 0f) { if (Random.value < Mathf.Min(1f, p)) n++; p -= 1f; }
                owed[head.group] = Mathf.Min(n, 3);
                if (n <= 0 || GroupBusy(head.group) || runs.Count >= MaxRuns) continue;
                string why;
                if (TryStart(g, null, null, false, null, out why)) owed[head.group] = n - 1;   // 条件の合う子がいなければ、次の5分にまた試す
            }
        }

        /// <summary>篤が新しい区画に入った：「篤が来た」のできごと。</summary>
        void CheckFArea()
        {
            var f = w.RomanceTarget;
            if (f == null || !f.Present) { fLastArea = null; return; }
            var a = w.LifeAreaAt(f.transform.position);
            if (a == fLastArea) return;
            fLastArea = a;
            if (a == null || runs.Count >= MaxRuns) return;
            float m = w.MinuteOfDay;
            foreach (var g in Groups("篤が来た"))
            {
                var head = g[0];
                if (!head.InTime(m) || !head.DayOk(w) || GroupBusy(head.group)) continue;
                if (!PlaceHas(head.place, a)) continue;
                if (Random.value >= Mathf.Clamp01(head.rate)) continue;
                string why;
                TryStart(g, null, null, false, a, out why);
            }
        }

        bool PlaceHas(string place, AreaInfo a)
        {
            if (place == "主役の部屋" || place == "篤のいる所" || place == "その場") return true;
            return w.LifeRoleAreas(place, true).Contains(a);
        }

        // ================= 始める =================
        class Cand { public LifeEventDef def; public SimCharacter main, partner; }

        /// <summary>グループのできごとを始める。forceMain・forcePartner は試験用（命令 life run）。inArea は「篤が来た」の区画。</summary>
        public bool TryStart(List<LifeEventDef> g, SimCharacter forceMain, SimCharacter forcePartner, bool ignoreCool, AreaInfo inArea, out string why, bool test = false)
        {
            why = "";
            if (runs.Count >= MaxRuns) { why = "できごとが多すぎる（同時に" + MaxRuns + "つまで）"; return false; }
            var f = w.RomanceTarget;
            var cands = new List<Cand>();
            foreach (var c in w.residents)
            {
                if (c.IsTarget || c.IsMob) continue;
                if (forceMain != null && c != forceMain) continue;
                foreach (var d in g)
                {
                    if (!c.LifeAvailable(d.mainCond.WantsSleeping, d.mainCond.WantsBathing)) continue;
                    int last;
                    if (!ignoreCool && lastDay.TryGetValue(d.group + "|" + c.letter, out last) && w.Day - last < d.coolDays) continue;
                    if (inArea != null && w.LifeAreaAt(c.transform.position) != inArea) continue;
                    if (!test && !d.mainCond.Test(c, null, w)) continue;
                    var pt = PickPartner(d, c, forcePartner, f, test);
                    if (d.partnerKind != "なし" && pt == null) continue;
                    cands.Add(new Cand { def = d, main = c, partner = pt });
                    break; // 同じグループは、合った最初の行だけ
                }
            }
            if (cands.Count == 0) { why = "条件の合う子がいない"; return false; }
            for (int tries = 0; tries < 3 && cands.Count > 0; tries++)
            {
                var k = cands[Random.Range(0, cands.Count)];
                if (Begin(k.def, k.main, k.partner, out why, test)) return true;
                cands.Remove(k);
            }
            return false;
        }

        SimCharacter PickPartner(LifeEventDef d, SimCharacter main, SimCharacter force, SimCharacter f, bool test = false)
        {
            if (d.partnerKind == "なし") return null;
            var list = new List<SimCharacter>();
            bool fOk = f != null && (f.LifeAvailableAsF || (d.trigger == "篤が入浴" && f.state == SimCharacter.State.Bathing && f.lifeEv == null) || (d.trigger == "二人きりのあと" && f.lifeEv == null));
            if ((d.partnerKind == "篤" || d.partnerKind == "寮生か篤") && fOk) list.Add(f);
            if (d.partnerKind == "寮生" || d.partnerKind == "寮生か篤")
                foreach (var c in w.residents)
                    if (c != main && !c.IsTarget && !c.IsMob && c.LifeAvailable(d.partnerCond.WantsSleeping, d.partnerCond.WantsBathing)) list.Add(c);
            list.RemoveAll(c => (force != null && c != force) || (!test && (!d.partnerCond.Test(c, main, w) || !d.pairCond.Test(main, c, w))));
            if (list.Count == 0) return null;
            return list[Random.Range(0, list.Count)];
        }

        bool Stays(SimCharacter c, LifeEventDef d, bool isMain)
        {
            if (c == null) return false;
            if (c.IsTarget) return true;
            if (c.state == SimCharacter.State.Sleeping || c.state == SimCharacter.State.Bathing || c.LifeSickInBed) return true;
            return isMain && d.pose == "そのまま";
        }

        bool Begin(LifeEventDef d, SimCharacter main, SimCharacter partner, out string why, bool test = false)
        {
            why = "";
            var r = new LifeRun { def = d, main = main, partner = partner, partnerIsF = partner != null && partner.IsTarget, startAt = w.Now };
            bool mainStays = Stays(main, d, true) || d.Instant, partnerStays = partner != null && (Stays(partner, d, false) || d.Instant);
            // ---- 場所 ----
            AreaInfo area = null;
            Vector3 center;
            switch (d.place)
            {
                case "その場": center = main.transform.position; area = w.LifeAreaAt(center); mainStays = true; break;
                case "篤のいる所":
                    {
                        var f = w.RomanceTarget;
                        if (f == null) { why = "篤がいない"; return false; }
                        center = f.transform.position; area = w.LifeAreaAt(center); break;
                    }
                case "主役の部屋": area = w.LifeRoomArea(main); center = area != null ? w.RandomPointIn(area) : Vector3.zero; break;
                case "相手の部屋": area = w.LifeRoomArea(partner); center = area != null ? w.RandomPointIn(area) : Vector3.zero; break;
                case "篤の部屋": area = w.LifeRoomArea(w.RomanceTarget); center = area != null ? w.RandomPointIn(area) : Vector3.zero; break;
                default:
                    {
                        var list = w.LifeRoleAreas(d.place, true);
                        if (list.Count == 0) { why = "場所「" + d.place + "」が今の建物にない（または開いていない）"; return false; }
                        Vector3 mp = main.transform.position;
                        list.Sort((a, b) => Dist(a, mp).CompareTo(Dist(b, mp)));
                        area = list[0];
                        center = w.RandomPointIn(area);
                        break;
                    }
            }
            if (area == null && d.place != "その場" && d.place != "篤のいる所") { why = "場所が決まらない（部屋がない）"; return false; }
            // 動かない人（篤・寝ている子・入浴中の子）がいれば、その人のところに集まる
            if (partnerStays && partner != null)
            {
                if (partner.IsTarget && area != null && d.place != "篤のいる所" && d.place != "その場" && w.LifeAreaAt(partner.transform.position) != area)
                {
                    if (!test) { why = "篤がその場所にいない"; return false; }
                    area = w.LifeAreaAt(partner.transform.position);   // 試験：篤のいる所で
                }
                r.pPartner = partner.transform.position;
                r.pMain = mainStays ? main.transform.position : w.LifeNear(r.pPartner, 1.0f);
            }
            else if (mainStays)
            {
                r.pMain = main.transform.position;
                r.pPartner = w.LifeNear(r.pMain, 1.0f);
            }
            else
            {
                r.pMain = center;
                r.pPartner = w.LifeNear(center, 1.1f);
            }
            if (mainStays && partner != null && partnerStays && !Near(main, partner, 10f)) { why = "ふたりが離れている"; return false; }
            r.area = area ?? w.LifeAreaAt(r.pMain);
            r.mainMoves = !mainStays;
            r.partnerMoves = partner != null && !partnerStays;
            r.mainWasAsleep = main.state == SimCharacter.State.Sleeping;
            r.partnerWasAsleep = partner != null && partner.state == SimCharacter.State.Sleeping;

            // ---- 加える ----
            string going = d.name + "（向かっている）";
            if (r.mainMoves) { if (!(d.pose == "座る" ? main.LifeSitNear(r, r.pMain, going) : (d.pose == "横になる" ? main.LifeLieBed(r, r.pMain, going) : main.LifeGoTo(r, r.pMain, going)))) { why = "主役が場所へ行けない"; return false; } }
            else main.LifeStay(r);
            if (partner != null)
            {
                if (r.partnerIsF) { if (!partner.HasOrder) { partner.LifeStay(r); r.fHeld = true; } }
                else if (r.partnerMoves) { if (!(d.pose == "座る" ? partner.LifeSitNear(r, r.pPartner, going) : partner.LifeGoTo(r, r.pPartner, going))) { main.LifeRelease(false); why = "相手が場所へ行けない"; return false; } }
                else partner.LifeStay(r);
            }
            // ほかに加わる寮生（複数人のできごと）
            if (d.extraMax > 0 && !d.Instant)
            {
                var pool = new List<SimCharacter>();
                foreach (var c in w.residents)
                    if (c != main && c != partner && !c.IsTarget && !c.IsMob && c.LifeAvailable(false, false) && d.extraCond.Test(c, main, w)) pool.Add(c);
                while (pool.Count > 0 && r.extras.Count < d.extraMax)
                {
                    var c = pool[Random.Range(0, pool.Count)];
                    pool.Remove(c);
                    Vector3 p = w.LifeNear(r.pMain, 1.4f);
                    bool ok = d.pose == "座る" ? c.LifeSitNear(r, p, going) : c.LifeGoTo(r, p, going);
                    if (ok) r.extras.Add(c);
                }
            }
            lastDay[d.group + "|" + main.letter] = w.Day;
            r.phase = LifeRun.Phase.Gather;
            runs.Add(r);
            if (d.Instant) Activate(r);
            return true;
        }

        static float Dist(AreaInfo a, Vector3 p)
        {
            return Vector2.Distance(a.rect.center, new Vector2(p.x, p.z)) + Mathf.Abs(LoungeWorld.FloorY(a.floor) - p.y) * 4f;
        }

        public static bool Near(SimCharacter a, SimCharacter b, float d)
        {
            if (a == null || b == null) return false;
            Vector3 v = a.transform.position - b.transform.position;
            return Mathf.Abs(v.y) < 1.2f && new Vector2(v.x, v.z).magnitude <= d;
        }

        // ================= 進める =================
        bool StillIn(SimCharacter c, LifeRun r) { return c != null && c.lifeEv == r && c.Present && !c.InPrivate; }

        void Step(LifeRun r, double now)
        {
            if (r.phase == LifeRun.Phase.Done) return;
            // 篤：命令で動いた・遠くへ行ったら、篤だけ外れる（できごとは続く）
            if (r.partnerIsF && r.partner != null)
            {
                if (r.fHeld && r.partner.HasOrder) { r.partner.lifeEv = null; r.fHeld = false; }
                if (!r.fHeld && r.phase == LifeRun.Phase.Active && !Near(r.partner, r.main, 8f)) r.choiceOpen = false;
            }
            if (r.fWatching)
            {
                var fw = w.RomanceTarget;
                if (fw == null || fw.HasOrder || fw.lifeEv != r) { if (fw != null && fw.lifeEv == r) fw.lifeEv = null; r.fWatching = false; }
            }
            bool okMain = StillIn(r.main, r);
            bool okPartner = r.partner == null || r.partnerIsF || StillIn(r.partner, r);
            r.extras.RemoveAll(c => !StillIn(c, r));   // ほかの子は抜けてもよい
            if (!okMain || !okPartner) { Cancel(r, "とちゅうでやめた"); return; }
            if (r.phase == LifeRun.Phase.Gather)
            {
                bool there = (!r.mainMoves || r.main.LifeArrived(r.pMain)) && (!r.partnerMoves || r.partner.LifeArrived(r.pPartner));
                if (there) Activate(r);
                else if (now - r.startAt > GatherLimit) Cancel(r, "集まれなかった");
                return;
            }
            // 最中
            if (r.def.choices.Length > 0 && r.chosen < 0 && !r.choiceOpen && FInvolved(r)) r.choiceOpen = true;
            if (r.choiceOpen && !FInvolved(r)) r.choiceOpen = false;
            if (r.choiceOpen && !r.partnerIsF && !r.fWatching)
            {
                // 見ている篤は、選択肢が出ている間その場に止める（自分の考えで歩き去らないように。命令で動かせば外れる）
                var fw = w.RomanceTarget;
                if (fw != null && fw.lifeEv == null && !fw.HasOrder && fw.LifeAvailableAsF) { fw.LifeStay(r); r.fWatching = true; }
            }
            if (r.fWatching && r.chosen >= 0) { var fw2 = w.RomanceTarget; if (fw2 != null && fw2.lifeEv == r) fw2.LifeRelease(false); r.fWatching = false; }
            HoldPoses(r);
            if (now >= r.nextTalk)
            {
                r.nextTalk = now + Random.Range(12f, 20f);
                float rv = Random.value;
                string role = r.extras.Count > 0 && rv < 0.3f ? "ほか" : (r.partner == null || r.partnerIsF || rv < 0.65f ? "主役" : "相手");
                Speak(r, "最中", role, 0f);
            }
            if (now >= r.until) Finish(r);
        }

        bool FInvolved(LifeRun r)
        {
            var f = w.RomanceTarget;
            if (f == null || !f.Present || f.InPrivate) return false;
            // 部屋のできごと（ひとりの時間など）は、篤がその部屋に入ったときだけ（2026-11-03）
            if ((r.def.place == "主役の部屋" || r.def.place == "相手の部屋") && r.area != null && w.LifeAreaAt(f.transform.position) != r.area) return false;
            if (r.partnerIsF) return Near(f, r.main, 8f);
            return Near(f, r.main, 6f);
        }

        void HoldPoses(LifeRun r)
        {
            Vector3 lookM = r.partner != null ? r.partner.transform.position : r.main.transform.position + r.main.transform.forward;
            r.main.LifeHold(r.def.scene, lookM, r.def.name);
            if (r.partner != null && !r.partnerIsF) r.partner.LifeHold(r.def.scene, r.main.transform.position, r.def.name);
            foreach (var c in r.extras) if (c.LifeArrived(c.transform.position) && !c.HasPath) c.LifeHold(r.def.scene, r.main.transform.position, r.def.name);
        }

        void Activate(LifeRun r)
        {
            r.phase = LifeRun.Phase.Active;
            r.until = w.Now + Mathf.Max(0f, r.def.length);
            r.nextTalk = w.Now + Random.Range(8f, 14f);
            HoldPoses(r);
            r.main.LifeGesture(r.def.gesture, 2.5f);
            Speak(r, "始まり", "主役", 0f);
            if (r.partner != null) Speak(r, "始まり", r.partnerIsF ? "篤" : "相手", 1.8f);
            if (r.extras.Count > 0) Speak(r, "始まり", "ほか", 3.4f);
            if (r.def.Instant) Finish(r);
        }

        /// <summary>篤の選択（画面のボタン・命令 life pick）。</summary>
        public void Choose(LifeRun r, int i)
        {
            if (r == null || r.phase != LifeRun.Phase.Active || r.chosen >= 0 || i < 0 || i >= r.def.choices.Length) return;
            r.chosen = i;
            r.choiceOpen = false;
            Apply(r, r.def.choiceResults[i]);
            // せりふの場面は、選択肢の言葉そのもの（例「手伝う」）か「選択1」のどちらでもよい
            string label = r.def.choices[i], num = "選択" + (i + 1);
            Speak(r, label, "篤", 0f, num);
            Speak(r, label, "主役", 1.8f, num);
            if (r.partner != null && !r.partnerIsF) Speak(r, label, "相手", 3.4f, num);
            if (r.pendingH != null) Finish(r);   // 二人きりの時間へ：ここで終わる
        }

        public LifeRun ChoiceRun { get { foreach (var r in runs) if (r.choiceOpen && r.chosen < 0) return r; return null; } }

        void Finish(LifeRun r)
        {
            if (r.phase == LifeRun.Phase.Done) return;
            r.phase = LifeRun.Phase.Done;
            Speak(r, "終わり", "主役", 0.6f);
            if (r.partner != null) Speak(r, "終わり", r.partnerIsF ? "篤" : "相手", 2.2f);
            Apply(r, r.def.result);
            string text = Fill(r.def.logText.Length > 0 ? r.def.logText : "{主役}：" + r.def.name, r, null);
            if (r.chosen >= 0) text += "（篤：" + r.def.choices[r.chosen] + "）";
            w.Log(text);
            AddRecent(text);
            Release(r);
            // 二人きりの時間へ（受けるかは今の決まりのまま：相手が望むときだけ）
            var f = w.RomanceTarget;
            if (r.pendingH != null && f != null && !r.main.IsSick && (r.main.state != SimCharacter.State.Sleeping || r.pendingH == "起こして誘う"))
            {
                if (r.pendingH == "エッチへ") r.main.LifeSeek(f);
                else if ((r.pendingH == "誘う" || r.pendingH == "起こして誘う") && (f.LifeAvailableAsF || (f.state == SimCharacter.State.Bathing && f.lifeEv == null && f.Present))) f.OrderInvite(r.main, null);   // 篤が入浴中でも誘える（2026-11-03）   // 寝ていれば、そばまで行って起こしてから聞く
            }
        }

        void Cancel(LifeRun r, string why)
        {
            if (r.phase == LifeRun.Phase.Done) return;
            r.phase = LifeRun.Phase.Done;
            AddRecent(r.def.name + "（" + r.Names + "）：" + why);
            Release(r);
        }

        void Release(LifeRun r)
        {
            bool night = w.MinuteOfDay >= 21.5f * 60f || w.MinuteOfDay < 5f * 60f;
            if (r.main.lifeEv == r) r.main.LifeRelease(r.bedMain || (r.mainWasAsleep && night));
            if (r.partner != null && r.partner.lifeEv == r) r.partner.LifeRelease(r.bedPartner || (r.partnerWasAsleep && night));
            foreach (var c in r.extras) if (c.lifeEv == r) c.LifeRelease(r.mainWasAsleep && night);
            var f = w.RomanceTarget;
            if (f != null && f.lifeEv == r) f.LifeRelease(false);
            r.fWatching = false;
            foreach (var kv in r.goOut) kv.Key.LifeGoOut(kv.Value, r.def.name);
            r.goOut.Clear();
        }

        void AddRecent(string s)
        {
            recent.Insert(0, "[" + w.Day + "日目 " + ScheduleMaker.Clock(w.Now) + "] " + s);
            if (recent.Count > 40) recent.RemoveAt(recent.Count - 1);
        }

        // ================= 結果 =================
        void Apply(LifeRun r, LifeEffect[] list)
        {
            if (list == null) return;
            var f = w.RomanceTarget;
            foreach (var e in list)
            {
                var who = new List<SimCharacter>();
                if (e.who == "主役" || e.who == "ふたり") who.Add(r.main);
                if ((e.who == "相手" || e.who == "ふたり") && r.partner != null) who.Add(r.partner);
                if (e.who == "篤" && f != null) who.Add(f);
                if (e.who == "みんな") { who.Add(r.main); if (r.partner != null) who.Add(r.partner); who.AddRange(r.extras); }
                if (e.who == "ほか") who.AddRange(r.extras);
                switch (e.kind)
                {
                    case "仲":
                        {
                            SimCharacter a = r.main, b = e.who == "篤" ? f : r.partner;
                            if (a != null && b != null && a != b) w.rel.Add(a.id, b.id, e.value, w.simTime);
                            continue;
                        }
                    case "評判": w.dormReputation += Mathf.RoundToInt(e.value); continue;
                    case "寮杯": HouseCup.Add(w, 0, Mathf.RoundToInt(e.value), r.def.name); continue;
                    case "銀貨":
                        if (w.player != null) { if (e.value > 0) w.player.Earn(Mathf.RoundToInt(e.value), r.def.name); else w.player.money = Mathf.Max(0, w.player.money + Mathf.RoundToInt(e.value)); }
                        continue;
                    case "エッチへ": case "誘う": case "起こして誘う": r.pendingH = e.kind; continue;
                }
                foreach (var c in who)
                {
                    switch (e.kind)
                    {
                        case "欲求": c.LifeAddNeed(LifeCond.NeedIndex(e.key), e.value); break;
                        case "状態": { StatusLine l; int st; if (LifeStatus.Find(e.text, out l, out st)) c.SetStage(l, st, true); break; }
                        case "治す":
                            {
                                StatusLine l; int st;
                                if (!LifeStatus.Find(e.text, out l, out st)) break;
                                if (l == StatusLine.Cold) c.HealCold(); else c.SetStage(l, 0, true);
                                break;
                            }
                        case "気持ち": if (!c.IsTarget) w.ChangeLove(c, e.value); break;
                        case "能力": c.GrowAbility(System.Array.IndexOf(Abilities.Names, e.key), e.value); break;
                        case "しるし": c.LifeSetFlag(e.text, e.value); break;
                        case "寝坊": c.LifeOversleep(e.value); break;
                        case "起こす": c.LifeWake(); break;
                        case "寝る": if (c == r.main) r.bedMain = true; else if (c == r.partner) r.bedPartner = true; break;
                        case "たまり": c.LifeAddHeat(e.value); break;
                        case "出かける": r.goOut.Add(new KeyValuePair<SimCharacter, float>(c, e.value)); break;
                    }
                }
            }
        }

        // ================= せりふ =================
        void Speak(LifeRun r, string scene, string role, float delay, string scene2 = null)
        {
            SimCharacter who = role == "主役" ? r.main : (role == "相手" ? r.partner : (role == "ほか" ? (r.extras.Count > 0 ? r.extras[Random.Range(0, r.extras.Count)] : null) : w.RomanceTarget));
            SimCharacter other = role == "主役" ? (r.partner ?? w.RomanceTarget) : r.main;
            if (who == null) return;
            string line = LifeBook.PickLine(r.def.group, scene, role, who, other, w);
            if (line == null && scene2 != null) line = LifeBook.PickLine(r.def.group, scene2, role, who, other, w);
            if (line == null) return;
            line = Fill(line, r, who);
            if (delay <= 0f) who.LifeSay(line, 2.4f);
            else StartCoroutine(SayLater(who, line, delay));
        }

        IEnumerator SayLater(SimCharacter who, string line, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (who != null) who.LifeSay(line, 2.4f);
        }

        string Fill(string s, LifeRun r, SimCharacter speaker)
        {
            var f = w.RomanceTarget;
            s = s.Replace("{主役}", r.main.displayName);
            s = s.Replace("{相手}", r.partner != null ? r.partner.displayName : "");
            s = s.Replace("{篤}", f != null ? f.displayName : "篤");
            if (s.Contains("{ほか}")) { var names = new List<string>(); foreach (var c in r.extras) names.Add(c.displayName); s = s.Replace("{ほか}", string.Join("・", names.ToArray())); }
            var p = speaker != null ? speaker.profile : r.main.profile;
            s = s.Replace("{呼び方}", p != null && !string.IsNullOrEmpty(p.callF) ? p.callF : "篤くん");
            return s;
        }

        // ================= 試験用（命令 life） =================
        public string Command(string[] a)
        {
            string sub = a.Length > 1 ? a[1] : "stat";
            switch (sub)
            {
                case "reload":
                    LifeBook.Load(true);
                    return "life reload: できごと " + LifeBook.Events.Count + "・せりふ " + LifeBook.Lines.Count + "・場所の役割 " + LifeBook.Roles.Count + "・直したほうがよい所 " + LifeBook.Errors.Count;
                case "check":
                    {
                        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/life_check.txt"));
                        System.IO.File.WriteAllText(path, LifeBook.Report(w), Encoding.UTF8);
                        return "life check: " + path + "（直したほうがよい所 " + LifeBook.Errors.Count + "）";
                    }
                case "run":
                    {
                        // life run ID [主役の記号] [相手の記号] [!]：時間・くじ・間隔を無視して始める（条件は見る。最後に ! で条件も見ない）
                        if (a.Length < 3) return "life run: ID がない";
                        string id = a[2].ToLowerInvariant();
                        var g = LifeBook.Events.FindAll(d => d.enabled && (d.group == id || d.id == id));
                        if (g.Count == 0) return "life run: " + id + " がない";
                        if (g.Exists(d => d.id == id) && !g.Exists(d => d.group == id)) g = g.FindAll(d => d.id == id);
                        SimCharacter mn = a.Length > 3 && a[3] != "!" ? w.characters.Find(c => c.letter == a[3].ToUpperInvariant()) : null;
                        SimCharacter pt = a.Length > 4 && a[4] != "!" ? w.characters.Find(c => c.letter == a[4].ToUpperInvariant()) : null;
                        string why;
                        bool skip = a[a.Length - 1] == "!";   // 最後に ! で、条件も見ない（試験用）
                        bool ok = TryStart(g, mn, pt, true, null, out why, skip);
                        var rr = ok ? runs[runs.Count - 1] : null;
                        return "life run " + id + ": " + (ok ? "始めた（" + rr.def.id + "・" + rr.Names + "・" + (rr.area != null ? rr.area.name : "?") + "）" : "始まらない（" + why + "）");
                    }
                case "pick":
                    {
                        var r = ChoiceRun;
                        if (r == null) return "life pick: 選択肢が出ていない";
                        int i = a.Length > 2 ? int.Parse(a[2]) - 1 : 0;
                        string label = i >= 0 && i < r.def.choices.Length ? r.def.choices[i] : "?";
                        Choose(r, i);
                        return "life pick: " + label;
                    }
                case "f":
                    {
                        // 試験用：篤を、動いているできごとの主役のそばへ歩かせる
                        var f = w.RomanceTarget;
                        if (f == null || runs.Count == 0) return "life f: できごとがない";
                        var r = runs[runs.Count - 1];
                        f.OrderMove(w.LifeNear(r.pMain, 1.5f));
                        return "life f: 篤が" + r.def.name + "の場所へ向かう";
                    }
                case "who":
                    {
                        // 試験用：life who 記号：その子の今のようす
                        var c = a.Length > 2 ? w.characters.Find(x => x.letter == a[2].ToUpperInvariant()) : null;
                        if (c == null) return "life who: 記号がない";
                        return "life who: " + c.displayName + " " + c.LifeDebugText;
                    }
                case "sleep":
                    {
                        // 試験用：life sleep 記号 [分]：その子を今すぐ自分のベッドで寝かせる
                        var c = a.Length > 2 ? w.characters.Find(x => x.letter == a[2].ToUpperInvariant()) : null;
                        if (c == null) return "life sleep: 記号がない";
                        float min = a.Length > 3 ? float.Parse(a[3], System.Globalization.CultureInfo.InvariantCulture) : 240f;
                        return "life sleep: " + c.displayName + " " + c.LifeTestSleep(min);
                    }
                case "promise":
                case "pretend":
                    {
                        // 試験用：life promise 記号＝「夜の約束」／life pretend 記号＝「寝たふりの約束」（1日）
                        var c = a.Length > 2 ? w.characters.Find(x => x.letter == a[2].ToUpperInvariant()) : null;
                        if (c == null) return "life " + sub + ": 記号がない";
                        string fl = sub == "pretend" ? SimCharacter.PretendFlag : SimCharacter.PromiseFlag;
                        c.LifeSetFlag(fl, 1f);
                        return "life " + sub + ": " + c.displayName + " に" + fl;
                    }
                case "lines":
                    {
                        string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/せりふ_書く所.csv"));
                        System.IO.File.WriteAllText(path, LifeBook.LineSlots(), new UTF8Encoding(true));
                        return "life lines: " + path;
                    }
                case "clear":
                    foreach (var r in runs) Cancel(r, "試験で止めた");
                    runs.Clear();
                    lastDay.Clear();
                    return "life clear: 止めて、間隔の記録も消した";
            }
            var sb = new StringBuilder("life stat: 動いている " + runs.Count + "\n");
            foreach (var r in runs)
            {
                sb.Append("  " + r.def.id + "・" + r.Names + "・" + r.phase + "・" + (r.area != null ? r.area.name : "?") + (r.choiceOpen ? "・選択肢あり" : "") + (r.chosen >= 0 ? "・選んだ " + r.def.choices[r.chosen] : "") + "\n");
                sb.Append("    主役 " + r.main.LifeDebug(r.pMain) + (r.partner != null ? "／相手 " + r.partner.LifeDebug(r.pPartner) : "") + "\n");
            }
            sb.Append("最近：\n");
            for (int i = 0; i < Mathf.Min(10, recent.Count); i++) sb.Append("  " + recent[i] + "\n");
            return sb.ToString();
        }
    }
}
