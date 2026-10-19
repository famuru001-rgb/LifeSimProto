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
        enum Intent { Talk, Love, Private, Bake, Give, Request }
        enum Choice { None, Social, Rest, Fun, Snack, Study, Hobby, Love, Nap, Private, Hover, Cling }
        enum AwayKind { Class, Stroll, Outing, Job }

        // ---- 設定 ----
        public int id;
        public string displayName;
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
        public float academic;        // 学力（0〜100）
        public float mana;            // 魔力（0〜100）
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
        float statusCheck, focusMin, highLonelyMin, highBoredMin, desireHighMin;
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
        public bool InPrivate { get { return state == State.Private; } }
        bool OnMission
        {
            get
            {
                return mealPending || snackPick || bakePending || privatePending || goal == Goal.Counter || goal == Goal.DiningSeat || goal == Goal.Bath
                    || goal == Goal.Bed || goal == Goal.Nap || goal == Goal.Exit || goal == Goal.Return || goal == Goal.Private || goal == Goal.MoveTo || act == Act.Work;
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

        float RateMul(NeedKind n)
        {
            float m = 1f;
            foreach (var e in Effects()) { float v; if (e.rate.TryGetValue(n, out v)) m *= v; }
            return m;
        }

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
                desireHighMin = desire > 60f ? desireHighMin + 30f : 0f;
                if (lv == 0 && loveNeed > 70f && Chance(0.3f)) SetStage(StatusLine.Love, 1);
                else if (lv == 1 && loveNeed > 85f && now - lastFContact > 120 && Chance(0.25f)) SetStage(StatusLine.Love, 2);
                else if (lv == 2 && desireHighMin >= 120f && Chance(0.2f)) SetStage(StatusLine.Love, 3);
                if (lv > 0 && loveNeed < 20f && lv < 3) Ease(StatusLine.Love);
            }

            // 風邪の悪化
            if (GetStage(StatusLine.Cold) == 1 && tired > 80f && CanActNow() && Chance(0.25f)) StartFever();

            // 魔力（量で段階が決まる）
            int target = mana >= 95f ? 3 : (mana >= 80f ? 2 : (mana >= 60f ? 1 : 0));
            int ms = GetStage(StatusLine.Mana);
            if (target != ms && GetStage(StatusLine.ManaOut) == 0)
            {
                if (target == 3 && Present && state != State.Sleeping && state != State.Private) ManaBurst();
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
                    academic = Mathf.Max(0f, academic - 2f);
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
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            baseSpeed = male ? 1.35f : 1.2f;
            lonely = Random.Range(10f, 40f);
            tired = Random.Range(0f, 20f);
            bored = Random.Range(10f, 40f);
            hunger = Random.Range(40f, 60f);
            study = Random.Range(10f, 40f);
            hobbyNeed = Random.Range(10f, 40f);
            loveNeed = Random.Range(0f, 30f);
            academic = 20f + diligence * 40f + Random.Range(0f, 15f);
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

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            stateTime += dt;

            UpdateNeeds(dt);
            if (Present)
            {
                if (state == State.Walking || state == State.Approaching) { Move(dt); CheckStuck(dt); }
                if (state == State.Approaching) UpdateApproach(dt);
                if (state == State.Waiting) UpdateWaiting(dt);
                if (state == State.Talking && talkPartner != null) FaceTowards(talkPartner.transform.position, dt);
                if (state == State.Activity && poi != null) FaceTowards(poi.lookAt, dt);
                if (state == State.Activity && poi == null && gathering != null) FaceTowards(gatherFocus, dt);
                if (state == State.Activity && goal == Goal.None && choice == Choice.Hover && world.RomanceTarget != null) FaceTowards(world.RomanceTarget.transform.position, dt);
                if (bubble == null && state != State.Sleeping && state != State.Private) IdleBubbles(dt);
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
        void IdleBubbles(float dt)
        {
            float r = Random.value;
            if (state == State.Activity && act == Act.Hobby && hobby == HobbyKind.Magic && r < dt * 0.4f) ShowBubble("✦", 1.0f);
            else if (GetStage(StatusLine.Mana) >= 2 && r < dt * 0.2f) ShowBubble("（ぱちぱち）", 1.2f);
            else if (GetStage(StatusLine.Love) >= 1 && r < dt * 0.1f) ShowBubble(GetStage(StatusLine.Love) >= 3 ? "…はぁ♡" : "…♡", 1.4f);
            else if (GetStage(StatusLine.Sleep) >= 1 && r < dt * 0.08f) ShowBubble("ふぁ…", 1.2f);
            else if (GetStage(StatusLine.MoodDown) >= 2 && r < dt * 0.06f) ShowBubble("はぁ…", 1.4f);
            else if (GetStage(StatusLine.Frustration) >= 1 && r < dt * 0.06f) ShowBubble("むしゃくしゃ…", 1.4f);
            else if (act == Act.Study && hasAssignment && assignProgress < 100f && r < dt * 0.08f)
                ShowBubble(assignProgress < 50f && (assignDeadline - world.Now) < 1440 ? "終わらない…！" : "課題、課題…", 1.4f);
        }

        // ================= 欲求 =================
        void UpdateNeeds(float dt)
        {
            float gm = dt * world.GameMinutesPerSecond(); // 進んだゲーム内の分
            UpdateStatuses(gm);

            hunger += gm * 0.2f * RateMul(NeedKind.Hunger);
            study += gm * 0.05f * (0.5f + diligence) * RateMul(NeedKind.Study);
            hobbyNeed += gm * 0.09f * RateMul(NeedKind.Hobby);
            sleepy += gm * 0.075f * RateMul(NeedKind.Sleepy);
            if (!IsTarget && LoveStage >= 1) loveNeed += gm * 0.05f * (0.5f + world.romance.love[id] / 50f) * RateMul(NeedKind.Love);
            desire += gm * DesireRate();
            if (GetStage(StatusLine.ManaOut) == 0) mana += gm * (hobby == HobbyKind.Magic ? 0.035f : 0.02f);

            float lonelyMul = RateMul(NeedKind.Lonely), tiredMul = RateMul(NeedKind.Tired), boredMul = RateMul(NeedKind.Bored);
            switch (state)
            {
                case State.Away:
                    if (awayKind == AwayKind.Stroll) { hobbyNeed -= gm * 2.5f; bored -= gm * 2f; }
                    else if (awayKind == AwayKind.Outing) { bored -= gm * 2f; lonely -= gm * 1f; hobbyNeed -= gm * 0.5f; }
                    else if (awayKind == AwayKind.Job) { tired += gm * 0.15f; bored += gm * 0.05f; }
                    else { study -= gm * 0.35f; weekStudy += gm * 0.35f; academic += gm * 0.004f; }
                    break;
                case State.Private:
                    desire -= gm * 4f; lonely -= gm * 2f; loveNeed -= gm * 3f; lastFContact = world.Now; break;
                case State.Sleeping:
                {
                    float recover = 0.6f;
                    if (!napping && !sickRest && !deepSleep && LoungeWorld.IsNight(world.MinuteOfDay) && desire >= 60f) recover *= 0.25f;
                    if (deepSleep) recover *= 1.5f;
                    sleepy -= gm * recover; tired -= dt * 5f; bored -= dt * 2f; lonely += dt * 0.1f;
                    break;
                }
                case State.Walking:
                case State.Approaching:
                    lonely += dt * 0.9f * (0.4f + sociability) * lonelyMul;
                    tired += dt * 0.9f * tiredMul; bored -= dt * 0.3f; break;
                case State.Sitting:
                    lonely += dt * 0.9f * (0.4f + sociability) * lonelyMul;
                    tired -= dt * 3.5f;
                    if (act == Act.Study) Studying(dt, gm);
                    else if (act == Act.Hobby) { hobbyNeed -= dt * 3f; bored -= dt * 2f; focusMin += gm; }
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
                    tired -= dt * 4f; bored -= dt * 2f; break;
                case State.Activity:
                    lonely += dt * 0.9f * (0.4f + sociability) * lonelyMul;
                    tired += dt * 0.3f * tiredMul;
                    if (act == Act.Study) Studying(dt, gm);
                    else if (act == Act.Hobby)
                    {
                        hobbyNeed -= dt * 3f; bored -= dt * 3f; focusMin += gm;
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
                    tired += dt * 0.35f * tiredMul; bored += dt * (0.6f + whim * 0.4f) * boredMul; break;
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
                    world.ChangeLove(this, gm * 0.015f);
                }
            }

            lonely = Mathf.Clamp(lonely, 0f, 100f);
            tired = Mathf.Clamp(tired, 0f, 100f);
            bored = Mathf.Clamp(bored, 0f, 100f);
            hunger = Mathf.Clamp(hunger, 0f, 100f);
            study = Mathf.Clamp(study, 0f, 100f);
            hobbyNeed = Mathf.Clamp(hobbyNeed, 0f, 100f);
            loveNeed = Mathf.Clamp(loveNeed, 0f, 100f);
            sleepy = Mathf.Clamp(sleepy, 0f, 100f);
            desire = Mathf.Clamp(desire, 0f, 100f);
            mana = Mathf.Clamp(mana, 0f, 100f);
            academic = Mathf.Clamp(academic, 0f, 100f);
        }

        void Studying(float dt, float gm)
        {
            float eff = Prod(e => e.studyEfficiency) * (requestEffort >= 0f ? 0.7f + requestEffort * 0.6f : 1f);
            float amount = dt * 2.5f * eff;
            study -= amount;
            weekStudy += amount * 0.4f;
            academic += amount * 0.003f;
            focusMin += gm;
            bored += dt * 0.3f;
            if (hasAssignment && assignProgress < 100f)
            {
                assignProgress += amount * 0.45f * (0.5f + academic / 100f);
                if (assignProgress >= 100f)
                {
                    assignProgress = 100f;
                    academic += 1f;
                    world.Log(displayName + "が課題を終わらせた");
                    ShowBubble("終わった〜！", 1.8f);
                    PositiveEvent(0.3f);
                }
            }
            // 近くで課題をしている子に教える（学力が高い子ほど）
            foreach (var o in world.characters)
            {
                if (o == this || !o.hasAssignment || o.assignProgress >= 100f || o.act != Act.Study) continue;
                if (o.state != State.Sitting && o.state != State.Activity) continue;
                if (academic < o.academic + 10f) continue;
                Vector3 d = o.transform.position - transform.position;
                if (Mathf.Abs(d.y) > 0.5f || d.sqrMagnitude > 2.5f * 2.5f) continue;
                o.assignProgress += amount * 0.15f * (academic / 100f);
                world.rel.Add(id, o.id, amount * 0.02f, world.simTime);
                if (taughtDay != world.Day)
                {
                    taughtDay = world.Day;
                    ShowBubble("ここはね…", 1.5f);
                    world.Log(displayName + "が" + o.displayName + "に課題を教えている");
                }
            }
        }

        /// <summary>性欲の増え方。「好き」以上だけが増え、気持ちが強いほど加速度的に速くなる。Fは自分を好きな子の気持ちで増える。</summary>
        float DesireRate()
        {
            var rom = world.romance;
            if (IsTarget)
            {
                float sum = 0f;
                foreach (var c in world.characters)
                    if (!c.IsTarget && c.LoveStage >= 2) { float x = rom.love[c.id] / 50f; sum += x * x; }
                return Mathf.Min(0.15f, 0.015f * sum) * RateMul(NeedKind.Desire);
            }
            if (LoveStage < 2) return 0f;
            float l = rom.love[id] / 50f;
            return 0.025f * l * l * l * RateMul(NeedKind.Desire);
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
                        awayLabel = it.Label == "試験" ? "試験を受けている" : "授業に出ている";
                        StartLeave(it.Label == "試験" ? "試験を受けに行く" : "授業に出かける");
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
                        if (now > it.start + 90) { it.done = true; continue; }
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
            }
            if (awayKind == AwayKind.Job && world.player != null && IsTarget) world.player.Earn(70, "街のアルバイト");
            gathering = null;
            hasOrder = false;
            if (!GoTo(world.entrancePoint, State.Walking)) { goal = Goal.None; SetState(State.Idle); }
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
            Poi counter = FindPoi(p => p.counter && p.FreeSlot() >= 0);
            if (counter == null) return false;
            bool ok = GoToPoi(counter, Act.Hobby, "お菓子を作っている", 35f);
            bakePending = ok;
            return ok;
        }

        // ================= 行動の選択 =================
        void Think()
        {
            EnsureSchedule();
            double now = world.Now;
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
            if (HandleSchedule(now)) return;
            if (state == State.Approaching || state == State.Waiting || state == State.Walking) return;
            if (hasOrder) return; // プレイヤーの指示を実行中

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

        SimCharacter PrivatePartner()
        {
            if (desire < 60f) return null;
            if (!IsTarget)
            {
                if (LoveStage < 2) return null;
                var f = world.RomanceTarget;
                if (f != null && f.AvailableForTalk && f.desire >= 40f && !f.hasOrder) return f;
                return null;
            }
            if (hasOrder) return null;
            SimCharacter best = null;
            foreach (var c in world.characters)
            {
                if (c == this || c.LoveStage < 2 || !c.AvailableForTalk || c.desire < 40f) continue;
                if (best == null || c.desire > best.desire) best = c;
            }
            return best;
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
                if (p.counter || p.snack || p.hobby >= 0 || p.owner >= 0) continue;
                int fs = p.FreeSlot();
                if (fs < 0) continue;
                float w = 1f / (1f + Vector3.Distance(transform.position, p.slots[fs]) / 6f);
                w *= Mathf.Exp(0.35f * Mathf.Clamp(PlaceBonus(p.slots[fs]), -4f, 6f));
                if (p.dynamicSpot) w *= 4f;
                if (world.BadWeatherToday && p.label == "暖炉にあたっている") w *= 2f;
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
                bool table = s.label == "大きなテーブル";
                bool visit = s.room != null && s.room != room && CanVisitRoom(s.room);
                if (!table && !visit) continue;
                float score = PlaceBonus(s.position) * 0.6f + (visit ? 1.5f : 0.5f) + Random.value - Vector3.Distance(transform.position, s.position) * 0.05f;
                if (score > bestScore) { bestScore = score; bestSeat = s; useDesk = false; }
            }
            string what = hasAssignment && assignProgress < 100f ? "課題をしている" : "勉強している";
            if (useDesk) return GoToPoi(room.desk, Act.Study, "机で" + what, Random.Range(18f, 30f) * mul);
            if (bestSeat == null) return false;
            string label = bestSeat.room != null ? bestSeat.label + "で" + what : what;
            return StartSitAt(bestSeat, Act.Study, label);
        }

        bool StartReadingSeat()
        {
            Seat s = BestSeat(x => (x.area == "談話室" && x.label != "大きなテーブル") || (x.room != null && x.room != room && CanVisitRoom(x.room)));
            if (s == null) return false;
            return StartSitAt(s, Act.Hobby, s.room != null ? s.label + "で本を読んでいる" : "本を読んでいる");
        }

        bool StartHobby()
        {
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
                p = world.nav0.RandomFreePoint(new Rect(-5.5f, -4.5f, 11f, 9f));
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
            world.Log((gift ? giver.displayName + "が" + displayName + "に" : displayName + "が") + "「" + d.name + "」を" + (gift ? "渡した" : "使った"));
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
            if (kind == RequestKind.Rest) p += tired / 200f;
            if (kind == RequestKind.Meet && meet != null) p += (world.rel.Get(id, meet.id) - 40f) / 200f;
            p = Mathf.Clamp(p, 0.05f, 0.95f);
            float roll = Random.value;
            string what = PlayerControl.RequestName(kind, meet);
            if (roll >= p)
            {
                ShowBubble(recent >= 2 ? "また？ ごめん…" : "ごめん、今はちょっと…", 2f);
                world.Log(displayName + "は「" + what + "」を断った");
                return;
            }
            float effort = Mathf.Clamp01((p - roll) / p);
            string mood = effort > 0.6f ? "張り切って" : (effort > 0.25f ? "" : "しぶしぶ");
            ShowBubble(effort > 0.6f ? "まかせて！" : (effort > 0.25f ? "いいよ" : "…わかった"), 1.8f);
            world.Log(displayName + "は「" + what + "」を" + (mood.Length > 0 ? mood : "") + "引き受けた");
            hasPendingRequest = true;
            pendingRequestKind = kind;
            pendingRequestMeet = meet;
            requestEffort = effort;
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
                case State.Private: return "部屋で過ごしている";
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
            else StartCoroutine(TalkRoutine(target));
        }

        bool CanJoinPrivate(SimCharacter by)
        {
            bool pair = (IsTarget && by.LoveStage >= 2) || (LoveStage >= 2 && by.IsTarget);
            return pair && desire >= 40f && !sickRest;
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
        static readonly string[] Topics = { "授業の話", "魔法の練習", "食堂のメニュー", "読んでいる本", "寮のうわさ", "週末の予定", "試験の話", "故郷の話", "使い魔の話", "図書館の話", "課題の話" };
        static readonly string[] Chatter = { "それでね…", "へえ！", "ほんとに？", "うんうん", "わかる〜", "そうなの？", "ふふっ", "なるほど", "えっ！", "それで？" };
        static readonly string[] KoibanaLines = { "Fくんってさ…", "えっ、わかる！", "きゃー", "内緒だよ？", "応援する！", "ふふ、顔赤いよ" };

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
                partner.ShowBubble(giftItem == ItemKind.Present || giftItem == ItemKind.Bouquet ? "わぁ…ありがとう！" : "ありがとう", 1.8f);
                partner.ApplyItem(giftItem, this);
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
            string topic = visit ? "お見舞い" : (koibana ? "恋バナ" : Topics[Random.Range(0, Topics.Length)]);
            actionText = partner.displayName + "と" + topic;
            partner.actionText = displayName + "と" + topic;

            int lines = Random.Range(2, 4) * 2;
            for (int i = 0; i < lines; i++)
            {
                SimCharacter speaker = (i % 2 == 0) ? this : partner;
                string line;
                if (i == 0) line = visit ? "具合どう？" : (koibana ? "ねえ、恋バナしよ？" : topic + "なんだけど…");
                else if (i == 1 && visit) line = "来てくれたんだ…";
                else line = koibana ? KoibanaLines[Random.Range(0, KoibanaLines.Length)] : Chatter[Random.Range(0, Chatter.Length)];
                speaker.ShowBubble(line, 1.5f);
                yield return new WaitForSeconds(1.6f);
            }

            float bonus = (koibana ? 0.3f : 0f) + (visit ? 0.3f : 0f) + TalkBonus() + partner.TalkBonus();
            if ((IsTarget && partner.IsLover) || (IsLover && partner.IsTarget)) bonus += 0.15f;
            if (requestEffort >= 0f && hasOrder) bonus += (requestEffort - 0.4f) * 0.4f;
            TalkResult result = world.rel.Roll(id, partner.id, bonus);
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
            // 風邪がうつる
            if (other.GetStage(StatusLine.Cold) >= 1 && GetStage(StatusLine.Cold) == 0 && Chance(0.05f))
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
            string[] pool = meal ? MealLines : (eating ? SnackLines : (bath ? BathLines : SeatLines));
            string place = meal ? "食事しながら" : (eating ? "おやつを食べながら" : (bath ? "お風呂の中" : "座りながら"));
            int lines = Random.Range(2, 5);
            bool ok = true;
            for (int i = 0; i < lines; i++)
            {
                if (!StillChat(o)) { ok = false; break; }
                var speaker = i % 2 == 0 ? this : o;
                speaker.ShowBubble(pool[Random.Range(0, pool.Length)], 1.5f);
                yield return new WaitForSeconds(1.6f);
            }
            if (ok && StillChat(o))
            {
                TalkResult result = world.rel.Roll(id, o.id, 0.1f + TalkBonus() + o.TalkBonus());
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
            string hello = m < 600f ? "おはよう" : (m >= 1260f ? "おやすみ" : "やあ");
            string a, b;
            if (aff < 25f) { a = "（会釈）"; b = "（会釈）"; }
            else if (aff < 60f) { a = hello; b = hello == "やあ" ? "おつかれ" : hello; }
            else { a = "あ、" + o.displayName + "！"; b = Random.value < 0.5f ? "あとで話そ♪" : "ふふっ"; }
            // Fとのすれ違い
            SimCharacter girl = IsTarget ? o : (o.IsTarget ? this : null);
            if (girl != null && girl.LoveStage >= 1)
            {
                var f = IsTarget ? this : o;
                string gl = girl.GetStage(StatusLine.Shy) > 0 ? "っ…（目をそらす）" : (girl.LoveStage >= 2 ? "あ…" + f.displayName + "くん♡" : "あ、" + f.displayName + "くん");
                if (girl == this) { a = gl; b = hello; } else { a = hello; b = gl; }
                world.ChangeLove(girl, 0.4f);
                girl.lastFContact = world.Now;
            }
            ShowBubble(a, 1.4f);
            o.ShowBubble(b, 1.4f);
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
            foreach (var c in world.characters)
            {
                if (group.Count >= 4) break;
                if (group.Contains(c) || c.IsTarget || c.LoveStage < 2 || c.desire < 55f || !c.AvailableForTalk) continue;
                c.privatePending = true;
                group.Add(c);
                c.ShowBubble("私も…", 1.5f);
            }
            ShowBubble("ちょっといい？", 1.5f);
            yield return new WaitForSeconds(1.5f);
            partner.ShowBubble("うん", 1.2f);
            yield return new WaitForSeconds(1.2f);

            RoomInfo r = (Random.value < 0.5f || f.room == null) ? girl.room : f.room;
            talkPartner = null;
            partner.talkPartner = null;
            if (r == null) { CancelPrivate(group); yield break; }
            var walkTo = new List<Vector3> { r.bed.approach, r.futon.approach, r.cushions[0].approach, r.cushions[1].approach };
            for (int i = 0; i < group.Count; i++) group[i].GoPrivate(walkTo[i % walkTo.Count]);

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

            // 並び：全員ベッドの縁に並んで座る（最大4人）
            Vector3 along = r.bed.forward;
            Vector3 edgeOut = new Vector3(0f, 0f, 1f);
            float[] offs = arrived.Count <= 2 ? new[] { -0.3f, 0.2f } : (arrived.Count == 3 ? new[] { -0.55f, -0.05f, 0.45f } : new[] { -0.72f, -0.25f, 0.22f, 0.69f });
            var spots = new List<KeyValuePair<Vector3, Vector3>>();
            foreach (var o in offs) spots.Add(new KeyValuePair<Vector3, Vector3>(r.bed.position + along * o + edgeOut * 0.15f, edgeOut));
            double until = world.Now + 30.0 + 10.0 * (arrived.Count - 2);
            var names = new List<string>();
            for (int i = 0; i < arrived.Count; i++)
            {
                var sp = spots[Mathf.Min(i, spots.Count - 1)];
                arrived[i].EnterPrivate(until, r, sp.Key, sp.Value, false);
                names.Add(arrived[i].displayName);
            }
            world.Log("♥ " + string.Join("・", names.ToArray()) + "が" + r.AreaName + "で一緒に過ごしている");
        }

        void CancelPrivate(List<SimCharacter> group)
        {
            foreach (var c in group) c.CancelPrivateSelf();
            target = null;
        }

        void CancelPrivateSelf()
        {
            privatePending = false;
            privateArrived = false;
            if (goal == Goal.Private) goal = Goal.None;
            if (state == State.Talking || state == State.Walking) { path.Clear(); SetState(State.Idle); }
            actionText = "ぼんやりしている";
        }

        void GoPrivate(Vector3 dest)
        {
            LeaveCurrentSpot();
            privatePending = true;
            goal = Goal.Private;
            privateArrived = false;
            actionText = "部屋へ向かっている";
            if (!GoTo(world.NearestFree(dest), State.Walking)) { privateArrived = true; SetState(State.Idle); }
        }

        bool privateOnFloor;

        void EnterPrivate(double until, RoomInfo r, Vector3 pos, Vector3 facing, bool floorSpot)
        {
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
            actionText = "一緒に過ごしている";
        }

        void EndPrivate()
        {
            if (privateRoom != null) transform.position = world.NearestFree(privateRoom.bed.approach + new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.4f, 0.6f)));
            desire = 0f;
            lonely = Mathf.Max(0f, lonely - 40f);
            loveNeed = 0f;
            if (GetStage(StatusLine.Love) >= 3) SetStage(StatusLine.Love, 0);
            if (!IsTarget)
            {
                world.ChangeLove(this, 4f);
                var f = world.RomanceTarget;
                if (f != null) world.rel.Add(id, f.id, 2f, world.simTime);
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
            bubble = text;
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
            Vector3 pos = transform.position;
            Vector3 next = path[0];
            Vector3 to = next - pos;
            float effort = (requestEffort >= 0f && hasOrder) ? 0.9f + requestEffort * 0.3f : 1f;
            float step = baseSpeed * Prod(e => e.speed) * effort * dt;
            if (to.magnitude <= step)
            {
                transform.position = next;
                path.RemoveAt(0);
            }
            else
            {
                transform.position = pos + to.normalized * step;
                FaceTowards(pos + Flat(to), dt);
            }
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
                        ShowBubble("おやすみ", 1.5f);
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
                    actionText = "部屋で待っている";
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
                if (m >= 0.55f || m < 0.0001f) continue;
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
                    np = transform.position + perp * side * Time.deltaTime * 1.2f;
                }
                else if (!meMoving && oMoving)
                {
                    continue;
                }
                else
                {
                    np = transform.position + d / m * (0.55f - m) * (meMoving ? 0.6f : 0.5f);
                }
                if (nav.IsFree(np)) transform.position = np;
            }
        }
    }
}
