using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>生活のできごと（LifeEvents.cs・LifeEventDirector.cs）のときの、キャラの側の動き（2026-11-02）。</summary>
    public partial class SimCharacter
    {
        /// <summary>今加わっている生活のできごと（null ならなし）。この間は自分の考えでは動かない。</summary>
        public LifeRun lifeEv;
        /// <summary>しるし（できごとの結果で付く印。名前 → いつまで（ゲームの分））。条件「しるし:名前」で使う。</summary>
        readonly Dictionary<string, double> lifeFlags = new Dictionary<string, double>();

        public bool LifeHasFlag(string name) { double u; return lifeFlags.TryGetValue(name, out u) && world.Now < u; }
        public void LifeSetFlag(string name, float days) { lifeFlags[name] = world.Now + Mathf.Max(0.01f, days) * 1440.0; }
        public bool LifeSickInBed { get { return sickRest; } }
        public bool LifeInOwnRoom
        {
            get
            {
                if (room == null) return false;
                var a = world.LifeAreaAt(transform.position);
                return a != null && a.name == room.AreaName;
            }
        }

        // ---- 欲求（疲れ・空腹・退屈・さみしさ・眠気・性欲・恋・勉強・趣味）----
        public float LifeNeed(int k)
        {
            switch (k)
            {
                case 0: return tired; case 1: return hunger; case 2: return bored; case 3: return lonely; case 4: return sleepy;
                case 5: return desire; case 6: return loveNeed; case 7: return study; case 8: return hobbyNeed;
            }
            return 0f;
        }

        public void LifeAddNeed(int k, float v)
        {
            switch (k)
            {
                case 0: tired = Mathf.Clamp(tired + v, 0f, 100f); break;
                case 1: hunger = Mathf.Clamp(hunger + v, 0f, 100f); break;
                case 2: bored = Mathf.Clamp(bored + v, 0f, 100f); break;
                case 3: lonely = Mathf.Clamp(lonely + v, 0f, 100f); break;
                case 4: sleepy = Mathf.Clamp(sleepy + v, 0f, 100f); break;
                case 5: desire = Mathf.Clamp(desire + v, 0f, 100f); break;
                case 6: loveNeed = Mathf.Clamp(loveNeed + v, 0f, 100f); break;
                case 7: study = Mathf.Clamp(study + v, 0f, 100f); break;
                case 8: hobbyNeed = Mathf.Clamp(hobbyNeed + v, 0f, 100f); break;
            }
        }

        /// <summary>できごとに加われるか（寝ている子・入浴中の子は、そのできごとが求めるときだけ）。</summary>
        public bool LifeAvailable(bool allowSleep, bool allowBath)
        {
            if (lifeEv != null || IsMob || !Present || InDungeon || chatting || privatePending || hs != null) return false;
            if (inPlaceClass || clubPhase == ClubPhase.Active || hasOrder || OnMission) return false;
            switch (state)
            {
                case State.Idle: case State.Walking: case State.Sitting: case State.Activity: return !(state == State.Walking && goal == Goal.Bed);
                case State.Sleeping: return allowSleep;
                case State.Bathing: return allowBath;
            }
            return false;
        }

        /// <summary>篤ができごとの相手になれるか（篤は動かさない。話している・二人きりの時間・出かけている間はなれない）。</summary>
        public bool LifeAvailableAsF
        {
            get { return lifeEv == null && Present && !InDungeon && !chatting && !privatePending && hs == null && state != State.Talking && state != State.Private && state != State.Sleeping && state != State.Bathing; }
        }

        /// <summary>できごとの場所へ歩いて行く。</summary>
        public bool LifeGoTo(LifeRun r, Vector3 p, string text)
        {
            lifeEv = r;
            LeaveCurrentSpot();
            target = null;
            sceneLabel = null;
            path.Clear();
            goal = Goal.None;
            actionText = text;
            if ((transform.position - p).sqrMagnitude < 0.36f) { SetState(State.Idle); return true; }
            if (!GoTo(p, State.Walking)) { lifeEv = null; return false; }
            goal = Goal.None;
            return true;
        }

        /// <summary>できごとの場所の近くの空いている椅子へ（なければ立つ）。</summary>
        public bool LifeSitNear(LifeRun r, Vector3 p, string text)
        {
            Seat best = null; float bd = 6f;
            foreach (var s in world.seats)
            {
                if (s.occupant != null || s.kind != SeatKind.Sit || s.room != null && s.room != room) continue;
                if (Mathf.Abs(s.position.y - p.y) > 1f) continue;
                float d = (s.position - p).magnitude;
                if (d < bd) { bd = d; best = s; }
            }
            if (best == null) return LifeGoTo(r, p, text);
            lifeEv = r;
            target = null;
            sceneLabel = null;
            if (!StartSitAt(best, Act.Rest, text)) { lifeEv = null; return LifeGoTo(r, p, text); }
            return true;
        }

        /// <summary>そのまま（寝ている・入浴中など）で、できごとに加わる。</summary>
        public void LifeStay(LifeRun r)
        {
            lifeEv = r;
            // 篤は、歩いている途中なら立ち止まる（自分の考えで歩いていたときだけ。命令で動いているときは加えない）
            if (IsTarget && state == State.Walking && !hasOrder) { LeaveCurrentSpot(); path.Clear(); goal = Goal.None; SetState(State.Idle); }
        }

        /// <summary>着いたか（座る・歩くのどちらでも）。</summary>
        public bool LifeArrived(Vector3 p)
        {
            if (state == State.Sitting || state == State.Sleeping || state == State.Bathing) return true;
            if (path.Count > 0) return false;
            Vector3 d = transform.position - p; d.y = 0f;
            return d.magnitude < 1.6f;
        }

        /// <summary>できごとの最中：相手の方を向いて、場面の動き（料理・暖炉 など）をする。</summary>
        public void LifeHold(string scene, Vector3 look, string text)
        {
            if (state == State.Walking) SetState(State.Idle);
            path.Clear();
            if (state == State.Idle || state == State.Activity)
            {
                Vector3 d = look - transform.position; d.y = 0f;
                if (d.sqrMagnitude > 0.04f) transform.rotation = Quaternion.LookRotation(d.normalized);
                sceneLabel = string.IsNullOrEmpty(scene) ? null : scene;
            }
            if (state != State.Sleeping && state != State.Bathing) actionText = text;
        }

        public void LifeGesture(string g, float sec)
        {
            if (string.IsNullOrEmpty(g)) return;
            var pb = GetComponent<ProtoBody>();
            if (pb != null) pb.Gesture(g, sec);
        }

        /// <summary>できごとが終わった：ふだんに戻る（夜なら寝に行く）。</summary>
        public void LifeRelease(bool toBed)
        {
            lifeEv = null;
            sceneLabel = null;
            if (state == State.Sleeping || state == State.Bathing) return;
            if (IsTarget) { if (state == State.Walking && !hasOrder) { path.Clear(); SetState(State.Idle); } return; }
            if (toBed && room != null) { StartBed(); return; }
            if (state == State.Sitting) LeaveCurrentSpot();
            path.Clear();
            goal = Goal.None;
            SetState(State.Idle);
            actionText = "ぼんやりしている";
        }

        public void LifeSay(string text, float sec)
        {
            if (string.IsNullOrEmpty(text) || state == State.Sleeping && !sickRest) return;
            ShowBubble(text, sec);
        }

        /// <summary>寝坊：起きる時刻を遅らせる。</summary>
        public void LifeOversleep(float min) { if (state == State.Sleeping) sleepUntil = System.Math.Max(sleepUntil, world.Now) + min; }

        /// <summary>起こす（寝込んでいる子は起こさない）。</summary>
        public void LifeWake() { if (state == State.Sleeping && !sickRest) WakeUp(); }

        /// <summary>二人きりの時間に誘いに行く（受けるかは相手しだい：今の決まりのまま）。</summary>
        public void LifeSeek(SimCharacter who)
        {
            if (who == null || sickRest || state == State.Sleeping || !Present || InPrivate) return;
            privateEscorts.Clear();
            BeginApproach(who, false, Intent.Private);
        }

        /// <summary>試験用：今のようす（状態・道の残り・目当てまでの距離）。</summary>
        public string LifeDebug(Vector3 p)
        {
            Vector3 d = transform.position - p;
            return state + "・道" + path.Count + "・あと" + new Vector2(d.x, d.z).magnitude.ToString("0.0") + "m（高さ" + d.y.ToString("0.0") + "）・" + (lifeEv != null ? "加わっている" : "外れた") + "・" + actionText;
        }

        // ---- セーブ（しるし）----
        public List<string> LifeFlagsSave()
        {
            var res = new List<string>();
            foreach (var kv in lifeFlags) if (kv.Value > world.Now) res.Add(kv.Key + "|" + kv.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            return res;
        }

        public void LifeFlagsLoad(List<string> list)
        {
            lifeFlags.Clear();
            if (list == null) return;
            foreach (var s in list)
            {
                var a = s.Split('|');
                double u;
                if (a.Length == 2 && double.TryParse(a[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out u)) lifeFlags[a[0]] = u;
            }
        }
    }

    public partial class LoungeWorld
    {
        /// <summary>その位置の区画（いちばん小さい物）。</summary>
        public AreaInfo LifeAreaAt(Vector3 p)
        {
            int fl = FloorOf(p.y);
            AreaInfo best = null; float ba = float.MaxValue;
            var v = new Vector2(p.x, p.z);
            foreach (var a in areaList)
            {
                if (a.floor != fl || !a.rect.Contains(v)) continue;
                float ar = a.rect.width * a.rect.height;
                if (ar < ba) { ba = ar; best = a; }
            }
            return best;
        }

        /// <summary>役割（場所の役割.csv）に当たる区画（名前にどれかの言葉を含む区画を全部）。onlyOpen なら鍵のかかった場所は除く。</summary>
        public List<AreaInfo> LifeRoleAreas(string role, bool onlyOpen)
        {
            var res = new List<AreaInfo>();
            var words = LifeBook.FindRole(role);
            if (words == null) return res;
            foreach (var wd in words)
                foreach (var a in areaList)
                {
                    if (!a.name.Contains(wd)) continue;
                    if (onlyOpen && !LifePlaceOpen(a.name)) continue;
                    if (!res.Contains(a)) res.Add(a);
                }
            return res;
        }

        /// <summary>鍵のかかる場所（中庭・温室・図書室・工房・天文台）は開いているときだけ。</summary>
        public bool LifePlaceOpen(string areaName)
        {
            foreach (var g in new[] { Courtyard, Greenhouse, Library, Workshop, Observatory })
                if (areaName == g || areaName.Contains(g)) return PlaceOpen(g);
            return true;
        }

        public AreaInfo LifeRoomArea(SimCharacter c)
        {
            if (c == null || c.room == null) return null;
            return areaList.Find(a => a.name == c.room.AreaName);
        }

        /// <summary>p のまわりの歩ける所（dist m 離れた所。ふさがっていれば近くの空いた所）。</summary>
        public Vector3 LifeNear(Vector3 p, float dist)
        {
            float ang = Random.Range(0f, 360f);
            for (int i = 0; i < 6; i++)
            {
                Vector3 q = p + Quaternion.Euler(0f, ang + i * 60f, 0f) * Vector3.forward * dist;
                Vector3 r = NearestFree(q);
                Vector3 d = r - p; d.y = 0f;
                if (d.magnitude > 0.5f && d.magnitude < dist + 1.2f) return r;
            }
            return NearestFree(p + Vector3.forward * dist);
        }
    }
}
