using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 仮の人形1体。1日の予定と行事を優先し、空き時間は9つの欲求と状態異常で行動を選ぶ。
    /// 欲求：さみしさ・疲れ・退屈・空腹・勉強・趣味・恋心・睡眠・性欲（性欲は「好き」以上のみ。描写はしない）
    /// パラメータ：学力（課題の進み方・試験の成績）、魔力（たまると魔力過多・暴走）
    /// Fはプレイヤーが操作できる（指示がないときは自分で動く）。
    /// </summary>
    public class SimCharacter : MonoBehaviour
    {
        public enum State { Idle, Walking, Sitting, Activity, Approaching, Waiting, Talking, Eating, Bathing, Sleeping, Away, Private }
        public enum HobbyKind { Reading, Magic, Tea, Walk }
        enum Goal { None, Seat, Poi, Wander, Counter, DiningSeat, Bath, Bed, Nap, Exit, Return, Private, Gather, Hover, Follow, MoveTo }
        enum Act { None, Rest, Fun, Study, Hobby, Meal, Snack, Tea, Work }
        enum Intent { Talk, Love, Private, Bake, Give, Request, Heal }
        enum Choice { None, Social, Rest, Fun, Snack, Study, Hobby, Love, Nap, Private, Hover, Cling }
        enum AwayKind { Class, Stroll, Outing, Job }

        // ---- 設定 ----
        public int id;
        public string letter;       // 記号（A〜J）。モデル・絵のファイル名
        public string displayName;  // 名前
        public string shortName;    // 短い名前（関係表）
        public CharProfile profile;
        public string note;
        public Color color;
        [Range(0, 1)] public float sociability; // 人と話したい度
        [Range(0, 1)] public float whim;        // 気まぐれ度
        [Range(0, 1)] public float diligence;   // 真面目さ
        public HobbyKind hobby;
        public bool male;
        public RoomInfo room;

        // ---- 欲求（0〜100）----
        public float lonely, tired, bored, hunger, study, hobbyNeed, loveNeed, sleepy, desire;
        // ---- パラメータ ----
        public readonly float[] skill = new float[Academics.Count];      // 科目ごとの学力（0〜100）
        public readonly float[] weekSubject = new float[Academics.Count]; // 今週の科目ごとの勉強量
        public int studySubject = -1;  // 今勉強している科目
        public int assignSubject = -1; // 課題の科目
        double studyPickedAt = -1000;
        int mishapDay = -1;
        readonly List<int> classSubjects = new List<int>();
        bool inPlaceClass;      // 開いた場所（中庭・工房・温室）で実技の授業を受けている
        // 二人きりの時間：昼は性欲が最大のときだけ。相手を探しに行く途中で会った子（性欲80以上）がついて来る
        SimCharacter escortLeader;
        readonly List<SimCharacter> privateEscorts = new List<SimCharacter>();
        bool desireMaxAtNight;  // 夜に性欲が最大になった（朝に寝不足なら 50 に下がり、寝不足と発情になる）
        double classUntil;
        readonly List<int> classSlots = new List<int>();
        /// <summary>学力（取っている科目の平均）。</summary>
        public float academic { get { return AverageSkill(); } }
        public float mana;            // 魔力のたまり（0〜100。たまりすぎると暴走）
        public readonly float[] ability = new float[Abilities.Count]; // 能力値（体力・魅力・器用さ・魔力・知力）
        public float weekStudy;       // 今週の勉強量
        public bool hasAssignment;
        public float assignProgress;  // 課題の進み具合（0〜100）
        public double assignDeadline;

        // ---- 状態 ----
        public State state = State.Idle;
        public string actionText = "ぼんやりしている";
        public string bubble;
        float bubbleTimer;
        public bool selected;
        public Vector3 approachGoal;
        public readonly List<ActiveStatus> statuses = new List<ActiveStatus>();

        // ---- 予定・行事・用事 ----
        public List<ScheduleItem> schedule = new List<ScheduleItem>();
        int scheduleDay = -1;
        double sleepUntil, awayUntil, eatUntil, bathUntil, privateUntil, workUntil, lastFContact;
        bool mealPending, readingPick, snackPick, bakePending, privatePending, privateArrived, sickRest, napping, dozing, deepSleep, nightOwlLogged;
        AwayKind awayKind;
        string awayLabel;
        int classBlocks;
        ScheduleItem bedItem;
        RoomInfo privateRoom;
        Gathering gathering;
        Vector3 gatherFocus;
        SimCharacter clingTarget;
        float statusCheck, focusMin, highLonelyMin, highBoredMin;
        // 性欲のたまり（0〜100。画面には出さない。決まりは Buildup.cs）：たまりで［そわそわ・悶々・発情］が変わり、たまりが多いと性欲が下がりにくい
        readonly Buildup desireBuild = new Buildup(BuildupBook.Desire);
        public float Heat { get { return desireBuild.value; } }
        int taughtDay = -1;

        // ---- プレイヤーの指示（F）・お願い ----
        ItemKind giftItem;
        RequestKind requestKind;
        SimCharacter requestMeet;
        RequestKind pendingRequestKind;
        SimCharacter pendingRequestMeet;
        bool hasPendingRequest;
        float requestEffort = -1f;   // 0〜1。-1 はお願いではない

        LoungeWorld world;
        Transform visual;
        GameObject ring;
        Renderer[] renderers;
        Collider[] colliders;
        readonly List<Vector3> path = new List<Vector3>();
        Goal goal = Goal.None;
        Act act = Act.None;
        Choice choice = Choice.None;
        Intent intent = Intent.Talk;
        Seat seat;
        Poi poi;
        int poiSlot = -1;
        string sitLabel;
        float stateTime;
        float activityDuration;
        float thinkTimer;
        float repathTimer;
        float waitTimer;
        float approachTimer;
        float slotOffset;
        SimCharacter target;
        SimCharacter talkPartner;
        SimCharacter pendingOrder;
        bool hasOrder;
        float baseSpeed;
        // その場での会話・すれ違いのあいさつ
        bool chatting;
        SimCharacter chatPartner;
        float chatCooldown = 5f;
        float greetTimer;
        float chatTime;
        int chatToken;
        Vector3 destination;
        Vector3 progressPos;
        float progressTimer;
        int stuckCount;

        public SimCharacter TalkPartner { get { return talkPartner; } }
        public bool HasOrder { get { return hasOrder; } }
        /// <summary>寮の中にいる（外出していない）。部屋で過ごしている間も寮の中。</summary>
        public bool Present { get { return state != State.Away; } }
        public bool IsMoving { get { return (state == State.Walking || state == State.Approaching) && path.Count > 0; } }
        public bool IsSick { get { return sickRest; } }
        /// <summary>最後に篤のそばにいた時刻（応援が届いたかの目安）。</summary>
        public double LastFContact { get { return lastFContact; } }
        /// <summary>風邪を1段階軽くする（篤の回復魔法）。</summary>
        public void HealCold() { if (GetStage(StatusLine.Cold) > 0) Ease(StatusLine.Cold); tired = Mathf.Max(0f, tired - 10f); }
        public bool InPrivate { get { return state == State.Private; } }
        bool OnMission
        {
            get
            {
                return mealPending || snackPick || bakePending || privatePending || goal == Goal.Counter || goal == Goal.DiningSeat || goal == Goal.Bath
                    || goal == Goal.Bed || goal == Goal.Nap || goal == Goal.Exit || goal == Goal.Return || goal == Goal.Private || goal == Goal.MoveTo || act == Act.Work
                    || (inPlaceClass && world != null && world.Now < classUntil); // 場所での授業中
            }
        }

        // ---- 恋愛 ----
        public bool IsTarget { get { return world != null && world.romance != null && id == world.romance.targetId; } }
        public int LoveStage { get { return IsTarget ? -1 : world.romance.Stage(id); } }
        public bool IsLover { get { return !IsTarget && world.romance.lover[id]; } }

        /// <summary>話しかけられる状態か。</summary>
        public bool AvailableForTalk
        {
            get
            {
                if (!Present || state == State.Bathing || state == State.Talking || state == State.Private) return false;
                if (state == State.Sleeping && !sickRest) return false;
                if (OnMission || hasOrder) return false;
                return true;
            }
        }

        // ================= 状態異常 =================
        public int GetStage(StatusLine l)
        {
            foreach (var s in statuses) if (s.line == l) return s.stage;
            return 0;
        }

        bool Chance(float p) { return Random.value < p * world.statusChance; }

        /// <summary>状態の段階を決める。新しい系統は、同時に持てる数の上限を超えるとつかない（force なら上限を無視）。</summary>
        public bool SetStage(StatusLine l, int stage, bool force = false, bool quiet = false)
        {
            var def = StatusBook.Get(l);
            stage = Mathf.Clamp(stage, 0, def.MaxStage);
            ActiveStatus cur = null;
            foreach (var s in statuses) if (s.line == l) { cur = s; break; }
            if (stage == 0)
            {
                if (cur != null) statuses.Remove(cur);
                return true;
            }
            if (cur == null)
            {
                if (!force && statuses.Count >= Mathf.Max(1, world.maxStatuses)) return false;
                cur = new ActiveStatus { line = l };
                statuses.Add(cur);
            }
            bool changed = cur.stage != stage;
            int before = cur.stage;
            cur.stage = stage;
            float mins = def.minutes[Mathf.Clamp(stage, 1, def.minutes.Length) - 1];
            cur.until = mins > 0f ? world.Now + mins : 0;
            if (changed && !quiet)
            {
                string word = before == 0 ? "になった" : (stage > before ? "に悪化した" : "に軽くなった");
                if (def.polarity == StatusPolarity.Positive && stage > before) word = before == 0 ? "になった" : "に上がった";
                world.Log(displayName + "が［" + def.NameOf(stage) + "］" + word);
            }
            if (l == StatusLine.MoodUp && stage > 0) SetStage(StatusLine.MoodDown, 0, false, true);
            if (l == StatusLine.MoodDown && stage > 0) SetStage(StatusLine.MoodUp, 0, false, true);
            return true;
        }

        public void Escalate(StatusLine l, bool force = false) { SetStage(l, GetStage(l) + 1, force); }
        public void Ease(StatusLine l) { int s = GetStage(l); if (s > 0) SetStage(l, s - 1); }

        /// <summary>嫌なことがあった（落ち込みがつく／悪化する）。</summary>
        public void NegativeEvent(float p)
        {
            if (GetStage(StatusLine.MoodUp) > 0) { Ease(StatusLine.MoodUp); return; }
            int st = GetStage(StatusLine.MoodDown);
            if (st > 0) { if (Chance(Mathf.Min(1f, p + 0.2f))) Escalate(StatusLine.MoodDown); }
            else if (Chance(p)) SetStage(StatusLine.MoodDown, 1);
        }

        /// <summary>うれしいことがあった（ご機嫌がつく／上がる）。</summary>
        public void PositiveEvent(float p)
        {
            if (GetStage(StatusLine.MoodDown) > 0) { Ease(StatusLine.MoodDown); return; }
            if (Chance(p)) Escalate(StatusLine.MoodUp);
        }

        /// <summary>Fがほかの子と楽しそうに話しているのを見た（仲は悪くならない）。</summary>
        public void SawFWithOther(SimCharacter f, SimCharacter other)
        {
            if (IsTarget || LoveStage < 2) return;
            if (!CanSee(f) && !CanSee(other)) return;
            if (!Chance(0.4f)) return;
            Escalate(StatusLine.Jealousy);
            ShowBubble(GetStage(StatusLine.Jealousy) >= 2 ? "むぅ…！" : "むぅ…", 2f);
        }

        /// <summary>相手が視界に入っているか（同じ階・8m以内・正面から左右70度以内・間に壁がない）。</summary>
        public bool CanSee(SimCharacter o)
        {
            if (o == null || !o.Present || state == State.Sleeping || state == State.Private) return false;
            Vector3 d = o.transform.position - transform.position;
            if (Mathf.Abs(d.y) > 1f) return false;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist > 8f) return false;
            if (dist > 0.5f && Vector3.Angle(transform.forward, d) > 70f) return false;
            var nav = world.NavAt(transform.position);
            return WallFree(nav, transform.position, o.transform.position);
        }

        /// <summary>2点の間に壁がないか（家具は見通せるとして、部屋の外側＝通れないマスが長く続くと壁とみなす）。</summary>
        static bool WallFree(GridNav nav, Vector3 a, Vector3 b)
        {
            float dist = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
            int steps = Mathf.CeilToInt(dist / 0.2f);
            int blockedRun = 0;
            for (int i = 1; i < steps; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)steps);
                if (!nav.IsFree(p)) { blockedRun++; if (blockedRun * 0.2f > 1.4f) return false; }
                else blockedRun = 0;
            }
            return true;
        }

        IEnumerable<StageEffect> Effects()
        {
            foreach (var s in statuses) yield return StatusBook.Get(s.line).EffectOf(s.stage);
        }

        // ================= 欲求の補正（種族・衣装・装飾品・家具・アイテム。表は NeedMods.cs） =================
        readonly List<ActiveMod> mods = new List<ActiveMod>();

        /// <summary>今ついている補正（種族＋衣装・アイテムなど）。</summary>
        public IEnumerable<NeedMod> Mods()
        {
            string race = profile != null ? profile.race : null;
            NeedMod r = null;
            if (!string.IsNullOrEmpty(race) && LoungeWorld.IsNight(world.MinuteOfDay))
            {
                r = NeedBook.Race(race + "（夜）"); // 夜だけの種族補正（月の精霊など）。なければふつうの補正
                if (r != null && r.id != "race:" + race + "（夜）") r = null;
            }
            if (r == null) r = NeedBook.Race(race);
            if (r != null) yield return r;
            foreach (var a in mods) yield return a.mod;
        }

        /// <summary>補正をつける（minutes が 0 以下なら外すまで続く。同じ id は付け直し）。</summary>
        public void AddMod(NeedMod m, float minutes = 0f)
        {
            if (m == null) return;
            mods.RemoveAll(a => a.mod.id == m.id);
            mods.Add(new ActiveMod { mod = m, until = minutes > 0f ? world.Now + minutes : 0 });
        }

        public void RemoveMod(string id) { mods.RemoveAll(a => a.mod.id == id); }

        void ExpireMods()
        {
            double now = world.Now;
            mods.RemoveAll(a => a.until > 0 && now >= a.until);
        }

        float ModMul(NeedKind n, System.Func<NeedMod, Dictionary<NeedKind, float>> pick)
        {
            float m = 1f;
            foreach (var x in Mods()) m *= NeedMod.Get(pick(x), n);
            return m;
        }

        /// <summary>ゲージの増え方（状態異常 × 種族 × 衣装・アイテムなど）。</summary>
        float RateMul(NeedKind n)
        {
            float m = 1f;
            foreach (var e in Effects()) { float v; if (e.rate.TryGetValue(n, out v)) m *= v; }
            return m * ModMul(n, x => x.rate);
        }

        /// <summary>1コマ分のゲージの変化に、下がり方の補正と変動の大きさを掛ける。</summary>
        float NeedChange(NeedKind n, float before, float after)
        {
            float d = after - before;
            if (d < 0f) d *= ReliefMul(n);
            return before + d * NeedBook.Swing(n);
        }

        /// <summary>身につけている物・効いているアイテム（種族は入らない）。画面の表示用。</summary>
        public IEnumerable<ActiveMod> ItemMods { get { return mods; } }

        /// <summary>ゲージの下がり方（満たしたとき）。</summary>
        float ReliefMul(NeedKind n) { return ModMul(n, x => x.relief); }
        /// <summary>たまりのたまりやすさ。</summary>
        float BuildMul(NeedKind n) { return ModMul(n, x => x.build); }
        /// <summary>たまりの減りやすさ。</summary>
        float DrainMul(NeedKind n) { return ModMul(n, x => x.drain); }

        float Prod(System.Func<StageEffect, float> f)
        {
            float m = 1f;
            foreach (var e in Effects()) m *= f(e);
            return m;
        }

        public float TalkBonus()
        {
            float b = 0f;
            foreach (var e in Effects()) b += e.talkBonus;
            return b;
        }

        bool NoTalk()
        {
            foreach (var e in Effects()) if (e.noTalk) return true;
            return false;
        }

        public List<KeyValuePair<string, Color>> StatusTagList()
        {
            var l = new List<KeyValuePair<string, Color>>();
            foreach (var s in statuses)
            {
                var d = StatusBook.Get(s.line);
                l.Add(new KeyValuePair<string, Color>(d.NameOf(s.stage), d.Color));
            }
            return l;
        }

        /// <summary>時間切れ・発動条件の判定（ゲーム内30分ごと）。</summary>
        void UpdateStatuses(float gm)
        {
            double now = world.Now;
            for (int i = statuses.Count - 1; i >= 0; i--)
            {
                var s = statuses[i];
                if (s.until <= 0 || now < s.until) continue;
                var def = StatusBook.Get(s.line);
                if (def.decays && s.stage > 1) SetStage(s.line, s.stage - 1);
                else SetStage(s.line, 0, false, true);
            }

            statusCheck += gm;
            if (statusCheck < 30f) return;
            statusCheck = 0f;
            if (NightSleeping) return; // 夜に眠っているあいだは状態異常にかからない

            // 孤独
            highLonelyMin = lonely > 80f ? highLonelyMin + 30f : 0f;
            int lo = GetStage(StatusLine.Lonely);
            if (lo == 0 && highLonelyMin >= 90f && Chance(0.4f)) SetStage(StatusLine.Lonely, 1);
            else if (lo == 1 && lonely > 90f && highLonelyMin >= 180f && Chance(0.4f)) SetStage(StatusLine.Lonely, 2);
            if (lo > 0 && lonely < 30f) SetStage(StatusLine.Lonely, 0);

            // 鬱憤
            highBoredMin = bored > 85f ? highBoredMin + 30f : 0f;
            int fr = GetStage(StatusLine.Frustration);
            if (fr == 0 && highBoredMin >= 120f && Chance(0.35f)) SetStage(StatusLine.Frustration, 1);
            else if (fr == 1 && highBoredMin >= 240f && Chance(0.35f)) SetStage(StatusLine.Frustration, 2);
            else if (fr == 2 && highBoredMin >= 360f && CanActNow() && Chance(0.4f)) Explode();
            if (fr > 0 && bored < 40f) SetStage(StatusLine.Frustration, 0);

            // 恋（Fを「好き」以上の子だけ。きっかけは本人の気持ち）
            if (!IsTarget && LoveStage >= 2)
            {
                int lv = GetStage(StatusLine.Love);
                if (lv == 0 && loveNeed > 70f && Chance(0.3f)) SetStage(StatusLine.Love, 1);
                else if (lv == 1 && loveNeed > 85f && now - lastFContact > 120 && Chance(0.25f)) SetStage(StatusLine.Love, 2);
                if (lv > 0 && loveNeed < 20f) Ease(StatusLine.Love);
            }

            // 風邪の悪化
            if (GetStage(StatusLine.Cold) == 1 && tired > 80f && CanActNow() && Chance(0.25f)) StartFever();

            // 魔力（量で段階が決まる）
            int target = mana >= 95f ? 3 : (mana >= 80f ? 2 : (mana >= 60f ? 1 : 0));
            int ms = GetStage(StatusLine.Mana);
            if (target != ms && GetStage(StatusLine.ManaOut) == 0)
            {
                if (target == 3 && Present && state != State.Sleeping && state != State.Private)
                {
                    // 魔力が強い子は、半分くらいはうまく逃がせる（使いこなせる）
                    if (Ab(Abilities.Magic) >= 70f && Random.value < 0.5f)
                    {
                        mana = 60f;
                        ShowBubble("✦（ふう、逃がせた）", 1.6f);
                        SetStage(StatusLine.Mana, 1, false, true);
                        GrowAbility(Abilities.Magic, 0.5f);
                    }
                    else ManaBurst();
                }
                else if (target < 3) SetStage(StatusLine.Mana, target, false, target < ms);
            }

            // 集中は続けている間だけ
            bool working = (state == State.Sitting || state == State.Activity) && (act == Act.Study || act == Act.Hobby);
            if (!working) { focusMin = 0f; if (GetStage(StatusLine.Focus) > 0) SetStage(StatusLine.Focus, 0, false, true); }
            else
            {
                int fo = GetStage(StatusLine.Focus);
                if (fo == 0 && focusMin >= 20f && Chance(0.5f)) SetStage(StatusLine.Focus, 1);
                else if (fo == 1 && focusMin >= 40f && Chance(0.4f)) SetStage(StatusLine.Focus, 2);
                else if (fo == 2 && focusMin >= 60f && Chance(0.25f)) SetStage(StatusLine.Focus, 3);
            }

            // 課題の締め切り
            if (hasAssignment && now >= assignDeadline)
            {
                hasAssignment = false;
                if (assignProgress < 100f)
                {
                    world.Log(displayName + "は課題が間に合わなかった（" + Mathf.RoundToInt(assignProgress) + "%）");
                    AddSkill(assignSubject, -2f, false);
                    NegativeEvent(0.9f);
                }
            }
        }

        /// <summary>会話・外出・入浴・睡眠・部屋で過ごしている最中でなければ true。</summary>
        bool CanActNow()
        {
            return state == State.Idle || state == State.Walking || state == State.Sitting || state == State.Activity || state == State.Waiting;
        }

        void Explode()
        {
            SetStage(StatusLine.Frustration, 3, true);
            ShowBubble("もう無理っ！", 2f);
            world.Log(displayName + "の鬱憤が爆発！ 寮を飛び出していった");
            bored = 0f;
            SetStage(StatusLine.Frustration, 0, false, true);
            awayUntil = world.Now + 40;
            awayKind = AwayKind.Stroll;
            awayLabel = "飛び出していった";
            StartLeave("寮を飛び出す");
        }

        void ManaBurst()
        {
            ShowBubble("ボンッ！", 1.8f);
            world.Log("★ " + displayName + "の魔力が暴走した！");
            foreach (var o in world.characters)
            {
                if (o == this || !o.Present) continue;
                if ((o.transform.position - transform.position).sqrMagnitude < 25f) { o.ShowBubble("わっ！", 1.5f); o.bored = Mathf.Max(0f, o.bored - 20f); }
            }
            if (state != State.Talking && state != State.Eating && state != State.Bathing && LoungeWorld.OnFloorLevel(transform.position))
            {
                LeaveCurrentSpot();
                var area = world.AreaAt(transform.position);
                Vector3 p = area != null ? world.NavAt(transform.position).RandomFreePoint(area.rect) : transform.position;
                transform.position = p;
                SetState(State.Idle);
                actionText = "目を回している";
            }
            mana = 0f;
            SetStage(StatusLine.Mana, 0, false, true);
            SetStage(StatusLine.ManaOut, 1, true);
        }

        void StartFever()
        {
            SetStage(StatusLine.Cold, 2, true);
            world.Log(displayName + "が熱を出して寝込んだ");
            if (room == null) return;
            LeaveCurrentSpot();
            sickRest = true;
            seat = room.bed;
            seat.occupant = this;
            goal = Goal.Nap;
            actionText = "熱で部屋に戻る";
            if (!GoTo(seat.approach, State.Walking)) { SnapToSeat(); SetState(State.Sleeping); }
        }

        // ================= 初期化 =================
        public void Init(LoungeWorld w, Transform visualRoot, GameObject selectionRing)
        {
            world = w;
            visual = visualRoot;
            ring = selectionRing;
            // 表示を切り替える対象は、今表示している部品だけ（仮モデルがあるときは仮の人形は出さない）
            var rl = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true)) if (r.enabled) rl.Add(r);
            renderers = rl.ToArray();
            colliders = GetComponentsInChildren<Collider>(true);
            baseSpeed = male ? 1.35f : 1.2f;
            lonely = Random.Range(10f, 40f);
            tired = Random.Range(0f, 20f);
            bored = Random.Range(10f, 40f);
            hunger = Random.Range(40f, 60f);
            study = Random.Range(10f, 40f);
            hobbyNeed = Random.Range(10f, 40f);
            loveNeed = Random.Range(0f, 30f);
            InitSkills();
            mana = Random.Range(10f, 40f);
            thinkTimer = Random.Range(0.2f, 1.5f);
            lastFContact = -1000;

            EnsureSchedule();
            if (room != null)
            {
                seat = room.bed;
                seat.occupant = this;
                transform.position = seat.position;
                transform.rotation = Quaternion.LookRotation(seat.forward);
                sleepUntil = ScheduleMaker.WakeTime(id, world.Day, world.holidayWeekdays);
                if (sleepUntil < world.Now) sleepUntil = world.Now + 1;
                actionText = "寝ている";
                SetState(State.Sleeping);
            }
            else
            {
                transform.position = world.NearestFree(new Vector3(0f, 0f, -2f));
                SetState(State.Idle);
            }
        }

        /// <summary>歩く・会いに行くの1回の動きの最大の長さ（秒）。速い速度（30倍・60倍など）では、1コマの歩きを細かく分ける（すり抜け・行き過ぎを防ぐ）。欲求や考える処理は1コマに1回。</summary>
        const float MaxMoveStep = 0.15f;

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            stateTime += dt;

            UpdateNeeds(dt);
            if (Present) Watchdog(dt);
            if (Present)
            {
                int n = Mathf.Clamp(Mathf.CeilToInt(dt / MaxMoveStep), 1, 24);
                float sdt = dt / n;
                for (int i = 0; i < n; i++)
                {
                    if (state == State.Walking || state == State.Approaching) { Move(sdt); CheckStuck(sdt); }
                    if (state == State.Approaching) UpdateApproach(sdt);
                    else if (state == State.Waiting) UpdateWaiting(sdt);
                    else if (state != State.Walking) break;
                }
                if (state == State.Talking && talkPartner != null) FaceTowards(talkPartner.transform.position, dt);
                if (state == State.Activity && poi != null) FaceTowards(poi.lookAt, dt);
                if (state == State.Activity && poi == null && gathering != null) FaceTowards(gatherFocus, dt);
                if (state == State.Activity && goal == Goal.None && choice == Choice.Hover && world.RomanceTarget != null) FaceTowards(world.RomanceTarget.transform.position, dt);
                if (bubble == null && state != State.Sleeping && state != State.Private) IdleBubbles(dt);
                // 二人きりの時間：寄り添って話す（描写はしない）
                else if (bubble == null && state == State.Private && Random.value < dt * 0.04f)
                {
                    string pl = privateGroupSize >= 3 && Random.value < 0.5f ? Pick("n_private_group") : null; // 3人以上のとき
                    if (pl == null) pl = Pick("n_private");
                    if (pl != null) ShowBubble(pl, 2.2f);
                }
                if (!chatting && (state == State.Eating || state == State.Bathing || state == State.Sitting))
                {
                    chatCooldown -= dt;
                    if (chatCooldown <= 0f) { chatCooldown = Random.Range(6f, 14f); TryAmbientChat(); }
                }
                if (chatting) { chatTime += dt; if (chatTime > 20f) EndChat(); } // 念のための安全装置
                greetTimer -= dt;
                if (greetTimer <= 0f) { greetTimer = 0.4f; TryGreet(); }
            }

            thinkTimer -= dt;
            if (thinkTimer <= 0f)
            {
                thinkTimer = 0.6f + Random.value * 0.6f;
                Think();
            }

            if (bubbleTimer > 0f)
            {
                bubbleTimer -= dt;
                if (bubbleTimer <= 0f) bubble = null;
            }

            if (ring != null)
            {
                bool show = selected && Present;
                if (ring.activeSelf != show) ring.SetActive(show);
            }
        }

        /// <summary>状態や作業が、行動や吹き出しでそれとなくわかるようにする。</summary>
        /// <summary>考えていること（吹き出しに（）で出す）。いちばん強い気持ちを1つ。なければ null。</summary>
        string Thought()
        {
            var cands = new List<KeyValuePair<float, string>>();
            if (state == State.Approaching && target != null) cands.Add(new KeyValuePair<float, string>(60f, "（" + target.displayName + "と話したいな）"));
            if (hunger > 60f) cands.Add(new KeyValuePair<float, string>(hunger, "（おなかすいたな…）"));
            if (sleepy > 70f) cands.Add(new KeyValuePair<float, string>(sleepy, "（眠い…）"));
            if (tired > 65f) cands.Add(new KeyValuePair<float, string>(tired, "（ちょっと休みたい…）"));
            if (lonely > 65f) cands.Add(new KeyValuePair<float, string>(lonely, "（だれかと話したいな）"));
            if (bored > 65f) cands.Add(new KeyValuePair<float, string>(bored, "（退屈…なにかしたいな）"));
            if (study > 70f) cands.Add(new KeyValuePair<float, string>(study, "（勉強しなきゃ）"));
            if (hasAssignment && assignProgress < 100f && AssignmentUrgency() > 0.5f) cands.Add(new KeyValuePair<float, string>(75f, "（課題、終わらせなきゃ…）"));
            if (hobbyNeed > 65f && profile != null) cands.Add(new KeyValuePair<float, string>(hobbyNeed, "（" + profile.hobbyText + "がしたいな）"));
            if (!IsTarget && LoveStage >= 1 && loveNeed > 60f) cands.Add(new KeyValuePair<float, string>(loveNeed, "（篤に会いたいな…）"));
            if (desire >= 70f) cands.Add(new KeyValuePair<float, string>(desire, IsTarget ? "（エッチしたいな…）" : "（エッチしたい…）"));
            if (cands.Count == 0) return null;
            cands.Sort((a, b) => b.Key.CompareTo(a.Key));
            return cands[Random.value < 0.7f || cands.Count == 1 ? 0 : 1].Value; // たまに2番目の気持ち
        }

        const float ThoughtRate = 0.04f; // 考えていることが出る頻度（1秒あたり）

        void IdleBubbles(float dt)
        {
            if (Random.value < dt * ThoughtRate)
            {
                string th = Thought();
                if (th != null) { ShowBubble(th, 1.8f); return; }
            }
            float r = Random.value;
            if (state == State.Activity && act == Act.Hobby && hobby == HobbyKind.Magic && r < dt * 0.4f) ShowBubble("✦", 1.0f);
            else if (GetStage(StatusLine.Mana) >= 2 && r < dt * 0.2f) ShowBubble("（ぱちぱち）", 1.2f);
            else if (GetStage(StatusLine.Desire) >= 3 && r < dt * 0.1f) ShowBubble(LoveStage >= 2 ? "…はぁ♡" : "…はぁ", 1.4f);
            else if (GetStage(StatusLine.Love) >= 1 && r < dt * 0.1f) ShowBubble("…♡", 1.4f);
            else if (GetStage(StatusLine.Sleep) >= 1 && r < dt * 0.08f) ShowBubble(Say("sleepy", "ふぁ…"), 1.2f);
            else if (GetStage(StatusLine.MoodDown) >= 2 && r < dt * 0.06f) ShowBubble(Say("sigh", "はぁ…"), 1.4f);
            else if (GetStage(StatusLine.Frustration) >= 1 && r < dt * 0.06f) ShowBubble(Say("frustr", "むしゃくしゃ…"), 1.4f);
            else if (act == Act.Study && hasAssignment && assignProgress < 100f && r < dt * 0.08f)
                ShowBubble(assignProgress < 50f && (assignDeadline - world.Now) < 1440 ? "終わらない…！" : Say("busy", "課題、課題…"), 1.4f);
            else if (HasTrait(CharTrait.WinterWeak) && Calendar.Season(world.Day) == 3 && r < dt * 0.03f) ShowBubble(Say("cold", "寒い…"), 1.4f);
            else if (HasTrait(CharTrait.FullMoon) && Calendar.IsFullMoon(world.Day) && LoungeWorld.IsNight(world.MinuteOfDay) == false && world.MinuteOfDay >= 1080f && r < dt * 0.05f) ShowBubble(Say("moon", "そわそわ…"), 1.4f);
            else if (HasTrait(CharTrait.RainWings) && Calendar.WeatherOf(world.Day) == Weather.Rain && r < dt * 0.03f) ShowBubble(Say("rain", "翼が重い…"), 1.4f);
            else if (r < dt * 0.02f && state != State.Talking && !chatting)
            {
                string line = AmbientLine();
                if (line != null) ShowBubble(line, 1.8f);
            }
        }

        // ================= 欲求 =================
        // 疲れ・空腹の増え方（famuru 決定 2026-10-21：立ち・歩きはゆっくり、勉強・趣味で疲れる。空腹は3食とおやつで足りる速さ）
        const float TiredWalk = 0.35f;       // 歩いている（前は 0.9）
        const float TiredStand = 0.12f;      // 立っている（前は 0.35）
        const float TiredStudySit = 0.3f;    // 座って勉強・趣味（前は座ると休めた）
        const float TiredStudyStand = 0.55f; // 立って勉強・趣味・実技の練習（前は 0.3）
        const float HungerPerMin = 0.16f;    // 空腹（ゲーム内1分あたり。前は 0.2）
        const float HungerSleepMul = 0.3f;   // 夜に眠っている間の空腹の増え方

        void UpdateNeeds(float dt)
        {
            float gm = dt * world.GameMinutesPerSecond(); // 進んだゲーム内の分
            UpdateStatuses(gm);
            // ゲージの変動（下がる分は補正の「下がり方」を掛け、全体に NeedBook.Swing を掛ける。さみしさ・退屈・勉強・趣味はゆっくり動く）
            float lonely0 = lonely, bored0 = bored, study0 = study, hobby0 = hobbyNeed, tired0 = tired, hunger0 = hunger, love0 = loveNeed, sleepy0 = sleepy;

            // 空腹：3食とおやつで足りる速さ（食事を抜かなければ 80 を超えない。眠っている間はゆっくり。2026-10-21）
            hunger += gm * HungerPerMin * (state == State.Sleeping && !napping ? HungerSleepMul : 1f) * RateMul(NeedKind.Hunger);
            study += gm * 0.05f * (0.5f + diligence) * RateMul(NeedKind.Study);
            hobbyNeed += gm * 0.09f * RateMul(NeedKind.Hobby);
            sleepy += gm * 0.075f * RateMul(NeedKind.Sleepy);
            if (!IsTarget && LoveStage >= 1) loveNeed += gm * 0.05f * (0.5f + world.romance.love[id] / 50f) * RateMul(NeedKind.Love);
            ExpireMods();
            bool studyingNow = ((state == State.Sitting || state == State.Activity) && act == Act.Study) || (state == State.Away && awayKind == AwayKind.Class) || inPlaceClass;
            desire += gm * DesireRate() * (studyingNow ? 2f : 1f); // 勉強中はたまりやすい
            if (GetStage(StatusLine.ManaOut) == 0) mana += gm * (hobby == HobbyKind.Magic ? 0.035f : 0.02f) * (0.5f + Ab(Abilities.Magic) / 100f); // 魔力が強い子ほどたまりやすい

            float lonelyMul = RateMul(NeedKind.Lonely), tiredMul = RateMul(NeedKind.Tired), boredMul = RateMul(NeedKind.Bored);
            float desireRelief = 0f, buildDrain = 0f; // 性欲を下げる量・性欲のたまりを減らす量（1分あたり）
            // 種族などの特徴
            if (HasTrait(CharTrait.Lonely)) lonelyMul *= 1.4f;
            tiredMul *= Mathf.Clamp(1.25f - Ab(Abilities.Body) / 200f, 0.7f, 1.3f); // 体力があるほど疲れにくい
            GrowFromActivity(dt, gm);
            if (HasTrait(CharTrait.WinterWeak) && Calendar.Season(world.Day) == 3) tiredMul *= 1.3f;
            if (HasTrait(CharTrait.RainWings) && Calendar.WeatherOf(world.Day) == Weather.Rain) tiredMul *= 1.3f;
            if (HasTrait(CharTrait.FullMoon) && Calendar.IsFullMoon(world.Day)) boredMul *= 1.5f;
            switch (state)
            {
                case State.Away:
                    if (awayKind == AwayKind.Stroll) { hobbyNeed -= gm * 2.5f; bored -= gm * 2f; }
                    else if (awayKind == AwayKind.Outing) { bored -= gm * 2f; lonely -= gm * 1f; hobbyNeed -= gm * 0.5f; }
                    else if (awayKind == AwayKind.Job) { tired += gm * 0.15f; bored += gm * 0.05f; }
                    else { study -= gm * 0.35f; weekStudy += gm * 0.35f; }
                    break;
                case State.Private:
                    desire -= gm * 4f * desireBuild.Relief * ReliefMul(NeedKind.Desire); lonely -= gm * 2f; loveNeed -= gm * 3f; lastFContact = world.Now; break;
                case State.Sleeping:
                {
                    float recover = 0.6f;
                    if (!napping && !sickRest && !deepSleep && LoungeWorld.IsNight(world.MinuteOfDay) && desire >= 60f) recover *= 0.25f;
                    if (deepSleep) recover *= 1.5f;
                    if (HasTrait(CharTrait.DeepSleeper)) recover *= 1.25f;
                    sleepy -= gm * recover; tired -= dt * 5f; bored -= dt * 2f; lonely += dt * 0.1f;
                    // 眠っても性欲のゲージは下がらない（たまりは、よく眠れると少し減る）
                    buildDrain += 0.08f * Mathf.Min(1f, recover / 0.6f);
                    break;
                }
                case State.Walking:
                case State.Approaching:
                    lonely += dt * 0.9f * (0.4f + sociability) * lonelyMul;
                    tired += dt * TiredWalk * tiredMul; bored -= dt * 0.3f; break;
                case State.Sitting:
                    lonely += dt * 0.9f * (0.4f + sociability) * lonelyMul;
                    // 座って休むと疲れが取れる。座って勉強・趣味をしているときは疲れる
                    if (act == Act.Study || act == Act.Hobby) tired += dt * TiredStudySit * tiredMul;
                    else tired -= dt * 3.5f;
                    if (act == Act.Study) Studying(dt, gm);
                    else if (act == Act.Hobby) { hobbyNeed -= dt * 3f; bored -= dt * 2f; focusMin += gm; desireRelief += 0.12f; buildDrain += 0.15f; }
                    else if (act == Act.Fun) { bored -= dt * 2.5f; lonely -= dt * 1.2f; }
                    else bored += dt * (0.5f + whim * 0.4f) * boredMul;
                    break;
                case State.Eating:
                    lonely += dt * 0.6f * (0.4f + sociability) * lonelyMul;
                    tired -= dt * 3f;
                    hunger -= gm * (act == Act.Tea ? 1.5f : 4f);
                    if (act == Act.Tea) { hobbyNeed -= dt * 3f; bored -= dt * 2f; }
                    break;
                case State.Bathing:
                    tired -= dt * 4f; bored -= dt * 2f;
                    desireRelief += 0.15f; buildDrain += 0.3f; // お風呂でさっぱりすると性欲も少し下がる
                    break;
                case State.Activity:
                    lonely += dt * 0.9f * (0.4f + sociability) * lonelyMul;
                    tired += dt * (act == Act.Study || act == Act.Hobby ? TiredStudyStand : 0.3f) * tiredMul;
                    if (act == Act.Study) Studying(dt, gm);
                    else if (act == Act.Hobby)
                    {
                        hobbyNeed -= dt * 3f; bored -= dt * 3f; focusMin += gm; desireRelief += 0.12f; buildDrain += 0.15f; // 趣味に打ち込むと性欲も下がる
                        if (hobby == HobbyKind.Magic && poi != null && poi.hobby >= 0) mana -= dt * 4f;
                    }
                    else if (act == Act.Work) { tired += dt * 0.5f; }
                    else { bored -= dt * 4f; if (gathering != null) lonely -= dt * 1.2f; }
                    break;
                case State.Talking:
                    if (seat != null) tired -= dt * 2f; else tired += dt * 0.2f;
                    bored -= dt * 1f; break;
                default:
                    lonely += dt * 0.9f * (0.4f + sociability) * lonelyMul;
                    tired += dt * TiredStand * tiredMul; bored += dt * (0.6f + whim * 0.4f) * boredMul; break;
            }

            // Fの近くにいると恋心が落ち着き、気持ちが少しずつ育つ
            var f = world.RomanceTarget;
            if (!IsTarget && f != null && f.Present && Present && state != State.Private)
            {
                Vector3 d = f.transform.position - transform.position;
                if (Mathf.Abs(d.y) < 1f && d.sqrMagnitude < 9f)
                {
                    loveNeed -= gm * 0.3f;
                    lastFContact = world.Now;
                    world.ChangeLove(this, gm * 0.015f * (0.6f + f.Ab(Abilities.Charm) / 125f) * (FocusOn(f) ? 1.3f : 1f)); // 篤の魅力で育ち方が変わる（篤のお守りで育ちやすい）
                }
            }

            lonely = NeedChange(NeedKind.Lonely, lonely0, lonely);
            bored = NeedChange(NeedKind.Bored, bored0, bored);
            study = NeedChange(NeedKind.Study, study0, study);
            hobbyNeed = NeedChange(NeedKind.Hobby, hobby0, hobbyNeed);
            tired = NeedChange(NeedKind.Tired, tired0, tired);
            hunger = NeedChange(NeedKind.Hunger, hunger0, hunger);
            loveNeed = NeedChange(NeedKind.Love, love0, loveNeed);
            sleepy = NeedChange(NeedKind.Sleepy, sleepy0, sleepy);
            lonely = Mathf.Clamp(lonely, 0f, 100f);
            tired = Mathf.Clamp(tired, 0f, 100f);
            bored = Mathf.Clamp(bored, 0f, 100f);
            hunger = Mathf.Clamp(hunger, 0f, 100f);
            study = Mathf.Clamp(study, 0f, 100f);
            hobbyNeed = Mathf.Clamp(hobbyNeed, 0f, 100f);
            loveNeed = Mathf.Clamp(loveNeed, 0f, 100f);
            sleepy = Mathf.Clamp(sleepy, 0f, 100f);
            desire = Mathf.Clamp(desire, 0f, 100f);
            if (desire >= 99.9f && PrivateNight) desireMaxAtNight = true;
            // 性欲：お風呂・睡眠・趣味で下がる（たまりが多いほど下がりにくい）
            desire = Mathf.Clamp(desire - gm * desireRelief * desireBuild.Relief * ReliefMul(NeedKind.Desire), 0f, 100f);
            // 性欲のたまり：性欲60以上でたまり、60より下だと少しずつ減る。二人きりの時間・お風呂・睡眠・趣味で減る。［発情］のあいだはたまりやすい
            if (state == State.Private) buildDrain += 1.5f;
            desireBuild.Tick(gm, desire, GetStage(StatusLine.Desire), buildDrain * DrainMul(NeedKind.Desire), BuildMul(NeedKind.Desire));
            int dst = desireBuild.StageFor(GetStage(StatusLine.Desire));
            if (dst != GetStage(StatusLine.Desire) && !NightSleeping) SetStage(StatusLine.Desire, dst, true); // 眠っているあいだは起きてから変わる
            mana = Mathf.Clamp(mana, 0f, 100f);
        }

        void Studying(float dt, float gm)
        {
            float eff = Prod(e => e.studyEfficiency) * (requestEffort >= 0f ? 0.7f + requestEffort * 0.6f : 1f);
            float amount = dt * 2.5f * eff;
            if (studySubject < 0 || world.Now - studyPickedAt > 120) PickStudySubject();
            int subj = hasAssignment && assignProgress < 100f && assignSubject >= 0 ? assignSubject : studySubject;
            study -= amount;
            weekStudy += amount * 0.4f;
            if (subj >= 0) weekSubject[subj] += amount * 0.4f;
            AddSkill(subj, amount * 0.006f);
            if (subj >= 0 && Academics.IsPractical(subj)) { GrowAbility(Abilities.Magic, amount * 0.0015f); if (subj == 8 || subj == 7) GrowAbility(Abilities.Body, amount * 0.001f); if (subj == 9) GrowAbility(Abilities.Dex, amount * 0.001f); }
            else GrowAbility(Abilities.Mind, amount * 0.0012f);
            focusMin += gm;
            bored += dt * 0.3f;
            if (hasAssignment && assignProgress < 100f)
            {
                assignProgress += amount * 0.45f * (0.5f + SkillOf(assignSubject) / 100f) * MindMul;
                if (assignProgress >= 100f)
                {
                    assignProgress = 100f;
                    AddSkill(assignSubject, 1f);
                    world.Log(displayName + "が課題（" + SubjectName(assignSubject) + "）を終わらせた");
                    ShowBubble(Say("done", "終わった〜！"), 1.8f);
                    PositiveEvent(0.3f);
                }
            }
            // 実技の自主練習は、たまにちょっとした失敗をする（1日1回まで）
            float clumsy = Mathf.Clamp(1.5f - Ab(Abilities.Dex) / 100f, 0.5f, 1.5f); // 器用な子ほど失敗しにくい
            if (inPlaceClass) clumsy *= 0.3f; // 授業中は先生が見ているので失敗は少なめ
            if (subj >= 0 && Academics.IsPractical(subj) && mishapDay != world.Day && Random.value < dt * 0.012f * clumsy * (profile != null && profile.IsWeak(subj) ? 2f : 1f))
                PracticeMishap(subj);
            // 近くで課題をしている子に教える（その科目が得意な子・学力が高い子ほど）
            foreach (var o in world.characters)
            {
                if (o == this || !o.hasAssignment || o.assignProgress >= 100f || o.act != Act.Study) continue;
                if (o.state != State.Sitting && o.state != State.Activity) continue;
                int os = o.assignSubject;
                bool strong = profile != null && profile.IsStrong(os);
                if (!strong && SkillOf(os) < o.SkillOf(os) + 15f) continue;
                if (SkillOf(os) < o.SkillOf(os) + 5f) continue;
                Vector3 d = o.transform.position - transform.position;
                if (Mathf.Abs(d.y) > 0.5f || d.sqrMagnitude > 2.5f * 2.5f) continue;
                o.assignProgress += amount * 0.15f * (SkillOf(os) / 100f);
                o.AddSkill(os, amount * 0.004f);
                AddSkill(os, amount * 0.002f);
                world.rel.Add(id, o.id, amount * 0.02f, world.simTime);
                if (taughtDay != world.Day)
                {
                    taughtDay = world.Day;
                    ShowBubble(Say("teach", "ここはね…"), 1.5f);
                    o.ShowBubble(o.Say("thanks", "ありがとう"), 1.5f);
                    GrowAbility(Abilities.Mind, 0.2f);
                    world.Log(displayName + "が" + o.displayName + "に" + SubjectName(os) + "の課題を教えている");
                }
            }
        }

        // ================= 科目と学力 =================
        void InitSkills()
        {
            for (int i = 0; i < Academics.Count; i++)
            {
                float v = 22f + diligence * 18f + Random.Range(0f, 10f);
                if (profile != null && profile.IsStrong(i)) v += 18f;
                if (profile != null && profile.IsWeak(i)) v -= 14f;
                if (IsTarget) v = 8f + Random.Range(0f, 6f); // 篤は異世界から来たばかり
                skill[i] = Mathf.Clamp(v, 0f, 100f);
            }
            // 篤の学力はループで持ち越す
            if (IsTarget && Loop.fSkills != null) for (int i = 0; i < Academics.Count && i < Loop.fSkills.Length; i++) skill[i] = Loop.fSkills[i];
            // 能力値（性格と種族から。少しだけばらつく。篤の分はループで持ち越す）
            var init = Abilities.Initial(letter);
            for (int i = 0; i < Abilities.Count; i++) ability[i] = Mathf.Clamp(init[i] + (IsTarget ? 0f : Random.Range(-3f, 3f)), 0f, 100f);
            if (IsTarget && Loop.fAbilities != null) for (int i = 0; i < Abilities.Count && i < Loop.fAbilities.Length; i++) ability[i] = Loop.fAbilities[i];
        }

        public bool Enrolled(int s) { return profile != null && s >= 0 && System.Array.IndexOf(profile.enrolled, s) >= 0; }
        public float SkillOf(int s) { return s >= 0 && s < Academics.Count ? skill[s] : AverageSkill(); }
        public static string SubjectName(int s) { return s >= 0 && s < Academics.Count ? Academics.Names[s] : "勉強"; }

        float AverageSkill()
        {
            if (profile == null || profile.enrolled.Length == 0) return 0f;
            float sum = 0f;
            foreach (int s in profile.enrolled) sum += skill[s];
            return sum / profile.enrolled.Length;
        }

        /// <summary>学力を足し引きする。grow が true なら、得意（1.5倍）・苦手（0.6倍）で伸び方が変わる。</summary>
        public void AddSkill(int s, float v, bool grow = true)
        {
            if (s < 0 || s >= Academics.Count) return;
            if (grow && v > 0f && profile != null) v *= profile.Growth(s) * MindMul;
            if (grow && v > 0f) foreach (var x in mods) v *= x.mod.skillGrowth; // 学びのバッジなど
            skill[s] = Mathf.Clamp(skill[s] + v, 0f, 100f);
        }

        /// <summary>能力値（身につけている物の分も足す）。</summary>
        public float Ab(int a)
        {
            if (a < 0 || a >= Abilities.Count) return 50f;
            float v = ability[a];
            foreach (var x in mods) { float b; if (x.mod.abilityBonus.TryGetValue(a, out b)) v += b; }
            return Mathf.Clamp(v, 0f, 100f);
        }

        /// <summary>この人を意識している（その人のお守りを身につけている）。</summary>
        public bool FocusOn(SimCharacter o)
        {
            if (o == null || o == this || o.profile == null) return false;
            foreach (var x in mods) if (x.mod.focusLetter != null && x.mod.focusLetter == o.profile.letter) return true;
            return false;
        }

        /// <summary>能力値を伸ばす（高いほど伸びにくい）。</summary>
        public void GrowAbility(int a, float v)
        {
            if (a < 0 || a >= Abilities.Count || v == 0f) return;
            string before = Abilities.Rank(ability[a]);
            if (v > 0f)
            {
                v *= Mathf.Clamp(1.3f - ability[a] / 100f, 0.3f, 1.3f);
                foreach (var x in mods) { float g; if (x.mod.abilityGrowth.TryGetValue(a, out g)) v *= g; } // 伸びやすくする装飾品
            }
            ability[a] = Mathf.Clamp(ability[a] + v, 0f, 100f);
            string after = Abilities.Rank(ability[a]);
            if (after != before && v > 0f && world != null) world.Log(displayName + "の" + Abilities.Names[a] + "が " + after + " になった");
        }

        /// <summary>知力による覚えのよさ（知力50でほぼ1倍、85で約1.2倍、30で約0.9倍）。</summary>
        float MindMul { get { return 0.7f + Ab(Abilities.Mind) / 170f; } }

        public string AbilityText()
        {
            var parts = new List<string>();
            for (int i = 0; i < Abilities.Count; i++) parts.Add(Abilities.Names[i] + " " + Abilities.Rank(Ab(i)));
            return string.Join("　", parts.ToArray());
        }

        /// <summary>していることで能力値が少しずつ伸びる。</summary>
        void GrowFromActivity(float dt, float gm)
        {
            switch (state)
            {
                case State.Away:
                    if (awayKind == AwayKind.Stroll) GrowAbility(Abilities.Body, gm * 0.004f);
                    else if (awayKind == AwayKind.Outing) GrowAbility(Abilities.Charm, gm * 0.002f);
                    else if (awayKind == AwayKind.Job) GrowAbility(Abilities.Dex, gm * 0.003f);
                    break;
                case State.Sitting:
                case State.Activity:
                    if (act == Act.Hobby)
                    {
                        if (hobby == HobbyKind.Reading) GrowAbility(Abilities.Mind, dt * 0.004f);
                        else if (hobby == HobbyKind.Magic) { GrowAbility(Abilities.Magic, dt * 0.003f); GrowAbility(Abilities.Dex, dt * 0.002f); }
                        else if (hobby == HobbyKind.Tea) GrowAbility(Abilities.Charm, dt * 0.003f);
                    }
                    else if (act == Act.Work) GrowAbility(Abilities.Dex, dt * 0.004f);
                    else if (act == Act.Fun && gathering != null) GrowAbility(Abilities.Charm, dt * 0.002f);
                    break;
                case State.Bathing:
                    GrowAbility(Abilities.Body, dt * 0.001f);
                    break;
            }
        }

        /// <summary>今から勉強する科目を決める。課題があれば課題の科目、試験の前の日は苦手な科目。</summary>
        void PickStudySubject()
        {
            studyPickedAt = world.Now;
            if (hasAssignment && assignProgress < 100f && assignSubject >= 0) { studySubject = assignSubject; return; }
            if (profile == null || profile.enrolled.Length == 0) { studySubject = -1; return; }
            if (world.ExamEveToday || Random.value < 0.3f * diligence)
            {
                int low = profile.enrolled[0];
                foreach (int s in profile.enrolled) if (skill[s] < skill[low]) low = s;
                studySubject = low;
                return;
            }
            studySubject = profile.enrolled[Random.Range(0, profile.enrolled.Length)];
        }

        /// <summary>実技の自主練習での、ちょっとした失敗。回復魔法が得意な子が駆けつける。</summary>
        void PracticeMishap(int subj)
        {
            mishapDay = world.Day;
            ShowBubble(Academics.MishapSound(subj), 1.6f);
            world.Log("★ " + displayName + "が" + SubjectName(subj) + (inPlaceClass ? "の授業で" : "の自主練習で") + Academics.MishapText(subj) + "！");
            tired = Mathf.Min(100f, tired + 10f);
            foreach (var o in world.characters)
            {
                if (o == this || !o.Present || o.InPrivate || o.state == State.Sleeping) continue;
                if ((o.transform.position - transform.position).sqrMagnitude < 16f) o.ShowBubble(o.Say("surprise", "わっ！"), 1.4f);
            }
            StartCoroutine(MishapAfter());
            // 駆けつける：回復魔法が得意な子 → いなければ回復魔法の学力が高い子
            SimCharacter healer = null;
            float best = -1f;
            foreach (var o in world.characters)
            {
                if (o == this || o.IsTarget || !o.AvailableForTalk || o.OnMission || o.hasOrder || !o.CanActNow()) continue;
                if (o.state == State.Talking || o.state == State.Sleeping) continue;
                float v = o.SkillOf(Academics.Healing) + (o.profile != null && o.profile.IsStrong(Academics.Healing) ? 40f : 0f);
                if (Mathf.Abs(o.transform.position.y - transform.position.y) > 1f) v -= 30f;
                if (v > best) { best = v; healer = o; }
            }
            if (healer != null && best >= 40f)
            {
                healer.ShowBubble("！", 1.2f);
                healer.BeginApproach(this, false, Intent.Heal);
                world.Log(healer.displayName + "が" + displayName + "のところへ駆けつける");
            }
        }

        IEnumerator MishapAfter()
        {
            yield return new WaitForSeconds(1.7f);
            ShowBubble(Say("hurt", "いてて…"), 1.8f);
        }

        /// <summary>そのキャラらしいせりふ（決まっていなければ fallback）。{0} は相手の名前。</summary>
        public string Say(string key, string fallback, string other = null)
        {
            string[] v;
            string line = fallback;
            if (profile != null && profile.lines.TryGetValue(key, out v) && v.Length > 0) line = v[Random.Range(0, v.Length)];
            if (other != null && line.Contains("{0}")) line = line.Replace("{0}", other);
            return line;
        }

        string Pick(string key)
        {
            string[] v;
            if (profile != null && profile.lines.TryGetValue(key, out v) && v.Length > 0) return v[Random.Range(0, v.Length)];
            return null;
        }

        /// <summary>相手の呼び方。篤は CharProfile.callF、知り合いは「さん」など、友達は名前、親友はあだ名。</summary>
        public string AddressOf(SimCharacter o)
        {
            if (o == null) return "";
            if (o.IsTarget && !IsTarget && profile != null && profile.callF.Length > 0) return profile.callF;
            int st = world.rel.Stage(id, o.id);
            string v;
            if (st >= 2 && o.letter != null && CharLines.Nick.TryGetValue(o.letter, out v)) return v;
            if (st == 0 && letter != null && CharLines.Honor.TryGetValue(letter, out v)) return o.displayName + v;
            return o.displayName;
        }

        /// <summary>相手に合わせたせりふ。篤への気持ち（_love1・_love2・_love）→ 関係の段階（親友 _s・知り合い _k）→ ふつう の順に探す。</summary>
        public string SayTo(string key, SimCharacter o, string fallback)
        {
            string line = null;
            if (o != null)
            {
                if (o.IsTarget && !IsTarget && LoveStage >= 1 && Random.value < 0.6f)
                {
                    // 恋人（3）→ 好き（2）→ 気になる（1）の順に探す
                    for (int ls = Mathf.Min(3, LoveStage); ls >= 1 && line == null; ls--) line = Pick(key + "_love" + ls);
                    if (line == null) line = Pick(key + "_love");
                }
                if (line == null && IsTarget && o.IsLover && Random.value < 0.5f) line = Pick(key + "_lover"); // 篤から恋人へ
                if (line == null)
                {
                    int st = world.rel.Stage(id, o.id);
                    if (st >= 2) line = Pick(key + "_s");
                    else if (st == 0) line = Pick(key + "_k");
                }
            }
            if (line == null) line = Pick(key) ?? fallback;
            if (line.Contains("{0}")) line = line.Replace("{0}", AddressOf(o));
            return line;
        }

        /// <summary>そのときの様子に合わせたひとこと（場所・授業・行事・天気・季節・状態）。なければ null。</summary>
        string AmbientLine()
        {
            var keys = new List<string>();
            if (inPlaceClass && world.Now < classUntil) keys.Add("c_in");
            if (gathering != null && gathering.Active(world.Now))
            {
                if (gathering.honoree == this) keys.Add("e_bday_me");
                else if (gathering.honoree != null && !gathering.honoree.IsTarget) keys.Add("e_bday");
                else if (gathering.key == "year_stars") keys.Add("e_stars");
                else if (gathering.key == "year_solstice") keys.Add("e_solstice");
                else keys.Add("e_fest");
            }
            if (keys.Count == 0)
            {
                var f = world.RomanceTarget;
                bool nearF = f != null && !IsTarget && (f.transform.position - transform.position).sqrMagnitude < 16f;
                if (GetStage(StatusLine.Cold) >= 1) keys.Add("st_sick");
                if (GetStage(StatusLine.Shy) >= 1) keys.Add(nearF && LoveStage >= 1 ? "shy_love" : "st_shy");
                if (GetStage(StatusLine.Jealousy) >= 1) keys.Add("st_jealous");
                if (GetStage(StatusLine.MoodUp) >= 2) keys.Add("st_up");
                if (GetStage(StatusLine.Full) >= 1) keys.Add("st_full");
                if (GetStage(StatusLine.Lonely) >= 1) keys.Add("st_lonely");
                if (tired > 75f) keys.Add("st_tired");
                if (act == Act.Study && world.ExamEveToday) keys.Add("c_eve");
            }
            // 夜いっしょに（22時〜）：談話室で夜ふかし・部屋で・屋上や中庭で
            float mm = world.MinuteOfDay;
            if (keys.Count == 0 && (mm >= 22f * 60f || LoungeWorld.IsNight(mm)) && SomeoneNear(3f))
            {
                string area = world.AreaName(transform.position);
                if (area == "談話室") keys.Add("n_lounge");
                else if (area.EndsWith("の部屋")) keys.Add("n_room");
                else if (area == LoungeWorld.Observatory || area == LoungeWorld.Courtyard) keys.Add("n_sky");
            }
            // かわいい系のひとこと（落ち込んでいないとき）
            if (keys.Count == 0 && GetStage(StatusLine.MoodDown) == 0 && Random.value < 0.2f) keys.Add("cute");
            if (keys.Count == 0)
            {
                string area = world.AreaName(transform.position);
                float m = world.MinuteOfDay;
                if (area.Length > 0 && Random.value < 0.5f) keys.Add("p_" + area);
                else if (m >= 390f && m < 660f)
                {
                    var w = Calendar.WeatherOf(world.Day);
                    if (Random.value < 0.4f) keys.Add("s_" + new[] { "spring", "summer", "autumn", "winter" }[Calendar.Season(world.Day)]);
                    else keys.Add(w == Weather.Rain ? "w_rain" : (w == Weather.Snow ? "w_snow" : (w == Weather.Sunny ? "w_sunny" : "s_" + new[] { "spring", "summer", "autumn", "winter" }[Calendar.Season(world.Day)])));
                }
                else if (area.Length > 0) keys.Add("p_" + area);
            }
            if (keys.Count == 0) return null;
            return Pick(keys[Random.Range(0, keys.Count)]);
        }

        bool SomeoneNear(float r)
        {
            foreach (var o in world.characters)
            {
                if (o == this || !o.Present) continue;
                Vector3 d = o.transform.position - transform.position;
                if (Mathf.Abs(d.y) < 1f && d.sqrMagnitude < r * r) return true;
            }
            return false;
        }

        string[] Pool(string key, string[] fallback)
        {
            string[] v;
            if (profile != null && profile.lines.TryGetValue(key, out v) && v.Length > 0) return v;
            return fallback;
        }

        bool HasTrait(CharTrait t) { return profile != null && (profile.traits & t) != 0; }

        /// <summary>性欲の増え方。「好き」以上だけが増え、気持ちが強いほど加速度的に速くなる。Fは自分を好きな子の気持ちで増える。</summary>
        /// <summary>性欲の増え方（1分あたり）：だれでも少しずつ増え（0.02）、篤を「好き」以上なら気持ちの強さでさらに増える。</summary>
        const float DesireBase = 0.02f;
        float DesireRate()
        {
            var rom = world.romance;
            if (IsTarget)
            {
                float sum = 0f;
                foreach (var c in world.characters)
                    if (!c.IsTarget && c.LoveStage >= 2) { float x = rom.love[c.id] / 50f; sum += x * x; }
                return (DesireBase + Mathf.Min(0.15f, 0.015f * sum)) * RateMul(NeedKind.Desire);
            }
            float love = 0f;
            if (LoveStage >= 2) { float l = rom.love[id] / 50f; love = 0.025f * l * l * l; }
            return (DesireBase + love) * RateMul(NeedKind.Desire);
        }

        float PlaceBonus(Vector3 p)
        {
            float b = 0f;
            bool avoid = GetStage(StatusLine.MoodDown) >= 2;
            foreach (var o in world.characters)
            {
                if (o == this || !o.Present || o.InPrivate) continue;
                Vector3 d = o.transform.position - p;
                if (Mathf.Abs(d.y) > 1f || d.sqrMagnitude > 3.5f * 3.5f) continue;
                if (avoid) { b -= 1f; continue; }
                // 近いほど強く効く（となりの席がいちばん）
                float k = 0.4f + 1.2f * Mathf.Clamp01((3.5f - Mathf.Sqrt(d.x * d.x + d.z * d.z)) / 2.5f);
                float v = (world.rel.Get(id, o.id) - 30f) / 20f;
                if (o.IsTarget && LoveStage >= 2) v += 3f;
                else if (o.IsTarget && LoveStage == 1) v += 1.5f;
                if (IsTarget && o.LoveStage >= 2) v += 1.5f;
                if (o == clingTarget) v += 6f;
                if (FocusOn(o)) v += 2f; // お守り：その人の近くに座りやすい
                b += v * k;
            }
            return b;
        }

        bool CanVisitRoom(RoomInfo r)
        {
            if (r == null || r.owner < 0 || r.owner == id || r.owner >= world.characters.Count) return false;
            var owner = world.characters[r.owner];
            if (!owner.Present || owner.InPrivate || world.AreaName(owner.transform.position) != r.AreaName) return false;
            if (owner.IsTarget) return LoveStage >= 2;
            if (IsTarget) return owner.LoveStage >= 2;
            return world.rel.Get(id, owner.id) >= 40f;
        }

        // ================= 課題 =================
        public void GiveAssignment(double deadline)
        {
            hasAssignment = true;
            assignProgress = 0f;
            // 課題の科目：取っている科目からひとつ
            assignSubject = profile != null && profile.enrolled.Length > 0 ? profile.enrolled[Random.Range(0, profile.enrolled.Length)] : -1;
            studySubject = assignSubject;
            assignDeadline = deadline;
            nightOwlLogged = false;
            study = Mathf.Max(study, 40f);
        }

        /// <summary>課題の急ぎ具合（0〜1）。締め切りが近く、進んでいないほど大きい。</summary>
        float AssignmentUrgency()
        {
            if (!hasAssignment || assignProgress >= 100f) return 0f;
            float hoursLeft = (float)((assignDeadline - world.Now) / 60.0);
            float left = 1f - assignProgress / 100f;
            float time = Mathf.Clamp01(1f - hoursLeft / 54f);
            return left * (0.25f + 0.75f * time) * (0.6f + 0.8f * diligence * (1f - time) + time);
        }

        // ================= 予定 =================
        void EnsureSchedule()
        {
            if (scheduleDay == world.Day) return;
            // 日付が変わっても、まだ寝ていなければ前の日の「就寝」を引き継ぐ
            ScheduleItem carry = null;
            foreach (var it in schedule) if (it.kind == TaskKind.Bed && !it.done && state != State.Sleeping) carry = it;
            scheduleDay = world.Day;
            schedule = ScheduleMaker.Make(id, scheduleDay, world.holidayWeekdays);
            foreach (var it in schedule) if (it.kind != TaskKind.Bed && it.end < world.Now) it.done = true;
            if (carry != null) schedule.Insert(0, carry);
        }

        bool NearMeal(double minutes)
        {
            foreach (var it in schedule)
            {
                if (it.done) continue;
                if (it.kind == TaskKind.Breakfast || it.kind == TaskKind.Lunch || it.kind == TaskKind.Dinner)
                    if (it.start - world.Now < minutes && it.start > world.Now - 30) return true;
            }
            return false;
        }

        bool HandleSchedule(double now)
        {
            if (hasOrder || privatePending || sickRest) return false;
            foreach (var it in schedule)
            {
                if (it.done) continue;
                bool earlyBed = it.kind == TaskKind.Bed && sleepy > 70f && now >= it.start - 75;
                if (it.start > now && !earlyBed) return false;
                switch (it.kind)
                {
                    case TaskKind.Class:
                        if (now >= it.end - 5) { it.done = true; continue; }
                        it.done = true;
                        awayUntil = it.end;
                        awayKind = AwayKind.Class;
                        classBlocks = Mathf.Max(1, Mathf.RoundToInt((float)((it.end - it.start) / 100.0)));
                        classSubjects.Clear(); classSubjects.AddRange(it.subjects);
                        classSlots.Clear(); classSlots.AddRange(it.slots);
                        bool examItem = it.Label.EndsWith("試験");
                        if (!examItem && TryClassInPlace(it)) return true;
                        awayLabel = examItem ? "試験を受けている" : "授業に出ている";
                        StartLeave(examItem ? it.Label + "を受けに行く" : "授業に出かける");
                        ShowBubble(Say(examItem ? "c_exam" : "c_go", examItem ? "がんばろう" : "いってきます"), 1.6f);
                        return true;
                    case TaskKind.Breakfast:
                    case TaskKind.Lunch:
                    case TaskKind.Dinner:
                        if (now > it.start + 60) { it.done = true; continue; }
                        it.done = true;
                        if (GetStage(StatusLine.MoodDown) >= 3 && Random.value < 0.5f) { world.Log(displayName + "は食欲がなくて" + it.Label + "を抜いた"); continue; }
                        StartMeal(Act.Meal, it.Label);
                        return true;
                    case TaskKind.Bath:
                        if (now > it.start + 150) { it.done = true; continue; }
                        // 浴場は男女共用。仲がよい人とだけ一緒に入る（famuru 決定 2026-10-21）。入っている人と仲がよくなければ、あとで入る
                        if (!BathCompanyOk()) { if (actionText != "お風呂が空くのを待っている" && state == State.Idle) actionText = "お風呂が空くのを待っている"; continue; }
                        it.done = true;
                        StartBath();
                        return true;
                    case TaskKind.Bed:
                        if (state == State.Sleeping && !napping && !dozing) { it.done = true; return false; }
                        if (goal == Goal.Bed) return true;
                        // 課題が終わっていなければ夜ふかし（22時〜2時）
                        float m = world.MinuteOfDay;
                        bool nightStudy = m >= 1320f || m < 120f;
                        if (nightStudy && AssignmentUrgency() > 0.45f && assignProgress < 90f)
                        {
                            if (!nightOwlLogged) { nightOwlLogged = true; world.Log(displayName + "が夜ふかしして課題をしている"); }
                            if (state != State.Sitting && state != State.Activity && goal == Goal.None) StartStudy();
                            return true;
                        }
                        bedItem = it;
                        StartBed();
                        return true;
                }
            }
            return false;
        }

        Poi FindPoi(System.Predicate<Poi> pred)
        {
            foreach (var p in world.pois) if (pred(p)) return p;
            return null;
        }

        void StartMeal(Act a, string label)
        {
            LeaveCurrentSpot();
            act = a;
            mealPending = true;
            Poi counter = FindPoi(p => p.counter);
            int slot = counter != null ? counter.FreeSlot() : -1;
            actionText = label + (a == Act.Meal ? "を食べに食堂へ" : "に食堂へ");
            if (slot >= 0)
            {
                poi = counter;
                poiSlot = slot;
                poi.occupants[slot] = this;
                goal = Goal.Counter;
                if (!GoTo(poi.slots[slot], State.Walking)) GoDiningSeat();
            }
            else GoDiningSeat();
        }

        void GoDiningSeat()
        {
            ReleaseSeatAndPoi();
            Seat best = null;
            float bestScore = float.MinValue;
            foreach (var s in world.seats)
            {
                if (s.kind != SeatKind.Sit || s.area != "食堂" || (s.occupant != null && s.occupant != this)) continue;
                float score = Random.value * 2f + PlaceBonus(s.position);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            mealPending = false;
            if (best == null) { SetState(State.Idle); return; }
            seat = best;
            seat.occupant = this;
            goal = Goal.DiningSeat;
            if (!GoTo(seat.approach, State.Walking)) { goal = Goal.None; SetState(State.Idle); }
        }

        bool StartSnack()
        {
            Poi trolley = FindPoi(p => p.snack && p.FreeSlot() >= 0);
            if (trolley == null) return false;
            bool ok = GoToPoi(trolley, Act.Snack, "おやつを取りに行く", 2f);
            snackPick = ok;
            return ok;
        }

        /// <summary>一緒にお風呂に入れる仲か：寮生同士は友達以上（好感度40）、篤と寮生は恋人か親友（好感度80）。</summary>
        bool BathPal(SimCharacter o)
        {
            float a = world.rel.Get(id, o.id);
            if (IsTarget || o.IsTarget)
            {
                var girl = IsTarget ? o : this;
                return girl.IsLover || a >= 80f;
            }
            return a >= 40f;
        }

        /// <summary>今お風呂にいる人・向かっている人が、みんな一緒に入れる仲か。</summary>
        bool BathCompanyOk()
        {
            foreach (var c in world.characters)
            {
                if (c == this) continue;
                if (c.state != State.Bathing && c.goal != Goal.Bath) continue;
                if (!BathPal(c)) return false;
            }
            return true;
        }

        void StartBath()
        {
            LeaveCurrentSpot();
            Seat free = null;
            float bestScore = float.MinValue;
            foreach (var s in world.seats)
            {
                if (s.kind != SeatKind.Bath || s.occupant != null) continue;
                float sc = PlaceBonus(s.position) + Random.value;
                if (sc > bestScore) { bestScore = sc; free = s; }
            }
            if (free == null) return;
            seat = free;
            seat.occupant = this;
            goal = Goal.Bath;
            actionText = "お風呂に向かう";
            if (!GoTo(seat.approach, State.Walking)) { ReleaseSeatAndPoi(); goal = Goal.None; }
        }

        void StartBed()
        {
            if (room == null) return;
            LeaveCurrentSpot();
            seat = room.bed;
            seat.occupant = this;
            goal = Goal.Bed;
            actionText = "部屋に戻って寝る";
            if (!GoTo(seat.approach, State.Walking)) { goal = Goal.None; }
        }

        bool StartNap()
        {
            Seat best = null;
            float bestScore = float.MinValue;
            if (room != null && (room.bed.occupant == null || room.bed.occupant == this)) { best = room.bed; bestScore = 0.5f + Random.value; }
            foreach (var r in world.rooms)
            {
                if (!CanVisitRoom(r) || r.futon.occupant != null) continue;
                float s = PlaceBonus(r.futon.position) * 0.5f + Random.value;
                if (s > bestScore) { bestScore = s; best = r.futon; }
            }
            if (best == null) return false;
            LeaveCurrentSpot();
            seat = best;
            seat.occupant = this;
            goal = Goal.Nap;
            actionText = best.room == room ? "部屋で昼寝しに行く" : best.label + "で昼寝しに行く";
            if (!GoTo(seat.approach, State.Walking)) { goal = Goal.None; return false; }
            return true;
        }

        void StartLeave(string text)
        {
            LeaveCurrentSpot();
            goal = Goal.Exit;
            actionText = text;
            if (!GoTo(world.exitPoint, State.Walking)) { goal = Goal.None; SetState(State.Idle); }
        }

        void ReturnHome()
        {
            transform.position = world.exitPoint;
            SetVisible(true);
            string what = awayKind == AwayKind.Stroll ? "散歩" : (awayKind == AwayKind.Outing ? "おでかけ" : (awayKind == AwayKind.Job ? "アルバイト" : (awayLabel == "試験を受けている" ? "試験" : "授業")));
            if (awayLabel == "飛び出していった") what = "外";
            world.Log(displayName + "が" + what + "から帰ってきた");
            goal = Goal.Return;
            actionText = what + "から帰ってきた";
            if (awayKind == AwayKind.Class)
            {
                float m = world.MinuteOfDay;
                if (m >= 12.5f * 60f && m <= 16.5f * 60f) hunger = Mathf.Min(hunger, 25f);
                if (IsTarget && world.player != null) world.player.Earn(15 * classBlocks, "授業に出た");
                AfterClass();
            }
            if (awayKind == AwayKind.Job && world.player != null && IsTarget) world.player.Earn(70, "街のアルバイト");
            gathering = null;
            hasOrder = false;
            if (!GoTo(world.entrancePoint, State.Walking)) { goal = Goal.None; SetState(State.Idle); }
        }

        /// <summary>
        /// 実技の授業が見える場所（中庭：飛行術・体術、工房：錬成、温室：薬草学）が開いていれば、外出せずにその場所で授業を受ける。
        /// </summary>
        bool TryClassInPlace(ScheduleItem it)
        {
            if (it.subjects.Count == 0) return false;
            foreach (int sj in it.subjects)
            {
                string pl = LoungeWorld.ClassPlace(sj);
                if (pl == null || !world.PlaceOpen(pl)) return false;
            }
            int s0 = it.subjects[0];
            if (LoungeWorld.ClassPlace(s0) == LoungeWorld.Courtyard && world.BadWeatherToday) return false;
            Poi p = FindPoi(x => x.classOnly && x.subject == s0 && x.FreeSlot() >= 0);
            if (p == null) return false;
            classSubjects.Clear(); classSubjects.AddRange(it.subjects);
            classSlots.Clear(); classSlots.AddRange(it.slots);
            classBlocks = Mathf.Max(1, Mathf.RoundToInt((float)((it.end - it.start) / 100.0)));
            studySubject = s0;
            studyPickedAt = world.Now;
            classUntil = it.end;
            inPlaceClass = true;
            float secs = world.GameToSimSeconds((float)(it.end - world.Now));
            if (!GoToPoi(p, Act.Study, SubjectName(s0) + "の授業を受けている", Mathf.Max(5f, secs))) { inPlaceClass = false; return false; }
            ShowBubble(Say("c_go", "いってきます"), 1.6f);
            return true;
        }

        static readonly HashSet<string> classEndLogged = new HashSet<string>();

        void EndPlaceClass()
        {
            inPlaceClass = false;
            if (IsTarget && world.player != null) world.player.Earn(15 * classBlocks, "授業に出た");
            AfterClass();
            string ck = Loop.Count + ":" + world.Day + ":" + (classSlots.Count > 0 ? classSlots[0] : 0);
            if (classEndLogged.Add(ck)) world.Log(world.AreaName(transform.position) + "での" + SubjectName(classSubjects.Count > 0 ? classSubjects[0] : -1) + "の授業が終わった");
            if (state == State.Activity && act == Act.Study) { LeaveCurrentSpot(); SetState(State.Idle); actionText = "授業が終わった"; }
        }

        /// <summary>授業から帰ったとき：科目の学力が伸び、同じ授業に出た子と仲よくなる。実技はたまに失敗の話が伝わる。</summary>
        void AfterClass()
        {
            foreach (int s in classSubjects)
            {
                AddSkill(s, 2.2f); weekSubject[s] += 6f;
                if (!Academics.IsPractical(s)) GrowAbility(Abilities.Mind, 0.3f);
                else
                {
                    GrowAbility(Abilities.Magic, 0.3f);
                    if (s == 7 || s == 8) GrowAbility(Abilities.Body, 0.3f);
                    if (s == 9) GrowAbility(Abilities.Dex, 0.3f);
                }
            }
            foreach (var o in world.characters)
            {
                if (o == this || o.classSlots.Count == 0) continue;
                int same = 0;
                foreach (int k in classSlots) if (o.classSlots.Contains(k)) same++;
                if (same > 0) world.rel.Add(id, o.id, 0.75f * same, world.simTime);
            }
            foreach (int s in classSubjects)
            {
                if (!Academics.IsPractical(s)) continue;
                float p = (profile != null && profile.IsWeak(s) ? 0.3f : 0.1f) * Mathf.Clamp(1.5f - Ab(Abilities.Dex) / 100f, 0.5f, 1.5f);
                if (Random.value >= p) continue;
                world.Log(displayName + "は" + SubjectName(s) + "の授業で" + Academics.MishapText(s) + "らしい");
                ShowBubble(Say("hurt", "いてて…"), 1.8f);
                break;
            }
        }

        void WakeUp()
        {
            bool fromNight = !napping && !sickRest && !dozing;
            napping = false;
            dozing = false;
            if (sickRest)
            {
                sickRest = false;
                if (GetStage(StatusLine.Cold) >= 2) SetStage(StatusLine.Cold, 1, true);
                else SetStage(StatusLine.Cold, 0);
            }
            if (fromNight)
            {
                // 寝不足：起きたときの眠気で段階が決まる（安眠のお香を使った夜はならない）
                int sl = deepSleep ? 0 : (sleepy > 70f ? 3 : (sleepy > 50f ? 2 : (sleepy > 30f ? 1 : 0)));
                SetStage(StatusLine.Sleep, sl, true);
                // 夜に性欲が最大のまま朝を迎えて寝不足：性欲は 50 まで下がり、発情のたまりが大きく増える（いっぱいになると［発情］）
                if (desireMaxAtNight && sl >= 1)
                {
                    desire = Mathf.Min(desire, 50f);
                    bool toHeat = false;
                    desireBuild.value = Mathf.Min(100f, desireBuild.value + 50f);
                    int dst = desireBuild.StageFor(GetStage(StatusLine.Desire));
                    if (dst >= 3 && GetStage(StatusLine.Desire) < 3) toHeat = true;
                    if (dst != GetStage(StatusLine.Desire)) SetStage(StatusLine.Desire, dst, true, true);
                    world.Log(displayName + "は眠れない夜を過ごした（寝不足" + (toHeat ? "・発情" : "") + "）");
                }
                desireMaxAtNight = false;
                deepSleep = false;
                // 風邪（寝不足だとひきやすい）
                if (GetStage(StatusLine.Cold) == 0 && Chance(0.03f + sl * 0.04f)) SetStage(StatusLine.Cold, 1);
            }
            else if (GetStage(StatusLine.Sleep) > 0) Ease(StatusLine.Sleep);

            if (seat != null && seat.occupant == this)
            {
                if (seat.kind == SeatKind.Bed) transform.position = world.NearestFree(seat.approach);
                if (seat.kind == SeatKind.Bed) seat.occupant = null;
            }
            if (seat != null && seat.kind == SeatKind.Bed) seat = null;
            EnsureSchedule();
            if (seat != null && seat.kind == SeatKind.Sit) { SetState(State.Sitting); actionText = "目をこすっている"; return; }
            actionText = "起きた";
            ShowBubble(GetStage(StatusLine.Sleep) > 0 ? "ねむい…" : "ふあ〜", 2f);
            SetState(State.Idle);
            thinkTimer = Random.Range(1f, 3f);
        }

        void SetVisible(bool v)
        {
            foreach (var r in renderers) if (r != null && r.gameObject != ring) r.enabled = v;
            foreach (var c in colliders) if (c != null) c.enabled = v;
            if (!v) selected = false;
        }

        // ================= 行事 =================
        void JoinGathering(Gathering g)
        {
            g.members.Add(this);
            if (g.outing)
            {
                gathering = g;
                awayKind = AwayKind.Outing;
                awayLabel = g.label;
                awayUntil = g.end;
                StartLeave(g.key == "date" ? "デートに出かける" : "出かける");
                return;
            }
            gatherFocus = g.focus;
            Seat best = null;
            float bestScore = float.MinValue;
            foreach (var s in world.seats)
            {
                if (s.kind != SeatKind.Sit || s.area != g.area || (s.occupant != null && s.occupant != this)) continue;
                float score = -Vector3.Distance(s.position, g.focus) * 0.6f + PlaceBonus(s.position) * 0.5f + Random.value;
                if (score > bestScore) { bestScore = score; best = s; }
            }
            if (best != null && Vector3.Distance(best.position, g.focus) < 4.5f)
            {
                StartSitAt(best, Act.Fun, g.label);
                gathering = g;
                return;
            }
            LeaveCurrentSpot();
            gathering = g;
            float a = Random.value * Mathf.PI * 2f;
            Vector3 p = world.NearestFree(g.focus + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(1.2f, 2.2f));
            goal = Goal.Gather;
            act = Act.Fun;
            sitLabel = g.label;
            actionText = g.label + "（向かっている）";
            if (!GoTo(p, State.Walking)) SetState(State.Idle);
        }

        public bool StartBaking()
        {
            if (!AvailableForTalk || state == State.Sleeping) return false;
            Poi counter = FindPoi(p => p.kitchen && p.FreeSlot() >= 0); // 台所が開いていれば台所で
            if (counter == null) counter = FindPoi(p => p.counter && p.FreeSlot() >= 0);
            if (counter == null) return false;
            bool ok = GoToPoi(counter, Act.Hobby, counter.kitchen ? "台所でお菓子を作っている" : "お菓子を作っている", 35f);
            bakePending = ok;
            return ok;
        }

        // ================= 行動の選択 =================
        double bedWalkStart = -1;

        void Think()
        {
            EnsureSchedule();
            double now = world.Now;
            // 寝に行く途中で詰まったら（45分たってもたどり着かない）、そのまま部屋で眠る
            if (goal == Goal.Bed && state == State.Walking)
            {
                if (bedWalkStart < 0) bedWalkStart = now;
                else if (now - bedWalkStart > 45) { world.Log("（" + displayName + "は道で詰まったので、部屋に戻して寝かせた）"); path.Clear(); bedWalkStart = -1; Arrive(); return; }
            }
            else bedWalkStart = -1;
            if (inPlaceClass && now >= classUntil) EndPlaceClass();
            if (chatting) return; // その場での会話が終わるまで待つ

            switch (state)
            {
                case State.Talking:
                    return;
                case State.Sleeping:
                    // 上機嫌・絶好調なら、眠気がなくなった時点で早起きする
                    if (!napping && !sickRest && !dozing && sleepy <= 0f && GetStage(StatusLine.MoodUp) >= 2 && world.MinuteOfDay >= 330f && world.MinuteOfDay < 600f && now < sleepUntil)
                    {
                        world.Log(displayName + "はすっきり早起きした");
                        sleepUntil = now;
                    }
                    if (now >= sleepUntil) WakeUp();
                    return;
                case State.Away:
                    if (now >= awayUntil) ReturnHome();
                    return;
                case State.Private:
                    if (now >= privateUntil) EndPrivate();
                    return;
                case State.Eating:
                    if (now < eatUntil) return;
                    ShowBubble(act == Act.Tea ? "ほっ…" : "ごちそうさま", 1.8f);
                    // 満腹
                    if (hunger < 5f)
                    {
                        if (act == Act.Snack && GetStage(StatusLine.Full) >= 1) Escalate(StatusLine.Full);
                        else if (Chance(0.3f)) SetStage(StatusLine.Full, 1);
                    }
                    LeaveCurrentSpot();
                    SetState(State.Idle);
                    actionText = "ぼんやりしている";
                    break;
                case State.Bathing:
                    if (now < bathUntil) return;
                    ShowBubble("さっぱり", 1.8f);
                    LeaveCurrentSpot();
                    SetState(State.Idle);
                    actionText = "ぼんやりしている";
                    break;
                case State.Activity:
                    if (inPlaceClass && now < classUntil && act == Act.Study) return; // 授業中は最後まで受ける
                    if (mealPending)
                    {
                        if (stateTime >= 2.5f) GoDiningSeat();
                        return;
                    }
                    if (snackPick && stateTime >= 2f)
                    {
                        snackPick = false;
                        Seat s = BestSeat(x => x.area == "談話室");
                        if (s == null || !StartSitAt(s, Act.Snack, "おやつを食べている")) SetState(State.Idle);
                        return;
                    }
                    if (readingPick && stateTime >= 3f)
                    {
                        readingPick = false;
                        if (!StartReadingSeat()) SetState(State.Idle);
                        return;
                    }
                    if (act == Act.Work)
                    {
                        if (now < workUntil) return;
                        if (world.player != null) world.player.Earn(20, "寮の手伝い");
                        hasOrder = false;
                        LeaveCurrentSpot();
                        SetState(State.Idle);
                        actionText = "手伝いを終えた";
                        return;
                    }
                    if (bakePending)
                    {
                        if (stateTime < activityDuration) return;
                        bakePending = false;
                        var f = world.RomanceTarget;
                        if (f != null && f.AvailableForTalk) { BeginApproach(f, false, Intent.Bake); return; }
                        world.Log(displayName + "はお菓子を渡しそびれた");
                        LeaveCurrentSpot();
                        SetState(State.Idle);
                    }
                    break;
            }

            if (hasPendingRequest && state != State.Talking) { ExecuteRequest(); return; }
            if (privatePending) return;
            if (escortLeader != null && UpdateEscort()) return;
            if (HandleSchedule(now)) return;
            if (state == State.Approaching || state == State.Waiting || state == State.Walking) return;
            if (hasOrder) return; // プレイヤーの指示を実行中

            // ---- 寮生の買い物：自分の状態と好みで、効く物を考えて買う（購買部が開いている 9〜20時。1時間半に1回考える）----
            if (!IsTarget && now >= nextShopThink && world.MinuteOfDay >= 9f * 60f && world.MinuteOfDay < 20f * 60f)
            {
                nextShopThink = now + ShopThinkEvery;
                SelfBuy(false);
            }

            // ---- 性欲が最大（昼）：相手を探して会いに行く ----
            if (!PrivateNight && desire >= 99.9f && !sickRest)
            {
                var pt = SeekPartner();
                if (pt != null)
                {
                    privateEscorts.Clear();
                    BeginApproach(pt, false, Intent.Private);
                    actionText = pt.displayName + "を探しに行く";
                    return;
                }
            }

            // ---- 限界：その場で居眠り ----
            if (GetStage(StatusLine.Sleep) >= 3 && Random.value < 0.08f)
            {
                if (state == State.Sitting && seat != null)
                {
                    dozing = true;
                    sleepUntil = now + 25;
                    SetState(State.Sleeping);
                    actionText = "居眠りしている";
                    ShowBubble("zzz", 1.5f);
                    return;
                }
                if (StartNap()) { choice = Choice.Nap; return; }
            }

            // ---- 赤面：Fが近いと離れる ----
            var fx = world.RomanceTarget;
            if (GetStage(StatusLine.Shy) >= 2 && fx != null && fx.Present && !fx.InPrivate && fx.state != State.Talking)
            {
                Vector3 d = fx.transform.position - transform.position;
                if (Mathf.Abs(d.y) < 1f && d.sqrMagnitude < 4f && state != State.Sitting && Random.value < 0.5f)
                {
                    ShowBubble("っ…！", 1.2f);
                    StartWander();
                    return;
                }
            }

            // ---- 嫉妬：Fがほかの子と話していたら、そばへ行って待つ ----
            if (GetStage(StatusLine.Jealousy) >= 3 && fx != null && fx.state == State.Talking && fx.TalkPartner != null && fx.TalkPartner != this && Random.value < 0.5f)
            {
                BeginApproach(fx, false, Intent.Love);
                actionText = fx.displayName + "の会話が気になる";
                return;
            }

            // ---- 行事 ----
            if (gathering != null)
            {
                if (gathering.Active(now) && (state == State.Sitting || state == State.Activity)) return;
                if (!gathering.Active(now)) gathering = null;
            }
            var g = world.ActiveGatheringFor(this);
            if (g != null && !g.declined.Contains(this))
            {
                if (g.members.Contains(this)) { if (!g.outing) { JoinGathering(g); return; } }
                else
                {
                    float chance = g.joinChance * (GetStage(StatusLine.MoodDown) > 0 ? 0.5f : 1f) * (0.7f + 0.3f * sociability);
                    if (g.invited != null) chance = 1f;
                    if (Random.value < chance) { JoinGathering(g); return; }
                    g.declined.Add(this);
                }
            }

            // ---- 孤独：仲のいい子にべったり ----
            if (GetStage(StatusLine.Lonely) >= 2)
            {
                if (clingTarget == null || !clingTarget.Present || clingTarget.InPrivate) clingTarget = BestFriendPresent();
                if (clingTarget != null && FollowCling()) return;
            }
            else clingTarget = null;

            // ---- 空き時間：いちばん強い欲求を選ぶ ----
            float noise = 40f * whim;
            var scores = new Dictionary<Choice, float>();
            scores[Choice.Social] = lonely * (0.7f + 0.6f * sociability) * Prod(e => e.socialScore);
            scores[Choice.Rest] = tired;
            scores[Choice.Fun] = bored * Prod(e => e.funScore) * (world.ExamEveToday ? 0.8f : 1f);
            bool hungry = hunger > 55f || (world.SnackBonus && hunger > 30f);
            scores[Choice.Snack] = (hungry && !NearMeal(45) && GetStage(StatusLine.Full) == 0) ? hunger * 0.9f + (world.SnackBonus ? 30f : 0f) : 0f;
            float studyBase = study * (0.6f + 0.8f * diligence) * Prod(e => e.studyScore) * (world.ExamEveToday ? 1.4f : 1f);
            scores[Choice.Study] = studyBase + AssignmentUrgency() * 110f;
            scores[Choice.Hobby] = hobbyNeed;
            var target0 = world.RomanceTarget;
            bool fFree = !IsTarget && LoveStage >= 1 && target0 != null && target0.AvailableForTalk;
            float loveScore = fFree ? loveNeed * 1.1f * Prod(e => e.loveScore) : 0f;
            if (GetStage(StatusLine.Shy) > 0 || (GetStage(StatusLine.Love) >= 2 && Random.value < 0.4f)) { scores[Choice.Hover] = loveScore; scores[Choice.Love] = 0f; }
            else scores[Choice.Love] = loveScore;
            float napAt = 85f - GetStage(StatusLine.Sleep) * 10f;
            scores[Choice.Nap] = sleepy > napAt ? sleepy : 0f;
            SimCharacter privatePartner = PrivatePartner();
            scores[Choice.Private] = privatePartner != null ? desire * 1.1f * Prod(e => e.privateScore) : 0f;

            Choice best = Choice.None;
            float bestScore = 0f;
            foreach (var kv in scores)
            {
                if (kv.Value <= 0f) continue;
                float v = kv.Value + (Random.value - 0.5f) * noise;
                if (v > bestScore) { bestScore = v; best = kv.Key; }
            }

            bool resting = state == State.Sitting || state == State.Activity;
            if (resting)
            {
                if (stateTime < 6f) return;
                if (best == choice && stateTime < 45f) return;
                if (state == State.Activity && stateTime < activityDuration && bestScore < 75f) return;
            }
            if (bestScore < 30f)
            {
                if (resting && stateTime < 25f) return;
                if (Random.value < 0.5f) StartWander(); else StartRest();
                choice = Choice.None;
                return;
            }

            requestEffort = -1f;
            bool ok;
            switch (best)
            {
                case Choice.Social: ok = StartSocial(); break;
                case Choice.Rest: ok = StartRest(); break;
                case Choice.Fun: ok = StartActivity(); break;
                case Choice.Snack: ok = StartSnack(); break;
                case Choice.Study: ok = StartStudy(); break;
                case Choice.Hobby: ok = StartHobby(); break;
                case Choice.Love: BeginApproach(target0, false, Intent.Love); ok = true; break;
                case Choice.Hover: ok = StartHover(); break;
                case Choice.Nap: ok = StartNap(); break;
                case Choice.Private: BeginApproach(privatePartner, false, Intent.Private); ok = true; break;
                default: ok = false; break;
            }
            if (ok) choice = best;
            else { StartWander(); choice = Choice.None; }
        }

        SimCharacter BestFriendPresent()
        {
            SimCharacter best = null;
            float bestA = -1f;
            foreach (var o in world.characters)
            {
                if (o == this || !o.Present || o.InPrivate || o.state == State.Sleeping) continue;
                float a = world.rel.Get(id, o.id) + (o.IsTarget && LoveStage >= 1 ? 20f : 0f);
                if (a > bestA) { bestA = a; best = o; }
            }
            return best;
        }

        /// <summary>孤独：相手のそばについて回る。相手が座っていれば近くの席に座る。</summary>
        bool FollowCling()
        {
            var t = clingTarget;
            Vector3 d = t.transform.position - transform.position;
            bool near = Mathf.Abs(d.y) < 0.5f && Flat(d).magnitude < 2.2f;
            if (near && (state == State.Sitting || state == State.Activity)) return true;
            if (near && t.AvailableForTalk && lonely > 50f && Random.value < 0.3f) { BeginApproach(t, false, Intent.Talk); return true; }
            if (near && (t.state == State.Sitting || t.state == State.Eating))
            {
                Seat s = BestSeat(x => Vector3.Distance(x.position, t.transform.position) < 2.5f);
                if (s != null && StartSitAt(s, Act.Rest, t.displayName + "にべったり")) { choice = Choice.Cling; return true; }
            }
            if (near) { SetState(State.Activity); actionText = t.displayName + "にべったり"; activityDuration = 5f; choice = Choice.Cling; return true; }
            LeaveCurrentSpot();
            Vector3 behind = t.transform.position - t.transform.forward * 1.0f;
            goal = Goal.Follow;
            act = Act.None;
            actionText = t.displayName + "について行く";
            choice = Choice.Cling;
            return GoTo(world.NearestFree(behind), State.Walking);
        }

        /// <summary>恋の病・照れ：Fに話しかけずに、少し離れて目で追う。</summary>
        bool StartHover()
        {
            var f = world.RomanceTarget;
            if (f == null || !f.Present || f.InPrivate) return false;
            float r = GetStage(StatusLine.Shy) > 0 ? 3.5f : 2.8f;
            float a = Random.value * Mathf.PI * 2f;
            Vector3 p = world.NearestFree(f.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
            LeaveCurrentSpot();
            goal = Goal.Hover;
            act = Act.None;
            actionText = f.displayName + "のほうを見ている";
            return GoTo(p, State.Walking);
        }

        /// <summary>試験用：今の場所・状態・行き先（ClaudeBridge の where で使う）。</summary>
        public string DebugWhere()
        {
            Vector3 p = transform.position;
            string dest = path.Count > 0 ? string.Format("→({0:F1},{1:F1},{2:F1}) 残り{3}点", path[path.Count - 1].x, path[path.Count - 1].y, path[path.Count - 1].z, path.Count) : "道なし";
            return string.Format("({0:F2},{1:F2},{2:F2}) {3} goal={4} {5} stuck={6}", p.x, p.y, p.z, state, goal, dest, stuckCount);
        }

        /// <summary>夜に眠っている（昼寝・寝込みは入らない）。このあいだは状態異常にかからない。</summary>
        bool NightSleeping { get { return state == State.Sleeping && !napping && !sickRest; } }

        /// <summary>二人きりの時間の「夜」（21時〜朝6時半）。夜以外は性欲が最大のときだけ起こる。</summary>
        bool PrivateNight { get { float m = world.MinuteOfDay; return m >= 21f * 60f || m < 6.5f * 60f; } }

        SimCharacter PrivatePartner()
        {
            if (desire < 60f) return null;
            if (!PrivateNight && desire < 99.9f) return null;
            // 篤との関係に関係なく起きる（famuru 決定 2026-10-21）。寮生同士では起きない
            if (!IsTarget)
            {
                var f = world.RomanceTarget;
                if (f != null && f.AvailableForTalk && f.desire >= 40f && !f.hasOrder) return f;
                return null;
            }
            if (hasOrder) return null;
            SimCharacter best = null;
            float bestScore = float.MinValue;
            foreach (var c in world.characters)
            {
                if (c == this || c.IsTarget || !c.AvailableForTalk || c.desire < 40f) continue;
                float sc = c.desire + world.romance.love[c.id] * 0.3f + world.rel.Get(id, c.id) * 0.2f;
                if (sc > bestScore) { bestScore = sc; best = c; }
            }
            return best;
        }

        // ================= 寮生の買い物（自分で考えてバフ・デバフを選ぶ） =================
        // 数字は試作用の仮の値。famuru 決定（2026-10-21）：今の状態＋性格の好み＋気まぐれで選ぶ／困っているのはデバフの状態異常だけ見る・
        // バフの状態異常がある欲求は優先度を下げる／お金はまだ使わず、必要なら何回でも買う（同じ物が効いている間は買わない）。あとで寮生にもお小遣いを持たせる
        const double ShopThinkEvery = 90;   // 考える間隔（ゲーム内の分）
        const float ShopBuyAt = 0.35f;      // この点数を超えた物があれば買う
        static readonly ItemShelf[] ShopShelves = { ItemShelf.Needs, ItemShelf.Personal }; // 寮生が自分で買う棚
        double nextShopThink;

        /// <summary>状態異常 → 関係する欲求と、バフかデバフか。</summary>
        static readonly KeyValuePair<StatusLine, NeedKind[]>[] DebuffNeeds =
        {
            new KeyValuePair<StatusLine, NeedKind[]>(StatusLine.MoodDown, new[] { NeedKind.Lonely, NeedKind.Bored }),
            new KeyValuePair<StatusLine, NeedKind[]>(StatusLine.Sleep, new[] { NeedKind.Sleepy, NeedKind.Tired }),
            new KeyValuePair<StatusLine, NeedKind[]>(StatusLine.Love, new[] { NeedKind.Love }),
            new KeyValuePair<StatusLine, NeedKind[]>(StatusLine.Cold, new[] { NeedKind.Tired }),
            new KeyValuePair<StatusLine, NeedKind[]>(StatusLine.Lonely, new[] { NeedKind.Lonely }),
            new KeyValuePair<StatusLine, NeedKind[]>(StatusLine.Frustration, new[] { NeedKind.Bored }),
            new KeyValuePair<StatusLine, NeedKind[]>(StatusLine.Desire, new[] { NeedKind.Desire }),
        };
        static readonly KeyValuePair<StatusLine, NeedKind[]>[] BuffNeeds =
        {
            new KeyValuePair<StatusLine, NeedKind[]>(StatusLine.MoodUp, new[] { NeedKind.Lonely, NeedKind.Bored }),
            new KeyValuePair<StatusLine, NeedKind[]>(StatusLine.Focus, new[] { NeedKind.Study }),
        };

        float Gauge(NeedKind n)
        {
            switch (n)
            {
                case NeedKind.Lonely: return lonely;
                case NeedKind.Tired: return tired;
                case NeedKind.Bored: return bored;
                case NeedKind.Hunger: return hunger;
                case NeedKind.Study: return study;
                case NeedKind.Hobby: return hobbyNeed;
                case NeedKind.Love: return loveNeed;
                case NeedKind.Sleepy: return sleepy;
                default: return desire;
            }
        }

        /// <summary>その欲求をどのくらい何とかしたいか（ゲージの高さ＋デバフの状態異常。バフの状態異常があると半分）。</summary>
        float Urgency(NeedKind n)
        {
            float u = Gauge(n) / 100f;
            foreach (var kv in DebuffNeeds) if (System.Array.IndexOf(kv.Value, n) >= 0) u += 0.5f * GetStage(kv.Key);
            foreach (var kv in BuffNeeds) if (System.Array.IndexOf(kv.Value, n) >= 0 && GetStage(kv.Key) > 0) u *= 0.5f;
            return u;
        }

        /// <summary>品物の点数（よくなる分 − 困る分 ＋ 好み ＋ 気まぐれ）。</summary>
        public float ItemAppeal(ItemDef d)
        {
            var m = d.Mod;
            if (m == null) return 0f;
            float score = 0f;
            foreach (NeedKind n in System.Enum.GetValues(typeof(NeedKind)))
            {
                float rate = NeedMod.Get(m.rate, n), relief = NeedMod.Get(m.relief, n), build = NeedMod.Get(m.build, n), drain = NeedMod.Get(m.drain, n);
                if (rate == 1f && relief == 1f && build == 1f && drain == 1f) continue;
                if (n == NeedKind.Study)
                {
                    // 勉強したい気持ちは「高まる」がよい。真面目な子・課題がある子ほど欲しがる
                    float e = (rate - 1f) + (relief - 1f) * 0.5f;
                    float w = 0.3f + diligence * 0.6f + (hasAssignment ? 0.4f : 0f) + (GetStage(StatusLine.Focus) > 0 ? -0.2f : 0f);
                    score += e * w;
                    continue;
                }
                if (n == NeedKind.Desire)
                {
                    // 性欲を上げる物（私物）は好みで、下げる物は悶々・発情のときにほしくなる
                    float up = (rate - 1f) + (build - 1f) - (relief - 1f) * 0.5f - (drain - 1f) * 0.5f;
                    if (up > 0f) score += up * PersonalTaste();
                    else score += -up * (GetStage(StatusLine.Desire) >= 2 ? 1.2f : desire / 200f);
                    continue;
                }
                // ほかの欲求は「たまりにくい・下がりやすい」がよい
                float good = (1f - rate) + (relief - 1f) * 0.5f;
                if (good >= 0f) score += good * Urgency(n) * 1.5f;
                else score += good * (0.3f + Gauge(n) / 100f); // 困った所：その欲求がもう高いと、なおさらいや
            }
            score += whim * Random.value * 0.4f; // 気まぐれ
            return score;
        }

        /// <summary>性欲を上げる私物の好み（性欲が0でも少しは買う。気まぐれな子・恋をしている子・性欲が高い子ほど）。</summary>
        float PersonalTaste()
        {
            return 0.3f + whim * 0.5f + (LoveStage >= 1 ? 0.2f : 0f) + desire / 150f;
        }

        /// <summary>自分で考えて品物を買って使う。force なら点数に関係なく、いちばんよい物を買う（試験用）。</summary>
        public void SelfBuy(bool force = true)
        {
            if (IsTarget) return;
            ItemDef best = null;
            float bestScore = float.MinValue;
            foreach (var d in ItemBook.All)
            {
                if (System.Array.IndexOf(ShopShelves, d.shelf) < 0 || d.Mod == null) continue;
                if (mods.Exists(a => a.mod.id == d.modId)) continue; // 同じ物が効いている間は買わない
                float sc = ItemAppeal(d);
                if (sc > bestScore) { bestScore = sc; best = d; }
            }
            if (best == null || (!force && bestScore < ShopBuyAt)) return;
            world.Log(displayName + "は購買部で「" + best.name + "」を買った");
            ApplyItem(best.kind, this);
        }

        /// <summary>性欲が最大のときに探しに行く相手（寮生→篤、篤→性欲のいちばん高い寮生。関係に関係なく）。</summary>
        SimCharacter SeekPartner()
        {
            var f = world.RomanceTarget;
            if (!IsTarget)
            {
                if (f == null || !f.Present || f.InPrivate) return null;
                return f;
            }
            SimCharacter best = null;
            foreach (var c in world.characters)
            {
                if (c == this || c.IsTarget || !c.Present || c.InPrivate || c.state == State.Sleeping || c.state == State.Bathing) continue;
                if (best == null || c.desire > best.desire) best = c;
            }
            return best;
        }

        /// <summary>相手を探しに行く途中で、二人きりの時間の相手になれる子（性欲80以上）に会ったら、いっしょに連れて行く（2人まで）。</summary>
        void RecruitEscorts()
        {
            if (privateEscorts.Count >= MaxPrivateGroup - 2) return;
            foreach (var c in world.characters)
            {
                if (c == this || c == target || c.IsTarget || c.desire < 80f) continue;
                if (!c.AvailableForTalk || c.privatePending || c.escortLeader != null || privateEscorts.Contains(c) || c.sickRest) continue;
                Vector3 d = c.transform.position - transform.position;
                if (Mathf.Abs(d.y) > 0.5f || Flat(d).magnitude > 3.0f) continue;
                c.escortLeader = this;
                privateEscorts.Add(c);
                c.LeaveCurrentSpot();
                c.ShowBubble("私も…", 1.5f);
                world.Log(c.displayName + "が" + displayName + "についていく");
                if (privateEscorts.Count >= MaxPrivateGroup - 2) return;
            }
        }

        /// <summary>ついていく子：先に行く子のうしろを歩く。先に行く子が探すのをやめたら、ついていくのもやめる。</summary>
        bool UpdateEscort()
        {
            var l = escortLeader;
            bool going = l != null && l.Present && !l.InPrivate && (l.privatePending || ((l.state == State.Approaching || l.state == State.Waiting || l.state == State.Talking) && l.intent == Intent.Private));
            if (!going) { escortLeader = null; return false; }
            Vector3 d = l.transform.position - transform.position;
            actionText = l.displayName + "についていく";
            if (Flat(d).magnitude > 1.6f || Mathf.Abs(d.y) > 0.5f)
            {
                goal = Goal.Follow;
                act = Act.None;
                GoTo(world.NearestFree(l.transform.position - l.transform.forward * 1.0f), State.Walking);
            }
            return true;
        }

        void ReleaseEscorts()
        {
            foreach (var c in privateEscorts) if (c != null && c.escortLeader == this) c.escortLeader = null;
            privateEscorts.Clear();
        }

        bool StartSocial()
        {
            var others = world.characters;
            float total = 0f;
            var weights = new float[others.Count];
            float shy = 1f - sociability;
            bool anyone = GetStage(StatusLine.Lonely) >= 1;
            for (int i = 0; i < others.Count; i++)
            {
                var o = others[i];
                if (o == this || !o.AvailableForTalk) continue;
                float a = world.rel.Get(id, o.id);
                if (o.sickRest && a < 40f) continue;
                float w = anyone ? 1f : Mathf.Pow((a + 10f) / 110f, 1f + shy * 3f);
                float dist = Vector3.Distance(transform.position, o.transform.position);
                w *= 1f / (1f + dist / 8f);
                if (IsTarget && o.LoveStage >= 2) w *= 1.5f;
                if (FocusOn(o)) w *= 2.5f; // お守り：その人に会いに行きやすい
                if (o.sickRest) w *= 3f;
                foreach (var c in others)
                    if (c != this && c.state == State.Approaching && c.target == o) { w *= 0.25f; break; }
                weights[i] = w;
                total += w;
            }
            if (total <= 0f) return false;
            float r = Random.value * total;
            for (int i = 0; i < others.Count; i++)
            {
                r -= weights[i];
                if (weights[i] > 0f && r <= 0f)
                {
                    BeginApproach(others[i], false, Intent.Talk);
                    if (others[i].sickRest) actionText = others[i].displayName + "のお見舞いに行く";
                    return true;
                }
            }
            return false;
        }

        Seat BestSeat(System.Predicate<Seat> filter)
        {
            Seat best = null;
            float bestScore = float.MinValue;
            foreach (var s in world.seats)
            {
                if (s.kind != SeatKind.Sit) continue;
                if (s.occupant != null && s.occupant != this) continue;
                if (!filter(s)) continue;
                float score = -Vector3.Distance(transform.position, s.approach) * 0.3f;
                if (Mathf.Abs(s.position.y - transform.position.y) > 1f) score -= 3f;
                score += PlaceBonus(s.position) * (1f - sociability * 0.3f);
                score += Random.value * (1f + whim * 3f);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            return best;
        }

        bool StartRest()
        {
            Seat s = BestSeat(x => x.room == null || CanVisitRoom(x.room) || x.room == room);
            return s != null && StartSitAt(s, Act.Rest, null);
        }

        bool StartSitAt(Seat s, Act a, string label)
        {
            LeaveCurrentSpot();
            seat = s;
            seat.occupant = this;
            goal = Goal.Seat;
            act = a;
            sitLabel = label;
            actionText = (label ?? (seat.label + "で休む")) + "（向かっている）";
            return GoTo(seat.approach, State.Walking);
        }

        bool StartActivity()
        {
            var candidates = new List<Poi>();
            var weights = new List<float>();
            float total = 0f;
            foreach (var p in world.pois)
            {
                if (p.counter || p.snack || p.hobby >= 0 || p.owner >= 0 || p.classOnly) continue;
                int fs = p.FreeSlot();
                if (fs < 0) continue;
                float w = 1f / (1f + Vector3.Distance(transform.position, p.slots[fs]) / 6f);
                w *= Mathf.Exp(0.35f * Mathf.Clamp(PlaceBonus(p.slots[fs]), -4f, 6f));
                if (p.dynamicSpot) w *= 4f;
                if (world.BadWeatherToday && p.label == "暖炉にあたっている") w *= 2f;
                if (p.outdoor && (world.BadWeatherToday || LoungeWorld.IsNight(world.MinuteOfDay))) w *= p.subject == 4 && !world.BadWeatherToday ? 1.5f : 0.08f; // 外は雨・雪・夜は行きにくい（星は夜）
                candidates.Add(p);
                weights.Add(w);
                total += w;
            }
            if (candidates.Count == 0) return false;
            float r = Random.value * total;
            Poi chosen = candidates[candidates.Count - 1];
            for (int i = 0; i < candidates.Count; i++) { r -= weights[i]; if (r <= 0f) { chosen = candidates[i]; break; } }
            return GoToPoi(chosen, Act.Fun, chosen.label, Random.Range(8f, 16f));
        }

        bool StartStudy()
        {
            float bestScore = float.MinValue;
            Seat bestSeat = null;
            bool useDesk = false;
            float mul = requestEffort >= 0f ? 0.5f + requestEffort : 1f;
            if (room != null && room.desk.FreeSlot() >= 0)
            {
                bestScore = 1f + PlaceBonus(room.desk.slots[0]) * 0.5f + Random.value + AssignmentUrgency() * 2f;
                useDesk = true;
            }
            foreach (var s in world.seats)
            {
                if (s.kind != SeatKind.Sit || (s.occupant != null && s.occupant != this)) continue;
                bool table = s.label == "大きなテーブル" || s.label == "図書室の机" || s.label == "工房の椅子";
                bool visit = s.room != null && s.room != room && CanVisitRoom(s.room);
                if (!table && !visit) continue;
                float score = PlaceBonus(s.position) * 0.6f + (visit ? 1.5f : (s.label == "図書室の机" ? 1.0f : 0.5f)) + Random.value - Vector3.Distance(transform.position, s.position) * 0.05f;
                if (score > bestScore) { bestScore = score; bestSeat = s; useDesk = false; }
            }
            PickStudySubject();
            // 実技の自主練習は、開いた場所（中庭・工房・温室）でする
            if (studySubject >= 0 && !(hasAssignment && assignProgress < 100f) && Random.value < 0.7f)
            {
                int sj = studySubject;
                Poi pp = FindPoi(x => !x.classOnly && x.subject == sj && x.FreeSlot() >= 0 && !(x.outdoor && world.BadWeatherToday));
                if (pp != null) return GoToPoi(pp, Act.Study, Academics.IsPractical(sj) ? SubjectName(sj) + "の自主練習をしている" : pp.label, Random.Range(15f, 25f) * mul);
            }
            string what = hasAssignment && assignProgress < 100f ? SubjectName(assignSubject) + "の課題をしている"
                : (studySubject >= 0 && Academics.IsPractical(studySubject) ? SubjectName(studySubject) + "の自主練習をしている" : SubjectName(studySubject) + "の勉強をしている");
            if (useDesk) return GoToPoi(room.desk, Act.Study, "机で" + what, Random.Range(18f, 30f) * mul);
            if (bestSeat == null) return false;
            string label = bestSeat.room != null ? bestSeat.label + "で" + what : what;
            return StartSitAt(bestSeat, Act.Study, label);
        }

        bool StartReadingSeat()
        {
            Seat s = BestSeat(x => (x.area == "談話室" && x.label != "大きなテーブル") || x.area == LoungeWorld.Library || (x.room != null && x.room != room && CanVisitRoom(x.room)));
            if (s == null) return false;
            return StartSitAt(s, Act.Hobby, s.room != null ? s.label + "で本を読んでいる" : (s.area == LoungeWorld.Library ? "図書室で本を読んでいる" : "本を読んでいる"));
        }

        bool StartHobby()
        {
            // 趣味の場所（ヴィエッタ：図書室、シルヴィエル：温室、ロッテ・キッカ：工房、リア：台所、ミレイユ：夜の天文台、ルピナ：中庭）が開いていれば、そこで過ごす
            string hp = profile != null ? profile.hobbyPlace : null;
            if (!string.IsNullOrEmpty(hp) && world.PlaceOpen(hp) && Random.value < 0.65f)
            {
                bool night = LoungeWorld.IsNight(world.MinuteOfDay) || world.MinuteOfDay >= 19f * 60f;
                bool outside = hp == LoungeWorld.Courtyard || hp == LoungeWorld.Observatory;
                bool ok = !(outside && world.BadWeatherToday) && !(hp == LoungeWorld.Observatory && !night) && !(hp == LoungeWorld.Courtyard && night);
                if (ok)
                {
                    var cands = new List<Poi>();
                    foreach (var p in world.pois) if (p.place == hp && !p.classOnly && p.FreeSlot() >= 0 && (p.hobby < 0 || p.hobby == (int)hobby)) cands.Add(p);
                    if (cands.Count > 0)
                    {
                        var pick = cands[Random.Range(0, cands.Count)];
                        return GoToPoi(pick, Act.Hobby, pick.label, Random.Range(15f, 25f));
                    }
                }
            }
            HobbyKind h = hobby;
            if (h == HobbyKind.Magic && GetStage(StatusLine.ManaOut) > 0) h = HobbyKind.Reading;
            if (h == HobbyKind.Walk && world.BadWeatherToday) h = HobbyKind.Reading;
            switch (h)
            {
                case HobbyKind.Reading:
                {
                    Poi shelf = null;
                    foreach (var p in world.pois)
                        if (p.label == "本棚の本を眺めている" && p.FreeSlot() >= 0 && (shelf == null || Random.value < 0.5f)) shelf = p;
                    if (shelf == null) return StartReadingSeat();
                    bool ok = GoToPoi(shelf, Act.Hobby, "本を選んでいる", 3f);
                    readingPick = ok;
                    return ok;
                }
                case HobbyKind.Magic:
                {
                    Poi p = FindPoi(x => x.hobby == (int)HobbyKind.Magic && x.FreeSlot() >= 0);
                    return p != null && GoToPoi(p, Act.Hobby, "魔法の練習をしている", Random.Range(15f, 25f));
                }
                case HobbyKind.Tea:
                    StartMeal(Act.Tea, "お茶");
                    return true;
                default:
                    awayUntil = world.Now + 25.0;
                    awayKind = AwayKind.Stroll;
                    awayLabel = "散歩に出ている";
                    StartLeave("散歩に出かける");
                    return true;
            }
        }

        bool GoToPoi(Poi p, Act a, string label, float duration)
        {
            LeaveCurrentSpot();
            poi = p;
            poiSlot = poi.FreeSlot();
            if (poiSlot < 0) { poi = null; return false; }
            poi.occupants[poiSlot] = this;
            goal = Goal.Poi;
            act = a;
            sitLabel = label;
            actionText = label + "（向かっている）";
            activityDuration = duration;
            return GoTo(poi.slots[poiSlot], State.Walking);
        }

        void StartWander()
        {
            LeaveCurrentSpot();
            goal = Goal.Wander;
            act = Act.None;
            actionText = "ぶらぶら歩く";
            AreaInfo area = world.AreaAt(transform.position);
            Vector3 p;
            if (area == null || area.name == "外" || Random.value < 0.35f)
                p = world.nav0.RandomFreePoint(new Rect(-6.5f, -7f, 13f, 14f));
            else
                p = world.NavAt(transform.position).RandomFreePoint(area.rect);
            if (!GoTo(p, State.Walking)) SetState(State.Idle);
        }

        // ================= プレイヤーの操作（F） =================
        bool CanTakeOrder()
        {
            if (!Present || state == State.Bathing || state == State.Private || sickRest) return false;
            if (state == State.Sleeping) return false;
            return true;
        }

        string BusyReason()
        {
            if (state == State.Away) return StateName();
            if (sickRest) return "体調が悪い";
            return StateName();
        }

        public void OrderMove(Vector3 p)
        {
            if (!CanTakeOrder()) { world.Log(displayName + "は今は動けない（" + BusyReason() + "）"); return; }
            if (state == State.Talking) return;
            LeaveCurrentSpot();
            hasOrder = true;
            goal = Goal.MoveTo;
            act = Act.None;
            actionText = "移動している（指示）";
            if (!GoTo(world.NearestFree(p), State.Walking)) { hasOrder = false; SetState(State.Idle); }
        }

        public void OrderTalk(SimCharacter g) { OrderApproach(g, Intent.Talk); }

        public void OrderGive(SimCharacter g, ItemKind item)
        {
            giftItem = item;
            OrderApproach(g, Intent.Give);
        }

        public void OrderRequest(SimCharacter g, RequestKind kind, SimCharacter meet)
        {
            requestKind = kind;
            requestMeet = meet;
            OrderApproach(g, Intent.Request);
        }

        void OrderApproach(SimCharacter g, Intent why)
        {
            if (g == null || g == this) return;
            if (!CanTakeOrder()) { world.Log(displayName + "は今は動けない（" + BusyReason() + "）"); RefundGift(why); return; }
            bool asleepOk = g.state == State.Sleeping && g.sickRest;
            if (!g.Present || g.InPrivate || g.state == State.Bathing || (g.state == State.Sleeping && !asleepOk))
            {
                ShowBubble(g.displayName + "は今いないみたい", 2.2f);
                world.Log(g.displayName + "は今会えない（" + g.StateName() + "）");
                RefundGift(why);
                return;
            }
            if (state == State.Talking) { world.Log(displayName + "は会話中。終わってからもう一度"); RefundGift(why); return; }
            ShowBubble("！", 1.2f);
            BeginApproach(g, true, why);
        }

        void RefundGift(Intent why)
        {
            if (why == Intent.Give && world.player != null) world.player.Refund(giftItem);
        }

        public void OrderWork()
        {
            if (!CanTakeOrder() || state == State.Talking) { world.Log(displayName + "は今は手伝えない"); return; }
            Poi counter = FindPoi(p => p.counter && p.FreeSlot() >= 0);
            if (counter == null) { world.Log("食堂の受け取り口が混んでいる"); return; }
            hasOrder = true;
            workUntil = world.Now + 60;
            if (!GoToPoi(counter, Act.Work, "寮の手伝いをしている", 60f)) { hasOrder = false; return; }
            workUntil = world.Now + 60 + 20;
        }

        public void OrderPartTime()
        {
            if (!CanTakeOrder() || state == State.Talking) { world.Log(displayName + "は今は出かけられない"); return; }
            float m = world.MinuteOfDay;
            if (m < 480f || m > 1140f) { world.Log("アルバイトは 8:00〜19:00 の間だけ"); return; }
            hasOrder = true;
            awayUntil = world.Now + 180;
            awayKind = AwayKind.Job;
            awayLabel = "街でアルバイトしている";
            StartLeave("街のアルバイトに出かける");
        }

        /// <summary>アイテムの効果。giver はくれた人（自分で使うなら自分）。</summary>
        public void ApplyItem(ItemKind k, SimCharacter giver)
        {
            var d = ItemBook.Get(k);
            bool gift = giver != null && giver != this;
            // 寮生の私物は、寮生が自分で使うだけ（渡せない）
            if (d != null && d.npcOnly && (gift || IsTarget)) { world.Log("「" + d.name + "」は寮生が自分で買って使う物（渡せない）"); return; }
            if (d != null && d.desireBoostMax > 0f) desire = Mathf.Min(100f, desire + Random.Range(d.desireBoostMin, d.desireBoostMax)); // 私物：すぐ増える分
            switch (k)
            {
                case ItemKind.Sweets:
                    if (gift) world.rel.Add(id, giver.id, 3f, world.simTime);
                    hunger = Mathf.Max(0f, hunger - 30f);
                    if (hunger < 5f) SetStage(StatusLine.Full, Mathf.Max(1, GetStage(StatusLine.Full)));
                    break;
                case ItemKind.Bouquet:
                    if (gift) world.rel.Add(id, giver.id, 6f, world.simTime);
                    if (gift && giver.IsTarget && LoveStage >= 0) world.ChangeLove(this, 5f);
                    PositiveEvent(0.5f);
                    break;
                case ItemKind.Present:
                    if (gift) world.rel.Add(id, giver.id, 10f, world.simTime);
                    if (gift && giver.IsTarget && LoveStage >= 0) world.ChangeLove(this, 8f);
                    if (GetStage(StatusLine.MoodDown) > 0) Ease(StatusLine.MoodDown); else Escalate(StatusLine.MoodUp, true);
                    break;
                case ItemKind.CheerPotion:
                    if (GetStage(StatusLine.MoodDown) > 0) Ease(StatusLine.MoodDown); else SetStage(StatusLine.MoodUp, Mathf.Max(1, GetStage(StatusLine.MoodUp)), true);
                    tired = Mathf.Max(0f, tired - 30f);
                    break;
                case ItemKind.WakePotion:
                    SetStage(StatusLine.Sleep, 0);
                    sleepy = Mathf.Max(0f, sleepy - 50f);
                    break;
                case ItemKind.SleepIncense:
                    deepSleep = true;
                    break;
                case ItemKind.ColdMedicine:
                    if (GetStage(StatusLine.Cold) >= 2) { SetStage(StatusLine.Cold, 1, true); sickRest = false; if (state == State.Sleeping) sleepUntil = world.Now; }
                    else SetStage(StatusLine.Cold, 0);
                    break;
                case ItemKind.Perfume:
                    if (IsTarget || LoveStage < 1) { world.Log("ときめきの香水は" + displayName + "には効かなかった"); return; }
                    if (gift) world.rel.Add(id, giver.id, 3f, world.simTime);
                    break;
                case ItemKind.LoveCharm:
                    if (!IsTarget && LoveStage >= 1)
                    {
                        loveNeed = Mathf.Min(100f, loveNeed + 30f);
                        world.ChangeLove(this, 5f);
                        if (GetStage(StatusLine.Love) == 0) SetStage(StatusLine.Love, 1, true);
                    }
                    else
                    {
                        world.Log("恋のおまじないは" + displayName + "には効かなかった");
                        return;
                    }
                    break;
            }
            // 欲求の補正（アイテム・プレゼント・装飾品）
            if (d != null && d.Mod != null)
            {
                if (d.accessory)
                {
                    // 装飾品は1人2つまで（3つ目は、いちばん前に身につけた物と付け替える）、お守りは1つまで
                    var src = d.Mod.source;
                    int slots = src == ModSource.Charm ? 1 : 2;
                    if (!mods.Exists(a => a.mod.id == d.Mod.id))
                    {
                        var same = mods.FindAll(a => a.mod.source == src);
                        while (same.Count >= slots)
                        {
                            world.Log(displayName + "は「" + same[0].mod.name + "」を外した");
                            mods.Remove(same[0]);
                            same.RemoveAt(0);
                        }
                    }
                    AddMod(d.Mod, 0f);
                    bool charm = src == ModSource.Charm;
                    if (gift) world.rel.Add(id, giver.id, charm ? 4f : 8f, world.simTime);
                    if (gift && giver.IsTarget && !IsTarget) world.ChangeLove(this, charm ? 3f : 6f);
                }
                else
                {
                    AddMod(d.Mod, d.modMinutes);
                    if (gift && k != ItemKind.Bouquet && k != ItemKind.Present && k != ItemKind.Perfume) world.rel.Add(id, giver.id, 2f, world.simTime);
                }
            }
            world.Log((gift ? giver.displayName + "が" + displayName + "に" : displayName + "が") + "「" + d.name + "」を" + (gift ? "渡した" : (d.accessory ? "身につけた" : "使った")));
        }

        // ---- お願い：引き受けるか・どのくらい気合を入れるか ----
        void DecideRequest(SimCharacter from, RequestKind kind, SimCharacter meet)
        {
            var pc = world.player;
            int recent = pc != null ? pc.RecentRequests(this) : 0;
            if (pc != null) pc.NoteRequest(this);
            float p = 0.35f + world.rel.Get(id, from.id) / 150f;
            if (!IsTarget && from.IsTarget) p += world.romance.love[id] / 200f + (IsLover ? 0.2f : 0f);
            p += GetStage(StatusLine.MoodUp) * 0.1f - GetStage(StatusLine.MoodDown) * 0.15f;
            p -= GetStage(StatusLine.Sleep) * 0.1f + (tired > 70f ? 0.2f : 0f) + recent * 0.12f;
            if (kind == RequestKind.Study) p += diligence * 0.2f - (study < 20f ? 0.15f : 0f);
            if (kind == RequestKind.Teach) p += 0.15f + (TeachSubjectFor(from) >= 0 ? 0f : -0.5f);
            if (kind == RequestKind.Rest) p += tired / 200f;
            if (kind == RequestKind.Meet && meet != null) p += (world.rel.Get(id, meet.id) - 40f) / 200f;
            p = Mathf.Clamp(p, 0.05f, 0.95f);
            float roll = Random.value;
            string what = PlayerControl.RequestName(kind, meet);
            if (roll >= p)
            {
                ShowBubble(recent >= 2 ? "また？ ごめん…" : Say("refuse", "ごめん、今はちょっと…"), 2f);
                world.Log(displayName + "は「" + what + "」を断った");
                return;
            }
            float effort = Mathf.Clamp01((p - roll) / p);
            if (kind == RequestKind.Teach) { TeachNow(from, effort); return; }
            string mood = effort > 0.6f ? "張り切って" : (effort > 0.25f ? "" : "しぶしぶ");
            ShowBubble(effort > 0.25f ? Say("accept", "まかせて！") : "…わかった", 1.8f);
            world.Log(displayName + "は「" + what + "」を" + (mood.Length > 0 ? mood : "") + "引き受けた");
            hasPendingRequest = true;
            pendingRequestKind = kind;
            pendingRequestMeet = meet;
            requestEffort = effort;
        }

        /// <summary>相手（篤など）に教えられる科目：自分の得意な勉学で、相手が取っている科目。なければ相手より学力が高い科目。</summary>
        public int TeachSubjectFor(SimCharacter learner)
        {
            if (learner == null || profile == null) return -1;
            int best = -1;
            float gap = 5f;
            foreach (int s in profile.enrolled)
            {
                if (!learner.Enrolled(s)) continue;
                float g = SkillOf(s) - learner.SkillOf(s) + (profile.IsStrong(s) ? 20f : 0f);
                if (g > gap) { gap = g; best = s; }
            }
            return best;
        }

        /// <summary>お願いされて、その場で勉強を教える。教えた側も教わった側も学力が伸び、仲よくなる。</summary>
        void TeachNow(SimCharacter learner, float effort)
        {
            int s = TeachSubjectFor(learner);
            if (s < 0) { ShowBubble(Say("refuse", "ごめん、教えられることがないかも"), 2f); return; }
            ShowBubble(Say("teach", "ここはね…"), 2f);
            float k = 0.6f + effort * 0.6f;
            learner.AddSkill(s, 3f * k);
            learner.weekSubject[s] += 8f * k;
            AddSkill(s, 1f * k);
            world.rel.Add(id, learner.id, 3f + 3f * effort, world.simTime);
            if (learner.IsTarget) world.ChangeLove(this, 1.5f);
            world.Log(displayName + "が" + learner.displayName + "に" + SubjectName(s) + "を教えた");
            StartCoroutine(ThanksLater(learner));
        }

        IEnumerator ThanksLater(SimCharacter who)
        {
            yield return new WaitForSeconds(2.1f);
            if (who != null) who.ShowBubble(who.Say("thanks", "ありがとう"), 1.8f);
        }

        void ExecuteRequest()
        {
            hasPendingRequest = false;
            switch (pendingRequestKind)
            {
                case RequestKind.Meet:
                    if (pendingRequestMeet != null && pendingRequestMeet.Present && !pendingRequestMeet.InPrivate)
                    {
                        BeginApproach(pendingRequestMeet, true, Intent.Talk);
                        actionText = pendingRequestMeet.displayName + "に会いに行く（お願い）";
                    }
                    break;
                case RequestKind.Study:
                    if (!StartStudy()) requestEffort = -1f;
                    else choice = Choice.Study;
                    break;
                default:
                    if (!(tired > 50f && StartNap()) && !StartRest()) requestEffort = -1f;
                    else choice = Choice.Rest;
                    break;
            }
        }

        // ================= 会いに行く =================
        void BeginApproach(SimCharacter who, bool order, Intent why)
        {
            if (who == null) return;
            LeaveCurrentSpot();
            target = who;
            hasOrder = order;
            intent = why;
            approachTimer = 0f;
            repathTimer = 0f;
            slotOffset = 0f;
            string verb;
            switch (why)
            {
                case Intent.Love: verb = "に会いたくて向かう"; break;
                case Intent.Private: verb = "のところへ向かう"; break;
                case Intent.Bake: verb = "にお菓子を渡しに行く"; break;
                case Intent.Give: verb = "に渡し物をしに行く"; break;
                case Intent.Request: verb = "にお願いをしに行く"; break;
                case Intent.Heal: verb = "のところへ駆けつける"; break;
                default: verb = "に会いに行く"; break;
            }
            actionText = who.displayName + verb + (order ? "（指示）" : "");
            SetState(State.Approaching);
            RepathToTarget();
        }

        /// <summary>だれかへの「お願い」（ほかのキャラを選んで相手をクリックしたとき）。Fが頼みに行く。</summary>
        public void Order(SimCharacter who)
        {
            var f = world.RomanceTarget;
            if (f == null || f == this) { OrderTalk(who); return; }
            f.OrderRequest(this, RequestKind.Meet, who);
        }

        public string StateName()
        {
            switch (state)
            {
                case State.Sleeping: return sickRest ? "熱で寝込んでいる" : (dozing ? "居眠りしている" : (napping ? "昼寝している" : "寝ている"));
                case State.Away: return awayLabel ?? "出かけている";
                case State.Private: return "エッチしている";
                case State.Bathing: return "お風呂";
                case State.Eating: return "食事中";
                case State.Talking: return "会話中";
                default: return actionText;
            }
        }

        void UpdateApproach(float dt)
        {
            bool sickOk = target != null && target.state == State.Sleeping && target.sickRest;
            if (target == null || !target.Present || target.InPrivate || (target.state == State.Sleeping && !sickOk) || target.state == State.Bathing) { GiveUp(); return; }
            if (intent == Intent.Private && !PrivateNight) RecruitEscorts();
            approachTimer += dt;
            repathTimer -= dt;
            Vector3 d3 = target.transform.position - transform.position;
            float d = Flat(d3).magnitude;
            bool sameFloor = Mathf.Abs(d3.y) < 0.5f;
            if (sameFloor && d <= 1.6f)
            {
                if (target.CanStartTalk(this)) { BeginInteraction(); return; }
                StartWaiting();
                return;
            }
            if (sameFloor && d <= 3f && target.state == State.Talking) { StartWaiting(); return; }
            if ((repathTimer <= 0f || path.Count == 0) && LoungeWorld.OnFloorLevel(transform.position)) RepathToTarget();
            if (!hasOrder && approachTimer > 30f) GiveUp();
        }

        void StartWaiting()
        {
            path.Clear();
            waitTimer = 0f;
            actionText = target.displayName + "の用事が終わるのを待つ";
            SetState(State.Waiting);
        }

        void BeginInteraction()
        {
            if (intent == Intent.Private && target.CanJoinPrivate(this)) StartCoroutine(PrivateRoutine(target));
            else { ReleaseEscorts(); StartCoroutine(TalkRoutine(target)); }
        }

        bool CanJoinPrivate(SimCharacter by)
        {
            bool pair = (IsTarget && !by.IsTarget) || (!IsTarget && by.IsTarget); // 篤と寮生（関係に関係なく）
            // 誘われた側も望んでいるときだけ（性欲40以上。famuru 決定 2026-10-21）
            return pair && !sickRest && desire >= 40f;
        }

        void UpdateWaiting(float dt)
        {
            waitTimer += dt;
            if (target == null || !target.Present || target.InPrivate) { GiveUp(); return; }
            FaceTowards(target.transform.position, dt);
            if (target.CanStartTalk(this))
            {
                Vector3 d3 = target.transform.position - transform.position;
                if (Flat(d3).magnitude <= 1.8f && Mathf.Abs(d3.y) < 0.5f) { BeginInteraction(); return; }
                SetState(State.Approaching);
                RepathToTarget();
                return;
            }
            if (waitTimer > (hasOrder ? 20f : 8f)) GiveUp();
        }

        void GiveUp()
        {
            if (target != null) world.Log(displayName + "は" + target.displayName + "に会うのをあきらめた");
            ReleaseEscorts();
            if (intent == Intent.Give) RefundGift(intent);
            target = null;
            hasOrder = false;
            intent = Intent.Talk;
            bored += 10f;
            path.Clear();
            SetState(State.Idle);
            actionText = "ぼんやりしている";
        }

        Vector3 ApproachPoint()
        {
            Vector3 tp = target.transform.position;
            var nav = world.NavAt(tp);
            Vector3 dir = Flat(transform.position - tp);
            float baseAng = (dir.sqrMagnitude > 0.01f && Mathf.Abs(transform.position.y - tp.y) < 0.5f)
                ? Mathf.Atan2(dir.z, dir.x) : Random.value * Mathf.PI * 2f;
            baseAng += slotOffset;
            for (int k = 0; k < 8; k++)
            {
                int step = (k + 1) / 2 * (k % 2 == 0 ? 1 : -1);
                float ang = baseAng + step * Mathf.PI / 4f;
                Vector3 p = new Vector3(tp.x + Mathf.Cos(ang) * 1.1f, nav.floorY, tp.z + Mathf.Sin(ang) * 1.1f);
                if (!nav.IsFree(p)) continue;
                bool taken = false;
                foreach (var c in world.characters)
                {
                    if (c == this || c == target || !c.Present) continue;
                    if (c.state == State.Approaching && (c.approachGoal - p).sqrMagnitude < 0.8f * 0.8f) { taken = true; break; }
                    if (!c.IsMoving && (c.transform.position - p).sqrMagnitude < 0.6f * 0.6f) { taken = true; break; }
                }
                if (!taken) return p;
            }
            return world.NearestFree(tp + (dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward) * 1.1f);
        }

        void RepathToTarget()
        {
            repathTimer = 1.0f;
            if (target == null) return;
            approachGoal = ApproachPoint();
            var p = world.FindPath(transform.position, approachGoal);
            path.Clear();
            if (p != null) path.AddRange(p);
        }

        public bool CanStartTalk(SimCharacter by)
        {
            if (!Present || state == State.Talking || state == State.Bathing || state == State.Private || chatting) return false;
            if (state == State.Sleeping && !sickRest) return false;
            if (hasOrder && target != by) return false;
            if (OnMission) return false;
            if (NoTalk() && !(by != null && by.IsTarget && by.hasOrder)) return false;
            return true;
        }

        // ================= 会話 =================
        static readonly string[] Chatter = { "それでね…", "へえ！", "ほんとに？", "うんうん", "わかる〜", "そうなの？", "ふふっ", "なるほど", "えっ！", "それで？" };
        static readonly string[] KoibanaLines = { "えっ、わかる！", "きゃー", "内緒だよ？", "応援する！", "ふふ、顔赤いよ" };

        /// <summary>話題を選ぶ。半分くらいは自分の好きなことの話をする。</summary>
        TalkTopic PickTopic()
        {
            var all = CharProfiles.Topics;
            if (profile != null && profile.likes.Length > 0 && Random.value < 0.5f)
            {
                var mine = new List<TalkTopic>();
                foreach (var t in all) foreach (var g in t.tags) if (profile.Likes(g)) { mine.Add(t); break; }
                if (mine.Count > 0) return mine[Random.Range(0, mine.Count)];
            }
            return all[Random.Range(0, all.Length)];
        }

        void NoteZukan(TalkTopic tp, int feelMe, SimCharacter partner, int feelYou)
        {
            var f = world.RomanceTarget;
            if (f == null) return;
            bool heard = IsTarget || partner.IsTarget || (f.Present && (f.transform.position - transform.position).sqrMagnitude < 36f);
            if (!heard) return;
            LearnTags(tp, feelMe);
            partner.LearnTags(tp, feelYou);
        }

        void LearnTags(TalkTopic tp, int feel)
        {
            if (feel == 0 || profile == null) return;
            foreach (var g in tp.tags)
            {
                if (feel > 0 && profile.Likes(g)) Loop.Learn(world, this, "好き", g);
                if (feel < 0 && profile.Dislikes(g)) Loop.Learn(world, this, "苦手", g);
            }
        }

        /// <summary>話題が好きなら 1、苦手なら -1、どちらでもなければ 0。</summary>
        public int TopicFeeling(TalkTopic t)
        {
            if (profile == null || t == null) return 0;
            bool like = false, dislike = false;
            foreach (var g in t.tags) { if (profile.Likes(g)) like = true; if (profile.Dislikes(g)) dislike = true; }
            return dislike ? -1 : (like ? 1 : 0);
        }

        IEnumerator TalkRoutine(SimCharacter partner)
        {
            path.Clear();
            talkPartner = partner;
            SetState(State.Talking);
            partner.EnterTalk(this);
            var rom = world.romance;
            if (IsTarget) partner.lastFContact = world.Now;
            if (partner.IsTarget) lastFContact = world.Now;

            // ---- Fからの渡し物 ----
            if (intent == Intent.Give)
            {
                actionText = partner.displayName + "に渡している";
                ShowBubble("これ、どうぞ", 1.6f);
                yield return new WaitForSeconds(1.7f);
                partner.ShowBubble(giftItem == ItemKind.Present || giftItem == ItemKind.Bouquet ? "わぁ…" + partner.Say("thanks", "ありがとう！") : partner.Say("thanks", "ありがとう"), 1.8f);
                partner.ApplyItem(giftItem, this);
                yield return new WaitForSeconds(1.9f);
                EndTalk(partner);
                yield break;
            }

            // ---- 実技の失敗に駆けつけて、回復魔法で手当てする ----
            if (intent == Intent.Heal)
            {
                actionText = partner.displayName + "を手当てしている";
                partner.actionText = displayName + "に手当てしてもらっている";
                ShowBubble(Say("heal", "大丈夫？ 治すね"), 1.8f);
                yield return new WaitForSeconds(1.9f);
                ShowBubble("✦", 1.2f);
                partner.ShowBubble(partner.Say("thanks", "ありがとう"), 1.8f);
                partner.tired = Mathf.Max(0f, partner.tired - 15f);
                AddSkill(Academics.Healing, 0.6f);
                world.rel.Add(id, partner.id, 4f, world.simTime);
                world.Log(displayName + "が" + partner.displayName + "を回復魔法で手当てした");
                yield return new WaitForSeconds(1.9f);
                EndTalk(partner);
                yield break;
            }

            // ---- Fからのお願い ----
            if (intent == Intent.Request)
            {
                actionText = partner.displayName + "にお願いしている";
                ShowBubble("お願いがあるんだけど…", 1.8f);
                yield return new WaitForSeconds(1.9f);
                partner.DecideRequest(this, requestKind, requestMeet);
                yield return new WaitForSeconds(2.0f);
                EndTalk(partner);
                yield break;
            }

            // ---- 手作りお菓子を渡す ----
            if (intent == Intent.Bake && partner.IsTarget)
            {
                actionText = partner.displayName + "にお菓子を渡している";
                partner.actionText = displayName + "からお菓子をもらっている";
                ShowBubble("これ、作ったの…", 1.8f);
                yield return new WaitForSeconds(1.9f);
                partner.ShowBubble("ありがとう！おいしい", 2f);
                yield return new WaitForSeconds(2.1f);
                world.ChangeLove(this, 5f);
                world.rel.Add(id, partner.id, 5f, world.simTime);
                PositiveEvent(0.8f);
                world.Log("♥ " + displayName + "が" + partner.displayName + "に手作りのお菓子を渡した");
                EndTalk(partner);
                yield break;
            }

            // ---- 告白 ----
            bool confess = !IsTarget && partner.IsTarget && !IsLover && rom.love[id] >= Romance.ConfessAt && world.Now >= rom.cooldownUntil[id];
            if (confess)
            {
                actionText = partner.displayName + "に告白している";
                partner.actionText = displayName + "から告白されている";
                world.Log(displayName + "が" + partner.displayName + "を呼び止めた…");
                ShowBubble("あの…", 1.6f);
                yield return new WaitForSeconds(1.7f);
                ShowBubble("ずっと好きでした！", 2.2f);
                yield return new WaitForSeconds(2.3f);
                partner.ShowBubble("…！", 1.2f);
                yield return new WaitForSeconds(1.3f);
                float aff = world.rel.Get(id, partner.id) / 100f;
                float p = Mathf.Clamp(0.25f + aff * 0.6f + (rom.love[id] - Romance.ConfessAt) / 100f, 0.1f, 0.95f);
                if (Random.value < p)
                {
                    rom.lover[id] = true;
                    var joy = GetComponent<ProtoBody>();
                    if (joy != null) joy.Gesture("Joy", 2.5f);
                    rom.Add(id, 10f, world.simTime);
                    world.rel.Add(id, partner.id, 10f, world.simTime);
                    partner.ShowBubble("ありがとう。よろしく", 2.4f);
                    ShowBubble("♥", 2.4f);
                    SetStage(StatusLine.MoodUp, 2, true);
                    world.Log("★♥ " + displayName + "が" + partner.displayName + "に告白して恋人になった！");
                }
                else
                {
                    partner.ShowBubble("ごめん…", 2.2f);
                    ShowBubble("（涙）", 2.4f);
                    world.ChangeLove(this, -25f);
                    world.rel.Add(id, partner.id, -5f, world.simTime);
                    rom.cooldownUntil[id] = world.Now + 1440.0 * 2.0;
                    SetStage(StatusLine.Love, 0, false, true);
                    NegativeEvent(1f);
                    world.Log(displayName + "の告白は実らなかった…（2日後までは告白しない）");
                }
                loveNeed = 0f;
                yield return new WaitForSeconds(2.5f);
                EndTalk(partner);
                yield break;
            }

            // ---- ふつうの会話／恋バナ／お見舞い ----
            bool visit = partner.sickRest;
            bool koibana = !visit && !IsTarget && !partner.IsTarget && LoveStage >= 1 && partner.LoveStage >= 1 && Random.value < 0.45f;
            TalkTopic tp = visit || koibana ? null : PickTopic();
            string topic = visit ? "お見舞い" : (koibana ? "恋バナ" : tp.text);
            int feelMe = TopicFeeling(tp), feelYou = partner.TopicFeeling(tp);
            actionText = partner.displayName + "と" + topic;
            partner.actionText = displayName + "と" + topic;

            int lines = Random.Range(2, 4) * 2;
            for (int i = 0; i < lines; i++)
            {
                SimCharacter speaker = (i % 2 == 0) ? this : partner;
                string line;
                if (i == 0) line = visit ? "具合どう？" : (koibana ? "ねえ、恋バナしよ？" : Say("topic", "{0}なんだけど…", topic.EndsWith("の話") ? topic.Substring(0, topic.Length - 2) : topic));
                else if (i == 1 && visit) line = "来てくれたんだ…";
                else if (i == 1 && !koibana && feelYou != 0) line = feelYou > 0 ? partner.Say("like", "それ好き！") : partner.Say("dislike", "その話はちょっと…");
                else if (koibana) line = i == 2 && speaker.profile != null && speaker.profile.callF.Length > 0 ? speaker.profile.callF + "ってさ…" : KoibanaLines[Random.Range(0, KoibanaLines.Length)];
                else line = speaker.SayTo("chatter", speaker == this ? partner : this, Chatter[Random.Range(0, Chatter.Length)]);
                speaker.ShowBubble(line, 1.5f);
                yield return new WaitForSeconds(1.6f);
            }

            float bonus = (koibana ? 0.3f : 0f) + (visit ? 0.3f : 0f) + TalkBonus() + partner.TalkBonus();
            // 好きな話題だと楽しく、苦手な話題だと気まずくなりやすい
            bonus += (feelYou > 0 ? 0.25f : (feelYou < 0 ? -0.35f : 0f)) + (feelMe > 0 ? 0.1f : 0f);
            // 篤が話した・そばで聞いた好き嫌いは、図鑑に書かれる
            if (tp != null && (feelYou != 0 || feelMe != 0)) NoteZukan(tp, feelMe, partner, feelYou);
            if ((IsTarget && partner.IsLover) || (IsLover && partner.IsTarget)) bonus += 0.15f;
            if (requestEffort >= 0f && hasOrder) bonus += (requestEffort - 0.4f) * 0.4f;
            bonus += (Ab(Abilities.Charm) + partner.Ab(Abilities.Charm) - 100f) / 500f; // 魅力がある2人ほど楽しくなりやすい
            bool focus = FocusOn(partner) || partner.FocusOn(this);
            if (focus) bonus += 0.15f; // お守り：話すと楽しくなりやすい
            TalkResult result = world.rel.Roll(id, partner.id, bonus);
            if (focus && result != TalkResult.Awkward) world.rel.Add(id, partner.id, result == TalkResult.Fun ? 1.5f : 0.5f, world.simTime); // 仲よくなりやすい
            if (result == TalkResult.Fun) { GrowAbility(Abilities.Charm, 0.15f); partner.GrowAbility(Abilities.Charm, 0.15f); }
            string icon = result == TalkResult.Fun ? "♪ ♥" : (result == TalkResult.Normal ? "…" : "（汗）");
            ShowBubble(icon, 2.2f);
            partner.ShowBubble(icon, 2.2f);
            world.ReportTalk(this, partner, result, koibana);
            if (visit)
            {
                world.rel.Add(id, partner.id, 3f, world.simTime);
                world.Log(displayName + "が" + partner.displayName + "のお見舞いに来た");
            }
            AfterTalkStatuses(partner, result);
            partner.AfterTalkStatuses(this, result);

            float relief = result == TalkResult.Fun ? 45f : (result == TalkResult.Normal ? 30f : 20f);
            lonely = Mathf.Max(0f, lonely - relief);
            partner.lonely = Mathf.Max(0f, partner.lonely - relief - (visit ? 20f : 0f));
            bored = Mathf.Max(0f, bored - 15f);
            partner.bored = Mathf.Max(0f, partner.bored - 15f);
            if (partner.IsTarget) loveNeed = Mathf.Max(0f, loveNeed - 50f);
            if (IsTarget) partner.loveNeed = Mathf.Max(0f, partner.loveNeed - 50f);

            yield return new WaitForSeconds(1.4f);
            EndTalk(partner);
        }

        /// <summary>会話のあとの状態の変化。</summary>
        void AfterTalkStatuses(SimCharacter other, TalkResult result)
        {
            float aff = world.rel.Get(id, other.id);
            if (result == TalkResult.Fun)
            {
                if (aff >= 60f && GetStage(StatusLine.MoodDown) > 0) Ease(StatusLine.MoodDown);
                else if (Chance(0.12f)) Escalate(StatusLine.MoodUp);
                if (GetStage(StatusLine.Lonely) > 0) Ease(StatusLine.Lonely);
                if (other.GetStage(StatusLine.MoodUp) >= 3 && GetStage(StatusLine.MoodUp) == 0 && Chance(0.5f)) SetStage(StatusLine.MoodUp, 1);
                if (other.IsTarget && LoveStage >= 1 && Chance(0.25f)) Escalate(StatusLine.Shy);
            }
            else if (result == TalkResult.Normal)
            {
                if (GetStage(StatusLine.Lonely) > 0 && Chance(0.5f)) Ease(StatusLine.Lonely);
            }
            else if (Chance(0.15f)) NegativeEvent(1f);
            if (other.IsTarget)
            {
                int lv = GetStage(StatusLine.Love);
                if (lv > 0 && lv < 3) Ease(StatusLine.Love);
                if (GetStage(StatusLine.Jealousy) > 0) Ease(StatusLine.Jealousy);
            }
            // 風邪がうつる（熱で寝込んでいる＝2段階以上の人からだけ）
            if (other.GetStage(StatusLine.Cold) >= 2 && GetStage(StatusLine.Cold) == 0 && Chance(0.05f))
            {
                SetStage(StatusLine.Cold, 1);
                world.Log(other.displayName + "の風邪が" + displayName + "にうつった");
            }
        }

        // ================= その場での会話（食事中・お風呂・座っているとき） =================
        static readonly string[] MealLines = { "これおいしいね", "今日のスープ好きかも", "おかわりしようかな", "食べたら何する？", "午後の授業さ…", "このパン、焼きたて！", "デザートあるかな" };
        static readonly string[] BathLines = { "いいお湯〜", "ふぅ…生き返る", "今日も疲れたね", "肩こった〜", "明日の予定は？", "のぼせそう…" };
        static readonly string[] SnackLines = { "おやつ半分こする？", "このクッキーおいしい", "お茶もう一杯どう？", "甘いもの最高…", "夕飯前なのに食べちゃった" };
        static readonly string[] SeatLines = { "ねえねえ", "それでさ…", "わかる〜", "ふふっ", "へえ！", "今日どうだった？", "眠くなってきた" };

        bool IsSeatedState(State s) { return s == State.Eating || s == State.Bathing || s == State.Sitting; }

        void TryAmbientChat()
        {
            if (NoTalk() || sickRest || dozing || hasOrder) return;
            if (Random.value > 0.45f * (0.6f + sociability)) return;
            float range = state == State.Bathing ? 2.6f : 1.8f;
            SimCharacter best = null;
            float total = 0f;
            var cands = new List<SimCharacter>();
            var ws = new List<float>();
            foreach (var o in world.characters)
            {
                if (o == this || !o.Present || o.chatting || o.dozing || o.sickRest || o.NoTalk() || o.hasOrder) continue;
                if (!IsSeatedState(o.state)) continue;
                if ((state == State.Bathing) != (o.state == State.Bathing)) continue;
                Vector3 d = o.transform.position - transform.position;
                if (Mathf.Abs(d.y) > 0.5f || Flat(d).magnitude > range) continue;
                float w = (world.rel.Get(id, o.id) + 10f) / 110f;
                if (o.IsTarget && LoveStage >= 1) w += 0.5f;
                cands.Add(o); ws.Add(w); total += w;
            }
            if (cands.Count == 0) return;
            float r = Random.value * total;
            for (int i = 0; i < cands.Count; i++) { r -= ws[i]; if (r <= 0f) { best = cands[i]; break; } }
            if (best == null) best = cands[cands.Count - 1];
            StartCoroutine(AmbientChat(best));
        }

        bool StillChat(SimCharacter o)
        {
            if (!chatting || !o.chatting || o.chatPartner != this) return false;
            if (!IsSeatedState(state) || !IsSeatedState(o.state)) return false;
            return (o.transform.position - transform.position).sqrMagnitude < 3.2f * 3.2f;
        }

        IEnumerator AmbientChat(SimCharacter o)
        {
            chatting = true; o.chatting = true;
            chatPartner = o; o.chatPartner = this;
            chatTime = 0f; o.chatTime = 0f;
            int token = ++chatToken;
            bool eating = state == State.Eating && o.state == State.Eating;
            bool meal = eating && (act == Act.Meal || o.act == Act.Meal);
            bool bath = state == State.Bathing;
            string key = meal ? "meal" : (eating ? "snack" : (bath ? "bath" : "seat"));
            string[] pool = meal ? MealLines : (eating ? SnackLines : (bath ? BathLines : SeatLines));
            string place = meal ? "食事しながら" : (eating ? "おやつを食べながら" : (bath ? "お風呂の中" : "座りながら"));
            int lines = Random.Range(2, 5);
            bool ok = true;
            for (int i = 0; i < lines; i++)
            {
                if (!StillChat(o)) { ok = false; break; }
                var speaker = i % 2 == 0 ? this : o;
                var sp = speaker.Pool(key, pool);
                speaker.ShowBubble(sp[Random.Range(0, sp.Length)], 1.5f);
                yield return new WaitForSeconds(1.6f);
            }
            if (ok && StillChat(o))
            {
                bool focus = FocusOn(o) || o.FocusOn(this);
                TalkResult result = world.rel.Roll(id, o.id, 0.1f + TalkBonus() + o.TalkBonus() + (Ab(Abilities.Charm) + o.Ab(Abilities.Charm) - 100f) / 500f + (focus ? 0.15f : 0f));
                if (focus && result != TalkResult.Awkward) world.rel.Add(id, o.id, result == TalkResult.Fun ? 1f : 0.3f, world.simTime);
                if (result == TalkResult.Fun) { ShowBubble("♪", 1.2f); o.ShowBubble("♪", 1.2f); }
                world.ReportTalk(this, o, result, false, place);
                AfterTalkStatuses(o, result);
                o.AfterTalkStatuses(this, result);
                lonely = Mathf.Max(0f, lonely - 20f);
                o.lonely = Mathf.Max(0f, o.lonely - 20f);
                bored = Mathf.Max(0f, bored - 8f);
                o.bored = Mathf.Max(0f, o.bored - 8f);
                if (o.IsTarget) loveNeed = Mathf.Max(0f, loveNeed - 25f);
                if (IsTarget) o.loveNeed = Mathf.Max(0f, o.loveNeed - 25f);
            }
            if (o.chatPartner == this) { o.chatting = false; o.chatPartner = null; o.chatCooldown = Random.Range(8f, 16f); }
            if (token == chatToken && chatPartner == o) { chatting = false; chatPartner = null; }
            chatCooldown = Random.Range(8f, 16f);
        }

        void EndChat()
        {
            if (!chatting) return;
            if (chatPartner != null && chatPartner.chatPartner == this) { chatPartner.chatting = false; chatPartner.chatPartner = null; }
            chatting = false;
            chatPartner = null;
        }

        // ================= すれ違いのあいさつ =================
        bool CanGreet()
        {
            if (!Present || state == State.Private || state == State.Talking || state == State.Sleeping || state == State.Bathing) return false;
            if (CurrentPose() != Pose.Stand || chatting || NoTalk()) return false;
            return state == State.Walking || state == State.Approaching || state == State.Idle || state == State.Waiting;
        }

        void TryGreet()
        {
            if (!CanGreet() || !IsMoving) return;
            foreach (var o in world.characters)
            {
                if (o == this || !o.CanGreet()) continue;
                Vector3 d = o.transform.position - transform.position;
                if (Mathf.Abs(d.y) > 0.5f || Flat(d).magnitude > 1.6f) continue;
                if (!world.GreetReady(id, o.id)) continue;
                world.MarkGreet(id, o.id);
                if (Random.value > 0.65f) continue;
                Greet(o);
                return;
            }
        }

        void Greet(SimCharacter o)
        {
            float aff = world.rel.Get(id, o.id);
            float m = world.MinuteOfDay;
            string hk = m < 600f ? "hello_m" : (m >= 1260f ? "hello_n" : "hello_d");
            string hello = Say(hk, m < 600f ? "おはよう" : (m >= 1260f ? "おやすみ" : "やあ"));
            string a, b;
            if (aff < 25f) { a = "（会釈）"; b = "（会釈）"; }
            else if (aff < 60f) { a = hello; b = o.Say(hk, "おつかれ"); }
            else if (aff >= 80f && Random.value < 0.6f) { a = SayTo("hello_s", o, hello); b = o.SayTo("reply", this, "ふふっ"); }
            else { a = SayTo("close", o, "あ、{0}！"); b = o.SayTo("reply", this, Random.value < 0.5f ? "あとで話そ♪" : "ふふっ"); }
            // Fとのすれ違い
            SimCharacter girl = IsTarget ? o : (o.IsTarget ? this : null);
            if (girl != null && girl.LoveStage >= 1)
            {
                var f = IsTarget ? this : o;
                string call = girl.profile != null && girl.profile.callF.Length > 0 ? girl.profile.callF : f.displayName;
                string gl = girl.GetStage(StatusLine.Shy) > 0 ? girl.Say("shy_love", "っ…（目をそらす）")
                    : girl.Say("hello_love" + Mathf.Min(2, girl.LoveStage), girl.LoveStage >= 2 ? "あ…" + call + "♡" : "あ、" + call);
                string fh = f.Say(hk, "やあ");
                if (girl == this) { a = gl; b = fh; } else { a = fh; b = gl; }
                world.ChangeLove(girl, 0.4f);
                girl.lastFContact = world.Now;
            }
            ShowBubble(a, 1.4f);
            o.ShowBubble(b, 1.4f);
            // 仲がいいと手を振る（仮モデルに「手を振る」動きがあるとき）
            if (aff >= 25f)
            {
                var pb = GetComponent<ProtoBody>();
                if (pb != null) pb.Gesture("Wave", 1.2f);
            }
            world.rel.Add(id, o.id, 0.5f, world.simTime);
            lonely = Mathf.Max(0f, lonely - 5f);
            o.lonely = Mathf.Max(0f, o.lonely - 5f);
        }

        void EndTalk(SimCharacter partner)
        {
            partner.ExitTalk();
            ExitTalk();
            target = null;
            hasOrder = false;
            intent = Intent.Talk;
        }

        // ---- 二人きり（以上）の時間：個室へ行き、寄り添って過ごす（描写はしない） ----
        IEnumerator PrivateRoutine(SimCharacter partner)
        {
            path.Clear();
            talkPartner = partner;
            SetState(State.Talking);
            partner.EnterTalk(this);
            privatePending = true;
            partner.privatePending = true;
            var f = IsTarget ? this : partner;
            var girl = IsTarget ? partner : this;

            var group = new List<SimCharacter> { this, partner };
            if (PrivateNight)
            {
                // 夜：二人きりになりたい子（性欲55以上）が集まる（最大4人。篤との関係に関係なく）
                foreach (var c in world.characters)
                {
                    if (group.Count >= MaxPrivateGroup) break;
                    if (group.Contains(c) || c.IsTarget || c.desire < 55f || !c.AvailableForTalk) continue;
                    c.privatePending = true;
                    group.Add(c);
                    c.ShowBubble("私も…", 1.5f);
                }
            }
            else
            {
                // 昼：探しに行く途中でついて来た子と、同じ相手を探しに来ていた子（性欲が最大）だけ
                foreach (var c in world.characters)
                {
                    if (c == this || c == partner || c.IsTarget || privateEscorts.Contains(c) || c.desire < 99.9f) continue;
                    if (c.target == partner && c.intent == Intent.Private && (c.state == State.Approaching || c.state == State.Waiting) && !c.privatePending)
                        privateEscorts.Add(c);
                }
                foreach (var c in privateEscorts)
                {
                    if (group.Count >= MaxPrivateGroup) break;
                    if (c == null || group.Contains(c) || !c.Present || c.InPrivate) continue;
                    c.escortLeader = null;
                    if (c.state == State.Approaching || c.state == State.Waiting) { c.target = null; c.hasOrder = false; c.SetState(State.Idle); }
                    c.privatePending = true;
                    c.ShowBubble("私も…", 1.5f);
                    group.Add(c);
                }
                privateEscorts.Clear();
            }
            ShowBubble("ねえ…エッチしよ？", 1.5f);
            yield return new WaitForSeconds(1.5f);
            partner.ShowBubble("うん", 1.2f);
            yield return new WaitForSeconds(1.2f);

            // 場所を選ぶ：個室（女の子の部屋・篤の部屋）か、開いている場所（温室・図書室・屋上の天文台・中庭）で、ほかの人がいない所
            var spot = PickPrivateSpot(group, f, girl);
            talkPartner = null;
            partner.talkPartner = null;
            if (spot == null) { CancelPrivate(group); yield break; }
            for (int i = 0; i < group.Count; i++) group[i].GoPrivate(spot.walkTo[i % spot.walkTo.Count], spot.name);

            float t = 0f;
            while (true)
            {
                bool all = true;
                foreach (var c in group) if (!c.privateArrived) { all = false; break; }
                if (all) break;
                t += Time.deltaTime;
                if (t > 60f) break;
                yield return null;
            }
            var arrived = group.FindAll(c => c.privateArrived);
            if (!arrived.Contains(f) || arrived.Count < 2) { CancelPrivate(group); yield break; }
            foreach (var c in group) if (!arrived.Contains(c)) c.CancelPrivateSelf();

            double until = world.Now + 30.0 + 10.0 * (arrived.Count - 2);
            var names = new List<string>();
            var spots = spot.Spots(arrived.Count);
            for (int i = 0; i < arrived.Count; i++)
            {
                var sp = spots[Mathf.Min(i, spots.Count - 1)];
                arrived[i].privateGroupSize = arrived.Count;
                arrived[i].EnterPrivate(until, spot.room, sp.pos, sp.facing, sp.floor);
                names.Add(arrived[i].displayName);
            }
            world.Log("♥ " + string.Join("・", names.ToArray()) + "が" + spot.name + "でエッチしている");
            // 夜（21時〜朝）なら「夜をいっしょに過ごした」と記録する（その周だけ。物語の針の条件）
            float mm = world.MinuteOfDay;
            if (mm >= 21f * 60f || LoungeWorld.IsNight(mm))
                foreach (var c in arrived) if (!c.IsTarget && c.letter != null) Loop.NightTogether.Add(c.letter);
        }

        /// <summary>二人きりの時間の場所。</summary>
        class PrivateSpot
        {
            public string name;
            public RoomInfo room;
            public readonly List<Vector3> walkTo = new List<Vector3>();
            public readonly List<Seat> seatList = new List<Seat>();

            public struct Place { public Vector3 pos, facing; public bool floor; }

            public List<Place> Spots(int n)
            {
                var res = new List<Place>();
                if (room != null)
                {
                    // 個室：ベッドの縁に並んで座る（最大4人）
                    Vector3 along = room.bed.forward;
                    Vector3 edgeOut = room.bedEdge;
                    float[] offs = n <= 2 ? new[] { -0.3f, 0.2f } : (n == 3 ? new[] { -0.55f, -0.05f, 0.45f } : new[] { -0.72f, -0.25f, 0.22f, 0.69f });
                    if (n > 4) offs = new[] { -0.72f, -0.25f, 0.22f, 0.69f };
                    foreach (var o in offs) res.Add(new Place { pos = room.bed.position + along * (o * room.bedSpread) + edgeOut * room.bedEdgeDist, facing = edgeOut, floor = false });
                    // 5人目からは、ベッドの前の床に並んで座る（ベッドを向く）
                    for (int i = 4; i < n; i++)
                    {
                        int row = (i - 4) / 4, col = (i - 4) % 4;
                        res.Add(new Place { pos = room.bed.position + along * (offs[col] * room.bedSpread) + edgeOut * (room.bedEdgeDist + 0.75f + row * 0.6f), facing = -edgeOut, floor = true });
                    }
                }
                else foreach (var s in seatList) res.Add(new Place { pos = s.position, facing = s.forward, floor = s.floorSeat });
                return res;
            }
        }

        PrivateSpot PickPrivateSpot(List<SimCharacter> group, SimCharacter f, SimCharacter girl)
        {
            var cands = new List<PrivateSpot>();
            foreach (var r in new[] { girl.room, f.room })
            {
                if (r == null) continue;
                var ps = new PrivateSpot { name = r.AreaName, room = r };
                ps.walkTo.AddRange(new[] { r.bed.approach, r.futon.approach, r.cushions[0].approach, r.cushions[1].approach });
                cands.Add(ps);
            }
            bool bad = world.BadWeatherToday;
            foreach (var place in new[] { LoungeWorld.Greenhouse, LoungeWorld.Library, LoungeWorld.Observatory, LoungeWorld.Courtyard })
            {
                if (!world.PlaceOpen(place)) continue;
                if (bad && (place == LoungeWorld.Observatory || place == LoungeWorld.Courtyard)) continue;
                // ほかの人がいない所だけ
                bool someone = false;
                foreach (var o in world.characters)
                    if (!group.Contains(o) && o.Present && world.AreaName(o.transform.position) == place) { someone = true; break; }
                if (someone) continue;
                // 並んで座れる席（いちばん近くに集まった席）
                var free = world.seats.FindAll(x => x.area == place && x.kind == SeatKind.Sit && x.occupant == null);
                if (free.Count < group.Count) continue;
                var first = free[Random.Range(0, free.Count)];
                free.Sort((x, y) => (x.position - first.position).sqrMagnitude.CompareTo((y.position - first.position).sqrMagnitude));
                var ps = new PrivateSpot { name = place };
                for (int i = 0; i < group.Count; i++) { ps.seatList.Add(free[i]); ps.walkTo.Add(free[i].approach); }
                cands.Add(ps);
            }
            if (cands.Count == 0) return null;
            // 個室は少し選ばれやすい
            float total = 0f;
            var ws = new List<float>();
            foreach (var c in cands) { float w = c.room != null ? 1.4f : 1f; ws.Add(w); total += w; }
            float pick = Random.value * total;
            for (int i = 0; i < cands.Count; i++) { pick -= ws[i]; if (pick <= 0f) return cands[i]; }
            return cands[cands.Count - 1];
        }

        void CancelPrivate(List<SimCharacter> group)
        {
            foreach (var c in group) c.CancelPrivateSelf();
            target = null;
        }

        void CancelPrivateSelf()
        {
            escortLeader = null;
            privatePending = false;
            privateArrived = false;
            if (goal == Goal.Private) goal = Goal.None;
            if (state == State.Talking || state == State.Walking) { path.Clear(); SetState(State.Idle); }
            actionText = "ぼんやりしている";
        }

        void GoPrivate(Vector3 dest, string where = null)
        {
            LeaveCurrentSpot();
            privatePending = true;
            goal = Goal.Private;
            privateArrived = false;
            actionText = (where ?? "部屋") + "へ向かっている";
            if (!GoTo(world.NearestFree(dest), State.Walking)) { privateArrived = true; SetState(State.Idle); }
        }

        bool privateOnFloor;

        const int MaxPrivateGroup = 11; // 一度にエッチできる人数（famuru 決定 2026-10-21）
        int privateGroupSize;

        void EnterPrivate(double until, RoomInfo r, Vector3 pos, Vector3 facing, bool floorSpot)
        {
            escortLeader = null;
            desireMaxAtNight = false; // 二人きりで過ごせたので、朝の寝不足・発情にはならない
            privatePending = false;
            privateArrived = false;
            privateUntil = until;
            privateRoom = r;
            privateOnFloor = floorSpot;
            goal = Goal.None;
            target = null;
            ReleaseSeatAndPoi();
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(facing);
            SetState(State.Private);
            actionText = "エッチしている";
        }

        void EndPrivate()
        {
            if (privateRoom != null) transform.position = world.NearestFree(privateRoom.bed.approach + new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.4f, 0.6f)));
            else transform.position = world.NearestFree(transform.position + transform.forward * 0.7f);
            desire *= 1f - desireBuild.Relief; // たまりが少なければ0に、多いと少し残る
            lonely = Mathf.Max(0f, lonely - 40f);
            loveNeed = 0f;
            if (!IsTarget)
            {
                // 二人きりで過ごすと、篤との関係が進む（篤への気持ち＋6・好感度＋4）
                world.ChangeLove(this, 6f);
                var f = world.RomanceTarget;
                if (f != null) world.rel.Add(id, f.id, 4f, world.simTime);
            }
            actionText = "ぼんやりしている";
            SetState(State.Idle);
            thinkTimer = Random.Range(0.5f, 2f);
            if (pendingOrder != null)
            {
                var who = pendingOrder;
                pendingOrder = null;
                BeginApproach(who, true, Intent.Talk);
            }
        }

        void EnterTalk(SimCharacter by)
        {
            EndChat();
            StopAllCoroutines();
            path.Clear();
            bool seated = state == State.Sitting || state == State.Eating || sickRest;
            if (!seated) ReleaseSeatAndPoi();
            if (!sickRest) goal = Goal.None;
            target = null;
            hasOrder = false;
            dozing = false;
            talkPartner = by;
            SetState(State.Talking);
        }

        void ExitTalk()
        {
            talkPartner = null;
            if (sickRest)
            {
                SetState(State.Sleeping);
                actionText = "熱で寝込んでいる";
                return;
            }
            bool atSeat = seat != null && seat.occupant == this && Flat(transform.position - seat.position).magnitude < 0.3f;
            if (atSeat && eatUntil > world.Now && (act == Act.Meal || act == Act.Snack || act == Act.Tea))
            {
                SetState(State.Eating);
                actionText = act == Act.Tea ? "お茶を飲んでいる" : (act == Act.Snack ? "おやつを食べている" : "食事をしている");
            }
            else if (atSeat)
            {
                SetState(State.Sitting);
                actionText = sitLabel ?? (seat.label + "で休んでいる");
            }
            else
            {
                SetState(State.Idle);
                actionText = "ぼんやりしている";
            }
            thinkTimer = Random.Range(0.5f, 2f);
            if (pendingOrder != null)
            {
                var who = pendingOrder;
                pendingOrder = null;
                ShowBubble("！", 1.5f);
                BeginApproach(who, true, Intent.Talk);
            }
        }

        public void ShowBubble(string text, float seconds)
        {
            bubble = SpeechStyle.Apply(this, text); // 状態で言い方が変わる（SpeechStyle.cs）
            bubbleTimer = seconds;
        }

        // ================= 移動 =================
        bool GoTo(Vector3 dest, State moveState)
        {
            var p = world.FindPath(transform.position, dest);
            path.Clear();
            if (p == null || p.Count == 0)
            {
                ReleaseSeatAndPoi();
                return false;
            }
            path.AddRange(p);
            destination = dest;
            SetState(moveState);
            return true;
        }

        void Move(float dt)
        {
            if (path.Count == 0)
            {
                if (state == State.Walking) Arrive();
                return;
            }
            float effort = (requestEffort >= 0f && hasOrder) ? 0.9f + requestEffort * 0.3f : 1f;
            float step = baseSpeed * Prod(e => e.speed) * effort * dt;
            // 道の点をいくつ越えても、進める分だけ進む（速い速度でも遅くならない）
            while (path.Count > 0 && step > 0f)
            {
                Vector3 pos = transform.position;
                Vector3 to = path[0] - pos;
                float d = to.magnitude;
                if (d <= step)
                {
                    transform.position = path[0];
                    path.RemoveAt(0);
                    step -= d;
                }
                else
                {
                    transform.position = pos + to / d * step;
                    FaceTowards(pos + Flat(to), dt);
                    step = 0f;
                }
            }
        }

        // ================= 動けなくなったときの見張り =================
        // 歩こうとしているのに一定時間ほとんど動かない、または階段の途中で止まっているときは、強制的に位置をずらす（famuru 決定 2026-10-21）
        const float WatchEvery = 3f;   // 調べる間隔（秒）
        const int WatchStillTimes = 2; // この回数つづけて動かなければずらす
        float watchTimer;
        Vector3 watchPos;
        int watchStill;

        void Watchdog(float dt)
        {
            watchTimer += dt;
            if (watchTimer < WatchEvery) return;
            watchTimer = 0f;
            Vector3 p = transform.position;
            float moved = (p - watchPos).magnitude;
            watchPos = p;
            bool wantsMove = state == State.Walking || state == State.Approaching;
            bool offFloor = !LoungeWorld.OnFloorLevel(p) && state != State.Sleeping && state != State.Bathing && state != State.Private;
            if ((wantsMove || offFloor) && moved < 0.2f) watchStill++; else watchStill = 0;
            if (watchStill >= WatchStillTimes) { watchStill = 0; Unstick(offFloor); }
        }

        void Unstick(bool offFloor)
        {
            Vector3 p = transform.position;
            if (offFloor)
            {
                // 階段の途中：いちばん近い階段の上か下に立たせる
                Vector3 best = p;
                float bd = float.MaxValue;
                var ends = new List<Vector3>();
                foreach (var st in world.stairs) { ends.Add(st.bottomApproach); ends.Add(st.topApproach); }
                if (world.roofStair != null) { ends.Add(world.roofStair.bottomApproach); ends.Add(world.roofStair.topApproach); }
                foreach (var e in ends) { float d = (e - p).sqrMagnitude; if (d < bd) { bd = d; best = e; } }
                transform.position = best;
            }
            else if (path.Count > 0)
            {
                // 道の先（同じ高さの、少し先の点）へずらす
                int k = Mathf.Min(3, path.Count - 1);
                while (k > 0 && Mathf.Abs(path[k].y - p.y) > 0.3f) k--;
                if (Mathf.Abs(path[k].y - p.y) > 0.3f) transform.position = world.NearestFree(p + new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f)));
                else { transform.position = path[k]; path.RemoveRange(0, k + 1); }
            }
            else transform.position = world.NearestFree(p + new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f)));
            world.Log("（" + displayName + "が動けなくなっていたので、位置を少しずらした）");
            // 行き先があれば、ずらした所から道を探し直す
            if (state == State.Walking || state == State.Approaching)
            {
                if (state == State.Approaching) { RepathToTarget(); return; }
                var np = world.FindPath(transform.position, destination);
                if (np != null) { path.Clear(); path.AddRange(np); }
                else if (path.Count == 0) Arrive();
            }
            else if (offFloor) SetState(State.Idle);
        }

        void CheckStuck(float dt)
        {
            if (path.Count == 0 || !LoungeWorld.OnFloorLevel(transform.position)) { progressTimer = 0f; progressPos = transform.position; return; }
            progressTimer += dt;
            if (progressTimer < 2.5f) return;
            float moved = (transform.position - progressPos).magnitude;
            progressPos = transform.position;
            progressTimer = 0f;
            if (moved > 0.25f) { stuckCount = 0; return; }
            stuckCount++;
            if (state == State.Approaching)
            {
                if (stuckCount >= 3) { GiveUp(); return; }
                slotOffset += Mathf.PI / 2f;
                RepathToTarget();
                return;
            }
            Vector3 last = path[path.Count - 1];
            if (Flat(last - transform.position).magnitude < 1.2f && Mathf.Abs(last.y - transform.position.y) < 0.5f) { path.Clear(); return; }
            if (stuckCount >= 3)
            {
                path.Clear();
                if (goal == Goal.Private) { privateArrived = true; SetState(State.Idle); return; }
                if (goal == Goal.Bed) { world.Log("（" + displayName + "は道で詰まったので、部屋に戻して寝かせた）"); Arrive(); return; } // 寝に行く途中で詰まった
                LeaveCurrentSpot();
                hasOrder = false;
                SetState(State.Idle);
                actionText = "ぼんやりしている";
                return;
            }
            Vector3 side = new Vector3(Random.Range(-0.6f, 0.6f), 0f, Random.Range(-0.6f, 0.6f));
            Vector3 np = transform.position + side;
            if (world.NavAt(transform.position).IsFree(np)) transform.position = np;
            var p = world.FindPath(transform.position, destination);
            if (p != null) { path.Clear(); path.AddRange(p); }
        }

        void Arrive()
        {
            Goal g = goal;
            goal = Goal.None;
            switch (g)
            {
                case Goal.Seat:
                    if (seat != null)
                    {
                        SnapToSeat();
                        if (act == Act.Snack)
                        {
                            eatUntil = world.Now + 12.0;
                            SetState(State.Eating);
                            actionText = sitLabel ?? "おやつを食べている";
                            if (world.SnackBonus && Random.value < 0.25f) PositiveEvent(1f);
                        }
                        else
                        {
                            SetState(State.Sitting);
                            actionText = sitLabel ?? (seat.label + "で休んでいる");
                        }
                    }
                    else SetState(State.Idle);
                    break;
                case Goal.Poi:
                    if (poi != null)
                    {
                        SetState(State.Activity);
                        actionText = sitLabel ?? poi.label;
                        if (act == Act.Work) workUntil = world.Now + 60;
                    }
                    else SetState(State.Idle);
                    break;
                case Goal.Gather:
                    SetState(State.Activity);
                    actionText = sitLabel;
                    break;
                case Goal.Hover:
                    SetState(State.Activity);
                    activityDuration = 10f;
                    var f = world.RomanceTarget;
                    actionText = f != null ? f.displayName + "を目で追っている" : "ぼんやりしている";
                    break;
                case Goal.Follow:
                    SetState(State.Idle);
                    break;
                case Goal.MoveTo:
                    hasOrder = false;
                    SetState(State.Idle);
                    actionText = "着いた";
                    break;
                case Goal.Counter:
                    if (poi != null) { SetState(State.Activity); actionText = "料理を受け取っている"; mealPending = true; }
                    else GoDiningSeat();
                    break;
                case Goal.DiningSeat:
                    if (seat != null)
                    {
                        SnapToSeat();
                        eatUntil = world.Now + (act == Act.Snack ? 12.0 : (act == Act.Tea ? 15.0 : 20.0));
                        SetState(State.Eating);
                        actionText = act == Act.Snack ? "おやつを食べている" : (act == Act.Tea ? "お茶を飲んでいる" : "食事をしている");
                    }
                    else SetState(State.Idle);
                    break;
                case Goal.Bath:
                    if (seat != null)
                    {
                        SnapToSeat();
                        bathUntil = world.Now + 25.0;
                        SetState(State.Bathing);
                        actionText = "お風呂に入っている";
                    }
                    else SetState(State.Idle);
                    break;
                case Goal.Bed:
                    if (seat != null)
                    {
                        SnapToSeat();
                        napping = false;
                        sleepUntil = bedItem != null ? bedItem.end : world.Now + 400.0;
                        if (bedItem != null) bedItem.done = true;
                        actionText = "寝ている";
                        SetState(State.Sleeping);
                        ShowBubble(Say("night", "おやすみ"), 1.5f);
                        world.Log(displayName + "が寝た");
                    }
                    else SetState(State.Idle);
                    break;
                case Goal.Nap:
                    if (seat != null)
                    {
                        SnapToSeat();
                        if (sickRest)
                        {
                            sleepUntil = ScheduleMaker.WakeTime(id, world.Day + 1, world.holidayWeekdays);
                            actionText = "熱で寝込んでいる";
                            SetState(State.Sleeping);
                            break;
                        }
                        napping = true;
                        sleepUntil = world.Now + 40.0;
                        actionText = seat.room == room ? "昼寝している" : seat.label + "で昼寝している";
                        SetState(State.Sleeping);
                        ShowBubble("zzz", 1.5f);
                    }
                    else SetState(State.Idle);
                    break;
                case Goal.Exit:
                    SetState(State.Away);
                    actionText = awayLabel ?? "出かけている";
                    SetVisible(false);
                    world.Log(displayName + "が出かけた：" + actionText + "（" + ScheduleMaker.Clock(awayUntil) + "ごろ帰る）");
                    break;
                case Goal.Private:
                    SetState(State.Idle);
                    privateArrived = true;
                    actionText = "待ち合わせの場所で待っている";
                    break;
                default:
                    SetState(State.Idle);
                    actionText = "ぼんやりしている";
                    break;
            }
        }

        void SnapToSeat()
        {
            transform.position = seat.position;
            transform.rotation = Quaternion.LookRotation(seat.forward);
        }

        void LeaveCurrentSpot()
        {
            EndChat();
            bool atSeat = seat != null && (state == State.Sitting || state == State.Eating || state == State.Bathing || state == State.Sleeping);
            if (atSeat) transform.position = world.NearestFree(seat.approach);
            ReleaseSeatAndPoi();
            if (goal != Goal.MoveTo) goal = Goal.None;
            mealPending = false;
            readingPick = false;
            snackPick = false;
            bakePending = false;
            dozing = false;
            if (act == Act.Work) act = Act.None;
            if (gathering != null && !gathering.outing) gathering = null;
        }

        void ReleaseSeatAndPoi()
        {
            if (seat != null && seat.occupant == this) seat.occupant = null;
            seat = null;
            if (poi != null && poiSlot >= 0 && poiSlot < poi.occupants.Length && poi.occupants[poiSlot] == this) poi.occupants[poiSlot] = null;
            poi = null;
            poiSlot = -1;
        }

        void SetState(State s)
        {
            state = s;
            stateTime = 0f;
            progressTimer = 0f;
            progressPos = transform.position;
            if (s != State.Walking && s != State.Approaching) stuckCount = 0;
            ApplyPose();
        }

        enum Pose { Stand, Sit, SitFloor, Lie, LieFloor, Soak }

        // 仮モデル（ProtoBody）に渡す今の姿勢
        public BodyPose BodyPoseNow { get { return (BodyPose)(int)CurrentPose(); } }
        public bool IsTalkingNow { get { return state == State.Talking || chatting; } }
        public bool IsEatingNow { get { return state == State.Eating; } }
        public bool IsDozingNow { get { var p = CurrentPose(); return dozing && (p == Pose.Sit || p == Pose.SitFloor); } }
        public bool EyesClosedNow { get { return state == State.Sleeping || dozing; } }
        public bool IsSpeakingNow { get { return (state == State.Talking || chatting) && !string.IsNullOrEmpty(bubble) && bubble.Length > 1 && bubble != "♪" && bubble != "♥"; } }
        /// <summary>今の服：Uniform（制服）／Towel（お風呂）／Sleep（寝間着）／Casual（休日の私服）。ない服は制服で表示される。</summary>
        public string OutfitNow
        {
            get
            {
                if (state == State.Bathing) return "Towel";
                if (state == State.Sleeping && !napping && !dozing) return "Sleep";
                if (world != null && world.IsHolidayToday) return "Casual";
                return "Uniform";
            }
        }
        /// <summary>今の表情：Smile／Sad／Angry／Surprised／Shy（照れ）／""（ふつう）</summary>
        public string FaceNow
        {
            get
            {
                if (!string.IsNullOrEmpty(bubble))
                {
                    if (bubble.Contains("♪") || bubble.Contains("♥") || bubble.Contains("♡") || bubble.Contains("ふふ")) return "Smile";
                    if (bubble.Contains("涙") || bubble.Contains("ごめん")) return "Sad";
                    if (bubble.Contains("！？") || bubble.Contains("えっ")) return "Surprised";
                }
                if (GetStage(StatusLine.Shy) > 0) return "Shy";
                if (GetStage(StatusLine.Jealousy) > 0 || GetStage(StatusLine.Frustration) > 0) return "Angry";
                if (GetStage(StatusLine.MoodDown) > 0 || GetStage(StatusLine.Lonely) >= 2) return "Sad";
                if (GetStage(StatusLine.MoodUp) > 0) return "Smile";
                return "";
            }
        }

        Pose CurrentPose()
        {
            bool onFloor = seat != null && seat.floorSeat;
            if (state == State.Private) return privateOnFloor ? Pose.SitFloor : Pose.Sit;
            if (dozing && state == State.Sleeping) return onFloor ? Pose.SitFloor : Pose.Sit;
            if (state == State.Sleeping || (state == State.Talking && sickRest)) return onFloor ? Pose.LieFloor : Pose.Lie;
            if (state == State.Bathing) return Pose.Soak;
            if (state == State.Sitting || state == State.Eating) return onFloor ? Pose.SitFloor : Pose.Sit;
            if (state == State.Talking && seat != null && seat.kind == SeatKind.Sit && Flat(transform.position - seat.position).magnitude < 0.3f) return onFloor ? Pose.SitFloor : Pose.Sit;
            return Pose.Stand;
        }

        void ApplyPose()
        {
            if (visual == null) return;
            float h = male ? 1.85f : 1.7f;
            switch (CurrentPose())
            {
                case Pose.Sit:
                    visual.localRotation = Quaternion.identity;
                    visual.localScale = new Vector3(0.45f, 0.45f, 0.45f);
                    visual.localPosition = new Vector3(0f, 0.9f, 0f);
                    break;
                case Pose.SitFloor:
                    visual.localRotation = Quaternion.identity;
                    visual.localScale = new Vector3(0.45f, 0.42f, 0.45f);
                    visual.localPosition = new Vector3(0f, 0.45f, 0f);
                    break;
                case Pose.Lie:
                    visual.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    visual.localScale = new Vector3(0.45f, h / 2f, 0.45f);
                    visual.localPosition = new Vector3(0f, 0.72f, 0f);
                    break;
                case Pose.LieFloor:
                    visual.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    visual.localScale = new Vector3(0.45f, h / 2f, 0.45f);
                    visual.localPosition = new Vector3(0f, 0.3f, 0f);
                    break;
                case Pose.Soak:
                    visual.localRotation = Quaternion.identity;
                    visual.localScale = new Vector3(0.45f, 0.45f, 0.45f);
                    visual.localPosition = new Vector3(0f, 0.3f, 0f);
                    break;
                default:
                    visual.localRotation = Quaternion.identity;
                    visual.localScale = new Vector3(0.45f, h / 2f, 0.45f);
                    visual.localPosition = new Vector3(0f, h / 2f, 0f);
                    break;
            }
        }

        public float HeadHeight
        {
            get
            {
                switch (CurrentPose())
                {
                    case Pose.Lie: return 1.0f;
                    case Pose.LieFloor: return 0.7f;
                    case Pose.Soak: return 1.0f;
                    case Pose.Sit: return 1.55f;
                    case Pose.SitFloor: return 1.15f;
                    default: return male ? 2.05f : 1.9f;
                }
            }
        }

        void FaceTowards(Vector3 p, float dt)
        {
            Vector3 d = Flat(p - transform.position);
            if (d.sqrMagnitude < 0.0001f) return;
            Quaternion want = Quaternion.LookRotation(d.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, Mathf.Clamp01(dt * 8f));
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void LateUpdate()
        {
            if (Time.deltaTime <= 0f || CurrentPose() != Pose.Stand || !Present) return;
            if (!LoungeWorld.OnFloorLevel(transform.position)) return;
            var nav = world.NavAt(transform.position);
            bool meMoving = IsMoving;
            foreach (var o in world.characters)
            {
                if (o == this || !o.Present || o.CurrentPose() != Pose.Stand) continue;
                Vector3 d = transform.position - o.transform.position;
                if (Mathf.Abs(d.y) > 0.3f) continue;
                d.y = 0f;
                float m = d.magnitude;
                // ぶつかる距離（話している人どうしは近づける。famuru 決定 2026-10-21 で少し小さく）
                float rad = (state == State.Talking || o.state == State.Talking || state == State.Waiting || o.state == State.Waiting) ? 0.32f : 0.45f;
                if (m >= rad || m < 0.0001f) continue;
                bool oMoving = o.IsMoving;
                Vector3 np;
                if (meMoving && oMoving)
                {
                    if (id < o.id) continue;
                    Vector3 fwd = path.Count > 0 ? Flat(path[0] - transform.position) : d;
                    if (fwd.sqrMagnitude < 0.0001f) continue;
                    fwd.Normalize();
                    Vector3 perp = new Vector3(fwd.z, 0f, -fwd.x);
                    float side = Vector3.Dot(perp, d) >= 0f ? 1f : -1f;
                    np = transform.position + perp * side * Mathf.Min(Time.deltaTime, 0.25f) * 1.2f;
                }
                else if (!meMoving && oMoving)
                {
                    continue;
                }
                else
                {
                    np = transform.position + d / m * (rad - m) * (meMoving ? 0.6f : 0.5f);
                }
                if (nav.IsFree(np)) transform.position = np;
            }
        }
    }
}
