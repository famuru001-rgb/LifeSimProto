using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// エッチの場面の数字（すべて仮の値。遊んで確かめながら直す）。構成はプロジェクトの claude/二人きりの時間_構成.md（2026-10-23 サブ連携のチャット）。
    /// 時間はすべてゲーム内の分。
    /// </summary>
    public static class HBook
    {
        // 場面の長さ（分）
        public static float StartMin = 10f, Mid1Min = 20f, Mid2Min = 30f, Mid3Min = 20f, FinishMin = 4f, ResumeMin = 5f, AfterMin = 20f;
        public static float MaxSessionMin = 720f;         // どんな状態でも最長12時間
        public static float SickMaxMin = 120f;            // 風邪・発熱のとき（2人だけ）の最長
        public static float SickStartMin = 5f, SickMid1Min = 25f, SickAfterMin = 15f;
        public static float NoOneEngagedEndMin = 60f;     // 全員が休憩のまま、この分だけ戻らなければ終わる

        // 寮の子
        public static float FinishDesireDrop = 30f;       // フィニッシュで下がる性欲
        public static float DirectExtraDrop = 5f;         // フィニッシュのとき直接相手をしていた子は、さらに下がる
        public static float ContinueLine = 40f;           // 性欲がこれ以上なら続ける
        public static float LoveSickContinueLine = 25f;   // 恋の病のときの続けるライン
        public static float TiredPerHour = 15f;           // 続けている間にたまる疲れ（1時間あたり）
        public static float RestDesirePerHour = 12f;      // 休憩中に増える性欲（1時間あたり）
        public static float RestTiredPerHour = 20f;       // 休憩中に減る疲れ（1時間あたり）
        public static float RejoinTiredLine = 60f;        // 疲れがこれ以下なら、休憩から戻れる

        // 順番
        public static int MaxEngaged = 4;                 // 同時に相手をする人数（直接1人＋ペッティング3人）
        public static float RotateMin = 20f;              // 直接の相手を入れかえる間隔

        // 途中参加
        public static float JoinCheckMin = 15f;           // 加わる子を探す間隔
        public static float JoinChance = 0.5f;            // 1回の確率

        // 篤の精力
        public static float VigorMax = 100f;
        public static float VigorMinAtStart = 30f;        // 始めるとき、ここまで回復する
        public static float VigorPerFinish = 10f;         // フィニッシュ1回で減る
        public static float VigorRegenPerHour = 6f;       // 回復（1時間あたり。性欲が高いほど速い：×(1＋性欲÷100)）
        public static float VigorBoostLine = 60f;         // 精力がこれ以上なら性欲の増え方が速くなる
        public static float VigorBoostDesireMul = 1.5f;
        public static float AtsushiFinishDesireDrop = 15f;

        // 量（ml）＝基本 ×（0.5＋精力÷100）×（0.8＋性欲÷250）× 回目の倍率
        public static float MlBase = 5f;
        public static float[] MlByFinish = { 1f, 0.8f, 0.65f, 0.5f };

        public static float Ml(float vigor, float desire, int finishNo)
        {
            float m = MlByFinish[Mathf.Clamp(finishNo - 1, 0, MlByFinish.Length - 1)];
            return MlBase * (0.5f + vigor / 100f) * (0.8f + desire / 250f) * m;
        }
    }

    /// <summary>キャラの、エッチの場面（HSession）の部分。</summary>
    public partial class SimCharacter
    {
        /// <summary>篤の精力（篤だけが使う。0〜100）。</summary>
        public float vigor = 100f;

        HSession hs;              // 参加しているエッチの場面（なければ null）
        bool hsEnded;             // 場面の終わりで部屋を出た（EndPrivate で性欲を下げない）

        public bool InHSession { get { return hs != null; } }

        /// <summary>篤の精力：ふだんの回復（場面の中は HSession が動かす）。</summary>
        void UpdateVigor(float gm)
        {
            if (!IsTarget || hs != null) return;
            vigor = Mathf.Min(HBook.VigorMax, vigor + gm * HBook.VigorRegenPerHour / 60f * (1f + desire / 100f));
        }

        float VigorDesireMul() { return IsTarget && vigor >= HBook.VigorBoostLine ? HBook.VigorBoostDesireMul : 1f; }

        /// <summary>エッチの場面の吹き出しを1本作る（せりふのファイルがなければ今までのせりふ）。</summary>
        string HSay(string scene, int size, string other, bool sick = false)
        {
            string key = letter;
            // 新しい演出（2026-10-25）：最中の本文を持たない子は、最中を声で出す（区切りの場面だけせりふ）
            if (HLines.IsMid(scene) && HLines.Has(key) && !HLines.HasMidBody(key))
            {
                string v = HLines.Voice(key, scene);
                if (v != null) return v;
            }
            var conds = HConds(sick);
            string s = HLines.Build(key, scene, size, conds, other, profile != null ? profile.callF : null, letter == "A" ? 1 : -1, letter == "A", displayName);
            if (s != null) return s;
            // せりふのファイルがある子は、ない場面では今までのせりふを出さない（最中なら声、ほかは黙る）
            if (HLines.Has(key)) return HLines.IsMid(scene) || scene == "wait" || scene == "rest" ? HLines.Voice(key, "mid1") : null;
            // まだせりふがない子：今までのせりふ
            if (scene == "invite") return IsTarget ? "…いいか？" : "ねえ…エッチしよ？";
            if (scene == "move" || scene == "after" || scene == "wait" || scene == "rest") return null;
            string pl = size >= 3 && Random.value < 0.5f ? Pick("n_private_group") : null;
            return pl ?? Pick("n_private");
        }

        HashSet<string> hBaseConds;

        HashSet<string> HConds(bool sick)
        {
            var c = new HashSet<string>();
            if (hBaseConds != null) c.UnionWith(hBaseConds);
            if (!IsTarget)
            {
                int ls = LoveStage;
                c.Add(ls >= 3 ? "lover" : (ls >= 1 ? "rel1" : "rel0"));
                if (GetStage(StatusLine.Desire) >= 1) c.Add("lust");
                if (GetStage(StatusLine.Love) >= 1) c.Add("love");
                if (GetStage(StatusLine.Jealousy) >= 1) c.Add("jealous");
                if (GetStage(StatusLine.Lonely) >= 1) c.Add("lonely");
                if (GetStage(StatusLine.Sleep) >= 1) c.Add("sleepy");
                if (GetStage(StatusLine.MoodUp) >= 1) c.Add("up");
                if (GetStage(StatusLine.MoodDown) >= 1) c.Add("down");
                if (GetStage(StatusLine.Shy) >= 1) c.Add("shy");
                if (GetStage(StatusLine.Mana) >= 1) c.Add("mana");
            }
            if (sick) c.Add("sick");
            return c;
        }

        /// <summary>この場面で変わらない条件（初めて・魂・慣れ・今日また・別の子のあと）を、記録から決める。</summary>
        void SetHBaseConds(HLog.Entry current)
        {
            hBaseConds = new HashSet<string>();
            if (IsTarget || string.IsNullOrEmpty(letter)) return;
            int n = HLog.LoopCountOf(letter, Loop.Count); // この場面の分も入っている
            if (n <= 1)
            {
                bool before = false;
                foreach (var e in HLog.Entries) if (e.loop < Loop.Count && System.Array.IndexOf(e.letters, letter) >= 0) { before = true; break; }
                hBaseConds.Add(before ? "soul" : "first");
            }
            else if (n >= 5) hBaseConds.Add("used");
            string f = world.RomanceTarget != null ? world.RomanceTarget.letter : null;
            foreach (var e in HLog.Entries)
            {
                if (e == current || e.loop != Loop.Count || e.day != world.Day) continue;
                bool me = System.Array.IndexOf(e.letters, letter) >= 0;
                if (me) hBaseConds.Add("again");
                else if (f != null && System.Array.IndexOf(e.letters, f) >= 0) hBaseConds.Add("other");
            }
        }

        // ================= エッチの場面 =================
        enum HRole { Direct, Pet, Wait, Rest }

        class HP
        {
            public SimCharacter c;
            public HRole role;
            public double restSince;
        }

        /// <summary>
        /// 1回のエッチの場面。篤が続ける限り（精力が0になるまで・最長12時間）続く。
        /// 直接の相手1人＋ペッティング3人まで。ほかは順番を待つ。終わりの条件になった子は休憩（その場に残る）。途中参加もある。
        /// </summary>
        class HSession
        {
            readonly LoungeWorld world;
            public readonly SimCharacter f;
            readonly List<HP> ps = new List<HP>();
            readonly PrivateSpot spot;
            readonly HLog.Entry entry;
            readonly bool sick;
            readonly double startAt;
            string phase;
            double phaseUntil, nextRotate, nextJoin, noneSince = -1;
            int finishes;
            float speakTimer = 1.5f;
            readonly List<KeyValuePair<SimCharacter, string>> queue = new List<KeyValuePair<SimCharacter, string>>();
            float queueTimer;
            readonly List<SimCharacter> joining = new List<SimCharacter>();
            readonly Dictionary<SimCharacter, float> joinWait = new Dictionary<SimCharacter, float>();
            bool ended;

            public HSession(LoungeWorld w, SimCharacter atsushi, List<SimCharacter> who, PrivateSpot sp, HLog.Entry e)
            {
                world = w; f = atsushi; spot = sp; entry = e;
                startAt = w.Now;
                f.vigor = Mathf.Max(f.vigor, HBook.VigorMinAtStart);
                int girls = 0;
                foreach (var c in who)
                {
                    c.hs = this; c.hsEnded = false;
                    c.SetHBaseConds(e);
                    if (c.IsTarget) continue;
                    ps.Add(new HP { c = c, role = girls == 0 ? HRole.Direct : (girls < HBook.MaxEngaged ? HRole.Pet : HRole.Wait) });
                    girls++;
                }
                var g = ps.Count > 0 ? ps[0].c : null;
                sick = ps.Count == 1 && g != null && g.GetStage(StatusLine.Cold) >= 1;
                SetPhase("start");
                nextRotate = w.Now + HBook.RotateMin;
                nextJoin = w.Now + HBook.JoinCheckMin;
            }

            int Size { get { int n = 1; foreach (var p in ps) if (p.role != HRole.Rest) n++; return n; } }
            HP Direct { get { return ps.Find(p => p.role == HRole.Direct); } }
            int Engaged { get { int n = 0; foreach (var p in ps) if (p.role == HRole.Direct || p.role == HRole.Pet) n++; return n; } }

            string OtherName(SimCharacter me)
            {
                var c = new List<SimCharacter>();
                foreach (var p in ps) if (p.c != me) c.Add(p.c);
                return c.Count > 0 ? c[Random.Range(0, c.Count)].displayName : null;
            }

            void Say(SimCharacter c, string scene)
            {
                if (c == null) return;
                string s = c.HSay(scene, Size, OtherName(c), sick);
                if (!string.IsNullOrEmpty(s)) queue.Add(new KeyValuePair<SimCharacter, string>(c, s));
            }

            void SetPhase(string p)
            {
                phase = p;
                float len;
                switch (p)
                {
                    case "start": len = sick ? HBook.SickStartMin : HBook.StartMin; break;
                    case "mid1": len = sick ? HBook.SickMid1Min : HBook.Mid1Min; break;
                    case "mid2": len = HBook.Mid2Min; break;
                    case "mid3": len = HBook.Mid3Min; break;
                    case "finish": len = HBook.FinishMin; break;
                    case "resume": len = HBook.ResumeMin; break;
                    case "after": len = sick ? HBook.SickAfterMin : HBook.AfterMin; break;
                    default: len = 10f; break;
                }
                phaseUntil = world.Now + len;
            }

            string SceneOf(string p)
            {
                if (p == "finish") return "finish" + Mathf.Clamp(finishes, 1, 3);
                return p;
            }

            /// <summary>篤のフレームごと（篤の Update から呼ぶ）。</summary>
            public void Tick(float dt)
            {
                if (ended) return;
                float gm = dt * world.GameMinutesPerSecond();
                double now = world.Now;

                // 吹き出しの順番待ち（重ならないように少しずつ）
                queueTimer -= dt;
                if (queue.Count > 0 && queueTimer <= 0f)
                {
                    var q = queue[0]; queue.RemoveAt(0);
                    if (q.Key != null && q.Key.InPrivate) q.Key.ShowBubble(q.Value, 2.6f);
                    queueTimer = 1.3f;
                }

                UpdateGauges(gm);
                UpdateJoining(dt);

                if (phase == "after")
                {
                    if (now >= phaseUntil) End();
                    else Chatter(dt);
                    return;
                }

                // 最長の時間
                if (now - startAt >= (sick ? HBook.SickMaxMin : HBook.MaxSessionMin)) { GoAfter(); return; }

                // 全員が休憩のまま
                if (Engaged == 0)
                {
                    if (noneSince < 0) noneSince = now;
                    if (now - noneSince >= HBook.NoOneEngagedEndMin || ps.Count == 0) { GoAfter(); return; }
                    Chatter(dt);
                    return;
                }
                noneSince = -1;

                // 順番の入れかえ
                if (now >= nextRotate && phase != "finish") { Rotate(); nextRotate = now + HBook.RotateMin; }
                // 途中参加
                if (now >= nextJoin && !sick) { nextJoin = now + HBook.JoinCheckMin; TryJoin(); }

                if (now >= phaseUntil) NextPhase();
                Chatter(dt);
            }

            void NextPhase()
            {
                switch (phase)
                {
                    case "start": SetPhase("mid1"); break;
                    case "mid1": if (sick) DoFinish(); else SetPhase("mid2"); break;
                    case "mid2": SetPhase("mid3"); break;
                    case "mid3": DoFinish(); break;
                    case "finish": AfterFinish(); break;
                    case "resume":
                    {
                        var d = Direct;
                        SetPhase(d != null && d.c.desire >= 70f ? "mid3" : "mid2");
                        break;
                    }
                }
            }

            void DoFinish()
            {
                finishes++;
                var d = Direct;
                if (d != null)
                {
                    float ml = HBook.Ml(f.vigor, f.desire, finishes);
                    HLog.AddFinish(entry, d.c.letter, ml);
                }
                f.vigor = Mathf.Max(0f, f.vigor - HBook.VigorPerFinish);
                f.desire = Mathf.Max(0f, f.desire - HBook.AtsushiFinishDesireDrop);
                foreach (var p in ps)
                {
                    if (p.role != HRole.Direct && p.role != HRole.Pet) continue;
                    var c = p.c;
                    if (c.GetStage(StatusLine.Desire) >= 3) continue; // 発情：性欲が下がらない
                    float drop = HBook.FinishDesireDrop + (p.role == HRole.Direct ? HBook.DirectExtraDrop : 0f);
                    int de = c.GetStage(StatusLine.Desire);
                    if (de == 2) drop *= 0.5f; else if (de == 1) drop *= 0.75f;
                    if (c.GetStage(StatusLine.MoodDown) >= 1) drop *= 1.5f;
                    c.desire = Mathf.Max(0f, c.desire - drop);
                }
                SetPhase("finish");
                string sc = SceneOf("finish");
                if (d != null) Say(d.c, sc);
                if (Random.value < 0.6f) Say(f, sc);
            }

            void AfterFinish()
            {
                // 終わりの条件になった子は休憩へ
                foreach (var p in ps)
                {
                    if (p.role != HRole.Direct && p.role != HRole.Pet) continue;
                    if (!CanContinue(p.c)) ToRest(p);
                }
                if (sick || f.vigor <= 0f) { GoAfter(); return; }
                FillEngaged();
                if (Engaged == 0) { phase = "resume"; phaseUntil = world.Now + HBook.ResumeMin; return; }
                SetPhase("resume");
                var d = Direct;
                if (d != null) Say(d.c, "resume");
            }

            bool CanContinue(SimCharacter c)
            {
                if (c.tired >= 100f) return false;
                if (c.GetStage(StatusLine.Desire) >= 3) return true; // 発情：疲れが100になるまで
                float line = c.GetStage(StatusLine.Love) >= 2 ? HBook.LoveSickContinueLine : HBook.ContinueLine;
                return c.desire >= line;
            }

            void ToRest(HP p)
            {
                p.role = HRole.Rest;
                p.restSince = world.Now;
                Say(p.c, "rest_in");
                var see = ps.Find(x => x != p && (x.role == HRole.Direct || x.role == HRole.Pet));
                if (see != null && Random.value < 0.6f) Say(see.c, "rest_see");
            }

            /// <summary>直接の相手がいなければ決め、ペッティングの空きを待っている子で埋める。</summary>
            void FillEngaged()
            {
                if (Direct == null)
                {
                    var next = ps.Find(p => p.role == HRole.Wait) ?? ps.Find(p => p.role == HRole.Pet);
                    if (next != null) { next.role = HRole.Direct; Say(next.c, "turn"); }
                }
                foreach (var p in ps)
                {
                    if (Engaged >= HBook.MaxEngaged) break;
                    if (p.role == HRole.Wait) p.role = HRole.Pet;
                }
            }

            void Rotate()
            {
                var d = Direct;
                if (d == null) { FillEngaged(); return; }
                // 待っている子が優先。いなければペッティングしている子
                var next = ps.Find(p => p.role == HRole.Wait) ?? ps.Find(p => p.role == HRole.Pet);
                if (next == null) return;
                d.role = HRole.Pet;
                next.role = HRole.Direct;
                // 動かした子を列の最後へ（順番に回す）
                ps.Remove(d); ps.Add(d);
                if (Engaged > HBook.MaxEngaged) d.role = HRole.Wait;
                Say(next.c, "turn");
            }

            void UpdateGauges(float gm)
            {
                // 篤の精力：性欲が高いほど回復が速い
                f.vigor = Mathf.Min(HBook.VigorMax, f.vigor + gm * HBook.VigorRegenPerHour / 60f * (1f + f.desire / 100f));
                foreach (var p in ps)
                {
                    var c = p.c;
                    if (p.role == HRole.Rest)
                    {
                        c.desire = Mathf.Min(100f, c.desire + gm * HBook.RestDesirePerHour / 60f);
                        c.tired = Mathf.Max(0f, c.tired - gm * HBook.RestTiredPerHour / 60f);
                        if (phase != "after" && c.desire >= HBook.ContinueLine && c.tired <= HBook.RejoinTiredLine)
                        {
                            p.role = Engaged >= HBook.MaxEngaged ? HRole.Wait : HRole.Pet;
                            Say(c, "rest_back");
                            if (Direct == null) FillEngaged();
                        }
                        continue;
                    }
                    if (p.role == HRole.Wait || phase == "after") continue;
                    float mul = 1f;
                    if (c.GetStage(StatusLine.MoodUp) >= 3) mul = 0f;          // 絶好調：疲れがたまらない
                    else if (c.GetStage(StatusLine.MoodUp) == 2) mul = 0.7f;
                    else if (c.GetStage(StatusLine.MoodUp) == 1) mul = 0.85f;
                    if (c.GetStage(StatusLine.Sleep) >= 1) mul *= 1.5f;
                    if (c.GetStage(StatusLine.Mana) >= 2) mul *= 0.5f;
                    c.tired = Mathf.Min(100f, c.tired + gm * HBook.TiredPerHour / 60f * mul);
                    if (c.tired >= 100f && phase != "finish") ToRest(p); // 疲れきった子は、フィニッシュを待たずに休憩
                }
                if (Direct == null && Engaged > 0) FillEngaged();
            }

            /// <summary>ときどき、だれかが今の場面のせりふを言う。</summary>
            void Chatter(float dt)
            {
                speakTimer -= dt;
                if (speakTimer > 0f || queue.Count > 0) return;
                speakTimer = Random.Range(2.5f, 4.5f);
                var cands = new List<SimCharacter>();
                var ws = new List<float>();
                foreach (var p in ps)
                {
                    float w = p.role == HRole.Direct ? 4f : (p.role == HRole.Pet ? 2.5f : 1f);
                    cands.Add(p.c); ws.Add(w);
                }
                cands.Add(f); ws.Add(2f);
                float total = 0f; foreach (var w in ws) total += w;
                float r = Random.value * total;
                SimCharacter who = cands[cands.Count - 1];
                for (int i = 0; i < cands.Count; i++) { r -= ws[i]; if (r <= 0f) { who = cands[i]; break; } }
                if (who.bubble != null) return;
                string scene = SceneOf(phase);
                var hp = ps.Find(p => p.c == who);
                if (hp != null && phase != "after" && phase != "finish")
                {
                    if (hp.role == HRole.Rest) scene = "rest";
                    else if (hp.role == HRole.Wait) scene = "wait";
                    else if (hp.role == HRole.Pet && Random.value < 0.5f) scene = "pet";
                }
                Say(who, scene);
            }

            // ---------------- 途中参加 ----------------
            void TryJoin()
            {
                if (Random.value > HBook.JoinChance || ps.Count + 1 + joining.Count >= MaxPrivateGroup || phase == "after") return;
                bool night = world.MinuteOfDay >= 21f * 60f || LoungeWorld.IsNight(world.MinuteOfDay);
                var cands = new List<SimCharacter>();
                foreach (var c in world.characters)
                {
                    if (c.IsTarget || c.hs != null || joining.Contains(c) || !c.Present || c.privatePending || c.sickRest) continue;
                    if (!c.AvailableForTalk) continue;
                    if (c.desire < (night ? 55f : 99.9f)) continue;
                    cands.Add(c);
                }
                if (cands.Count == 0) return;
                var j = cands[Random.Range(0, cands.Count)];
                joining.Add(j);
                joinWait[j] = 0f;
                j.GoPrivate(spot.walkTo[Random.Range(0, spot.walkTo.Count)], spot.name);
            }

            void UpdateJoining(float dt)
            {
                for (int i = joining.Count - 1; i >= 0; i--)
                {
                    var j = joining[i];
                    joinWait[j] += dt;
                    if (ended || phase == "after" || joinWait[j] > 60f) { joining.RemoveAt(i); joinWait.Remove(j); j.CancelPrivateSelf(); continue; }
                    if (!j.privateArrived) continue;
                    joining.RemoveAt(i); joinWait.Remove(j);
                    Join(j);
                }
            }

            void Join(SimCharacter j)
            {
                var host = Direct != null ? Direct.c : (ps.Count > 0 ? ps[0].c : null);
                j.ShowBubble(j.HSay("join_find", Size + 1, host != null ? host.displayName : null) ?? "あ…", 2f);
                Say(j, "join_ask");
                // 入られる側との関係値が0のときだけ断る
                if (host != null && world.rel.Get(host.id, j.id) <= 0f)
                {
                    Say(host, "join_no");
                    j.CancelPrivateSelf();
                    return;
                }
                if (host != null) Say(host, "join_ok");
                Say(f, "join_atsushi");
                int n = ps.Count + 2;
                var spots = spot.Spots(n);
                var sp = spots[Mathf.Min(n - 1, spots.Count - 1)];
                j.privateGroupSize = n;
                j.EnterPrivate(double.MaxValue, spot.room, sp.pos, sp.facing, sp.floor);
                j.hs = this; j.hsEnded = false;
                HLog.AddMember(entry, j);
                j.SetHBaseConds(entry);
                ps.Add(new HP { c = j, role = Engaged >= HBook.MaxEngaged ? HRole.Wait : HRole.Pet });
                foreach (var p in ps) p.c.privateGroupSize = ps.Count + 1;
                f.privateGroupSize = ps.Count + 1;
                if (Direct == null) FillEngaged();
                Say(j, "join_in");
                world.Log("♥ " + j.displayName + "が加わった（" + spot.name + "）");
            }

            // ---------------- 終わり ----------------
            void GoAfter()
            {
                SetPhase("after");
                foreach (var p in ps) if (Random.value < 0.7f) Say(p.c, "after");
                Say(f, "after");
            }

            void End()
            {
                ended = true;
                foreach (var j in joining) j.CancelPrivateSelf();
                joining.Clear();
                var all = new List<SimCharacter>();
                foreach (var p in ps) all.Add(p.c);
                all.Add(f);
                foreach (var c in all)
                {
                    c.hs = null;
                    c.hsEnded = true;
                    c.hBaseConds = null;
                    if (c.state == State.Private) c.EndPrivate();
                    c.hsEnded = false;
                }
                if (entry != null) world.Log("♥ エッチが終わった（フィニッシュ " + finishes + "回・合計 " + entry.mlTotal.ToString("0.0") + "ml）");
            }
        }
    }
}
