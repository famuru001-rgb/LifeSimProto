using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 仮の人形1体。1日の予定（食事・授業・お風呂・就寝）を優先し、空き時間は3つの欲求（さみしさ・疲れ・退屈）で行動を選ぶ。
    /// </summary>
    public class SimCharacter : MonoBehaviour
    {
        public enum State { Idle, Walking, Sitting, Activity, Approaching, Waiting, Talking, Eating, Bathing, Sleeping, Away }
        enum Goal { None, Seat, Poi, Wander, Counter, DiningSeat, Bath, Bed, Exit, Return }

        // ---- 設定 ----
        public int id;
        public string displayName;
        public string note;
        public Color color;
        [Range(0, 1)] public float sociability; // 人と話したい度
        [Range(0, 1)] public float whim;        // 気まぐれ度
        public bool male;
        public RoomInfo room;

        // ---- 欲求（0〜100）----
        public float lonely, tired, bored;

        // ---- 状態 ----
        public State state = State.Idle;
        public string actionText = "ぼんやりしている";
        public string bubble;
        float bubbleTimer;
        public bool selected;

        // ---- 1日の予定 ----
        public List<ScheduleItem> schedule = new List<ScheduleItem>();
        int scheduleDay = -1;
        double sleepUntil, awayUntil, eatUntil, bathUntil;
        bool mealPending;
        ScheduleItem bedItem;

        LoungeWorld world;
        Transform visual;
        GameObject ring;
        Renderer[] renderers;
        Collider[] colliders;
        readonly List<Vector3> path = new List<Vector3>();
        Goal goal = Goal.None;
        Seat seat;
        Poi poi;
        int poiSlot = -1;
        float stateTime;
        float activityDuration;
        float thinkTimer;
        float repathTimer;
        float waitTimer;
        float approachTimer;
        SimCharacter target;
        SimCharacter talkPartner;
        SimCharacter pendingOrder;
        bool hasOrder;
        float speed;

        public SimCharacter TalkPartner { get { return talkPartner; } }
        public bool HasOrder { get { return hasOrder; } }
        public bool Present { get { return state != State.Away; } }
        bool OnMission { get { return mealPending || goal == Goal.Counter || goal == Goal.DiningSeat || goal == Goal.Bath || goal == Goal.Bed || goal == Goal.Exit || goal == Goal.Return; } }

        /// <summary>話しかけられる状態か（寝ている・出かけている・入浴中・急いでいるときは不可）。</summary>
        public bool AvailableForTalk
        {
            get
            {
                if (state == State.Sleeping || state == State.Away || state == State.Bathing || state == State.Talking) return false;
                if (OnMission || hasOrder) return false;
                return true;
            }
        }

        public void Init(LoungeWorld w, Transform visualRoot, GameObject selectionRing)
        {
            world = w;
            visual = visualRoot;
            ring = selectionRing;
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            speed = male ? 1.35f : 1.2f;
            lonely = Random.Range(10f, 40f);
            tired = Random.Range(0f, 20f);
            bored = Random.Range(10f, 40f);
            thinkTimer = Random.Range(0.2f, 1.5f);

            // 最初は自分の部屋のベッドで寝ている
            EnsureSchedule();
            if (room != null)
            {
                seat = room.bed;
                seat.occupant = this;
                transform.position = seat.position;
                transform.rotation = Quaternion.LookRotation(seat.forward);
                sleepUntil = ScheduleMaker.WakeTime(id, world.Day);
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

            if (state != State.Away)
            {
                UpdateNeeds(dt);
                if (state == State.Walking || state == State.Approaching) Move(dt);
                if (state == State.Approaching) UpdateApproach(dt);
                if (state == State.Waiting) UpdateWaiting(dt);
                if (state == State.Talking && talkPartner != null) FaceTowards(talkPartner.transform.position, dt);
                if (state == State.Activity && poi != null) FaceTowards(poi.lookAt, dt);
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

        // ================= 欲求 =================
        void UpdateNeeds(float dt)
        {
            switch (state)
            {
                case State.Sleeping:
                    tired -= dt * 5f; lonely += dt * 0.1f; bored -= dt * 2f; break;
                case State.Walking:
                case State.Approaching:
                    lonely += dt * 0.9f * (0.4f + sociability);
                    tired += dt * 0.9f; bored -= dt * 0.3f; break;
                case State.Sitting:
                case State.Eating:
                    lonely += dt * 0.9f * (0.4f + sociability);
                    tired -= dt * 3.5f; bored += dt * (0.5f + whim * 0.4f); break;
                case State.Bathing:
                    tired -= dt * 4f; bored -= dt * 2f; break;
                case State.Activity:
                    lonely += dt * 0.9f * (0.4f + sociability);
                    tired += dt * 0.3f; bored -= dt * 4f; break;
                case State.Talking:
                    if (seat != null) tired -= dt * 2f; else tired += dt * 0.2f;
                    bored -= dt * 1f; break;
                default:
                    lonely += dt * 0.9f * (0.4f + sociability);
                    tired += dt * 0.35f; bored += dt * (0.6f + whim * 0.4f); break;
            }
            lonely = Mathf.Clamp(lonely, 0f, 100f);
            tired = Mathf.Clamp(tired, 0f, 100f);
            bored = Mathf.Clamp(bored, 0f, 100f);
        }

        // ================= 予定 =================
        void EnsureSchedule()
        {
            if (scheduleDay == world.Day) return;
            scheduleDay = world.Day;
            schedule = ScheduleMaker.Make(id, scheduleDay);
            // 始めた時刻より前の予定は済んだことにする
            foreach (var it in schedule) if (it.kind != TaskKind.Bed && it.end < world.Now) it.done = true;
        }

        public ScheduleItem NextItem()
        {
            foreach (var it in schedule) if (!it.done) return it;
            return null;
        }

        /// <summary>予定の時刻になっていれば、その行動を始める。始めたら true。</summary>
        bool HandleSchedule(double now)
        {
            if (hasOrder) return false;
            foreach (var it in schedule)
            {
                if (it.done) continue;
                if (it.start > now) return false;
                switch (it.kind)
                {
                    case TaskKind.Class:
                        if (now >= it.end - 5) { it.done = true; continue; }
                        it.done = true;
                        awayUntil = it.end;
                        StartLeave();
                        return true;
                    case TaskKind.Breakfast:
                    case TaskKind.Lunch:
                    case TaskKind.Dinner:
                        if (now > it.start + 60) { it.done = true; continue; }
                        it.done = true;
                        StartMeal(it.Label);
                        return true;
                    case TaskKind.Bath:
                        if (now > it.start + 90) { it.done = true; continue; }
                        it.done = true;
                        StartBath();
                        return true;
                    case TaskKind.Bed:
                        if (state == State.Sleeping) { it.done = true; return false; }
                        if (goal == Goal.Bed) return true;
                        bedItem = it;
                        StartBed();
                        return true;
                }
            }
            return false;
        }

        void StartMeal(string label)
        {
            LeaveCurrentSpot();
            mealPending = true;
            Poi counter = null;
            foreach (var p in world.pois) if (p.counter) { counter = p; break; }
            int slot = counter != null ? counter.FreeSlot() : -1;
            actionText = label + "を食べに食堂へ";
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
                float score = Random.value * 2f;
                foreach (var o in world.characters)
                {
                    if (o == this || !o.Present) continue;
                    if (Vector3.Distance(o.transform.position, s.position) < 2.2f) score += (world.rel.Get(id, o.id) - 30f) / 20f;
                }
                if (score > bestScore) { bestScore = score; best = s; }
            }
            if (best == null) { mealPending = false; SetState(State.Idle); return; }
            seat = best;
            seat.occupant = this;
            goal = Goal.DiningSeat;
            mealPending = false;
            if (!GoTo(seat.approach, State.Walking)) { goal = Goal.None; SetState(State.Idle); }
        }

        void StartBath()
        {
            LeaveCurrentSpot();
            Seat free = null;
            foreach (var s in world.seats)
                if (s.kind == SeatKind.Bath && s.occupant == null) { free = s; break; }
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

        void StartLeave()
        {
            LeaveCurrentSpot();
            goal = Goal.Exit;
            actionText = "授業に出かける";
            if (!GoTo(world.exitPoint, State.Walking)) { goal = Goal.None; SetState(State.Idle); }
        }

        void ReturnFromClass()
        {
            transform.position = world.exitPoint;
            SetVisible(true);
            world.Log(displayName + "が授業から帰ってきた");
            goal = Goal.Return;
            actionText = "授業から帰ってきた";
            if (!GoTo(world.entrancePoint, State.Walking)) { goal = Goal.None; SetState(State.Idle); }
        }

        void WakeUp()
        {
            if (seat != null && seat.occupant == this)
            {
                transform.position = world.NearestFree(seat.approach);
                seat.occupant = null;
            }
            seat = null;
            EnsureSchedule();
            actionText = "起きた";
            ShowBubble("ふあ〜", 2f);
            SetState(State.Idle);
            thinkTimer = Random.Range(1f, 3f);
        }

        void SetVisible(bool v)
        {
            foreach (var r in renderers) if (r != null && r.gameObject != ring) r.enabled = v;
            foreach (var c in colliders) if (c != null) c.enabled = v;
            if (!v) selected = false;
        }

        // ================= 行動の選択 =================
        void Think()
        {
            EnsureSchedule();
            double now = world.Now;

            switch (state)
            {
                case State.Talking:
                    return;
                case State.Sleeping:
                    if (now >= sleepUntil) WakeUp();
                    return;
                case State.Away:
                    if (now >= awayUntil) ReturnFromClass();
                    return;
                case State.Eating:
                    if (now < eatUntil) return;
                    ShowBubble("ごちそうさま", 1.8f);
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
                    break;
            }

            if (HandleSchedule(now)) return;
            if (state == State.Approaching || state == State.Waiting || state == State.Walking) return;

            float sSocial = lonely * (0.7f + 0.6f * sociability);
            float sRest = tired;
            float sFun = bored;
            float noise = 40f * whim;
            sSocial += (Random.value - 0.5f) * noise;
            sRest += (Random.value - 0.5f) * noise;
            sFun += (Random.value - 0.5f) * noise;

            if (state == State.Sitting)
            {
                if (stateTime < 5f) return;
                float keep = Mathf.Max(sRest, 30f);
                if (sSocial < keep + 20f && sFun < keep + 20f && stateTime < 30f) return;
            }
            if (state == State.Activity)
            {
                if (stateTime < activityDuration && sSocial < 75f) return;
            }

            float best = Mathf.Max(sSocial, Mathf.Max(sRest, sFun));
            if (state == State.Sitting && best == sRest) return;
            if (state == State.Activity && best == sFun && stateTime < activityDuration * 2f) return;
            if (best < 30f)
            {
                if (state == State.Sitting || state == State.Activity) { if (stateTime < 25f) return; }
                if (Random.value < 0.5f) StartWander(); else StartSit();
                return;
            }
            if (best == sSocial) { if (!StartSocial()) StartWander(); }
            else if (best == sRest) { if (!StartSit()) StartWander(); }
            else { if (!StartActivity()) StartWander(); }
        }

        bool StartSocial()
        {
            var others = world.characters;
            float total = 0f;
            var weights = new float[others.Count];
            float shy = 1f - sociability;
            for (int i = 0; i < others.Count; i++)
            {
                var o = others[i];
                if (o == this || !o.AvailableForTalk) continue;
                float a = world.rel.Get(id, o.id);
                float w = Mathf.Pow((a + 10f) / 110f, 1f + shy * 3f);
                float dist = Vector3.Distance(transform.position, o.transform.position);
                w *= 1f / (1f + dist / 8f);
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
                    BeginApproach(others[i], false);
                    return true;
                }
            }
            return false;
        }

        bool StartSit()
        {
            Seat best = null;
            float bestScore = float.MinValue;
            foreach (var s in world.seats)
            {
                if (s.kind != SeatKind.Sit) continue;
                if (s.occupant != null && s.occupant != this) continue;
                float score = -Vector3.Distance(transform.position, s.approach) * 0.4f;
                if (Mathf.Abs(s.position.y - transform.position.y) > 1f) score -= 4f;
                foreach (var o in world.characters)
                {
                    if (o == this || !o.Present) continue;
                    float d = Vector3.Distance(o.transform.position, s.position);
                    if (d < 3f) score += (world.rel.Get(id, o.id) - 30f) / 25f * (1f - sociability * 0.3f);
                }
                score += Random.value * (1f + whim * 3f);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            if (best == null) return false;
            LeaveCurrentSpot();
            seat = best;
            seat.occupant = this;
            goal = Goal.Seat;
            actionText = seat.label + "で休みに行く";
            return GoTo(seat.approach, State.Walking);
        }

        bool StartActivity()
        {
            var candidates = new List<Poi>();
            var weights = new List<float>();
            float total = 0f;
            foreach (var p in world.pois)
            {
                if (p.counter) continue;
                if (p.owner >= 0 && p.owner != id) continue;
                int fs = p.FreeSlot();
                if (fs < 0) continue;
                float w = 1f / (1f + Vector3.Distance(transform.position, p.slots[fs]) / 6f);
                if (p.owner == id) w *= 1.5f;
                candidates.Add(p);
                weights.Add(w);
                total += w;
            }
            if (candidates.Count == 0) return false;
            float r = Random.value * total;
            Poi chosen = candidates[candidates.Count - 1];
            for (int i = 0; i < candidates.Count; i++) { r -= weights[i]; if (r <= 0f) { chosen = candidates[i]; break; } }
            LeaveCurrentSpot();
            poi = chosen;
            poiSlot = poi.FreeSlot();
            poi.occupants[poiSlot] = this;
            goal = Goal.Poi;
            actionText = poi.label + "（向かっている）";
            activityDuration = Random.Range(8f, 16f);
            return GoTo(poi.slots[poiSlot], State.Walking);
        }

        void StartWander()
        {
            LeaveCurrentSpot();
            goal = Goal.Wander;
            actionText = "ぶらぶら歩く";
            AreaInfo area = world.AreaAt(transform.position);
            Vector3 p;
            if (area == null || area.name == "外" || Random.value < 0.35f)
                p = world.nav0.RandomFreePoint(new Rect(-5.5f, -4.5f, 11f, 9f));
            else
                p = world.NavAt(transform.position).RandomFreePoint(area.rect);
            if (!GoTo(p, State.Walking)) SetState(State.Idle);
        }

        // ================= 会いに行く =================
        void BeginApproach(SimCharacter who, bool order)
        {
            LeaveCurrentSpot();
            target = who;
            hasOrder = order;
            approachTimer = 0f;
            repathTimer = 0f;
            actionText = who.displayName + "に会いに行く" + (order ? "（指示）" : "");
            SetState(State.Approaching);
            RepathToTarget();
        }

        /// <summary>プレイヤーからの指示「〇〇に会いに行って」。</summary>
        public void Order(SimCharacter who)
        {
            if (who == null || who == this) return;
            if (state == State.Sleeping || state == State.Away || state == State.Bathing)
            {
                world.Log(displayName + "は今は動けない（" + StateName() + "）");
                return;
            }
            if (who.state == State.Sleeping || who.state == State.Away || who.state == State.Bathing)
            {
                ShowBubble(who.displayName + "は今いないみたい", 2.2f);
                world.Log(who.displayName + "は今会えない（" + who.StateName() + "）");
                return;
            }
            if (state == State.Talking)
            {
                pendingOrder = who;
                ShowBubble("！（あとで行くね）", 1.8f);
                return;
            }
            ShowBubble("！", 1.5f);
            BeginApproach(who, true);
        }

        public string StateName()
        {
            switch (state)
            {
                case State.Sleeping: return "寝ている";
                case State.Away: return "授業に出ている";
                case State.Bathing: return "お風呂";
                case State.Eating: return "食事中";
                case State.Talking: return "会話中";
                default: return actionText;
            }
        }

        void UpdateApproach(float dt)
        {
            if (target == null || !target.Present || target.state == State.Sleeping || target.state == State.Bathing) { GiveUp(); return; }
            approachTimer += dt;
            repathTimer -= dt;
            Vector3 d3 = target.transform.position - transform.position;
            float d = Flat(d3).magnitude;
            bool sameFloor = Mathf.Abs(d3.y) < 0.5f;
            if (sameFloor && d <= 1.3f)
            {
                if (target.CanStartTalk(this)) { StartCoroutine(TalkRoutine(target)); return; }
                path.Clear();
                waitTimer = 0f;
                actionText = target.displayName + "の用事が終わるのを待つ";
                SetState(State.Waiting);
                return;
            }
            if ((repathTimer <= 0f || path.Count == 0) && LoungeWorld.OnFloorLevel(transform.position)) RepathToTarget();
            if (!hasOrder && approachTimer > 30f) GiveUp();
        }

        void UpdateWaiting(float dt)
        {
            waitTimer += dt;
            if (target == null || !target.Present) { GiveUp(); return; }
            FaceTowards(target.transform.position, dt);
            if (target.CanStartTalk(this))
            {
                Vector3 d3 = target.transform.position - transform.position;
                if (Flat(d3).magnitude <= 1.4f && Mathf.Abs(d3.y) < 0.5f) { StartCoroutine(TalkRoutine(target)); return; }
                actionText = target.displayName + "に会いに行く" + (hasOrder ? "（指示）" : "");
                SetState(State.Approaching);
                RepathToTarget();
                return;
            }
            if (waitTimer > (hasOrder ? 20f : 6f)) GiveUp();
        }

        void GiveUp()
        {
            if (target != null) world.Log(displayName + "は" + target.displayName + "に会うのをあきらめた");
            target = null;
            hasOrder = false;
            bored += 10f;
            path.Clear();
            SetState(State.Idle);
            actionText = "ぼんやりしている";
        }

        void RepathToTarget()
        {
            repathTimer = 1.0f;
            if (target == null) return;
            Vector3 tp = target.transform.position;
            Vector3 dir = Flat(transform.position - tp);
            if (dir.sqrMagnitude < 0.01f || Mathf.Abs(transform.position.y - tp.y) > 0.5f) dir = target.transform.forward;
            Vector3 goalPos = world.NearestFree(tp + dir.normalized * 1.0f);
            var p = world.FindPath(transform.position, goalPos);
            path.Clear();
            if (p != null) path.AddRange(p);
        }

        public bool CanStartTalk(SimCharacter by)
        {
            if (state == State.Talking || state == State.Sleeping || state == State.Away || state == State.Bathing) return false;
            if (hasOrder && target != by) return false;
            if (OnMission) return false;
            return true;
        }

        // ================= 会話 =================
        static readonly string[] Topics = { "授業の話", "魔法の練習", "食堂のメニュー", "読んでいる本", "寮のうわさ", "週末の予定", "試験の話", "故郷の話", "使い魔の話", "図書館の話" };
        static readonly string[] Chatter = { "それでね…", "へえ！", "ほんとに？", "うんうん", "わかる〜", "そうなの？", "ふふっ", "なるほど", "えっ！", "それで？" };

        IEnumerator TalkRoutine(SimCharacter partner)
        {
            path.Clear();
            talkPartner = partner;
            SetState(State.Talking);
            partner.EnterTalk(this);
            string topic = Topics[Random.Range(0, Topics.Length)];
            actionText = partner.displayName + "と" + topic;
            partner.actionText = displayName + "と" + topic;

            int lines = Random.Range(2, 4) * 2;
            for (int i = 0; i < lines; i++)
            {
                SimCharacter speaker = (i % 2 == 0) ? this : partner;
                speaker.ShowBubble(i == 0 ? topic + "なんだけど…" : Chatter[Random.Range(0, Chatter.Length)], 1.5f);
                yield return new WaitForSeconds(1.6f);
            }

            TalkResult result = world.rel.Roll(id, partner.id);
            string icon = result == TalkResult.Fun ? "♪ ♥" : (result == TalkResult.Normal ? "…" : "（汗）");
            ShowBubble(icon, 2.2f);
            partner.ShowBubble(icon, 2.2f);
            world.ReportTalk(this, partner, result);

            float relief = result == TalkResult.Fun ? 45f : (result == TalkResult.Normal ? 30f : 20f);
            lonely = Mathf.Max(0f, lonely - relief);
            partner.lonely = Mathf.Max(0f, partner.lonely - relief);
            bored = Mathf.Max(0f, bored - 15f);
            partner.bored = Mathf.Max(0f, partner.bored - 15f);

            yield return new WaitForSeconds(1.4f);

            partner.ExitTalk();
            ExitTalk();
            target = null;
            hasOrder = false;
        }

        void EnterTalk(SimCharacter by)
        {
            StopAllCoroutines();
            path.Clear();
            bool seated = state == State.Sitting || state == State.Eating;
            if (!seated) ReleaseSeatAndPoi();
            goal = Goal.None;
            target = null;
            hasOrder = false;
            talkPartner = by;
            SetState(State.Talking);
        }

        void ExitTalk()
        {
            talkPartner = null;
            bool atSeat = seat != null && seat.occupant == this && Flat(transform.position - seat.position).magnitude < 0.3f;
            if (atSeat && seat.area == "食堂" && eatUntil > world.Now)
            {
                SetState(State.Eating);
                actionText = "食事をしている";
            }
            else if (atSeat)
            {
                SetState(State.Sitting);
                actionText = seat.label + "で休んでいる";
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
                BeginApproach(who, true);
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
            float step = speed * dt;
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

        void Arrive()
        {
            Goal g = goal;
            goal = Goal.None;
            switch (g)
            {
                case Goal.Seat:
                    if (seat != null) { SnapToSeat(); SetState(State.Sitting); actionText = seat.label + "で休んでいる"; }
                    else SetState(State.Idle);
                    break;
                case Goal.Poi:
                    if (poi != null) { SetState(State.Activity); actionText = poi.label; }
                    else SetState(State.Idle);
                    break;
                case Goal.Counter:
                    if (poi != null) { SetState(State.Activity); actionText = poi.label; mealPending = true; }
                    else GoDiningSeat();
                    break;
                case Goal.DiningSeat:
                    if (seat != null)
                    {
                        SnapToSeat();
                        eatUntil = world.Now + 20.0;
                        SetState(State.Eating);
                        actionText = "食事をしている";
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
                        sleepUntil = bedItem != null ? bedItem.end : world.Now + 400.0;
                        if (bedItem != null) bedItem.done = true;
                        actionText = "寝ている";
                        SetState(State.Sleeping);
                        ShowBubble("おやすみ", 1.5f);
                        world.Log(displayName + "が寝た");
                    }
                    else SetState(State.Idle);
                    break;
                case Goal.Exit:
                    SetState(State.Away);
                    actionText = "授業に出ている";
                    SetVisible(false);
                    world.Log(displayName + "が授業に出かけた（" + ScheduleMaker.Clock(awayUntil) + "ごろ帰る）");
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
            bool atSeat = seat != null && (state == State.Sitting || state == State.Eating || state == State.Bathing || state == State.Sleeping);
            if (atSeat) transform.position = world.NearestFree(seat.approach);
            ReleaseSeatAndPoi();
            goal = Goal.None;
            mealPending = false;
        }

        void ReleaseSeatAndPoi()
        {
            if (seat != null && seat.occupant == this) seat.occupant = null;
            seat = null;
            if (poi != null && poiSlot >= 0 && poi.occupants[poiSlot] == this) poi.occupants[poiSlot] = null;
            poi = null;
            poiSlot = -1;
        }

        void SetState(State s)
        {
            state = s;
            stateTime = 0f;
            ApplyPose();
        }

        enum Pose { Stand, Sit, Lie, Soak }

        Pose CurrentPose()
        {
            if (state == State.Sleeping) return Pose.Lie;
            if (state == State.Bathing) return Pose.Soak;
            if (state == State.Sitting || state == State.Eating) return Pose.Sit;
            if (state == State.Talking && seat != null && seat.kind == SeatKind.Sit && Flat(transform.position - seat.position).magnitude < 0.3f) return Pose.Sit;
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
                case Pose.Lie:
                    visual.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    visual.localScale = new Vector3(0.45f, h / 2f, 0.45f);
                    visual.localPosition = new Vector3(0f, 0.72f, 0f);
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
                    case Pose.Soak: return 1.0f;
                    case Pose.Sit: return 1.55f;
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

        // 立っている人同士が重ならないように少し離す
        void LateUpdate()
        {
            if (Time.deltaTime <= 0f || CurrentPose() != Pose.Stand || !Present) return;
            if (!LoungeWorld.OnFloorLevel(transform.position)) return;
            var nav = world.NavAt(transform.position);
            foreach (var o in world.characters)
            {
                if (o == this || !o.Present || o.CurrentPose() != Pose.Stand) continue;
                Vector3 d = transform.position - o.transform.position;
                if (Mathf.Abs(d.y) > 0.3f) continue;
                d.y = 0f;
                float m = d.magnitude;
                if (m < 0.55f && m > 0.0001f)
                {
                    Vector3 push = d / m * (0.55f - m) * 0.5f;
                    Vector3 np = transform.position + push;
                    if (nav.IsFree(np)) transform.position = np;
                }
            }
        }
    }
}
