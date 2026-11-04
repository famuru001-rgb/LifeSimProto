using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 教室で授業を受ける・教える（2026-11-04 famuru「教室に入ったら適当なところに座って授業を受けるようにして。先生モブも授業に参加するようにして」）。
    /// ・学生（寮生・ほかの寮の学生）：その科目の教室へ歩いて、空いている席に座って授業を受ける（見えたまま。机で書く動き）。
    ///   席がいっぱいなら教室の後ろに立って聞く。机の無い実習室などでは、科目に合った動き（剣術・魔法・薬草 など）で立って受ける
    /// ・先生（寮の先生・先生のモブ）：黒板と机のあいだに立って教える（話す身ぶり）。試験は大講堂の長いすに座り、先生は演台に立つ
    /// ・授業が終わったら：寮生は寮へ歩いて帰る（部活の日は部活へ）、ほかの寮の学生・先生は次のひまつぶしへ
    /// 席（LoungeWorld.classSeats）と先生の立つ所（teachSpots）は SchoolInterior.cs が机と一緒に作る。
    /// </summary>
    public partial class SimCharacter
    {
        bool classRoom;           // 教室で授業中（座っている・立っている）
        bool classTeaching;       // 先生として教えている
        string classScene;        // 机の無い所で立って受けるときの動き
        double mobClassEnd = -1;  // ほかの寮の学生・先生：授業の終わり

        /// <summary>授業の場所の区画：その科目の教室（なければ実習室など）。試験は大講堂。</summary>
        AreaInfo ClassArea(bool exam, int subject)
        {
            AreaInfo a = null;
            if (!exam && subject >= 0)
            {
                string key = Academics.Names[subject];
                a = world.MobAreas(x => x.name.EndsWith(key + "の教室") || x.name.EndsWith(key + "の準備室")).Find(x => true);
                // 教室の無い実技の科目：合った場所（剣術は体育館 など）。みんな同じ所へ行く
                string room = PracticalRoom(subject);
                if (a == null && room != null) a = world.MobAreas(x => x.name.EndsWith(room)).Find(x => true);
                if (a == null)
                {
                    var rooms = world.MobAreas(x => x.name.EndsWith("の教室") || x.name.EndsWith("実習室") || x.name.EndsWith("調合室"));
                    if (rooms.Count > 0) a = rooms[subject % rooms.Count];
                }
            }
            if (a == null) a = world.areaList.Find(x => x.name == "大講堂" && x.floor == 0);
            return a;
        }

        static bool SameArea(string seatArea, AreaInfo a)
        {
            if (a == null || string.IsNullOrEmpty(seatArea)) return false;
            return a.name == seatArea || a.name.EndsWith(seatArea) || seatArea.EndsWith(a.name);
        }

        /// <summary>教室の無い科目の授業の場所（区画の名前の終わり）。</summary>
        static string PracticalRoom(int subject)
        {
            if (subject < 0 || subject >= Academics.Names.Length) return null;
            switch (Academics.Names[subject])
            {
                case "剣術": return "体育館";
                case "魔法術": return "魔法実習室";
                case "飛行術": return "本館の中庭";
                case "魔法医学": return "医学実習室";
                case "錬成": return "魔法薬の調合室";
                default: return null;
            }
        }

        /// <summary>机の無い所での、科目に合った立ってする動き（MotionBook の場面の言葉）。</summary>
        static string PracticalScene(int subject)
        {
            if (subject < 0 || subject >= Academics.Names.Length) return null;
            switch (Academics.Names[subject])
            {
                case "剣術": return "剣術";
                case "魔法術": case "飛行術": return "魔法";
                case "薬草学": case "魔法医学": return "薬草";
                case "魔道具学": return "工房";
                case "錬成": return "釜";
                default: return null;
            }
        }

        /// <summary>教室へ行って座る（先生は前に立つ）。行けなければ false（今までどおり見えなくなる）。</summary>
        bool GoClassroom(bool exam, int subject, bool teacher, string label, bool allowJump)
        {
            var a = ClassArea(exam, subject);
            if (a == null) return false;
            LeaveCurrentSpot();
            strolling = false;
            classRoom = true;
            classTeaching = teacher;
            classScene = null;
            sitLabel = label;
            Seat s = null;
            var list = teacher ? world.teachSpots : world.classSeats;
            var free = list.FindAll(x => x.occupant == null && SameArea(x.area, a));
            if (free.Count == 0 && a.name != "大講堂" && list.Exists(x => SameArea(x.area, a)))
            {
                // 教室に入りきらない（学年みんなで受ける授業など）：大講堂で合同の授業を受ける（先生も演台へ）
                var hall = world.areaList.Find(x => x.name == "大講堂" && x.floor == 0);
                var hallFree = hall != null ? list.FindAll(x => x.occupant == null && SameArea(x.area, hall)) : null;
                if (hallFree != null && hallFree.Count > 0) { a = hall; free = hallFree; }
            }
            if (free.Count > 0) s = PickSeat(free);
            Vector3 dest;
            if (s != null)
            {
                seat = s; s.occupant = this;
                goal = teacher ? Goal.Teach : Goal.Seat;
                act = Act.Study;
                dest = s.approach;
            }
            else
            {
                // 席が無い・いっぱい：教室の中に立つ（机の無い実習室では科目に合った動き）
                bool hasSeats = list.Exists(x => SameArea(x.area, a));
                classScene = teacher ? "授業をして" : (hasSeats ? null : PracticalScene(subject));
                goal = Goal.Teach;
                dest = world.RandomPointIn(a);
            }
            actionText = label + "（向かっている）";
            bool ok = GoTo(dest, State.Walking);
            // ほかの寮の学生・先生：学校は広いので、遠いときは歩かずに教室へ入ったことにする
            if (allowJump && (!ok || PathLength() > MobWalkLimit)) { path.Clear(); transform.position = world.NearestFree(dest); ok = true; Arrive(); }
            if (!ok) { ReleaseSeatAndPoi(); classRoom = false; classTeaching = false; goal = Goal.None; return false; }
            return true;
        }

        /// <summary>席えらび：前の方・まとまって座るように、うまっている席の近くを少し選びやすく。</summary>
        Seat PickSeat(List<Seat> free)
        {
            if (free.Count == 1) return free[0];
            Seat best = null; float bs = float.MinValue;
            foreach (var x in free)
            {
                float sc = Random.Range(0f, 3f);
                foreach (var o in world.classSeats) if (o.occupant != null && o.area == x.area && (o.position - x.position).sqrMagnitude < 2.6f * 2.6f) sc += 0.8f;
                if (sc > bs) { bs = sc; best = x; }
            }
            return best ?? free[0];
        }

        float classSeatCheckAt = -1f;

        /// <summary>立って授業を受けている子：席が空いたら座る。</summary>
        void ClassroomFindSeat()
        {
            if (classTeaching || seat != null || state != State.Idle) return;
            if (Time.time < classSeatCheckAt) return;
            classSeatCheckAt = Time.time + Random.Range(2f, 5f);
            var a = world.AreaAt(transform.position);
            if (a == null) return;
            var free = world.classSeats.FindAll(x => x.occupant == null && SameArea(x.area, a));
            if (free.Count == 0) return;
            var s = PickSeat(free);
            seat = s; s.occupant = this;
            sceneLabel = null;
            goal = Goal.Seat;
            act = Act.Study;
            if (!GoTo(s.approach, State.Walking)) { s.occupant = null; seat = null; goal = Goal.None; SetState(State.Idle); sceneLabel = classScene; }
        }

        /// <summary>先生の立つ所・教室の中の立つ所に着いた。</summary>
        void ArriveTeach()
        {
            if (seat != null) SnapToSeat();
            SetState(State.Idle);
            sceneLabel = classTeaching ? "授業をして" : classScene;
            actionText = sitLabel ?? (classTeaching ? "授業をしている" : "授業を受けている");
        }

        /// <summary>授業が終わった：席を立つ。</summary>
        void LeaveClassroom()
        {
            if (!classRoom) return;
            classRoom = false;
            classTeaching = false;
            classScene = null;
            sceneLabel = null;
            if (state == State.Sitting || state == State.Idle || state == State.Activity || state == State.Walking) { LeaveCurrentSpot(); path.Clear(); goal = Goal.None; SetState(State.Idle); }
            actionText = "授業が終わった";
        }

        // ================= 寮生（寮の先生も） =================
        /// <summary>授業の時間：教室へ行く（HandleSchedule から）。</summary>
        bool TryClassroom(ScheduleItem it, bool exam, bool teach)
        {
            int s0 = it.subjects.Count > 0 ? it.subjects[0] : -1;
            string label = teach ? (exam ? "試験の監督をしている" : SubjectName(s0) + "の授業をしている") : (exam ? it.Label + "を受けている" : SubjectName(s0) + "の授業を受けている");
            if (!GoClassroom(exam, s0, teach, label, false)) return false;
            classUntil = it.end;
            inPlaceClass = true;   // 授業中は話しかけられない・ほかのことをしない（場所での授業と同じしくみ）
            studySubject = s0;
            studyPickedAt = world.Now;
            return true;
        }

        /// <summary>授業中（Think から）。true ならこの回はこれで終わり。</summary>
        bool ClassroomThink(double now)
        {
            if (!classRoom || !inPlaceClass) return false;
            if (now >= classUntil) return false;   // 終わりは EndPlaceClass
            if (state == State.Away || state == State.Private || state == State.Sleeping) { classRoom = false; return false; }
            ClassroomFindSeat();
            return true;
        }

        /// <summary>教室での授業の終わり（EndPlaceClass から）：寮へ歩いて帰る（部活の日は Think が部活へ向かわせる）。</summary>
        void EndClassroom()
        {
            if (!classRoom) return;
            LeaveClassroom();
            float m = world.MinuteOfDay;
            if (m >= 12.5f * 60f && m <= 16.5f * 60f) hunger = Mathf.Min(hunger, 25f); // 学校で昼を食べた
            if (IsMob) return;
            goal = Goal.Return;
            actionText = "授業から帰っている";
            if (!GoTo(world.entrancePoint, State.Walking)) { goal = Goal.None; SetState(State.Idle); }
        }

        // ================= ほかの寮の学生・先生 =================
        /// <summary>授業へ（MobGoClass から）。教室で座る・教える。行けなければ false。</summary>
        bool MobTryClassroom(ScheduleItem it, bool exam)
        {
            int s0 = it.subjects.Count > 0 ? it.subjects[0] : -1;
            string label = IsTeacher ? (exam ? "試験の監督をしている" : SubjectName(s0) + "の授業をしている") : (exam ? "試験を受けている" : SubjectName(s0) + "の授業を受けている");
            if (!GoClassroom(exam, s0, IsTeacher, label, true)) return false;
            mobClassEnd = it.end;
            return true;
        }

        /// <summary>授業中（MobThink から）。true ならこの回はこれで終わり。</summary>
        bool MobClassroomThink(double now)
        {
            if (mobClassEnd < 0) return false;
            if (now < mobClassEnd)
            {
                if (state == State.Talking || state == State.Private) return false; // 篤に誘われた など
                if (!classRoom) { mobClassEnd = -1; return false; }
                ClassroomFindSeat();
                return true;
            }
            // 終わった：学力が伸びる（見えなくなっていたときと同じ）
            mobClassEnd = -1;
            foreach (int s in mobClassSubjects)
            {
                if (s < 0 || s >= Academics.Count) continue;
                AddSkill(s, IsTeacher ? 0.3f : 2.2f);
                weekSubject[s] += 6f;
            }
            mobClassSubjects.Clear();
            LeaveClassroom();
            actionText = IsTeacher ? "授業を終えた" : "授業が終わった";
            mobUntil = now + Random.Range(1f, 6f);
            return true;
        }

        /// <summary>試験用（life who）：教室の席の数・うまっている数・その教室にいる人数。</summary>
        string ClassroomDebug()
        {
            if (!classRoom) return "";
            var a = world.AreaAt(transform.position);
            if (a == null) return " 教室（区画なし）";
            int all = 0, used = 0, people = 0;
            foreach (var s in world.classSeats) if (SameArea(s.area, a)) { all++; if (s.occupant != null) used++; }
            foreach (var c in world.characters) if (c != null && c.classRoom && !c.classTeaching && world.AreaAt(c.transform.position) == a) people++;
            return " 教室=" + a.name + " 席" + used + "/" + all + " 生徒" + people + (seat != null ? " 座席あり" : " 立ち");
        }

        /// <summary>教室で座っている・教えている（動きの使い分けに使う）。</summary>
        public bool InClassroom { get { return classRoom; } }
    }
}
