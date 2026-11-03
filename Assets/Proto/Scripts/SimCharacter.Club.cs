using UnityEngine;

namespace LifeSimProto
{
    /// <summary>部活動の動き（2026-11-01 famuru。仕組みは Clubs.cs）。</summary>
    public partial class SimCharacter
    {
        /// <summary>入っている部（Clubs の番号。-1 はなし）。</summary>
        public int club = -1;
        enum ClubPhase { None, Going, Active, Home }
        ClubPhase clubPhase;
        double clubUntil = -1, clubStarted = -1;
        int clubDay = -1;
        /// <summary>立ってする場面の動きを選ぶ言葉（部活・ダンジョン）。null ならいつもどおり。</summary>
        public string sceneLabel;

        public bool InClub { get { return clubPhase == ClubPhase.Active; } }
        /// <summary>試験用：部活のようす。</summary>
        public string ClubDebug { get { return ClubName + "・" + clubPhase + "・" + (clubDay == world.Day ? "今日済" : "未") + "・" + state + "・" + awayKind; } }
        public string ClubName { get { return Clubs.NameOf(club); } }

        /// <summary>今から部活へ行く時間か（寮生・篤）。</summary>
        bool ClubDue(double now)
        {
            if (club < 0 || IsTeacher || clubDay == world.Day || sickRest || hasOrder || privatePending || lifeEv != null) return false;
            double m = now - ScheduleMaker.DayStart(world.Day);
            if (m < Clubs.StartMin - 10 || m > Clubs.EndMin - 30) return false;
            if (!Clubs.ActiveDay(world, world.Day)) return false;
            return true;
        }

        /// <summary>寮から部活へ出かける（寮生・篤）。学校までは遠いので、寮を出たら少しして学校の部の場所に現れる。</summary>
        bool TryStartClubTrip(double now)
        {
            if (!ClubDue(now)) return false;
            if (state == State.Talking || state == State.Sleeping || state == State.Away || state == State.Private || state == State.Eating || state == State.Bathing) return false;
            if (state == State.Approaching || state == State.Waiting) return false;
            clubDay = world.Day;
            clubPhase = ClubPhase.Going;
            awayKind = AwayKind.Club;
            awayUntil = now + 12;
            awayLabel = Clubs.NameOf(club) + "の活動に向かっている";
            // すでに学校にいる（実技の授業の場所など）なら、その場で向かう
            if (LoungeWorld.InSchool(transform.position) && WalkToClub()) return true; // 学校の中からも歩いて行く（2026-11-04）
            if (LoungeWorld.InSchool(transform.position)) { LeaveCurrentSpot(); path.Clear(); goal = Goal.None; SetState(State.Away); SetVisible(false); actionText = awayLabel; return true; }
            if (!WalkToClub()) StartLeave(Clubs.NameOf(club) + "の活動に出かける"); // 部の場所まで歩く（SimCharacter.Outside.cs）
            ShowBubble(Say("c_go", "いってきます"), 1.6f);
            return true;
        }

        /// <summary>外出が終わったとき：部活の行き帰りなら処理して true。</summary>
        bool ClubAfterAway(double now)
        {
            if (awayKind == AwayKind.Class && ClubDue(now))
            {
                // 授業のあとそのまま部活へ（寮に戻らない）
                if (IsTarget && world.player != null) world.player.Earn(15 * classBlocks, "授業に出た");
                AfterClass();
                clubDay = world.Day;
                if (leaveSet)
                {
                    // 教室から出てきて、部の場所まで歩く（2026-11-04）
                    transform.position = leaveTo; leaveSet = false;
                    SetVisible(true);
                    SetState(State.Idle);
                    if (WalkToClub()) return true;
                }
                AppearAtClub();
                return true;
            }
            if (awayKind != AwayKind.Club) return false;
            if (clubPhase == ClubPhase.Going) { AppearAtClub(); return true; }
            clubPhase = ClubPhase.None;
            return false; // 帰ってきた：ReturnHome
        }

        /// <summary>学校の部の場所に現れて、活動を始める。</summary>
        void AppearAtClub()
        {
            var areas = world.MobAreas(Clubs.Place(club, world.BadWeatherToday));
            if (areas.Count == 0) { clubPhase = ClubPhase.None; ReturnHome(); return; }
            var a = areas[Random.Range(0, areas.Count)];
            transform.position = world.RandomPointIn(a);
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            SetVisible(true);
            path.Clear();
            goal = Goal.None;
            act = Act.None;
            poi = null;
            StartClubActive();
        }

