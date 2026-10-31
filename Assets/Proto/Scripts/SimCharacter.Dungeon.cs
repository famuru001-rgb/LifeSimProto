using UnityEngine;

namespace LifeSimProto
{
    /// <summary>ダンジョン実習のときのキャラの動き（2026-11-01。進み方は Dungeon.cs の DungeonDirector が決める）。</summary>
    public partial class SimCharacter
    {
        /// <summary>参加しているダンジョン実習の組（null なら参加していない）。</summary>
        public DungeonRun dRun;
        public bool InDungeon { get { return dRun != null; } }
        public bool HasPath { get { return path.Count > 0; } }

        /// <summary>ダンジョン実習に来られるか。</summary>
        public bool CanJoinDungeon()
        {
            if (dRun != null || !Present || sickRest || profile == null || IsTeacher) return false;
            if (state == State.Sleeping || state == State.Private || state == State.Talking || state == State.Bathing || state == State.Eating) return false;
            if (privatePending || chatting) return false;
            return true;
        }

        /// <summary>組に入る：準備室へ向かう（学校にいればその場で、寮にいれば寮を出てから）。</summary>
        public void JoinDungeon(DungeonRun r)
        {
            dRun = r;
            if (clubPhase == ClubPhase.Active) EndClub(false);
            LeaveCurrentSpot();
            hasOrder = false;
            target = null;
            intent = Intent.Talk;
            sceneLabel = null;
            awayKind = AwayKind.Dungeon;
            awayUntil = world.Now + 600;
            awayLabel = "ダンジョン実習に出かけている";
            if (IsMob || LoungeWorld.InSchool(transform.position))
            {
                path.Clear();
                goal = Goal.None;
                SetState(State.Away);
                SetVisible(false);
                actionText = awayLabel;
            }
            else
            {
                StartLeave("ダンジョン実習に出かける");
                if (!IsMob) ShowBubble(Say("c_go", "いってきます"), 1.6f);
            }
        }

        /// <summary>ダンジョンの場所へ移る（入口の魔法陣・下の階へ降りたとき）。</summary>
        public void DungeonTeleport(Vector3 p)
        {
            path.Clear();
            goal = Goal.None;
            transform.position = p;
            transform.rotation = Quaternion.LookRotation(Vector3.forward);
            SetVisible(true);
            SetState(State.Idle);
            sceneLabel = null;
            actionText = "ダンジョンに入った";
        }

        /// <summary>次の部屋へ歩く。</summary>
        public void DungeonWalk(Vector3 p, string text)
        {
            path.Clear();
            path.Add(p);
            destination = p;
            goal = Goal.None;
            sceneLabel = null;
            SetState(State.Walking);
            actionText = text;
        }

        /// <summary>部屋に着いて、出来事の方を向いて立つ。</summary>
        public void DungeonStand(Vector3 look)
        {
            path.Clear();
            goal = Goal.None;
            if (state != State.Idle) SetState(State.Idle);
            Vector3 d = look - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d.normalized);
            actionText = "まわりを見ている（ダンジョン）";
        }

        /// <summary>出来事の間の動き（戦う＝剣術・魔法、調べる＝工房 など）。</summary>
        public void DungeonPose(string scene, string text)
        {
            sceneLabel = scene;
            actionText = text;
            if (state != State.Idle) SetState(State.Idle);
        }

        /// <summary>ダンジョンの場所をほかの組にゆずって、見えない所で続ける。</summary>
        public void DungeonHide()
        {
            path.Clear();
            goal = Goal.None;
            sceneLabel = null;
            SetState(State.Away);
            SetVisible(false);
            actionText = awayLabel = "ダンジョン実習に出かけている";
        }

        /// <summary>組の探索が終わった：寮生・篤は寮へ帰る。ほかの寮の学生は準備室に戻る。</summary>
        public void LeaveDungeon(DungeonRun r)
        {
            if (dRun != r) return;
            dRun = null;
            sceneLabel = null;
            path.Clear();
            goal = Goal.None;
            if (IsMob)
            {
                var rooms = world.MobAreas(a => a.name.EndsWith("ダンジョン実習の準備室"));
                transform.position = rooms.Count > 0 ? world.RandomPointIn(rooms[0]) : LoungeWorld.SchoolGate;
                SetVisible(true);
                SetState(State.Idle);
                actionText = "ダンジョン実習から戻った";
                mobUntil = world.Now + Random.Range(2f, 8f);
                mobStarted = -1;
                return;
            }
            awayKind = AwayKind.Dungeon;
            awayUntil = world.Now + 10;
            awayLabel = "ダンジョン実習から帰っている";
            actionText = awayLabel;
            SetState(State.Away);
            SetVisible(false);
        }

        /// <summary>出かけている間：ダンジョン実習の途中なら帰らない。</summary>
        bool DungeonAfterAway(double now) { return dRun != null; }
    }
}
