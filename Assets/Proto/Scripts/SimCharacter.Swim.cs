using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// プールと浜辺の水着の着がえ（famuru 2026-11-13「プールにも着替え要素を含めて」。家具のチャット）。
    /// 昼にプールか浜辺へ行くときは、先に更衣小屋（プール）か浜の休憩小屋（浜辺）に寄って水着に着がえる。
    /// 水着のままプールと浜辺の外へ行くときは、近いほうの小屋に寄って着がえ直す。
    /// 見た目が水着になるのは水着の服（Outfit_Swim_…）を持っている子だけ（OutfitNow）。持っていない子も小屋に寄る動きはする。
    /// </summary>
    public partial class SimCharacter
    {
        /// <summary>水着に着がえ済みか。</summary>
        public bool wearingSwim;
        bool swimDetour, swimTo;
        Vector3 swimDest;
        State swimState;
        float changeUntil = -1f;
        string changeSavedText;
        const float ChangeSeconds = 6f;

        /// <summary>着がえ中（小屋の中で立ち止まっている）。</summary>
        public bool ChangingNow { get { return swimDetour && changeUntil > 0f; } }

        /// <summary>GoTo の初めに呼ぶ。着がえに寄るなら小屋への道を作って true。</summary>
        bool SwimDetour(Vector3 dest, State moveState)
        {
            if (world == null || !CampusPreview.PoolBeachOn || InDungeon || (moveState != State.Walking && moveState != State.Approaching)) { swimDetour = false; changeUntil = -1f; return false; }
            bool destSwim = CampusPreview.InSwimZone(dest) && !CampusPreview.InHut(dest);
            bool want;
            if (!wearingSwim && destSwim && !LoungeWorld.IsNight(world.MinuteOfDay)) want = true;
            else if (wearingSwim && !CampusPreview.InSwimZone(dest)) want = false;
            else { swimDetour = false; changeUntil = -1f; return false; } // 着がえが要らない行き先なら、寄り道はやめる
            if (swimDetour && swimTo == want)
            {
                // 寄り道の途中で行き先だけ変わった（人について行くときなど、何度も呼ばれる）：小屋への道と着がえはそのまま
                swimDest = dest; swimState = moveState;
                if (state != moveState) SetState(moveState);
                return true;
            }
            swimDetour = false; changeUntil = -1f;
            Vector3 spot = CampusPreview.ChangingSpot(want ? dest : transform.position);
            var p = world.FindPath(transform.position, spot);
            if (p == null || p.Count == 0) { wearingSwim = want; return false; } // 小屋へ行けないときは、その場で着がえたことにする
            path.Clear(); path.AddRange(p);
            destination = spot;
            SetState(moveState);
            swimDetour = true; swimTo = want; swimDest = dest; swimState = moveState;
            return true;
        }

        /// <summary>Move で道が尽きたときに呼ぶ。着がえの寄り道の途中なら true（Arrive しない）。</summary>
        bool SwimArrive()
        {
            if (!swimDetour) return false;
            if (changeUntil < 0f)
            {
                changeUntil = Time.time + ChangeSeconds;
                changeSavedText = actionText;
                actionText = swimTo ? "水着に着がえている" : "制服に着がえている";
                return true;
            }
            if (Time.time < changeUntil) return true;
            wearingSwim = swimTo;
            if (!swimTo && CampusPreview.InPoolHut(transform.position)) ShowerWant(240f); // プールのあとはシャワー（2026-11-14）
            swimDetour = false; changeUntil = -1f;
            if (!string.IsNullOrEmpty(changeSavedText)) actionText = changeSavedText;
            if (!GoTo(swimDest, swimState)) { goal = Goal.None; SetState(State.Idle); }
            return true;
        }
    }
}
