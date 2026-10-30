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
        // 2026-10-25 famuru「速く」：1回目のフィニッシュまで約30分（5＋10＋10＋5）、2回目からは約20分（4＋3＋10＋5。性欲70以上なら後半から）
        public static float StartMin = 5f, Mid1Min = 10f, Mid2Min = 10f, Mid3Min = 5f, FinishMin = 4f, ResumeMin = 3f, AfterMin = 20f;
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
        public static float RotateMin = 10f;              // 直接の相手を入れかえる間隔（2026-10-25 速くした。前は20）

        // 途中参加
        public static float JoinCheckMin = 10f;           // 加わる子を探す間隔（前は15）
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

        /// <summary>エッチの場面の今の様子（キャラの情報に出す）。場面の中でなければ null。</summary>
        public List<string> HStatusLines() { return hs != null ? hs.StatusLines(this) : null; }

        bool privateLie; // エッチの場面で、ベッドに寝転んでいる

        /// <summary>エッチの場面の中で、場所と姿勢を変える（ベッドに寝転ぶ／腰かける）。</summary>
        void PlaceInPrivate(Vector3 pos, Vector3 facing, bool floor, bool lie)
        {
            if (state != State.Private) return;
            transform.position = pos;
            if (facing.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(facing);
            privateOnFloor = floor;
            privateLie = lie;
            ApplyPose();
        }

        /// <summary>篤の精力：ふだんの回復（場面の中は HSession が動かす）。ヒロインは中出しの量が少しずつ減る。</summary>
        void UpdateVigor(float gm)
        {
            if (!IsTarget)
            {
                if (inside > 0f) inside = Mathf.Max(0f, inside - gm * HSkill.InsideDecayPerHour / 60f * HSkill.InsideDecayMul(HLevel));
                return;
            }
            if (hs != null) return;
            vigor = Mathf.Min(VigorMaxNow, vigor + gm * HBook.VigorRegenPerHour / 60f * (1f + desire / 100f));
        }

        /// <summary>エッチのスキルの段（0〜10。通算回数で決まる）。</summary>
        public int HLevel { get { return HSkill.Level(letter); } }
        /// <summary>篤の精力の最大値（スキルで増える）。</summary>
        public float VigorMaxNow { get { return HSkill.VigorMax(HLevel); } }

        /// <summary>中出しの量（ml。ヒロインだけ。時間で減る）。量に応じて能力すべてが上がる。</summary>
        public float inside;
        float InsideBonus { get { return IsTarget || inside <= 0f ? 0f : Mathf.Min(HSkill.InsideMaxBonus, Mathf.Floor(inside / HSkill.InsideMlPerAbility)); } }
        public int InsideBonusNow { get { return Mathf.RoundToInt(InsideBonus); } }

        /// <summary>性欲の増え方（ヒロインのスキル）。</summary>
        float HDesireMul() { return IsTarget ? 1f : HSkill.DesireRate(HLevel); }

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
            public float pleasure;   // 快感ゲージ（2026-10-25）
            public int climax;       // この場面での絶頂の回数
            public int drops;        // 絶頂で最大値が下がった回数（3回まで）
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
                Story("start", Direct != null ? Direct.c : null);
                Arrange();
            }

            // ---------------- 場面の流れの文（記録。HStory.cs） ----------------
            void Story(string ev, SimCharacter who, float ml = 0f)
            {
                var names = new List<string>();
                foreach (var p in ps) names.Add(p.c.displayName);
                var v = new Dictionary<string, string>
                {
                    { "名", who != null ? who.displayName : (names.Count > 0 ? names[0] : "") },
                    { "篤", f.displayName },
                    { "場所", spot != null ? spot.name : "" },
                    { "みんな", string.Join("・", names.ToArray()) },
                    { "回", finishes.ToString() },
                    { "ml", ml.ToString("0.0") },
                    { "合計", entry != null ? entry.mlTotal.ToString("0.0") : "0" },
                };
                HStory.Add(world, entry, ev, who != null ? who.letter : null, v);
            }

            // ---------------- ベッドの上の並び（2026-10-25 famuru：している子はベッドに寝転ぶ、順番待ちと休憩はベッドに腰かける） ----------------
            string arrangedSig;

            string RoleSig()
            {
                var sb = new System.Text.StringBuilder();
                foreach (var p in ps) sb.Append(p.c.id).Append((int)p.role).Append(',');
                return sb.ToString();
            }

            void Arrange()
            {
                arrangedSig = RoleSig();
                var room = spot != null ? spot.room : null;
                if (room == null || room.bed == null) return; // 個室のベッドのときだけ（ほかの場所は席に座ったまま）
                var lie = new List<SimCharacter> { f };
                var sit = new List<SimCharacter>();
                foreach (var p in ps)
                {
                    if (phase != "after" && (p.role == HRole.Direct || p.role == HRole.Pet)) lie.Add(p.c);
                    else if (phase == "after") lie.Add(p.c);
                    else sit.Add(p.c);
                }
                Vector3 along = room.bed.forward, edge = room.bedEdge;
                // 本番の家具のベッド（頭板は奥・足元の縁に座る）は、並ぶ向きと間かくを部屋の値で決める（家具 2026-10-26）
                bool fb = room.bedSeatOffs != null;
                Vector3 lieAx = room.bedAcross.sqrMagnitude > 0.01f ? room.bedAcross : edge;
                Vector3 sitAx = room.bedSitAlong.sqrMagnitude > 0.01f ? room.bedSitAlong : along;
                float width = 0.8f * Mathf.Clamp(room.bedSpread, 1f, 2f);
                float gap = fb ? Mathf.Min(room.bedLieGap, room.bedLieWidth / lie.Count) : Mathf.Min(0.42f, width / lie.Count);
                Vector3 center = room.bed.position - edge * (fb ? 0.15f : 0.08f);
                for (int i = 0; i < lie.Count; i++)
                {
                    Vector3 pos = center + lieAx * ((i - (lie.Count - 1) / 2f) * gap);
                    lie[i].PlaceInPrivate(pos, along, false, true);
                }
                // 腰かける：ベッドの縁（前のベッドは4人まで、本番の家具は座る所の数まで）、あふれた人はベッドの前の床
                int cap = fb ? room.bedSeatOffs.Length : 4;
                float[] offs = fb ? room.SeatOffs(Mathf.Min(sit.Count, cap)) : (sit.Count <= 2 ? new[] { -0.45f, 0.35f } : (sit.Count == 3 ? new[] { -0.55f, -0.05f, 0.45f } : new[] { -0.72f, -0.25f, 0.22f, 0.69f }));
                float mul = fb ? 1f : room.bedSpread;
                float[] o4 = fb ? room.bedSeatOffs : new[] { -0.72f, -0.25f, 0.22f, 0.69f };
                for (int i = 0; i < sit.Count; i++)
                {
                    if (i < cap)
                    {
                        Vector3 pos = room.bed.position + sitAx * (offs[Mathf.Min(i, offs.Length - 1)] * mul) + edge * (room.bedEdgeDist + 0.12f);
                        sit[i].PlaceInPrivate(pos, edge, false, false);
                    }
                    else
                    {
                        int row = (i - cap) / o4.Length, col = (i - cap) % o4.Length;
                        Vector3 pos = room.bed.position + sitAx * (o4[col] * mul) + edge * (room.bedEdgeDist + 0.85f + row * 0.6f);
                        sit[i].PlaceInPrivate(pos, -edge, true, false);
                    }
                }
            }

            // ---------------- 今の様子（キャラの情報の「ようす」に出す） ----------------
            static string PhaseName(string p)
            {
                switch (p)
                {
                    case "start": return "始まり";
                    case "mid1": return "前半";
                    case "mid2": return "中盤";
                    case "mid3": return "後半";
                    case "finish": return "フィニッシュ";
                    case "resume": return "ひと息";
                    case "after": return "終わったあと";
                    default: return p;
                }
            }

            static string RoleName(HRole r)
            {
                switch (r)
                {
                    case HRole.Direct: return "挿入";
                    case HRole.Pet: return "愛撫";
                    case HRole.Wait: return "参加";
                    default: return "休憩";
                }
            }

            public List<string> StatusLines(SimCharacter me)
            {
                var res = new List<string>();
                int min = Mathf.RoundToInt((float)(world.Now - startAt));
                res.Add("エッチ中：" + PhaseName(phase) + "　" + (spot != null ? spot.name : "") + "（" + (min / 60 > 0 ? (min / 60) + "時間" : "") + (min % 60) + "分）");
                var mine = ps.Find(p => p.c == me);
                if (mine != null) res.Add("今：" + RoleName(mine.role) + "　快感 " + Mathf.RoundToInt(mine.pleasure) + "／" + Mathf.RoundToInt(GaugeMax(mine)) + "・絶頂 " + mine.climax + "回" + (sick ? "（体調が悪いので軽めに）" : ""));
                // 役ごとに全員（挿入1人・愛撫3人まで・ほかは参加・休憩）。画面では折り返して出す
                foreach (var r in new[] { HRole.Direct, HRole.Pet, HRole.Wait, HRole.Rest })
                {
                    var parts = new List<string>();
                    foreach (var p in ps) if (p.role == r) parts.Add(p.c.displayName + "（快感" + Mathf.RoundToInt(p.pleasure) + "／" + Mathf.RoundToInt(GaugeMax(p)) + "・絶頂" + p.climax + "）");
                    if (parts.Count > 0) res.Add(RoleName(r) + "：" + string.Join("、", parts.ToArray()));
                }
                res.Add("篤：フィニッシュ " + finishes + "回・合計 " + (entry != null ? entry.mlTotal.ToString("0.0") : "0.0") + "ml・精力 " + Mathf.RoundToInt(f.vigor) + "／" + Mathf.RoundToInt(f.VigorMaxNow));
                if (joining.Count > 0)
                {
                    var jn = new List<string>();
                    foreach (var j in joining) jn.Add(j.displayName);
                    res.Add("向かっている：" + string.Join("・", jn.ToArray()));
                }
                return res;
            }

            void UpdateActionTexts()
            {
                string ph = PhaseName(phase);
                var d = Direct;
                foreach (var p in ps)
                    p.c.actionText = "エッチ中（" + ph + "・" + RoleName(p.role) + (p.role == HRole.Rest ? "" : "・快感 " + Mathf.RoundToInt(p.pleasure) + "／" + Mathf.RoundToInt(GaugeMax(p))) + "）";
                f.actionText = "エッチ中（" + ph + (d != null ? "・挿入：" + d.c.displayName : "") + "・" + (ps.Count + 1) + "人）";
            }

            // ---------------- 快感ゲージと絶頂（2026-10-25 famuru） ----------------
            /// <summary>快感ゲージの最大値（状態・欲を高める物・恋人・回数・この場面の絶頂・スキルで下がる。いちばん低くて10）。</summary>
            float GaugeMax(HP p)
            {
                var c = p.c;
                int de = c.GetStage(StatusLine.Desire);
                float m = de >= 3 ? 50f : (de == 2 ? 70f : (de == 1 ? 90f : 100f));
                float items = 0f;
                foreach (var a in c.mods)
                {
                    if (a.mod == null) continue;
                    float r, b;
                    bool up = (a.mod.rate.TryGetValue(NeedKind.Desire, out r) && r > 1f) || (a.mod.build.TryGetValue(NeedKind.Desire, out b) && b > 1f);
                    if (up) items += 5f;
                }
                m -= Mathf.Min(10f, items);
                if (c.IsLover) m -= 5f;
                if (HLog.TotalOf(c.letter) >= 5) m -= 5f;
                m -= 5f * Mathf.Min(3, p.drops);
                m -= HSkill.GaugeMaxDrop(c.HLevel);
                return Mathf.Max(HSkill.GaugeMin, m);
            }

            void UpdatePleasure(float gm)
            {
                if (phase == "after") return;
                float fSex = f.Ab(Abilities.Sex);
                bool anyone = false;
                foreach (var p in ps)
                {
                    float per, grow;
                    switch (p.role)
                    {
                        case HRole.Direct: per = HSkill.InsertPerMin; grow = HSkill.GrowInsertPerMin; break;
                        case HRole.Pet: per = HSkill.CaressPerMin; grow = HSkill.GrowCaressPerMin; break;
                        case HRole.Wait: per = HSkill.PettingPerMin; grow = HSkill.GrowPettingPerMin; break;
                        default: continue;
                    }
                    anyone = true;
                    var c = p.c;
                    float own = c.Ab(Abilities.Sex);
                    float mul = HSkill.SkillSpeed(own, fSex) * HSkill.GaugeSpeed(c.HLevel) * HSkill.PartnerSpeed(f.HLevel);
                    if (sick) mul *= 0.5f;
                    p.pleasure += gm * per * mul;
                    c.GrowAbility(Abilities.Sex, gm * grow);
                    float max = GaugeMax(p);
                    if (p.pleasure >= max)
                    {
                        p.pleasure = 0f;
                        p.climax++;
                        if (p.drops < 3) p.drops++;
                        HLog.AddClimax(entry, c.letter);
                        Story("climax", c);
                        Say(c, "climax");
                    }
                }
                if (anyone) f.GrowAbility(Abilities.Sex, gm * HSkill.GrowInsertPerMin);
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
                // ヒロインのスキル：篤の相手のとき、篤のフィニッシュまでの時間が短くなる
                var dd = Direct;
                if (dd != null && (p == "mid1" || p == "mid2" || p == "mid3" || p == "resume")) len *= HSkill.FinishTimeMul(dd.c.HLevel);
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
                if (RoleSig() != arrangedSig) Arrange(); // 順番の入れかえ・休憩・途中参加で並びを直す
                UpdateActionTexts(); // 「今：」の表示を今の様子にする（2026-10-25 famuru「更新されない」）

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
                var dd = Direct != null ? Direct.c : null;
                switch (phase)
                {
                    case "start": SetPhase("mid1"); Story("mid1", dd); break;
                    case "mid1": if (sick) DoFinish(); else { SetPhase("mid2"); Story("mid2", dd); } break;
                    case "mid2": SetPhase("mid3"); Story("mid3", dd); break;
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
                    float ml = HBook.Ml(f.vigor, f.desire, finishes) * HSkill.MlMul(f.HLevel);
                    HLog.AddFinish(entry, d.c.letter, ml);
                    d.c.inside += ml; // 中出しの量（能力が上がるバフ。時間で減る）
                    Story("finish", d.c, ml);
                }
                f.vigor = Mathf.Max(0f, f.vigor - HSkill.VigorPerFinish(f.HLevel));
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
                Story("rest", p.c);
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
                    if (next != null) { next.role = HRole.Direct; Say(next.c, "turn"); Story("turn", next.c); }
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
                Story("turn", next.c);
            }

            void UpdateGauges(float gm)
            {
                // 篤の精力：性欲が高いほど回復が速い
                f.vigor = Mathf.Min(f.VigorMaxNow, f.vigor + gm * HBook.VigorRegenPerHour / 60f * (1f + f.desire / 100f));
                UpdatePleasure(gm);
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
                            Story("back", c);
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
                Story("join", j);
                Arrange();
                world.Log("♥ " + j.displayName + "が加わった（" + spot.name + "）");
            }

            // ---------------- 終わり ----------------
            void GoAfter()
            {
                SetPhase("after");
                Story("after", Direct != null ? Direct.c : null);
                Arrange(); // 終わったあとは、みんなでベッドに寝転ぶ
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
                foreach (var c in all) c.GrowAbility(Abilities.Sex, HSkill.GrowPerSession); // 性技：1回ごとに伸びる
                foreach (var c in all)
                {
                    c.hs = null;
                    c.hsEnded = true;
                    c.hBaseConds = null;
                    if (c.state == State.Private) c.EndPrivate();
                    c.hsEnded = false;
                }
                Story("end", null);
                if (entry != null) world.Log("♥ エッチが終わった（フィニッシュ " + finishes + "回・合計 " + entry.mlTotal.ToString("0.0") + "ml）");
            }
        }
    }
}
