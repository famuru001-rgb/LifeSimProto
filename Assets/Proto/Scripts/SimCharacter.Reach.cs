using System.Collections.Generic;

namespace LifeSimProto
{
    /// <summary>
    /// 手を相手の体に合わせる（IK の B。2026-11-08 famuru「B をためして」）：二人の動きをしている相手（ProtoBody.UpdateReach が使う）。
    /// 二人きりの時間は、いっしょにいる全員（篤・相手・ペッティングの子）。生活のできごとの2人の動きは、もう1人。
    /// </summary>
    public partial class SimCharacter
    {
        public void MotionPartners(List<SimCharacter> list)
        {
            list.Clear();
            if (hs != null) { if (state == State.Private) hs.Others(this, list); return; }
            var r = lifeEv;
            if (r != null && r.pair != null && r.pairSet)
            {
                if (r.main == this && r.partner != null) list.Add(r.partner);
                else if (r.partner == this && r.main != null) list.Add(r.main);
            }
        }
    }
}
