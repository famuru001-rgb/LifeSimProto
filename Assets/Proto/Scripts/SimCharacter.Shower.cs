using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// シャワー（famuru 2026-11-14「シャワーはトレーニングの後に使うようにして、プールの後にも」。家具のチャット）。
    /// 塔のトレーニング部屋で体を動かし終えたら（2時間以内）、プールで遊んで更衣小屋で着がえ直したら（4時間以内）、
    /// 次に手が空いたとき、寮の南西の塔3階のシャワールームへ行ってシャワーを浴びる。
    /// あわせて、近いほど選ばれやすい見どころの選び方（PickPoi）。
    /// </summary>
    public partial class SimCharacter
    {
        double showerWantUntil = -1.0;
        const string ShowerLabel = "シャワーを浴びている";
        static readonly string[] TrainingLabels = { "マットの上で体をほぐしている", "肋木でのびをしている", "木人で剣の練習をしている", "魔法の的に向かって練習している" };

        /// <summary>これから minutes 分（ゲーム）のうちにシャワーを浴びたい。</summary>
        void ShowerWant(float minutes) { if (world != null) showerWantUntil = world.Now + minutes; }

        /// <summary>空き時間の選び方の前に呼ぶ。シャワーへ向かったら true。</summary>
        bool ShowerAfter()
        {
            if (world == null) return false;
            if (state != State.Idle && state != State.Sitting && state != State.Activity) return false;
            if (state == State.Activity && poi != null && System.Array.IndexOf(TrainingLabels, poi.label) >= 0)
            {
                if (stateTime < activityDuration) return false; // 練習の途中
                ShowerWant(120f);
            }
            if (showerWantUntil < 0.0) return false;
            if (world.Now > showerWantUntil) { showerWantUntil = -1.0; return false; }
            if (state == State.Activity && stateTime < activityDuration) return false;
            if (state == State.Sitting && stateTime < 6f) return false;
            if (wearingSwim || InDungeon) return false;
            Poi sh = FindPoi(x => x.label == ShowerLabel && x.FreeSlot() >= 0);
            if (sh == null) return false; // 空いていなければ、あとでもう一度
            showerWantUntil = -1.0;
            return GoToPoi(sh, Act.Fun, ShowerLabel, Random.Range(8f, 12f));
        }

        /// <summary>条件に合う見どころを、近いほど選ばれやすく1つ選ぶ（なければ null）。</summary>
        Poi PickPoi(System.Predicate<Poi> pred, float range = 10f)
        {
            Poi best = null; float total = 0f;
            foreach (var p in world.pois)
            {
                if (!pred(p)) continue;
                int fs = p.FreeSlot();
                Vector3 sp = fs >= 0 ? p.slots[fs] : p.lookAt;
                float w = 1f / (1f + Vector3.Distance(transform.position, sp) / range);
                total += w;
                if (Random.value * total <= w) best = p;
            }
            return best;
        }
    }
}
