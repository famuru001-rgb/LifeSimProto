using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 寮生以外の学生（冒険者寮・騎士寮・魔法師寮に30人ずつ）と先生7人の動き（2026-10-30 famuru）。
    /// ・欲求なし・関係なし・会話なし。エッチはできる（篤が誘えば受ける。自分からは誘わない）
    /// ・朝は正門から登校し、取っている科目の授業の時間は教室へ（授業中は教室の中＝見えない）、昼は学食、あいた時間は学校の中や前庭で過ごす。
    ///   夕方〜夜に正門から自分の寮へ帰る（ほかの寮はまだ作っていないので、帰ると見えなくなる）
    /// ・寮ごとの評価（試験・行事）のため、ひとりずつ学力と能力がある。授業・自習・運動で少しずつ伸びる
    /// </summary>
    public partial class SimCharacter
    {
        public bool IsMob { get { return profile != null && profile.mob; } }
        public bool IsTeacher { get { return profile != null && profile.teacher; } }
        /// <summary>寮の名前（寮生以外の学生）。先生は「先生」。</summary>
        public string MobDormName { get { return IsMob && profile.mobDorm >= 0 ? CharProfiles.MobDormNames[profile.mobDorm] : ""; } }

        double mobUntil = -1;       // 今のひまつぶしの終わり
        double mobStarted;          // 今のひまつぶしを始めた時刻
        double mobHomeAt = -1;      // 今日帰る時刻
        int mobDay = -1;
        bool mobHomeTrip;           // 帰り道（正門に着いたら自分の寮へ）
        bool mobLunchDone;
        int mobGrowSubject = -1;    // ひまつぶしで伸びる科目（-1 はなし）
        int mobGrowAbility = -1;    // ひまつぶしで伸びる能力（-1 はなし）
        readonly List<int> mobClassSubjects = new List<int>();
        /// <summary>いちばん新しい試験の平均点（寮生以外の学生。-1 はまだ）。</summary>
        public float lastExamAverage = -1f;

        /// <summary>篤が誘いに来ている（このときだけ話しかけられる）。</summary>
        bool MobInviteOk()
        {
            var f = world != null ? world.RomanceTarget : null;
            return Present && state != State.Talking && state != State.Private && f != null && f.hasOrder && f.target == this;
        }

        static int MobSeed(int id, int day, int salt) { return id * 7919 + day * 104729 + salt * 31; }

        /// <summary>学校に来る時刻（その日の通算分）。</summary>
        double MobArriveTime(int day)
        {
            var rnd = new System.Random(MobSeed(id, day, 11));
            bool holiday = Calendar.IsHoliday(day, world.holidayWeekdays);
            double d0 = ScheduleMaker.DayStart(day);
            if (IsTeacher) return d0 + (holiday ? 600 + rnd.Next(0, 180) : 460 + rnd.Next(0, 35));
            return d0 + (holiday ? 560 + rnd.Next(0, 150) : 470 + rnd.Next(0, 40)); // 授業の日は 7:50〜8:30
        }

        /// <summary>自分の寮へ帰る時刻（その日の通算分）。</summary>
        double MobLeaveTime(int day)
        {
            var rnd = new System.Random(MobSeed(id, day, 23));
            double d0 = ScheduleMaker.DayStart(day);
            if (IsTeacher) return d0 + 1080 + rnd.Next(0, 150);  // 18:00〜20:30
            return d0 + 1020 + rnd.Next(0, 210);                   // 17:00〜20:30
        }

        string MobHomeLabel { get { return IsTeacher ? "家に帰っている" : MobDormName + "にいる"; } }

        /// <summary>最初の状態：自分の寮にいる（見えない）。来る時刻になったら正門から入ってくる。</summary>
        void MobInit()
        {
            if (seat != null && seat.occupant == this) seat.occupant = null;
            seat = null;
            path.Clear();
            goal = Goal.None;
            transform.position = LoungeWorld.SchoolGate;
            mobDay = world.Day;
            mobHomeAt = MobLeaveTime(mobDay);
            double now = world.Now;
            double arrive = MobArriveTime(mobDay);
            if (now >= mobHomeAt) arrive = MobArriveTime(mobDay + 1);
            else if (now >= arrive) arrive = now + Random.Range(1f, 20f); // もう学校にいる時間：ばらばらに来る
            mobHomeTrip = true;
            awayUntil = arrive;
            awayLabel = MobHomeLabel;
            actionText = awayLabel;
            SetState(State.Away);
            SetVisible(false);
        }

        void MobThink()
        {
            double now = world.Now;
            EnsureSchedule();
            if (world.Day != mobDay) { mobDay = world.Day; mobHomeAt = MobLeaveTime(mobDay); mobLunchDone = false; }
            switch (state)
            {
                case State.Talking:
                    return;
                case State.Private:
                    if (now >= privateUntil || (hs == null && privateUntil > 1e17)) EndPrivate();
                    return;
                case State.Away:
                    if (now >= awayUntil) MobComeBack();
                    return;
                case State.Eating:
                    if (now < eatUntil) return;
                    LeaveCurrentSpot();
                    SetState(State.Idle);
                    actionText = "ぼんやりしている";
                    mobUntil = now;
                    break;
            }
            if (chatting || privatePending || hasOrder) return; // 篤に誘われて動いている
            if (state == State.Approaching || state == State.Waiting) return;
            // 歩いている途中でも、ひまつぶし・昼食へ向かう途中なら授業を優先する（授業へ・寮へ向かう途中はそのまま）
            bool walkingFree = state == State.Walking && goal != Goal.Exit;
            if (state == State.Walking && !walkingFree) return;

            // 帰る時間（授業の途中でなければ）
            if (now >= mobHomeAt && !walkingFree) { MobGoHome(); return; }

            // 授業（取っている科目のコマ）・昼食
            foreach (var it in schedule)
            {
                if (it.done || it.start > now) continue;
                if (it.kind == TaskKind.Class)
                {
                    it.done = true;
                    if (now >= it.end - 5) continue;
                    MobFinishActivity();
                    MobGoClass(it);
                    return;
                }
                if (walkingFree) continue; // 昼食などは、着いてから
                if (it.kind == TaskKind.Lunch)
                {
                    it.done = true;
                    if (now > it.start + 60 || mobLunchDone) continue;
                    mobLunchDone = true;
                    MobFinishActivity();
                    if (MobEat()) return;
                    continue;
                }
                it.done = true; // 朝食・夕食・お風呂・就寝は自分の寮で
            }
            if (walkingFree) return;

            if (now < mobUntil) return;
            MobFinishActivity();
            MobPickActivity();
        }

        /// <summary>エッチのあと：すぐ次のひまつぶしを考える。</summary>
        void MobAfterPrivate() { mobUntil = world.Now; mobStarted = -1; mobGrowSubject = -1; mobGrowAbility = -1; }

        // ================= 行き来 =================
        void MobComeBack()
        {
            SetVisible(true);
            if (mobHomeTrip)
            {
                mobHomeTrip = false;
                transform.position = world.NearestFree(LoungeWorld.SchoolGate + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-1f, 1f)));
                actionText = IsTeacher ? "出勤してきた" : "登校してきた";
                mobDay = world.Day;
                mobHomeAt = MobLeaveTime(mobDay);
            }
            else
            {
                // 授業が終わった：同じ所（教室）から出てくる
                foreach (int s in mobClassSubjects)
                {
                    if (s < 0 || s >= Academics.Count) continue;
                    AddSkill(s, IsTeacher ? 0.3f : 2.2f);
                    weekSubject[s] += 6f;
                }
                mobClassSubjects.Clear();
                actionText = IsTeacher ? "授業を終えた" : "授業が終わった";
                transform.position = world.NearestFree(transform.position);
            }
            SetState(State.Idle);
            mobUntil = world.Now + Random.Range(1f, 6f);
        }

        void MobGoHome()
        {
            LeaveCurrentSpot();
            mobHomeTrip = true;
            awayUntil = MobArriveTime(world.Day + 1);
            awayLabel = MobHomeLabel;
            goal = Goal.Exit;
            actionText = IsTeacher ? "帰る" : MobDormName + "へ帰る";
            if (!GoTo(world.NearestFree(LoungeWorld.SchoolGate), State.Walking)) MobVanish();
        }

        void MobGoClass(ScheduleItem it)
        {
            LeaveCurrentSpot();
            bool exam = it.Label.EndsWith("試験");
            mobHomeTrip = false;
            mobClassSubjects.Clear();
            mobClassSubjects.AddRange(it.subjects);
            awayUntil = it.end;
            awayLabel = IsTeacher ? (exam ? "試験の監督をしている" : "授業をしている") : (exam ? "試験を受けている" : "授業に出ている");
            int s0 = it.subjects.Count > 0 ? it.subjects[0] : -1;
            Vector3 p;
            if (!MobClassPoint(exam, s0, out p)) { MobVanish(); return; }
            goal = Goal.Exit;
            actionText = (IsTeacher ? "授業をしに行く" : (exam ? "試験を受けに行く" : SubjectName(s0) + "の授業へ"));
            // 学校は広いので（ゲームの1分で1.4mほどしか歩けない）、教室が遠いときは歩かずに教室へ入ったことにする（寮生が授業で見えなくなるのと同じ）
            if (!GoTo(p, State.Walking) || PathLength() > MobWalkLimit) { path.Clear(); transform.position = p; MobVanish(); }
        }

        /// <summary>歩いて行く上限（m）。授業の15分前に出るので、約15分で歩ける距離。</summary>
        const float MobWalkLimit = 22f;

        float PathLength()
        {
            float len = 0f;
            Vector3 c = transform.position;
            foreach (var q in path) { len += (q - c).magnitude; c = q; }
            return len;
        }

        /// <summary>授業の場所：その科目の教室（なければ空いている教室か大講堂）。試験は大講堂。</summary>
        bool MobClassPoint(bool exam, int subject, out Vector3 p)
        {
            AreaInfo a = null;
            if (!exam && subject >= 0)
            {
                string key = Academics.Names[subject];
                a = world.MobAreas(x => x.name.EndsWith(key + "の教室") || x.name.EndsWith(key + "の準備室")).Find(x => true);
                if (a == null)
                {
                    var rooms = world.MobAreas(x => x.name.EndsWith("の教室") || x.name.EndsWith("実習室") || x.name.EndsWith("調合室"));
                    if (rooms.Count > 0) a = rooms[(id + subject) % rooms.Count];
                }
            }
            if (a == null) a = world.areaList.Find(x => x.name == "大講堂" && x.floor == 0);
            if (a == null) { p = transform.position; return false; }
            p = world.RandomPointIn(a);
            return true;
        }

        /// <summary>その場で見えなくなる（授業中・自分の寮にいる）。</summary>
        void MobVanish()
        {
            path.Clear();
            goal = Goal.None;
            SetState(State.Away);
            actionText = awayLabel ?? MobHomeLabel;
            SetVisible(false);
        }

        // ================= 昼食 =================
        bool MobEat()
        {
            Seat best = null;
            float bestScore = float.MinValue;
            foreach (var s in world.mobSeats)
            {
                if (s.occupant != null) continue;
                float score = Random.value * 3f - Vector3.Distance(s.position, transform.position) * 0.02f;
                if (score > bestScore) { bestScore = score; best = s; }
            }
            if (best == null) return false;
            LeaveCurrentSpot();
            seat = best;
            seat.occupant = this;
            goal = Goal.DiningSeat;
            actionText = "学食へ";
            if (!GoTo(seat.approach, State.Walking) || PathLength() > 40f) { path.Clear(); seat.occupant = null; seat = null; goal = Goal.None; SetState(State.Idle); return false; } // 学食が遠いときは食べない（自分の寮で食べた）
            return true;
        }

        // ================= ひまつぶし =================
        class MobPlan { public string label; public System.Predicate<AreaInfo> where; public float weight; public int subject = -1, ability = -1; public float minMin = 15f, maxMin = 45f; }

        void MobPickActivity()
        {
            var plans = new List<MobPlan>();
            int study = profile.enrolled.Length > 0 ? profile.enrolled[Random.Range(0, profile.enrolled.Length)] : -1;
            if (IsTeacher)
            {
                plans.Add(new MobPlan { label = "職員室で仕事をしている", where = a => a.name.EndsWith("職員室"), weight = 6f, minMin = 30f, maxMin = 90f });
                plans.Add(new MobPlan { label = "廊下を見回っている", where = a => a.name.EndsWith("大廊下") || a.name == "外廊下", weight = 1.5f });
                plans.Add(new MobPlan { label = "学食でひと休みしている", where = a => a.name.EndsWith("学食・購買部"), weight = 1f });
            }
            else
            {
                bool lateStudy = world.MinuteOfDay >= 16f * 60f;
                plans.Add(new MobPlan { label = "教室で勉強している", where = a => a.name.EndsWith("の教室"), weight = 1f + profile.diligence * 3f, subject = study, minMin = 25f, maxMin = 60f });
                plans.Add(new MobPlan { label = "廊下にいる", where = a => a.name.EndsWith("大廊下"), weight = 2f, minMin = 8f, maxMin = 25f });
                plans.Add(new MobPlan { label = "外廊下でぼんやりしている", where = a => a.name == "外廊下", weight = 1.2f, minMin = 8f, maxMin = 25f });
                plans.Add(new MobPlan { label = "学食でひと休みしている", where = a => a.name.EndsWith("学食・購買部"), weight = 1.5f });
                plans.Add(new MobPlan { label = "玄関ホールにいる", where = a => a.name == "玄関ホール", weight = 0.8f, minMin = 5f, maxMin = 15f });
                if (!world.BadWeatherToday)
                {
                    plans.Add(new MobPlan { label = "本館の中庭で過ごしている", where = a => a.name == "本館の中庭", weight = 2f });
                    plans.Add(new MobPlan { label = "前庭を歩いている", where = a => a.name == "前庭", weight = 0.8f, minMin = 8f, maxMin = 20f });
                }
                plans.Add(new MobPlan { label = "体育館で体を動かしている", where = a => a.name == "体育館" && a.floor == 0, weight = profile.mobDorm == 1 ? 2.5f : 1f, ability = Abilities.Body, subject = profile.mobDorm == 1 ? Academics.Sword : -1 });
                plans.Add(new MobPlan { label = "魔法実習室で練習している", where = a => a.name.EndsWith("魔法実習室"), weight = profile.mobDorm == 2 ? 2.5f : 0.8f, ability = Abilities.Magic, subject = Academics.MagicArts });
                plans.Add(new MobPlan { label = "部室にいる", where = a => a.floor == 2 && a.name.Contains("部室"), weight = lateStudy ? 2.5f : 0.8f, minMin = 30f, maxMin = 70f });
            }
            float total = 0f;
            foreach (var pl in plans) total += pl.weight;
            float r = Random.value * total;
            MobPlan pick = plans[plans.Count - 1];
            foreach (var pl in plans) { r -= pl.weight; if (r <= 0f) { pick = pl; break; } }

            // 学食では空いている長いすに座る
            if (pick.label.StartsWith("学食") && Random.value < 0.7f)
            {
                Seat best = null;
                foreach (var s in world.mobSeats) if (s.occupant == null && (best == null || Random.value < 0.15f)) best = s;
                if (best != null)
                {
                    LeaveCurrentSpot();
                    seat = best;
                    seat.occupant = this;
                    goal = Goal.Seat;
                    sitLabel = pick.label;
                    MobStartPlan(pick);
                    actionText = "学食へ";
                    if (!GoTo(seat.approach, State.Walking)) { seat.occupant = null; seat = null; goal = Goal.None; mobUntil = world.Now + 5; }
                    return;
                }
            }

            var areas = world.MobAreas(pick.where);
            if (areas.Count == 0) { mobUntil = world.Now + 10; return; }
            var area = areas[Random.Range(0, areas.Count)];
            Vector3 p = world.RandomPointIn(area);
            LeaveCurrentSpot();
            goal = Goal.Wander;
            sitLabel = pick.label;
            MobStartPlan(pick);
            string an = world.AreaName(p);
            actionText = !string.IsNullOrEmpty(an) ? an + "へ" : "歩いている";
            if (!GoTo(p, State.Walking)) { goal = Goal.None; SetState(State.Idle); mobUntil = world.Now + 5; }
        }

        void MobStartPlan(MobPlan pl)
        {
            mobGrowSubject = pl.subject;
            mobGrowAbility = pl.ability;
            mobStarted = -1;
            mobUntil = world.Now + 60; // 着いてから決め直す（途中で篤に誘われたときの念のため）
            mobPlanMin = pl.minMin; mobPlanMax = pl.maxMin;
        }
        float mobPlanMin = 15f, mobPlanMax = 45f;

        /// <summary>ひまつぶしの終わり：過ごした時間の分だけ、学力や能力が少し伸びる。</summary>
        void MobFinishActivity()
        {
            if (mobStarted < 0) { mobGrowSubject = -1; mobGrowAbility = -1; return; }
            float mins = Mathf.Clamp((float)(world.Now - mobStarted), 0f, 120f);
            if (mobGrowSubject >= 0) { AddSkill(mobGrowSubject, mins * 0.02f * (0.5f + profile.diligence)); weekSubject[mobGrowSubject] += mins * 0.05f; }
            if (mobGrowAbility >= 0) GrowAbility(mobGrowAbility, mins * 0.004f);
            mobStarted = -1;
            mobGrowSubject = -1;
            mobGrowAbility = -1;
        }

        /// <summary>着いたとき（寮生以外の学生・先生だけの行き先）。true なら処理した。</summary>
        bool MobArrive()
        {
            switch (goal)
            {
                case Goal.Exit:
                    goal = Goal.None;
                    MobVanish();
                    return true;
                case Goal.Wander:
                    goal = Goal.None;
                    SetState(State.Idle);
                    actionText = sitLabel ?? "ぼんやりしている";
                    mobStarted = world.Now;
                    mobUntil = world.Now + Random.Range(mobPlanMin, mobPlanMax);
                    transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    return true;
                case Goal.DiningSeat:
                    goal = Goal.None;
                    if (seat == null) { SetState(State.Idle); return true; }
                    SnapToSeat();
                    act = Act.Meal;
                    eatUntil = world.Now + Random.Range(20f, 35f);
                    SetState(State.Eating);
                    actionText = "学食で昼食を食べている";
                    return true;
                case Goal.Seat:
                    goal = Goal.None;
                    if (seat == null) { SetState(State.Idle); return true; }
                    SnapToSeat();
                    act = Act.Rest;
                    SetState(State.Sitting);
                    actionText = sitLabel ?? "座っている";
                    mobStarted = world.Now;
                    mobUntil = world.Now + Random.Range(mobPlanMin, mobPlanMax);
                    return true;
            }
            return false;
        }
    }
}