        void StartClubActive()
        {
            clubPhase = ClubPhase.Active;
            clubStarted = world.Now;
            clubUntil = ScheduleMaker.DayStart(world.Day) + Clubs.EndMin + (IsMob ? 0 : Random.Range(-5f, 5f));
            sceneLabel = Clubs.Scene[club];
            actionText = Clubs.Doing[club] + "（" + Clubs.NameOf(club) + "）";
            SetState(IsMob ? State.Idle : State.Activity);
        }

        /// <summary>部活の終わり：伸びる・仲良くなる。寮生・篤は寮へ帰る（見えなくなって寮の入口に戻る）。</summary>
        void EndClub(bool goHome)
        {
            if (clubPhase != ClubPhase.Active) return;
            float mins = Mathf.Clamp((float)(world.Now - clubStarted), 0f, 120f);
            clubPhase = ClubPhase.None;
            sceneLabel = null;
            int s = Clubs.Subject[club];
            AddSkill(s, mins * 0.025f * (0.5f + (profile != null ? profile.diligence : 0.5f)));
            weekSubject[s] += mins * 0.05f;
            GrowAbility(Clubs.Ability[club], mins * 0.006f);
            if (!IsMob)
            {
                bored = Mathf.Max(0f, bored - mins * 0.3f);
                lonely = Mathf.Max(0f, lonely - mins * 0.2f);
                tired += mins * 0.12f;
                // 一緒に活動した仲間（寮生と篤）と仲良くなる
                foreach (var o in world.residents)
                {
                    if (o == this || o.club != club || !o.InClub || o.IsMob) continue;
                    world.rel.Add(id, o.id, 1.5f, world.simTime); // 先に終わった方が足す（1組1回）
                }
                var f = world.RomanceTarget;
                if (f != null && f != this && f.club == club && f.InClub && !IsTarget) world.ChangeLove(this, 0.6f);
            }
            if (!goHome) return;
            if (IsMob) { SetState(State.Idle); actionText = "部活が終わった"; mobUntil = world.Now + Random.Range(1f, 6f); return; }
            // 寮生・篤：寮へ歩いて帰る（SimCharacter.Outside.cs）。道が無ければ前のとおり見えなくなって、少しして寮の入口に戻る
            if (WalkHomeFromClub()) return;
            LeaveCurrentSpot();
            path.Clear();
            goal = Goal.None;
            awayKind = AwayKind.Club;
            clubPhase = ClubPhase.Home;
            awayUntil = world.Now + 12;
            awayLabel = "部活から帰っている";
            actionText = awayLabel;
            SetState(State.Away);
            SetVisible(false);
        }

        /// <summary>部活の途中で別のことをした（話しかけられた・誘われた）：活動はそこまで。</summary>
        void CheckClubInterrupted()
        {
            if (clubPhase != ClubPhase.Active) return;
            // 話しかけられたあとなど：まだ時間があれば活動にもどる
            if (state == State.Idle && world.Now < clubUntil) { SetState(State.Activity); actionText = Clubs.Doing[club] + "（" + Clubs.NameOf(club) + "）"; return; }
            if (state != State.Activity && state != State.Talking) EndClub(false);
        }

        // ================= ほかの寮の学生 =================
        bool MobClubDue(double now)
        {
            if (club < 0 || IsTeacher || clubDay == world.Day) return false;
            double m = now - ScheduleMaker.DayStart(world.Day);
            if (m < Clubs.StartMin - 5 || m > Clubs.EndMin - 30) return false;
            return Clubs.ActiveDay(world, world.Day);
        }

        void MobGoClub()
        {
            clubDay = world.Day;
            var areas = world.MobAreas(Clubs.Place(club, world.BadWeatherToday));
            if (areas.Count == 0) return;
            var a = areas[Random.Range(0, areas.Count)];
            Vector3 p = world.RandomPointIn(a);
            LeaveCurrentSpot();
            clubPhase = ClubPhase.Going;
            actionText = Clubs.NameOf(club) + "の活動へ";
            goal = Goal.Wander;
            mobGrowSubject = -1; mobGrowAbility = -1; mobStarted = -1;
            // 遠いときは、着いたことにする（授業へ行くときと同じ）
            if (!GoTo(p, State.Walking) || PathLength() > 40f) { path.Clear(); transform.position = p; goal = Goal.None; StartClubActive(); mobUntil = clubUntil; }
            if (mobHomeAt < ScheduleMaker.DayStart(world.Day) + Clubs.EndMin) mobHomeAt = ScheduleMaker.DayStart(world.Day) + Clubs.EndMin + Random.Range(5f, 60f);
        }

        /// <summary>ほかの寮の学生が部の場所に着いた。</summary>
        bool MobClubArrive()
        {
            if (clubPhase != ClubPhase.Going) return false;
            goal = Goal.None;
            StartClubActive();
            mobUntil = clubUntil;
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            return true;
        }
    }
}
