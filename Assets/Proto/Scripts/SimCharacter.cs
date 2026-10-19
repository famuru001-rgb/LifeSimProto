using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 仮の人形1体。1日の予定（食事・授業・お風呂・就寝）を優先し、空き時間は9つの欲求で行動を選ぶ。
    /// 欲求：さみしさ・疲れ・退屈・空腹・勉強・趣味・恋心・睡眠・性欲（性欲は恋人同士のみ。描写はしない）
    /// </summary>
    public class SimCharacter : MonoBehaviour
    {
        public enum State { Idle, Walking, Sitting, Activity, Approaching, Waiting, Talking, Eating, Bathing, Sleeping, Away, Private }
        public enum HobbyKind { Reading, Magic, Tea, Walk }
        enum Goal { None, Seat, Poi, Wander, Counter, DiningSeat, Bath, Bed, Nap, Exit, Return, Private }
        enum Act { None, Rest, Fun, Study, Hobby, Meal, Snack, Tea }
        enum Intent { Talk, Love, Private }
        enum Choice { None, Social, Rest, Fun, Snack, Study, Hobby, Love, Nap, Private }
        enum SeatFilter { Any, Lounge, BigTable }

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

        // ---- 状態 ----
        public State state = State.Idle;
        public string actionText = "ぼんやりしている";
        public string bubble;
        float bubbleTimer;
        public bool selected;
        public Vector3 approachGoal;

        // ---- 1日の予定 ----
        public List<ScheduleItem> schedule = new List<ScheduleItem>();
        int scheduleDay = -1;
        double sleepUntil, awayUntil, eatUntil, bathUntil, privateUntil;
        bool mealPending, readingPick, privatePending, privateArrived, strolling;
        ScheduleItem bedItem;
        RoomInfo privateRoom;

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
        float speed;
        Vector3 destination;
        Vector3 progressPos;
        float progressTimer;
        int stuckCount;

        public SimCharacter TalkPartner { get { return talkPartner; } }
        public bool HasOrder { get { return hasOrder; } }
        public bool Present { get { return state != State.Away && state != State.Private; } }
        public bool IsMoving { get { return (state == State.Walking || state == State.Approaching) && path.Count > 0; } }
        bool OnMission
        {
            get
            {
                return mealPending || privatePending || goal == Goal.Counter || goal == Goal.DiningSeat || goal == Goal.Bath
                    || goal == Goal.Bed || goal == Goal.Nap || goal == Goal.Exit || goal == Goal.Return || goal == Goal.Private;
            }
        }

        // ---- 恋愛 ----
        public bool IsTarget { get { return world != null && world.romance != null && id == world.romance.targetId; } }
        public int LoveStage { get { return IsTarget ? -1 : world.romance.Stage(id); } }
        public bool IsLover { get { return !IsTarget && world.romance.lover[id]; } }
        bool HasLoverRole { get { return IsLover || (IsTarget && world.romance.HasAnyLover()); } }

        /// <summary>話しかけられる状態か（寝ている・出かけている・入浴中・急いでいるときは不可）。</summary>
        public bool AvailableForTalk
        {
            get
            {
                if (!Present || state == State.Sleeping || state == State.Bathing || state == State.Talking) return false;
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
            hunger = Random.Range(40f, 60f);
            study = Random.Range(10f, 40f);
            hobbyNeed = Random.Range(10f, 40f);
            loveNeed = Random.Range(0f, 30f);
            sleepy = 0f;
            desire = 0f;
            thinkTimer = Random.Range(0.2f, 1.5f);

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

            UpdateNeeds(dt);
            if (Present)
            {
                if (state == State.Walking || state == State.Approaching) { Move(dt); CheckStuck(dt); }
                if (state == State.Approaching) UpdateApproach(dt);
                if (state == State.Waiting) UpdateWaiting(dt);
                if (state == State.Talking && talkPartner != null) FaceTowards(talkPartner.transform.position, dt);
                if (state == State.Activity && poi != null) FaceTowards(poi.lookAt, dt);
                if (state == State.Activity && act == Act.Hobby && hobby == HobbyKind.Magic && bubble == null && Random.value < dt * 0.4f) ShowBubble("✦", 1.0f);
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
            float gm = dt * world.GameMinutesPerSecond(); // 進んだゲーム内の分

            // 時間とともに増えるもの（ゲーム内の時間で）
            hunger += gm * 0.2f;
            study += gm * 0.05f * (0.5f + diligence);
            hobbyNeed += gm * 0.09f;
            sleepy += gm * 0.075f;
            if (!IsTarget && LoveStage >= 1) loveNeed += gm * 0.05f * (0.5f + world.romance.love[id] / 50f);
            if (HasLoverRole) desire += gm * 0.07f;

            switch (state)
            {
                case State.Away:
                    if (strolling) { hobbyNeed -= gm * 2.5f; bored -= gm * 2f; }
                    else study -= gm * 0.35f;
                    break;
                case State.Private:
                    desire -= gm * 4f; lonely -= gm * 2f; loveNeed -= gm * 3f; break;
                case State.Sleeping:
                    sleepy -= gm * 0.6f; tired -= dt * 5f; bored -= dt * 2f; lonely += dt * 0.1f; break;
                case State.Walking:
                case State.Approaching:
                    lonely += dt * 0.9f * (0.4f + sociability);
                    tired += dt * 0.9f; bored -= dt * 0.3f; break;
                case State.Sitting:
                    lonely += dt * 0.9f * (0.4f + sociability);
                    tired -= dt * 3.5f;
                    if (act == Act.Study) { study -= dt * 2.5f; bored += dt * 0.3f; }
                    else if (act == Act.Hobby) { hobbyNeed -= dt * 3f; bored -= dt * 2f; }
                    else bored += dt * (0.5f + whim * 0.4f);
                    break;
                case State.Eating:
                    lonely += dt * 0.6f * (0.4f + sociability);
                    tired -= dt * 3f;
                    hunger -= gm * (act == Act.Tea ? 1.5f : 4f);
                    if (act == Act.Tea) { hobbyNeed -= dt * 3f; bored -= dt * 2f; }
                    break;
                case State.Bathing:
                    tired -= dt * 4f; bored -= dt * 2f; break;
                case State.Activity:
                    lonely += dt * 0.9f * (0.4f + sociability);
                    tired += dt * 0.3f;
                    if (act == Act.Study) { study -= dt * 2.5f; bored += dt * 0.2f; }
                    else if (act == Act.Hobby) { hobbyNeed -= dt * 3f; bored -= dt * 3f; }
                    else bored -= dt * 4f;
                    break;
                case State.Talking:
                    if (seat != null) tired -= dt * 2f; else tired += dt * 0.2f;
                    bored -= dt * 1f; break;
                default:
                    lonely += dt * 0.9f * (0.4f + sociability);
                    tired += dt * 0.35f; bored += dt * (0.6f + whim * 0.4f); break;
            }

            // Fの近くにいると恋心が落ち着き、気持ちが少しずつ育つ
            var f = world.RomanceTarget;
            if (!IsTarget && f != null && f.Present && Present && LoveStage >= 0)
            {
                Vector3 d = f.transform.position - transform.position;
                if (Mathf.Abs(d.y) < 1f && d.sqrMagnitude < 9f)
                {
                    loveNeed -= gm * 0.3f;
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
        }

        // ================= 予定 =================
        void EnsureSchedule()
        {
            if (scheduleDay == world.Day) return;
            scheduleDay = world.Day;
            schedule = ScheduleMaker.Make(id, scheduleDay);
            foreach (var it in schedule) if (it.kind != TaskKind.Bed && it.end < world.Now) it.done = true;
        }

        /// <summary>予定の時刻になっていれば、その行動を始める。始めたら true。</summary>
        bool HandleSchedule(double now)
        {
            if (hasOrder || privatePending) return false;
            foreach (var it in schedule)
            {
                if (it.done) continue;
                // 眠いときは少し早めに寝る
                bool earlyBed = it.kind == TaskKind.Bed && sleepy > 70f && now >= it.start - 75;
                if (it.start > now && !earlyBed) return false;
                switch (it.kind)
                {
                    case TaskKind.Class:
                        if (now >= it.end - 5) { it.done = true; continue; }
                        it.done = true;
                        awayUntil = it.end;
                        StartLeave(false);
                        return true;
                    case TaskKind.Breakfast:
                    case TaskKind.Lunch:
                    case TaskKind.Dinner:
                        if (now > it.start + 60) { it.done = true; continue; }
                        it.done = true;
                        StartMeal(Act.Meal, it.Label);
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

        void StartMeal(Act a, string label)
        {
            LeaveCurrentSpot();
            act = a;
            mealPending = true;
            Poi counter = null;
            foreach (var p in world.pois) if (p.counter) { counter = p; break; }
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
                float score = Random.value * 2f;
                foreach (var o in world.characters)
                {
                    if (o == this || !o.Present) continue;
                    if (Vector3.Distance(o.transform.position, s.position) < 2.2f)
                    {
                        score += (world.rel.Get(id, o.id) - 30f) / 20f;
                        if (IsLover && o.IsTarget) score += 3f;
                    }
                }
                if (score > bestScore) { bestScore = score; best = s; }
            }
            mealPending = false;
            if (best == null) { SetState(State.Idle); return; }
            seat = best;
            seat.occupant = this;
            goal = Goal.DiningSeat;
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

        bool StartNap()
        {
            if (room == null || (room.bed.occupant != null && room.bed.occupant != this)) return false;
            LeaveCurrentSpot();
            seat = room.bed;
            seat.occupant = this;
            goal = Goal.Nap;
            choice = Choice.Nap;
            actionText = "部屋で昼寝しに行く";
            if (!GoTo(seat.approach, State.Walking)) { goal = Goal.None; return false; }
            return true;
        }

        void StartLeave(bool stroll)
        {
            LeaveCurrentSpot();
            strolling = stroll;
            goal = Goal.Exit;
            actionText = stroll ? "散歩に出かける" : "授業に出かける";
            if (!GoTo(world.exitPoint, State.Walking)) { goal = Goal.None; SetState(State.Idle); }
        }

        void ReturnHome()
        {
            transform.position = world.exitPoint;
            SetVisible(true);
            world.Log(displayName + (strolling ? "が散歩から帰ってきた" : "が授業から帰ってきた"));
            // 昼をまたぐ授業なら、学内で昼食を食べてきたことにする
            float m = world.MinuteOfDay;
            if (!strolling && m >= 12.5f * 60f && m <= 16.5f * 60f) hunger = Mathf.Min(hunger, 25f);
            goal = Goal.Return;
            actionText = strolling ? "散歩から帰ってきた" : "授業から帰ってきた";
            strolling = false;
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
                    if (now >= awayUntil) ReturnHome();
                    return;
                case State.Private:
                    if (now >= privateUntil) EndPrivate();
                    return;
                case State.Eating:
                    if (now < eatUntil) return;
                    ShowBubble(act == Act.Tea ? "ほっ…" : "ごちそうさま", 1.8f);
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
                    if (readingPick && stateTime >= 3f)
                    {
                        readingPick = false;
                        if (!StartSit(Act.Hobby, "本を読んでいる", SeatFilter.Lounge)) SetState(State.Idle);
                        return;
                    }
                    break;
            }

            if (privatePending) return;
            if (HandleSchedule(now)) return;
            if (state == State.Approaching || state == State.Waiting || state == State.Walking) return;

            // ---- 空き時間：いちばん強い欲求を選ぶ ----
            float noise = 40f * whim;
            var scores = new Dictionary<Choice, float>();
            scores[Choice.Social] = lonely * (0.7f + 0.6f * sociability);
            scores[Choice.Rest] = tired;
            scores[Choice.Fun] = bored;
            scores[Choice.Snack] = hunger > 55f ? hunger * 0.9f : 0f;
            scores[Choice.Study] = study * (0.6f + 0.8f * diligence);
            scores[Choice.Hobby] = hobbyNeed;
            var f = world.RomanceTarget;
            scores[Choice.Love] = (!IsTarget && LoveStage >= 1 && f != null && f.AvailableForTalk) ? loveNeed * 1.1f : 0f;
            scores[Choice.Nap] = sleepy > 85f ? sleepy : 0f;
            SimCharacter privatePartner = PrivatePartner();
            scores[Choice.Private] = privatePartner != null ? desire * 1.1f : 0f;

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
                if (Random.value < 0.5f) StartWander(); else StartSit(Act.Rest, null, SeatFilter.Any);
                choice = Choice.None;
                return;
            }

            bool ok;
            switch (best)
            {
                case Choice.Social: ok = StartSocial(); break;
                case Choice.Rest: ok = StartSit(Act.Rest, null, SeatFilter.Any); break;
                case Choice.Fun: ok = StartActivity(); break;
                case Choice.Snack: StartMeal(Act.Snack, "おやつ"); ok = true; break;
                case Choice.Study:
                    ok = (room != null && Random.value < 0.6f) ? StartDesk() : StartSit(Act.Study, "勉強している", SeatFilter.BigTable);
                    if (!ok) ok = StartDesk();
                    break;
                case Choice.Hobby: ok = StartHobby(); break;
                case Choice.Love: BeginApproach(f, false, Intent.Love); ok = true; break;
                case Choice.Nap: ok = StartNap(); break;
                case Choice.Private: BeginApproach(privatePartner, false, Intent.Private); ok = true; break;
                default: ok = false; break;
            }
            if (ok) choice = best;
            else { StartWander(); choice = Choice.None; }
        }

        /// <summary>二人きりの時間を過ごせる恋人（どちらも気持ちが高まっていて、手が空いているとき）。</summary>
        SimCharacter PrivatePartner()
        {
            if (!HasLoverRole || desire < 60f) return null;
            if (!IsTarget)
            {
                var f = world.RomanceTarget;
                if (f != null && f.AvailableForTalk && f.desire > 45f) return f;
                return null;
            }
            SimCharacter best = null;
            foreach (var c in world.characters)
            {
                if (c == this || !c.IsLover || !c.AvailableForTalk || c.desire <= 45f) continue;
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
            for (int i = 0; i < others.Count; i++)
            {
                var o = others[i];
                if (o == this || !o.AvailableForTalk) continue;
                float a = world.rel.Get(id, o.id);
                float w = Mathf.Pow((a + 10f) / 110f, 1f + shy * 3f);
                float dist = Vector3.Distance(transform.position, o.transform.position);
                w *= 1f / (1f + dist / 8f);
                if (IsTarget && o.IsLover) w *= 1.5f;
                // ほかの人がすでに会いに行っている相手は避ける（重なり防止）
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
                    return true;
                }
            }
            return false;
        }

        bool StartSit(Act a, string label, SeatFilter filter)
        {
            Seat best = null;
            float bestScore = float.MinValue;
            foreach (var s in world.seats)
            {
                if (s.kind != SeatKind.Sit) continue;
                if (s.occupant != null && s.occupant != this) continue;
                if (filter == SeatFilter.BigTable && s.label != "大きなテーブル") continue;
                if (filter == SeatFilter.Lounge && (s.area != "談話室" || s.label == "大きなテーブル")) continue;
                float score = -Vector3.Distance(transform.position, s.approach) * 0.4f;
                if (Mathf.Abs(s.position.y - transform.position.y) > 1f) score -= 4f;
                foreach (var o in world.characters)
                {
                    if (o == this || !o.Present) continue;
                    float d = Vector3.Distance(o.transform.position, s.position);
                    if (d < 3f)
                    {
                        score += (world.rel.Get(id, o.id) - 30f) / 25f * (1f - sociability * 0.3f);
                        if (IsLover && o.IsTarget) score += 3f;
                    }
                }
                score += Random.value * (1f + whim * 3f);
                if (score > bestScore) { bestScore = score; best = s; }
            }
            if (best == null) return false;
            LeaveCurrentSpot();
            seat = best;
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
                if (p.counter || p.hobby >= 0 || p.owner >= 0) continue;
                int fs = p.FreeSlot();
                if (fs < 0) continue;
                float w = 1f / (1f + Vector3.Distance(transform.position, p.slots[fs]) / 6f);
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

        bool StartDesk()
        {
            if (room == null || room.desk.FreeSlot() < 0) return false;
            return GoToPoi(room.desk, Act.Study, "机で勉強している", Random.Range(18f, 30f));
        }

        bool StartHobby()
        {
            switch (hobby)
            {
                case HobbyKind.Reading:
                {
                    Poi shelf = null;
                    foreach (var p in world.pois)
                        if (p.label == "本棚の本を眺めている" && p.FreeSlot() >= 0 && (shelf == null || Random.value < 0.5f)) shelf = p;
                    if (shelf == null) return StartSit(Act.Hobby, "本を読んでいる", SeatFilter.Lounge);
                    bool ok = GoToPoi(shelf, Act.Hobby, "本を選んでいる", 3f);
                    readingPick = ok;
                    return ok;
                }
                case HobbyKind.Magic:
                {
                    foreach (var p in world.pois)
                        if (p.hobby == (int)HobbyKind.Magic && p.FreeSlot() >= 0) return GoToPoi(p, Act.Hobby, "魔法の練習をしている", Random.Range(15f, 25f));
                    return false;
                }
                case HobbyKind.Tea:
                    StartMeal(Act.Tea, "お茶");
                    return true;
                default:
                    awayUntil = world.Now + 25.0;
                    StartLeave(true);
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
            string verb = why == Intent.Love ? "に会いたくて向かう" : (why == Intent.Private ? "のところへ向かう" : "に会いに行く");
            actionText = who.displayName + verb + (order ? "（指示）" : "");
            SetState(State.Approaching);
            RepathToTarget();
        }

        /// <summary>プレイヤーからの指示「〇〇に会いに行って」。</summary>
        public void Order(SimCharacter who)
        {
            if (who == null || who == this) return;
            if (!Present || state == State.Sleeping || state == State.Bathing)
            {
                world.Log(displayName + "は今は動けない（" + StateName() + "）");
                return;
            }
            if (!who.Present || who.state == State.Sleeping || who.state == State.Bathing)
            {
                ShowBubble(who.displayName + "は今いないみたい", 2.2f);
                world.Log(who.displayName + "は今会えない（" + who.StateName() + "）");
                return;
            }
            if (state == State.Talking || privatePending)
            {
                pendingOrder = who;
                ShowBubble("！（あとで行くね）", 1.8f);
                return;
            }
            ShowBubble("！", 1.5f);
            BeginApproach(who, true, Intent.Talk);
        }

        public string StateName()
        {
            switch (state)
            {
                case State.Sleeping: return "寝ている";
                case State.Away: return strolling ? "散歩に出ている" : "授業に出ている";
                case State.Private: return "部屋で過ごしている";
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
            if (sameFloor && d <= 1.6f)
            {
                if (target.CanStartTalk(this)) { BeginInteraction(); return; }
                StartWaiting();
                return;
            }
            // 相手が会話中なら、近づきすぎずに少し離れて待つ
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
            if (intent == Intent.Private && target.PrivatePartnerOk(this)) StartCoroutine(PrivateRoutine(target));
            else StartCoroutine(TalkRoutine(target));
        }

        bool PrivatePartnerOk(SimCharacter by)
        {
            bool pair = (IsTarget && by.IsLover) || (IsLover && by.IsTarget);
            return pair && desire > 45f;
        }

        void UpdateWaiting(float dt)
        {
            waitTimer += dt;
            if (target == null || !target.Present) { GiveUp(); return; }
            FaceTowards(target.transform.position, dt);
            if (target.CanStartTalk(this))
            {
                Vector3 d3 = target.transform.position - transform.position;
                if (Flat(d3).magnitude <= 1.8f && Mathf.Abs(d3.y) < 0.5f) { BeginInteraction(); return; }
                actionText = target.displayName + "に会いに行く" + (hasOrder ? "（指示）" : "");
                SetState(State.Approaching);
                RepathToTarget();
                return;
            }
            if (waitTimer > (hasOrder ? 20f : 8f)) GiveUp();
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

        /// <summary>相手のまわりの、ほかの人とかぶらない立ち位置を選ぶ。</summary>
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
            if (!Present || state == State.Talking || state == State.Sleeping || state == State.Bathing) return false;
            if (hasOrder && target != by) return false;
            if (OnMission) return false;
            return true;
        }

        // ================= 会話 =================
        static readonly string[] Topics = { "授業の話", "魔法の練習", "食堂のメニュー", "読んでいる本", "寮のうわさ", "週末の予定", "試験の話", "故郷の話", "使い魔の話", "図書館の話" };
        static readonly string[] Chatter = { "それでね…", "へえ！", "ほんとに？", "うんうん", "わかる〜", "そうなの？", "ふふっ", "なるほど", "えっ！", "それで？" };
        static readonly string[] KoibanaLines = { "Fくんってさ…", "えっ、わかる！", "きゃー", "内緒だよ？", "応援する！", "ふふ、顔赤いよ" };

        IEnumerator TalkRoutine(SimCharacter partner)
        {
            path.Clear();
            talkPartner = partner;
            SetState(State.Talking);
            partner.EnterTalk(this);
            var rom = world.romance;

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
                    world.Log("★♥ " + displayName + "が" + partner.displayName + "に告白して恋人になった！");
                }
                else
                {
                    partner.ShowBubble("ごめん…", 2.2f);
                    ShowBubble("（涙）", 2.4f);
                    world.ChangeLove(this, -25f);
                    world.rel.Add(id, partner.id, -5f, world.simTime);
                    rom.cooldownUntil[id] = world.Now + 1440.0 * 2.0;
                    world.Log(displayName + "の告白は実らなかった…（2日後までは告白しない）");
                }
                loveNeed = 0f;
                yield return new WaitForSeconds(2.5f);
                EndTalk(partner);
                yield break;
            }

            // ---- ふつうの会話／恋バナ ----
            bool koibana = !IsTarget && !partner.IsTarget && LoveStage >= 1 && partner.LoveStage >= 1 && Random.value < 0.45f;
            string topic = koibana ? "恋バナ" : Topics[Random.Range(0, Topics.Length)];
            actionText = partner.displayName + "と" + topic;
            partner.actionText = displayName + "と" + topic;

            int lines = Random.Range(2, 4) * 2;
            for (int i = 0; i < lines; i++)
            {
                SimCharacter speaker = (i % 2 == 0) ? this : partner;
                string line = i == 0 ? (koibana ? "ねえ、恋バナしよ？" : topic + "なんだけど…")
                    : (koibana ? KoibanaLines[Random.Range(0, KoibanaLines.Length)] : Chatter[Random.Range(0, Chatter.Length)]);
                speaker.ShowBubble(line, 1.5f);
                yield return new WaitForSeconds(1.6f);
            }

            float bonus = koibana ? 0.3f : 0f;
            if ((IsTarget && partner.IsLover) || (IsLover && partner.IsTarget)) bonus += 0.15f;
            TalkResult result = world.rel.Roll(id, partner.id, bonus);
            string icon = result == TalkResult.Fun ? "♪ ♥" : (result == TalkResult.Normal ? "…" : "（汗）");
            ShowBubble(icon, 2.2f);
            partner.ShowBubble(icon, 2.2f);
            world.ReportTalk(this, partner, result, koibana);

            float relief = result == TalkResult.Fun ? 45f : (result == TalkResult.Normal ? 30f : 20f);
            lonely = Mathf.Max(0f, lonely - relief);
            partner.lonely = Mathf.Max(0f, partner.lonely - relief);
            bored = Mathf.Max(0f, bored - 15f);
            partner.bored = Mathf.Max(0f, partner.bored - 15f);
            if (partner.IsTarget) loveNeed = Mathf.Max(0f, loveNeed - 50f);
            if (IsTarget) partner.loveNeed = Mathf.Max(0f, partner.loveNeed - 50f);

            yield return new WaitForSeconds(1.4f);
            EndTalk(partner);
        }

        void EndTalk(SimCharacter partner)
        {
            partner.ExitTalk();
            ExitTalk();
            target = null;
            hasOrder = false;
        }

        // ---- 恋人同士の二人きりの時間（個室へ行き、画面から消える。描写はしない） ----
        IEnumerator PrivateRoutine(SimCharacter partner)
        {
            path.Clear();
            talkPartner = partner;
            SetState(State.Talking);
            partner.EnterTalk(this);
            privatePending = true;
            partner.privatePending = true;
            ShowBubble("ちょっといい？", 1.5f);
            yield return new WaitForSeconds(1.5f);
            partner.ShowBubble("うん", 1.2f);
            yield return new WaitForSeconds(1.2f);

            RoomInfo r = !IsTarget ? room : partner.room;
            talkPartner = null;
            partner.talkPartner = null;
            if (r == null) { CancelPrivate(partner); yield break; }
            GoPrivate(r.bed.approach);
            partner.GoPrivate(r.bed.approach + new Vector3(0f, 0f, 0.8f));

            float t = 0f;
            while (!(privateArrived && partner.privateArrived))
            {
                t += Time.deltaTime;
                if (t > 60f) { CancelPrivate(partner); yield break; }
                yield return null;
            }
            double until = world.Now + 30.0;
            EnterPrivate(until, r);
            partner.EnterPrivate(until, r);
            world.Log("♥ " + displayName + "と" + partner.displayName + "が" + r.name + "の部屋で二人きりの時間を過ごしている");
        }

        void CancelPrivate(SimCharacter partner)
        {
            privatePending = false;
            partner.privatePending = false;
            if (state != State.Idle) SetState(State.Idle);
            if (partner.state != State.Idle) partner.SetState(State.Idle);
            goal = Goal.None;
            partner.goal = Goal.None;
            target = null;
        }

        void GoPrivate(Vector3 dest)
        {
            LeaveCurrentSpot();
            goal = Goal.Private;
            privateArrived = false;
            actionText = "部屋へ向かっている";
            if (!GoTo(world.NearestFree(dest), State.Walking)) { privateArrived = true; SetState(State.Idle); }
        }

        void EnterPrivate(double until, RoomInfo r)
        {
            privatePending = false;
            privateArrived = false;
            privateUntil = until;
            privateRoom = r;
            goal = Goal.None;
            target = null;
            ReleaseSeatAndPoi();
            SetVisible(false);
            SetState(State.Private);
            actionText = "部屋で過ごしている";
        }

        void EndPrivate()
        {
            SetVisible(true);
            if (privateRoom != null) transform.position = world.NearestFree(privateRoom.bed.approach + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f)));
            desire = 0f;
            lonely = Mathf.Max(0f, lonely - 40f);
            loveNeed = 0f;
            if (!IsTarget)
            {
                world.ChangeLove(this, 4f);
                var f = world.RomanceTarget;
                if (f != null) world.rel.Add(id, f.id, 2f, world.simTime);
            }
            actionText = "ぼんやりしている";
            SetState(State.Idle);
            thinkTimer = Random.Range(0.5f, 2f);
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
                actionText = act == Act.Tea ? "お茶を飲んでいる" : "食事をしている";
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

        /// <summary>しばらく進めていなければ、道を探し直す。それでもだめなら諦める。</summary>
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
            // 目的地のすぐ近くなら着いたことにする
            Vector3 last = path[path.Count - 1];
            if (Flat(last - transform.position).magnitude < 1.2f && Mathf.Abs(last.y - transform.position.y) < 0.5f) { path.Clear(); return; }
            if (stuckCount >= 3)
            {
                path.Clear();
                LeaveCurrentSpot();
                privateArrived = true;
                SetState(State.Idle);
                actionText = "ぼんやりしている";
                return;
            }
            // 少し横にずれてから、もう一度道を探す
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
                    if (seat != null) { SnapToSeat(); SetState(State.Sitting); actionText = sitLabel ?? (seat.label + "で休んでいる"); }
                    else SetState(State.Idle);
                    break;
                case Goal.Poi:
                    if (poi != null) { SetState(State.Activity); actionText = sitLabel ?? poi.label; }
                    else SetState(State.Idle);
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
                        sleepUntil = world.Now + 40.0;
                        actionText = "昼寝している";
                        SetState(State.Sleeping);
                        ShowBubble("zzz", 1.5f);
                    }
                    else SetState(State.Idle);
                    break;
                case Goal.Exit:
                    SetState(State.Away);
                    actionText = strolling ? "散歩に出ている" : "授業に出ている";
                    SetVisible(false);
                    world.Log(displayName + (strolling ? "が散歩に出かけた" : "が授業に出かけた") + "（" + ScheduleMaker.Clock(awayUntil) + "ごろ帰る）");
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
            bool atSeat = seat != null && (state == State.Sitting || state == State.Eating || state == State.Bathing || state == State.Sleeping);
            if (atSeat) transform.position = world.NearestFree(seat.approach);
            ReleaseSeatAndPoi();
            goal = Goal.None;
            mealPending = false;
            readingPick = false;
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
            progressTimer = 0f;
            progressPos = transform.position;
            if (s != State.Walking && s != State.Approaching) stuckCount = 0;
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

        /// <summary>
        /// 重なり防止。止まっている人に歩いている人が近づいたら、歩いている人が横にずれる。
        /// 歩いている人同士なら、番号が大きい方が横によける。止まっている人同士は少しずつ離れる。
        /// </summary>
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
                    continue; // 歩いている人がよける
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
