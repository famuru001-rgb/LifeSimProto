using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 仮の人形1体。1日の予定（食事・授業・お風呂・就寝）と行事を優先し、空き時間は9つの欲求で行動を選ぶ。
    /// 欲求：さみしさ・疲れ・退屈・空腹・勉強・趣味・恋心・睡眠・性欲（性欲は「好き」以上のみ。描写はしない）
    /// 欲求を満たす場所は、仲のいい子・好きな人・恋人がいる所を選びやすい。勉強・趣味・昼寝・二人きりの時間は他の人の部屋も使う。
    /// </summary>
    public class SimCharacter : MonoBehaviour
    {
        public enum State { Idle, Walking, Sitting, Activity, Approaching, Waiting, Talking, Eating, Bathing, Sleeping, Away, Private }
        public enum HobbyKind { Reading, Magic, Tea, Walk }
        enum Goal { None, Seat, Poi, Wander, Counter, DiningSeat, Bath, Bed, Nap, Exit, Return, Private, Gather }
        enum Act { None, Rest, Fun, Study, Hobby, Meal, Snack, Tea }
        enum Intent { Talk, Love, Private, Gift }
        enum Choice { None, Social, Rest, Fun, Snack, Study, Hobby, Love, Nap, Private }
        enum AwayKind { Class, Stroll, Outing }

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
        public float weekStudy; // 今週どれだけ勉強したか（試験の結果に使う）

        // ---- 状態 ----
        public State state = State.Idle;
        public string actionText = "ぼんやりしている";
        public string bubble;
        float bubbleTimer;
        public bool selected;
        public Vector3 approachGoal;
        public readonly List<ActiveStatus> statuses = new List<ActiveStatus>();

        // ---- 予定・行事 ----
        public List<ScheduleItem> schedule = new List<ScheduleItem>();
        int scheduleDay = -1;
        double sleepUntil, awayUntil, eatUntil, bathUntil, privateUntil;
        bool mealPending, readingPick, snackPick, bakePending, privatePending, privateArrived, sickRest, napping;
        AwayKind awayKind;
        string awayLabel;
        ScheduleItem bedItem;
        RoomInfo privateRoom;
        Gathering gathering;
        Vector3 gatherFocus;

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
        Vector3 destination;
        Vector3 progressPos;
        float progressTimer;
        int stuckCount;

        public SimCharacter TalkPartner { get { return talkPartner; } }
        public bool HasOrder { get { return hasOrder; } }
        public bool Present { get { return state != State.Away && state != State.Private; } }
        public bool IsMoving { get { return (state == State.Walking || state == State.Approaching) && path.Count > 0; } }
        public bool IsSick { get { return sickRest; } }
        bool OnMission
        {
            get
            {
                return mealPending || snackPick || bakePending || privatePending || goal == Goal.Counter || goal == Goal.DiningSeat || goal == Goal.Bath
                    || goal == Goal.Bed || goal == Goal.Nap || goal == Goal.Exit || goal == Goal.Return || goal == Goal.Private;
            }
        }

        // ---- 恋愛 ----
        public bool IsTarget { get { return world != null && world.romance != null && id == world.romance.targetId; } }
        public int LoveStage { get { return IsTarget ? -1 : world.romance.Stage(id); } }
        public bool IsLover { get { return !IsTarget && world.romance.lover[id]; } }

        /// <summary>話しかけられる状態か（寝ている・出かけている・入浴中・急いでいるときは不可。風邪で寝込んでいるときはお見舞い可）。</summary>
        public bool AvailableForTalk
        {
            get
            {
                if (!Present || state == State.Bathing || state == State.Talking) return false;
                if (state == State.Sleeping && !sickRest) return false;
                if (OnMission || hasOrder) return false;
                return true;
            }
        }

        // ================= 状態（ステータス） =================
        public bool HasStatus(StatusKind k)
        {
            foreach (var s in statuses) if (s.kind == k) return true;
            return false;
        }

        /// <summary>状態をつける（minutes はゲーム内の分）。すでにあれば長い方に延ばす。</summary>
        public void AddStatus(StatusKind k, double minutes)
        {
            if (minutes <= 0) return;
            double until = world.Now + minutes;
            foreach (var s in statuses)
                if (s.kind == k) { if (until > s.until) s.until = until; return; }
            statuses.Add(new ActiveStatus { kind = k, until = until });
            if (k == StatusKind.Cheerful) RemoveStatus(StatusKind.Down);
            if (k == StatusKind.Down) RemoveStatus(StatusKind.Cheerful);
        }

        public void RemoveStatus(StatusKind k) { statuses.RemoveAll(s => s.kind == k); }

        float RateMul(NeedKind n)
        {
            float m = 1f;
            foreach (var s in statuses) { float v; if (Statuses.Get(s.kind).rate.TryGetValue(n, out v)) m *= v; }
            return m;
        }

        float StatusProduct(System.Func<StatusDef, float> f)
        {
            float m = 1f;
            foreach (var s in statuses) m *= f(Statuses.Get(s.kind));
            return m;
        }

        public float TalkBonus()
        {
            float b = 0f;
            foreach (var s in statuses) b += Statuses.Get(s.kind).talkBonus;
            return b;
        }

        public string StatusTags()
        {
            if (statuses.Count == 0) return "";
            var names = new List<string>();
            foreach (var s in statuses) names.Add(Statuses.Get(s.kind).name);
            return "［" + string.Join("・", names.ToArray()) + "］";
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
            thinkTimer = Random.Range(0.2f, 1.5f);

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
                if (state == State.Activity && poi == null && goal == Goal.None && gathering != null) FaceTowards(gatherFocus, dt);
                if (bubble == null && state != State.Sleeping)
                {
                    if (state == State.Activity && act == Act.Hobby && hobby == HobbyKind.Magic && Random.value < dt * 0.4f) ShowBubble("✦", 1.0f);
                    else if (HasStatus(StatusKind.Lovesick) && Random.value < dt * 0.12f) ShowBubble("…♡", 1.4f);
                    else if (HasStatus(StatusKind.SleepDeprived) && Random.value < dt * 0.08f) ShowBubble("ふぁ…", 1.2f);
                }
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
            double now = world.Now;
            statuses.RemoveAll(s => s.until <= now);

            // 時間とともに増えるもの（ゲーム内の時間で）
            hunger += gm * 0.2f * RateMul(NeedKind.Hunger);
            study += gm * 0.05f * (0.5f + diligence) * RateMul(NeedKind.Study);
            hobbyNeed += gm * 0.09f * RateMul(NeedKind.Hobby);
            sleepy += gm * 0.075f * RateMul(NeedKind.Sleepy);
            if (!IsTarget && LoveStage >= 1) loveNeed += gm * 0.05f * (0.5f + world.romance.love[id] / 50f) * RateMul(NeedKind.Love);
            desire += gm * DesireRate();

            float lonelyMul = RateMul(NeedKind.Lonely), tiredMul = RateMul(NeedKind.Tired), boredMul = RateMul(NeedKind.Bored);
            switch (state)
            {
                case State.Away:
                    if (awayKind == AwayKind.Stroll) { hobbyNeed -= gm * 2.5f; bored -= gm * 2f; }
                    else if (awayKind == AwayKind.Outing) { bored -= gm * 2f; lonely -= gm * 1f; hobbyNeed -= gm * 0.5f; }
                    else { study -= gm * 0.35f; weekStudy += gm * 0.35f; }
                    break;
                case State.Private:
                    desire -= gm * 4f; lonely -= gm * 2f; loveNeed -= gm * 3f; break;
                case State.Sleeping:
                {
                    // 性欲が高いまま夜を迎えると、眠りが浅くなる
                    float recover = 0.6f;
                    if (!napping && !sickRest && LoungeWorld.IsNight(world.MinuteOfDay) && desire >= 60f) recover *= 0.25f;
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
                    if (act == Act.Study) Studying(dt);
                    else if (act == Act.Hobby) { hobbyNeed -= dt * 3f; bored -= dt * 2f; }
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
                    if (act == Act.Study) Studying(dt);
                    else if (act == Act.Hobby) { hobbyNeed -= dt * 3f; bored -= dt * 3f; }
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
            if (!IsTarget && f != null && f.Present && Present)
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

            // 状態の自動のつけ外し
            if (!IsTarget && LoveStage >= 2 && loveNeed > 70f && !HasStatus(StatusKind.Lovesick)) AddStatus(StatusKind.Lovesick, 600);
            if (HasStatus(StatusKind.Lovesick) && loveNeed < 25f) RemoveStatus(StatusKind.Lovesick);
            if (HasStatus(StatusKind.Assignment) && study < 15f) { RemoveStatus(StatusKind.Assignment); world.Log(displayName + "が課題を終わらせた"); }
        }

        void Studying(float dt)
        {
            float eff = StatusProduct(d => d.studyEfficiency);
            float amount = dt * 2.5f * eff;
            study -= amount;
            weekStudy += amount * 0.4f;
            bored += dt * 0.3f;
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

        /// <summary>その場所のまわりに、仲のいい子・好きな人・恋人がいるほど高くなる。</summary>
        float PlaceBonus(Vector3 p)
        {
            float b = 0f;
            foreach (var o in world.characters)
            {
                if (o == this || !o.Present) continue;
                Vector3 d = o.transform.position - p;
                if (Mathf.Abs(d.y) > 1f || d.sqrMagnitude > 3.5f * 3.5f) continue;
                b += (world.rel.Get(id, o.id) - 30f) / 20f;
                if (o.IsTarget && LoveStage >= 2) b += 3f;
                else if (o.IsTarget && LoveStage == 1) b += 1.5f;
                if (IsTarget && o.LoveStage >= 2) b += 1.5f;
            }
            return b;
        }

        /// <summary>その部屋に遊びに行ってよいか（部屋の主が部屋にいて、仲がいい／好きな相手）。</summary>
        bool CanVisitRoom(RoomInfo r)
        {
            if (r == null || r.owner < 0 || r.owner == id || r.owner >= world.characters.Count) return false;
            var owner = world.characters[r.owner];
            if (!owner.Present || world.AreaName(owner.transform.position) != r.AreaName) return false;
            if (owner.IsTarget) return LoveStage >= 2;
            if (IsTarget) return owner.LoveStage >= 2;
            return world.rel.Get(id, owner.id) >= 40f;
        }

        // ================= 予定 =================
        void EnsureSchedule()
        {
            if (scheduleDay == world.Day) return;
            scheduleDay = world.Day;
            schedule = ScheduleMaker.Make(id, scheduleDay, world.holidayWeekdays);
            foreach (var it in schedule) if (it.kind != TaskKind.Bed && it.end < world.Now) it.done = true;
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

        /// <summary>予定の時刻になっていれば、その行動を始める。始めたら true。</summary>
        bool HandleSchedule(double now)
        {
            if (hasOrder || privatePending) return false;
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
                        awayLabel = it.Label == "試験" ? "試験を受けている" : "授業に出ている";
                        StartLeave(it.Label == "試験" ? "試験を受けに行く" : "授業に出かける");
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
                        if (state == State.Sleeping && !napping) { it.done = true; return false; }
                        if (goal == Goal.Bed) return true;
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

        /// <summary>おやつ：談話室のワゴンで取って、仲のいい子の近くに座って食べる。</summary>
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

        /// <summary>昼寝：自分のベッドか、仲のいい子・好きな人の部屋の来客用の布団で。</summary>
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
            string what = awayKind == AwayKind.Stroll ? "散歩" : (awayKind == AwayKind.Outing ? "おでかけ" : (awayLabel == "試験を受けている" ? "試験" : "授業"));
            world.Log(displayName + "が" + what + "から帰ってきた");
            goal = Goal.Return;
            actionText = what + "から帰ってきた";
            if (awayKind == AwayKind.Class)
            {
                float m = world.MinuteOfDay;
                if (m >= 12.5f * 60f && m <= 16.5f * 60f) hunger = Mathf.Min(hunger, 25f); // 学内で昼食
                if (awayLabel != "試験を受けている" && Random.value < 0.2f)
                {
                    AddStatus(StatusKind.Assignment, 3 * 1440);
                    study = Mathf.Max(study, 50f);
                    world.Log(displayName + "は課題が出たらしい");
                }
            }
            gathering = null;
            if (!GoTo(world.entrancePoint, State.Walking)) { goal = Goal.None; SetState(State.Idle); }
        }

        void WakeUp()
        {
            bool fromNight = !napping && !sickRest;
            napping = false;
            if (sickRest)
            {
                sickRest = false;
                RemoveStatus(StatusKind.Cold);
                world.Log(displayName + "の風邪が治った");
            }
            // 風邪をひく（寝不足だとひきやすい）
            if (fromNight && room != null && seat == room.bed)
            {
                float p = 0.03f + (sleepy > 30f ? 0.07f : 0f);
                if (Random.value < p)
                {
                    sickRest = true;
                    sleepUntil = ScheduleMaker.WakeTime(id, world.Day + 1, world.holidayWeekdays);
                    AddStatus(StatusKind.Cold, sleepUntil - world.Now);
                    actionText = "風邪で寝込んでいる";
                    ShowBubble("けほっ", 2f);
                    world.Log(displayName + "が風邪をひいて寝込んでいる");
                    SetState(State.Sleeping);
                    return;
                }
            }
            if (fromNight && sleepy > 30f)
            {
                double untilNight = ScheduleMaker.DayStart(world.Day) + 1380 - world.Now;
                AddStatus(StatusKind.SleepDeprived, untilNight);
            }
            if (seat != null && seat.occupant == this)
            {
                transform.position = world.NearestFree(seat.approach);
                seat.occupant = null;
            }
            seat = null;
            EnsureSchedule();
            actionText = "起きた";
            ShowBubble(HasStatus(StatusKind.SleepDeprived) ? "ねむい…" : "ふあ〜", 2f);
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
            gathering = g;
            if (g.outing)
            {
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
                gathering = g; // StartSitAt の中で一度外れるので、あとで設定し直す
                return;
            }
            // 座る場所がなければ、中心のまわりに立つ
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

        /// <summary>手作りお菓子：食堂で作って、Fに渡しに行く。</summary>
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
                    if (bakePending)
                    {
                        if (stateTime < activityDuration) return;
                        bakePending = false;
                        var f = world.RomanceTarget;
                        if (f != null && f.AvailableForTalk) { BeginApproach(f, false, Intent.Gift); return; }
                        world.Log(displayName + "はお菓子を渡しそびれた");
                        LeaveCurrentSpot();
                        SetState(State.Idle);
                    }
                    break;
            }

            if (privatePending) return;
            if (HandleSchedule(now)) return;
            if (state == State.Approaching || state == State.Waiting || state == State.Walking) return;

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
                    float chance = g.joinChance * (HasStatus(StatusKind.Down) ? 0.5f : 1f) * (0.7f + 0.3f * sociability);
                    if (g.invited != null) chance = 1f;
                    if (Random.value < chance) { JoinGathering(g); return; }
                    g.declined.Add(this);
                }
            }

            // ---- 空き時間：いちばん強い欲求を選ぶ ----
            float noise = 40f * whim;
            var scores = new Dictionary<Choice, float>();
            scores[Choice.Social] = lonely * (0.7f + 0.6f * sociability) * StatusProduct(d => d.socialScore);
            scores[Choice.Rest] = tired;
            scores[Choice.Fun] = bored * StatusProduct(d => d.funScore);
            bool hungry = hunger > 55f || (world.SnackBonus && hunger > 30f);
            scores[Choice.Snack] = (hungry && !NearMeal(45)) ? hunger * 0.9f + (world.SnackBonus ? 30f : 0f) : 0f;
            scores[Choice.Study] = study * (0.6f + 0.8f * diligence) * StatusProduct(d => d.studyScore);
            scores[Choice.Hobby] = hobbyNeed;
            var target0 = world.RomanceTarget;
            scores[Choice.Love] = (!IsTarget && LoveStage >= 1 && target0 != null && target0.AvailableForTalk) ? loveNeed * 1.1f : 0f;
            float napAt = HasStatus(StatusKind.SleepDeprived) ? 70f : 85f;
            scores[Choice.Nap] = sleepy > napAt ? sleepy : 0f;
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
                if (Random.value < 0.5f) StartWander(); else StartRest();
                choice = Choice.None;
                return;
            }

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
                case Choice.Nap: ok = StartNap(); break;
                case Choice.Private: BeginApproach(privatePartner, false, Intent.Private); ok = true; break;
                default: ok = false; break;
            }
            if (ok) choice = best;
            else { StartWander(); choice = Choice.None; }
        }

        /// <summary>二人きり（以上）の時間を過ごせる相手。どちらも「好き」以上の関係で、気持ちが高まっていて、手が空いているとき。</summary>
        SimCharacter PrivatePartner()
        {
            if (desire < 60f) return null;
            if (!IsTarget)
            {
                if (LoveStage < 2) return null;
                var f = world.RomanceTarget;
                if (f != null && f.AvailableForTalk && f.desire >= 40f) return f;
                return null;
            }
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
            for (int i = 0; i < others.Count; i++)
            {
                var o = others[i];
                if (o == this || !o.AvailableForTalk) continue;
                float a = world.rel.Get(id, o.id);
                if (o.sickRest && a < 40f) continue;
                float w = Mathf.Pow((a + 10f) / 110f, 1f + shy * 3f);
                float dist = Vector3.Distance(transform.position, o.transform.position);
                w *= 1f / (1f + dist / 8f);
                if (IsTarget && o.LoveStage >= 2) w *= 1.5f;
                if (o.sickRest) w *= 3f; // お見舞い
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

        /// <summary>勉強：自分の机・談話室の大きなテーブル・仲のいい子や好きな人の部屋から、いちばん居心地のよい所を選ぶ。</summary>
        bool StartStudy()
        {
            float bestScore = float.MinValue;
            Seat bestSeat = null;
            bool useDesk = false;
            if (room != null && room.desk.FreeSlot() >= 0)
            {
                bestScore = 1f + PlaceBonus(room.desk.slots[0]) * 0.5f + Random.value;
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
            if (useDesk) return GoToPoi(room.desk, Act.Study, "机で勉強している", Random.Range(18f, 30f));
            if (bestSeat == null) return false;
            string label = bestSeat.room != null ? bestSeat.label + "で勉強している" : "勉強している";
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
            if (h == HobbyKind.Magic && HasStatus(StatusKind.ManaDepleted)) h = HobbyKind.Reading;
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
            string verb = why == Intent.Love ? "に会いたくて向かう" : (why == Intent.Private ? "のところへ向かう" : (why == Intent.Gift ? "にお菓子を渡しに行く" : "に会いに行く"));
            actionText = who.displayName + verb + (order ? "（指示）" : "");
            SetState(State.Approaching);
            RepathToTarget();
        }

        /// <summary>プレイヤーからの指示「〇〇に会いに行って」。</summary>
        public void Order(SimCharacter who)
        {
            if (who == null || who == this) return;
            if (!Present || (state == State.Sleeping && !sickRest) || state == State.Bathing || sickRest)
            {
                world.Log(displayName + "は今は動けない（" + StateName() + "）");
                return;
            }
            if (!who.Present || (who.state == State.Sleeping && !who.sickRest) || who.state == State.Bathing)
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
                case State.Sleeping: return sickRest ? "風邪で寝込んでいる" : (napping ? "昼寝している" : "寝ている");
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
            if (target == null || !target.Present || (target.state == State.Sleeping && !target.sickRest) || target.state == State.Bathing) { GiveUp(); return; }
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
            if (!Present || state == State.Talking || state == State.Bathing) return false;
            if (state == State.Sleeping && !sickRest) return false;
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

            // ---- 手作りお菓子を渡す ----
            if (intent == Intent.Gift && partner.IsTarget)
            {
                actionText = partner.displayName + "にお菓子を渡している";
                partner.actionText = displayName + "からお菓子をもらっている";
                ShowBubble("これ、作ったの…", 1.8f);
                yield return new WaitForSeconds(1.9f);
                partner.ShowBubble("ありがとう！おいしい", 2f);
                yield return new WaitForSeconds(2.1f);
                world.ChangeLove(this, 5f);
                world.rel.Add(id, partner.id, 5f, world.simTime);
                AddStatus(StatusKind.Cheerful, 240);
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
                    AddStatus(StatusKind.Cheerful, 480);
                    world.Log("★♥ " + displayName + "が" + partner.displayName + "に告白して恋人になった！");
                }
                else
                {
                    partner.ShowBubble("ごめん…", 2.2f);
                    ShowBubble("（涙）", 2.4f);
                    world.ChangeLove(this, -25f);
                    world.rel.Add(id, partner.id, -5f, world.simTime);
                    rom.cooldownUntil[id] = world.Now + 1440.0 * 2.0;
                    AddStatus(StatusKind.Down, 1440);
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
            if (result == TalkResult.Fun && world.rel.Get(id, partner.id) >= 60f && Random.value < 0.1f)
            {
                AddStatus(StatusKind.Cheerful, 240);
                partner.AddStatus(StatusKind.Cheerful, 240);
            }
            if (result == TalkResult.Awkward && Random.value < 0.15f) AddStatus(StatusKind.Down, 180);

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

        void EndTalk(SimCharacter partner)
        {
            partner.ExitTalk();
            ExitTalk();
            target = null;
            hasOrder = false;
            intent = Intent.Talk;
        }

        // ---- 二人きり（以上）の時間：個室へ行き、画面から消える（描写はしない） ----
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

            // 同じ気持ちの子も誘う（最大でFと女の子3人）
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
            var spots = new List<Vector3> { r.bed.approach, r.futon.approach, r.cushions[0].approach, r.cushions[1].approach };
            for (int i = 0; i < group.Count; i++) group[i].GoPrivate(spots[i % spots.Count]);

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
            foreach (var c in arrived) { c.EnterPrivate(until, r); names.Add(c.displayName); }
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
            if (privateRoom != null) transform.position = world.NearestFree(privateRoom.bed.approach + new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.4f, 0.6f)));
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
            if (pendingOrder != null)
            {
                var who = pendingOrder;
                pendingOrder = null;
                BeginApproach(who, true, Intent.Talk);
            }
        }

        void EnterTalk(SimCharacter by)
        {
            StopAllCoroutines();
            path.Clear();
            bool seated = state == State.Sitting || state == State.Eating || sickRest;
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
            if (sickRest)
            {
                SetState(State.Sleeping);
                actionText = "風邪で寝込んでいる";
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
            float step = baseSpeed * StatusProduct(d => d.speed) * dt;
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
            Vector3 last = path[path.Count - 1];
            if (Flat(last - transform.position).magnitude < 1.2f && Mathf.Abs(last.y - transform.position.y) < 0.5f) { path.Clear(); return; }
            if (stuckCount >= 3)
            {
                path.Clear();
                if (goal == Goal.Private) { privateArrived = true; SetState(State.Idle); return; }
                LeaveCurrentSpot();
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
                            if (world.SnackBonus && Random.value < 0.25f)
                            {
                                AddStatus(StatusKind.VigorPotion, 240);
                                ShowBubble("元気出てきた！", 1.8f);
                            }
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
                    if (poi != null) { SetState(State.Activity); actionText = sitLabel ?? poi.label; }
                    else SetState(State.Idle);
                    break;
                case Goal.Gather:
                    SetState(State.Activity);
                    actionText = sitLabel;
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
            // 魔法の練習のあとは、ときどき暴発して魔力切れになる
            if (state == State.Activity && act == Act.Hobby && hobby == HobbyKind.Magic && poi != null && poi.hobby >= 0 && stateTime > 8f && Random.value < 0.15f)
            {
                AddStatus(StatusKind.ManaDepleted, 360);
                ShowBubble("ボンッ！", 1.5f);
                world.Log(displayName + "の魔法が暴発した！（魔力切れ）");
            }
            bool atSeat = seat != null && (state == State.Sitting || state == State.Eating || state == State.Bathing || state == State.Sleeping);
            if (atSeat) transform.position = world.NearestFree(seat.approach);
            ReleaseSeatAndPoi();
            goal = Goal.None;
            mealPending = false;
            readingPick = false;
            snackPick = false;
            bakePending = false;
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
