using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 仮の人形1体。3つの欲求（さみしさ・疲れ・退屈）で行動を選び、会いに行って会話する。
    /// </summary>
    public class SimCharacter : MonoBehaviour
    {
        public enum State { Idle, Walking, Sitting, Activity, Approaching, Waiting, Talking }
        enum Goal { None, Seat, Poi, Wander }

        // ---- 設定 ----
        public int id;
        public string displayName;
        public string note;
        public Color color;
        [Range(0, 1)] public float sociability; // 人と話したい度
        [Range(0, 1)] public float whim;        // 気まぐれ度
        public bool male;

        // ---- 欲求（0〜100）----
        public float lonely, tired, bored;

        // ---- 状態 ----
        public State state = State.Idle;
        public string actionText = "ぼんやりしている";
        public string bubble;
        float bubbleTimer;
        public bool selected;

        LoungeWorld world;
        Transform visual;
        GameObject ring;
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
        bool hasOrder;
        float speed;

        public bool IsSitting { get { return state == State.Sitting || (state == State.Talking && seat != null); } }
        public SimCharacter TalkPartner { get { return talkPartner; } }
        public bool HasOrder { get { return hasOrder; } }

        public void Init(LoungeWorld w, Transform visualRoot, GameObject selectionRing)
        {
            world = w;
            visual = visualRoot;
            ring = selectionRing;
            speed = male ? 1.35f : 1.2f;
            lonely = Random.Range(10f, 50f);
            tired = Random.Range(10f, 50f);
            bored = Random.Range(10f, 50f);
            thinkTimer = Random.Range(0.2f, 1.5f);
            ApplyPose(false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            stateTime += dt;

            UpdateNeeds(dt);

            if (state == State.Walking || state == State.Approaching) Move(dt);
            if (state == State.Approaching) UpdateApproach(dt);
            if (state == State.Waiting) UpdateWaiting(dt);
            if (state == State.Talking && talkPartner != null) FaceTowards(talkPartner.transform.position, dt);
            if (state == State.Activity && poi != null) FaceTowards(poi.lookAt, dt);

            thinkTimer -= dt;
            if (thinkTimer <= 0f)
            {
                thinkTimer = 0.8f + Random.value * 0.6f;
                Think();
            }

            if (bubbleTimer > 0f)
            {
                bubbleTimer -= dt;
                if (bubbleTimer <= 0f) bubble = null;
            }

            if (ring != null && ring.activeSelf != selected) ring.SetActive(selected);
        }

        // ================= 欲求 =================
        void UpdateNeeds(float dt)
        {
            if (state != State.Talking) lonely += dt * 0.9f * (0.4f + sociability);

            switch (state)
            {
                case State.Walking:
                case State.Approaching:
                    tired += dt * 0.9f; bored -= dt * 0.3f; break;
                case State.Sitting:
                    tired -= dt * 3.5f; bored += dt * (0.5f + whim * 0.4f); break;
                case State.Activity:
                    tired += dt * 0.3f; bored -= dt * 4f; break;
                case State.Talking:
                    if (seat != null) tired -= dt * 2f; else tired += dt * 0.2f;
                    bored -= dt * 1f; break;
                default:
                    tired += dt * 0.35f; bored += dt * (0.6f + whim * 0.4f); break;
            }
            lonely = Mathf.Clamp(lonely, 0f, 100f);
            tired = Mathf.Clamp(tired, 0f, 100f);
            bored = Mathf.Clamp(bored, 0f, 100f);
        }

        // ================= 行動の選択 =================
        void Think()
        {
            if (state == State.Talking || state == State.Approaching || state == State.Waiting) return;
            if (state == State.Walking) return; // 目的地へ向かっている途中

            float sSocial = lonely * (0.7f + 0.6f * sociability);
            float sRest = tired;
            float sFun = bored;
            float noise = 40f * whim;
            sSocial += (Random.value - 0.5f) * noise;
            sRest += (Random.value - 0.5f) * noise;
            sFun += (Random.value - 0.5f) * noise;

            // 今していることを続けるか
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
            // いちばんしたいことを今している最中なら、そのまま続ける
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
                if (o == this || o.state == State.Talking || o.HasOrder) continue;
                float a = world.rel.Get(id, o.id);
                float w = Mathf.Pow((a + 10f) / 110f, 1f + shy * 3f);
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
                if (s.occupant != null && s.occupant != this) continue;
                float score = -Vector3.Distance(transform.position, s.approach) * 0.4f;
                foreach (var o in world.characters)
                {
                    if (o == this) continue;
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
            foreach (var p in world.pois) if (p.FreeSlot() >= 0) candidates.Add(p);
            if (candidates.Count == 0) return false;
            Poi chosen = candidates[Random.Range(0, candidates.Count)];
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
            Vector3 p = transform.position + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
            p = world.nav.NearestFreePoint(p);
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
            if (state == State.Talking)
            {
                // 会話が終わってから向かう
                pendingOrder = who;
                ShowBubble("！（あとで行くね）", 1.8f);
                return;
            }
            ShowBubble("！", 1.5f);
            BeginApproach(who, true);
        }
        SimCharacter pendingOrder;

        void UpdateApproach(float dt)
        {
            if (target == null) { SetState(State.Idle); return; }
            approachTimer += dt;
            repathTimer -= dt;
            float d = Flat(target.transform.position - transform.position).magnitude;
            if (d <= 1.3f)
            {
                if (target.CanStartTalk(this)) { StartCoroutine(TalkRoutine(target)); return; }
                path.Clear();
                waitTimer = 0f;
                actionText = target.displayName + "の話が終わるのを待つ";
                SetState(State.Waiting);
                return;
            }
            if (repathTimer <= 0f || path.Count == 0) RepathToTarget();
            if (!hasOrder && approachTimer > 25f) GiveUp();
        }

        void UpdateWaiting(float dt)
        {
            waitTimer += dt;
            if (target == null) { SetState(State.Idle); return; }
            FaceTowards(target.transform.position, dt);
            if (target.CanStartTalk(this))
            {
                float d = Flat(target.transform.position - transform.position).magnitude;
                if (d <= 1.4f) { StartCoroutine(TalkRoutine(target)); return; }
                actionText = target.displayName + "に会いに行く" + (hasOrder ? "（指示）" : "");
                SetState(State.Approaching);
                RepathToTarget();
                return;
            }
            if (waitTimer > (hasOrder ? 15f : 6f)) GiveUp();
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
            Vector3 tp = target.transform.position;
            Vector3 dir = Flat(transform.position - tp);
            if (dir.sqrMagnitude < 0.01f) dir = target.transform.forward;
            Vector3 goalPos = world.nav.NearestFreePoint(tp + dir.normalized * 1.0f);
            var p = world.nav.FindPath(transform.position, goalPos);
            path.Clear();
            if (p != null) path.AddRange(p);
        }

        public bool CanStartTalk(SimCharacter by)
        {
            if (state == State.Talking) return false;
            if (hasOrder && target != by) return false;
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
                if (partner == null) break;
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
            if (state == State.Walking || state == State.Approaching || state == State.Waiting || state == State.Activity)
            {
                // 座りに行く途中・作業中などは中断（座っている人は座ったまま話す）
                if (state != State.Sitting) ReleaseSeatAndPoi();
            }
            target = null;
            hasOrder = false;
            talkPartner = by;
            SetState(State.Talking);
        }

        void ExitTalk()
        {
            talkPartner = null;
            if (seat != null && seat.occupant == this && Flat(transform.position - seat.position).magnitude < 0.3f)
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
            var p = world.nav.FindPath(transform.position, dest);
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
            Vector3 to = Flat(next - pos);
            float step = speed * dt;
            if (to.magnitude <= step)
            {
                transform.position = new Vector3(next.x, 0f, next.z);
                path.RemoveAt(0);
            }
            else
            {
                Vector3 dir = to.normalized;
                transform.position = pos + dir * step;
                FaceTowards(pos + dir, dt);
            }
        }

        void Arrive()
        {
            switch (goal)
            {
                case Goal.Seat:
                    if (seat != null)
                    {
                        transform.position = seat.position;
                        transform.rotation = Quaternion.LookRotation(seat.forward);
                        SetState(State.Sitting);
                        actionText = seat.label + "で休んでいる";
                    }
                    else SetState(State.Idle);
                    break;
                case Goal.Poi:
                    if (poi != null)
                    {
                        SetState(State.Activity);
                        actionText = poi.label;
                    }
                    else SetState(State.Idle);
                    break;
                default:
                    SetState(State.Idle);
                    actionText = "ぼんやりしている";
                    break;
            }
            goal = Goal.None;
        }

        void LeaveCurrentSpot()
        {
            if (seat != null && state == State.Sitting)
            {
                transform.position = world.nav.NearestFreePoint(seat.approach);
            }
            ReleaseSeatAndPoi();
            goal = Goal.None;
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
            ApplyPose(IsSitting);
        }

        void ApplyPose(bool sitting)
        {
            if (visual == null) return;
            float h = male ? 1.85f : 1.7f;
            if (sitting)
            {
                visual.localScale = new Vector3(0.45f, 0.45f, 0.45f);
                visual.localPosition = new Vector3(0f, 0.45f + 0.45f, 0f);
            }
            else
            {
                visual.localScale = new Vector3(0.45f, h / 2f, 0.45f);
                visual.localPosition = new Vector3(0f, h / 2f, 0f);
            }
        }

        public float HeadHeight { get { return IsSitting ? 1.55f : (male ? 2.05f : 1.9f); } }

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
            if (Time.deltaTime <= 0f || IsSitting) return;
            foreach (var o in world.characters)
            {
                if (o == this || o.IsSitting) continue;
                Vector3 d = Flat(transform.position - o.transform.position);
                float m = d.magnitude;
                if (m < 0.55f && m > 0.0001f)
                {
                    Vector3 push = d / m * (0.55f - m) * 0.5f;
                    Vector3 np = transform.position + push;
                    if (world.nav.IsFree(np)) transform.position = np;
                }
            }
        }
    }
}
