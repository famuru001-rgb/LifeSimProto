using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 篤といっしょに歩く（手をつなぐ・腕を組む。2026-11-08 famuru「進めて」）。
    /// famuru の決定：篤といっしょに歩くとき・篤の操作（右クリック）・散歩や行事のとき。気持ちが「好き」なら手をつなぐ、恋人なら腕を組む。
    /// ・右クリックの「手をつないで歩く」「腕を組んで歩く」で篤が誘う。気持ちが足りないと断る
    /// ・いっしょに歩いている間、女の子は篤の左にくっついて歩く（自分の予定はお休み）。歩く動きは
    ///   L_HandWalk_Walk・L_ArmWalk_Walk（あれば）、手は ProtoBody.Reach.cs の表で篤の手・腕に合わせる
    /// ・終わる：右クリックの「手をはなす」、篤が二人きりの時間・寝る・座る、45分たった、授業や食事の時間になった
    /// </summary>
    public partial class SimCharacter
    {
        public static float WalkSideHand = 0.30f, WalkSideArm = 0.24f;   // 篤の腰からの横の間（m）
        public static float WalkTogetherMin = 45f;

        SimCharacter walkWith;       // いっしょに歩いている篤（女の子の側だけ持つ）
        bool walkArm;                 // 腕を組んでいる（でなければ手をつなぐ）
        double walkUntil;
        float walkSitSince = -1f;

        public bool WalkingTogether { get { return walkWith != null; } }
        public bool WalkArm { get { return walkArm; } }

        /// <summary>篤の側：いっしょに歩いている女の子（いなければ null）。</summary>
        public SimCharacter WalkGirl
        {
            get
            {
                if (!IsTarget) return null;
                foreach (var c in world.residents) if (c != null && c.walkWith == this) return c;
                return null;
            }
        }

        /// <summary>手をつなげるか（気持ちが「好き」以上）・腕を組めるか（恋人）。</summary>
        public bool CanHoldHands { get { return !IsTarget && !IsMob && (IsLover || LoveStage >= 2); } }

        /// <summary>篤が誘う（右クリック）。受けたら true。</summary>
        public bool AskWalkTogether(SimCharacter f, bool arm)
        {
            if (f == null || IsTarget || IsMob) return false;
            if (!Present || state == State.Sleeping || InPrivate || state == State.Bathing || lifeEv != null || InDungeon || IsSick)
            { world.Log(displayName + "は今は手をつなげない"); return false; }
            bool ok = arm ? IsLover : CanHoldHands;
            if (!ok)
            {
                ShowBubble(Say("walk_no", arm ? "えっ…腕は、まだちょっと…" : "て、手は…まだ恥ずかしいかな"), 2.2f);
                world.Log(displayName + "は" + (arm ? "腕を組むのを" : "手をつなぐのを") + "照れて断った");
                return false;
            }
            var other = f.WalkGirl;
            if (other != null && other != this) other.EndWalkTogether("ほかの子と");
            LeaveCurrentSpot();
            path.Clear();
            hasOrder = false;
            walkWith = f;
            walkArm = arm;
            walkUntil = world.Now + WalkTogetherMin;
            walkSitSince = -1f;
            goal = Goal.None;
            SetState(State.Idle);
            actionText = f.displayName + (arm ? "と腕を組んで歩いている" : "と手をつないで歩いている");
            ShowBubble(Say(arm ? "walk_arm" : "walk_hand", arm ? "ふふっ、離さないからね" : "…うん。手、あったかい"), 2.2f);
            world.Log("♥ " + displayName + "が" + f.displayName + "と" + (arm ? "腕を組んで" : "手をつないで") + "歩きはじめた");
            world.rel.Add(id, f.id, 2f, world.simTime);
            return true;
        }

        public void EndWalkTogether(string why)
        {
            if (walkWith == null) return;
            var f = walkWith;
            walkWith = null;
            walkSitSince = -1f;
            if (state == State.Idle) actionText = "ひと休み";
            if (why != null) world.Log(displayName + "が" + f.displayName + "と" + (walkArm ? "組んでいた腕" : "つないでいた手") + "をはなした（" + why + "）");
        }

        /// <summary>毎コマ（UpdateCore2）：篤の左にくっついて歩く。</summary>
        void WalkTogetherFollow(float dt)
        {
            var f = walkWith;
            if (f == null) return;
            float side = walkArm ? WalkSideArm : WalkSideHand;
            Vector3 want = f.transform.position - f.transform.right * side;
            Vector3 cur = transform.position;
            // 段や壁で離れすぎたら、歩ける所に置き直す
            Vector3 next = Vector3.Lerp(cur, want, 1f - Mathf.Exp(-dt * 12f));
            if ((next - want).sqrMagnitude > 1.5f * 1.5f || Mathf.Abs(next.y - f.transform.position.y) > 0.6f) next = world.NearestFree(want);
            transform.position = next;
            transform.rotation = Quaternion.Slerp(transform.rotation, f.transform.rotation, 1f - Mathf.Exp(-dt * 10f));
        }

        /// <summary>Think から：いっしょに歩いている間は自分の予定をお休み。true ならこの回はこれで終わり。</summary>
        bool WalkTogetherThink(double now)
        {
            if (walkWith == null) return false;
            var f = walkWith;
            if (!f.Present || f.InPrivate || f.state == State.Sleeping || f.state == State.Bathing || f.InDungeon) { EndWalkTogether(null); return false; }
            if (InPrivate || state == State.Sleeping || lifeEv != null) { EndWalkTogether(null); return false; }
            if (now >= walkUntil) { EndWalkTogether("しばらく歩いた"); return false; }
            // 篤が座ったら、少しして手をはなす（となりに座るかは今までどおり自分で決める）
            if (f.state == State.Sitting || f.state == State.Eating)
            {
                if (walkSitSince < 0f) walkSitSince = Time.time;
                else if (Time.time - walkSitSince > 3f) { EndWalkTogether("篤が座った"); return false; }
            }
            else walkSitSince = -1f;
            // 授業・食事などの予定の時間になったら、はなして向かう
            foreach (var it in schedule)
            {
                if (it.done || it.kind == TaskKind.Bed || it.kind == TaskKind.Bath) continue;
                double lead = it.kind == TaskKind.Class ? ClassLeadMin : 0;
                if (now >= it.start - lead && now < it.end) { EndWalkTogether(it.kind == TaskKind.Class ? "授業の時間" : "ごはんの時間"); return false; }
            }
            if (state != State.Idle && state != State.Talking) { path.Clear(); goal = Goal.None; SetState(State.Idle); }
            actionText = f.displayName + (walkArm ? "と腕を組んで歩いている" : "と手をつないで歩いている");
            return true;
        }

        /// <summary>歩く動き（ProtoBody が聞く）：いっしょに歩いている間の2人の歩き（無ければ null）。</summary>
        public string PairWalkClip
        {
            get
            {
                if (walkWith != null) return "L_" + (walkArm ? "ArmWalk" : "HandWalk") + "_Walk_F";
                var g = IsTarget ? WalkGirl : null;
                if (g != null) return "L_" + (g.walkArm ? "ArmWalk" : "HandWalk") + "_Walk_M";
                return null;
            }
        }

        /// <summary>手を合わせる表で使う名前（女の子の側だけ。篤は自分の手を動かさない）。</summary>
        public string WalkReachClip { get { return walkWith != null ? PairWalkClip : null; } }
    }
}
