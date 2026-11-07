using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// どこを見るか（2026-11-07 famuru「IK の A：話している相手（や見ている物）のほうへ首と頭を向ける」）。
    /// famuru の決定：話し相手・授業中は先生と黒板（先生は生徒を見回す）・作業している物。目はまだ動かさない（目は顔の絵のため）。
    /// 実際に首と頭を回すのは ProtoBody.UpdateLook（動きを流したあと、揺れの計算の前）。
    /// </summary>
    public partial class SimCharacter
    {
        ProtoBody lookBody;
        SimCharacter lookPick;      // 授業で見ている人（先生：生徒／生徒：先生）
        float lookPickUntil;

        /// <summary>この人の頭の位置（モデルが無いときは背の高さから）。</summary>
        public Vector3 HeadPos
        {
            get
            {
                if (lookBody == null) lookBody = GetComponent<ProtoBody>();
                return lookBody != null ? lookBody.HeadPos : transform.position + Vector3.up * 1.5f;
            }
        }

        /// <summary>今見る所。無ければ false（動きのとおりの向き）。lookWhy は試験用の説明。</summary>
        public bool LookTargetNow(out Vector3 p, out string why)
        {
            p = Vector3.zero; why = null;
            if (!Present || state == State.Sleeping || state == State.Private || state == State.Away || InPrivate) return false;

            // 話し相手（会話・おしゃべり）
            SimCharacter o = state == State.Talking ? talkPartner : (chatting ? chatPartner : null);
            if (o != null && o != this && o.Present) { p = o.HeadPos; why = "話し相手（" + o.displayName + "）"; return true; }

            // 授業中：生徒は先生（いなければ黒板）、先生は生徒を見回す
            if (classRoom && (state == State.Sitting || state == State.Idle))
            {
                var a = world.AreaAt(transform.position);
                if (a != null)
                {
                    if (lookPick != null && (Time.time > lookPickUntil || !lookPick.classRoom || world.AreaAt(lookPick.transform.position) != a)) lookPick = null;
                    if (classTeaching)
                    {
                        if (lookPick == null)
                        {
                            var list = new System.Collections.Generic.List<SimCharacter>();
                            foreach (var s in world.classSeats) if (s.occupant != null && s.occupant != this && SameArea(s.area, a)) list.Add(s.occupant);
                            if (list.Count > 0) { lookPick = list[Random.Range(0, list.Count)]; lookPickUntil = Time.time + Random.Range(2.5f, 6f); }
                        }
                        if (lookPick != null) { p = lookPick.HeadPos; why = "生徒（" + lookPick.displayName + "）"; return true; }
                    }
                    else
                    {
                        if (lookPick == null)
                            foreach (var c in world.characters)
                                if (c != null && c.classRoom && c.classTeaching && world.AreaAt(c.transform.position) == a) { lookPick = c; lookPickUntil = Time.time + 30f; break; }
                        if (lookPick != null) { p = lookPick.HeadPos; why = "先生（" + lookPick.displayName + "）"; return true; }
                        foreach (var s in world.teachSpots)
                            if (SameArea(s.area, a)) { p = s.position - s.forward * 0.8f + Vector3.up * 1.5f; why = "黒板"; return true; }   // 先生の立つ所の後ろ（黒板）
                    }
                }
            }

            // 作業している物（料理・楽器・本棚・魔法の的 など）
            if ((state == State.Activity || state == State.Sitting) && poi != null) { p = poi.lookAt; why = "作業の物（" + poi.label + "）"; return true; }
            return false;
        }
    }
}
