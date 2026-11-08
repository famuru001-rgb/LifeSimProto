using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 篤の操作「頭をなでる」「抱きしめる」（2026-11-08 famuru「篤の操作に足す」）。近くにいる子に、その場で2人の動きを流す。
    /// ・頭をなでる：L_HeadPat（篤が M）。気持ちが「気になる」以上なら照れて受ける、それより低いと身を引く
    /// ・抱きしめる：正面から抱きしめる動き（L_Hug_Hold）ができるまでは、しがみつく（L_Cling）で代わり。「好き」以上・恋人で受ける
    /// 手は ProtoBody.Reach.cs の表で相手の頭・背中へ合わせる。
    /// </summary>
    public partial class SimCharacter
    {
        public static float TouchNear = 2.5f;     // この距離より近いときだけ（遠いときは先に話しかけて近づく）

        SimCharacter touchF;        // なでて・抱きしめてくれている篤（女の子の側だけ持つ）
        string touchForm, touchPart;
        float touchT0, touchEnd;
        Vector3 touchPos; Quaternion touchRot;

        public bool Touching { get { return touchF != null; } }

        /// <summary>篤の側：今なでている・抱きしめている女の子。</summary>
        SimCharacter TouchGirl
        {
            get
            {
                if (!IsTarget) return null;
                foreach (var c in world.residents) if (c != null && c.touchF == this) return c;
                return null;
            }
        }

        public bool NearForTouch(SimCharacter f) { return f != null && Present && Vector3.Distance(f.transform.position, transform.position) < TouchNear && Mathf.Abs(f.transform.position.y - transform.position.y) < 0.5f; }

        /// <summary>篤が頭をなでる（hug=false）・抱きしめる（hug=true）。受けたら true。</summary>
        public bool AskTouch(SimCharacter f, bool hug)
        {
            if (f == null || IsTarget || IsMob) return false;
            if (!NearForTouch(f)) { world.Log("篤：" + displayName + "のそばへ行ってから"); return false; }
            if (state == State.Sleeping || InPrivate || state == State.Bathing || lifeEv != null || InDungeon || walkWith != null) { world.Log(displayName + "は今はむり"); return false; }
            bool ok = hug ? (IsLover || LoveStage >= 2) : LoveStage >= 1 || IsLover;
            if (!ok)
            {
                ShowBubble(Say(hug ? "hug_no" : "pat_no", hug ? "ちょ、ちょっと待って…！" : "…えっ、なに？"), 2.2f);
                world.Log(displayName + "は" + (hug ? "抱きしめられるのを" : "頭をなでられるのを") + "かわした");
                world.rel.Add(id, f.id, -1f, world.simTime);
                return false;
            }
            string form = hug ? (ProtoBody.FindClip("L_Hug_Hold_F") != null ? "Hug" : "Cling") : "HeadPat";
            string part = form == "Hug" ? "Hold" : (form == "Cling" ? "Cling" : "Pat");
            if (ProtoBody.FindClip("L_" + form + "_" + part + "_F") == null) { world.Log("（" + form + " の動きが無い）"); return false; }
            LeaveCurrentSpot(); path.Clear(); goal = Goal.None; SetState(State.Idle);
            f.path.Clear(); if (f.state == State.Walking || f.state == State.Approaching) f.SetState(State.Idle);
            // 2人を向かい合わせて、女の子の位置に合わせて動きの基準の点を決める
            Vector3 toF = f.transform.position - transform.position; toF.y = 0f;
            if (toF.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(toF);
            var sp = new LifePairBook.Spec { form = form, parts = new[] { part } };
            LifePairBook.Anchor(sp, transform.position, transform.rotation, out touchPos, out touchRot);
            touchF = f; touchForm = form; touchPart = part;
            touchT0 = Time.time; touchEnd = Time.time + (hug ? 12f : 10f);
            actionText = hug ? f.displayName + "に抱きしめられている" : f.displayName + "に頭をなでられている";
            ShowBubble(Say(hug ? "hug_yes" : "pat_yes", hug ? (IsLover ? "…もう少しだけ、このまま" : "わっ…！　…あったかい") : "えへへ…もっと"), 2.4f);
            world.Log("♥ " + f.displayName + "が" + displayName + (hug ? "を抱きしめた" : "の頭をなでた"));
            world.rel.Add(id, f.id, hug ? 3f : 2f, world.simTime);
            return true;
        }

        void EndTouch()
        {
            if (touchF == null) return;
            touchF = null;
            if (state == State.Idle) actionText = "ひと息ついた";
        }

        /// <summary>Think から。true ならこの回はこれで終わり。</summary>
        bool TouchThink()
        {
            if (IsTarget)
            {
                // 篤の側：なでている・抱きしめている間はその場に止まる（指示が来たら、指示が優先して終わる）
                var g = TouchGirl;
                if (g == null || hasOrder) return false;
                if (state == State.Walking || state == State.Approaching) { path.Clear(); goal = Goal.None; SetState(State.Idle); }
                actionText = g.displayName + (g.touchForm == "HeadPat" ? "の頭をなでている" : "を抱きしめている");
                return true;
            }
            if (touchF == null) return false;
            var f = touchF;
            if (Time.time >= touchEnd || !f.Present || f.InPrivate || f.hasOrder || state == State.Sleeping) { EndTouch(); return false; }   // 篤に指示が来たら終わる
            if (state != State.Idle) { path.Clear(); goal = Goal.None; SetState(State.Idle); }
            return true;
        }

        /// <summary>毎コマ（UpdateCore2）：篤の側、なでている・抱きしめている間は歩かない（食事の列などで動かされないように）。</summary>
        void TouchHoldF()
        {
            if (!IsTarget || hasOrder || TouchGirl == null) return;
            path.Clear();
            if (state == State.Walking || state == State.Approaching || state == State.Waiting) { goal = Goal.None; SetState(State.Idle); }
        }

        /// <summary>HMotionNow から：なでる・抱きしめるの2人の動き。</summary>
        bool TouchMotion(out string clip, out float time, out Vector3 pos, out Quaternion rot)
        {
            clip = null; time = 0f; pos = transform.position; rot = transform.rotation;
            SimCharacter girl = touchF != null ? this : TouchGirl;
            if (girl == null || Time.time >= girl.touchEnd) return false;
            bool m = girl != this;
            string name = "L_" + girl.touchForm + "_" + girl.touchPart + "_" + (m ? "M" : "F");
            var c = ProtoBody.FindClip(name);
            if (c == null) return false;
            clip = name; time = (Time.time - girl.touchT0) % Mathf.Max(0.1f, c.length); pos = girl.touchPos; rot = girl.touchRot;
            return true;
        }

        /// <summary>手を合わせる相手（SimCharacter.Reach.cs）。</summary>
        SimCharacter TouchPartner { get { return touchF != null ? touchF : TouchGirl; } }
    }
}
