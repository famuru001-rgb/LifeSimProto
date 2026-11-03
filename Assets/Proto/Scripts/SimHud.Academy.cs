using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 学園の画面（2026-11-01）：成績表の上のつまみで「成績・寮杯・部活・ダンジョン」を切りかえる。
    /// ダンジョン実習で篤の組が出来事に出会ったら、画面の下にどうするかの選択を出す。
    /// </summary>
    public partial class SimHud
    {
        int academyTab;   // 0 成績・1 寮杯・2 部活・3 ダンジョン
        static readonly string[] AcademyTabs = { "成績", "寮杯", "部活", "ダンジョン" };
        Vector2 academyScroll;

        /// <summary>成績表の右上に、つまみを出す。成績以外なら中身もここで描いて true。</summary>
        bool DrawAcademyTabs(Rect r)
        {
            float x = r.xMax - 92 - 4 * 104;
            for (int i = 0; i < AcademyTabs.Length; i++)
                if (GUI.Button(new Rect(x + i * 104, r.y + 8, 98, 30), AcademyTabs[i], academyTab == i ? buttonOn : smallButton)) academyTab = i;
            if (academyTab == 0) return false;
            float y = r.y + 50;
            switch (academyTab)
            {
                case 1: DrawCupTab(r, y); break;
                case 2: DrawClubTab(r, y); break;
                case 3: DrawDungeonTab(r, y); break;
            }
            return true;
        }

        void DrawCupTab(Rect r, float y)
        {
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 28), "寮杯：" + HouseCup.Standings(), small); y += 34;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), HouseCup.OurName + "は今 " + HouseCup.OurPlace() + "位。冬至の祭りで優勝を発表（点は良いことでだけ入る：試験の平均点・魔法競技会・行事・部の大会・ダンジョン実習）" + (HouseCup.Wins > 0 ? "　これまでの優勝 " + HouseCup.Wins + "回" : ""), tiny); y += 28;
            if (!string.IsNullOrEmpty(HouseCup.LastResult)) { GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), HouseCup.LastResult, tiny); y += 28; }
            // 点の棒
            int max = 1; for (int i = 0; i < HouseCup.Count; i++) max = Mathf.Max(max, HouseCup.Points[i]);
            var order = HouseCup.Order();
            foreach (int d in order)
            {
                GUI.Label(new Rect(r.x + 14, y, 120, 24), HouseCup.Name(d), small);
                float w = (r.width - 260) * HouseCup.Points[d] / (float)max;
                var old = GUI.color;
                GUI.color = d == 0 ? new Color(1f, 0.82f, 0.4f) : new Color(0.6f, 0.7f, 0.9f);
                GUI.DrawTexture(new Rect(r.x + 140, y + 4, Mathf.Max(2f, w), 18), Texture2D.whiteTexture);
                GUI.color = old;
                GUI.Label(new Rect(r.x + 150 + w, y, 100, 24), HouseCup.Points[d] + "点", small);
                y += 28;
            }
            y += 6;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "最近の点：", small); y += 26;
            var view = new Rect(r.x + 14, y, r.width - 28, r.yMax - y - 10);
            var content = new Rect(0, 0, view.width - 20, HouseCup.Recent.Count * 22 + 4);
            academyScroll = GUI.BeginScrollView(view, academyScroll, content);
            for (int i = 0; i < HouseCup.Recent.Count; i++) GUI.Label(new Rect(0, i * 22, content.width, 22), HouseCup.Recent[i], tiny);
            GUI.EndScrollView();
        }

        void DrawClubTab(Rect r, float y)
        {
            var f = F;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 26), "篤の部：" + (f != null ? f.ClubName : "―") + "　（授業の日の 16:30〜18:00 に学校で活動。選び直せる）", small); y += 32;
            float bx = r.x + 14;
            for (int k = 0; k < Clubs.Count; k++)
            {
                bool on = f != null && f.club == k;
                if (GUI.Button(new Rect(bx, y, 140, 30), Clubs.Names[k], on ? buttonOn : smallButton) && f != null && !on) Clubs.SetF(world, k);
                bx += 146;
            }
            y += 40;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "次の部の大会：" + AcademyDirector.NextTournamentText(world) + "　（寮ごとに上位3人の強さで比べ、寮杯の点）", tiny); y += 26;
            if (!string.IsNullOrEmpty(Clubs.LastTournament)) { GUI.Label(new Rect(r.x + 14, y, r.width - 28, 44), Clubs.LastTournament, tiny); y += 46; }
            for (int k = 0; k < Clubs.Count; k++)
            {
                var names = new List<string>();
                var per = new int[HouseCup.Count];
                foreach (var c in world.characters)
                {
                    if (c.club != k || c.IsTeacher) continue;
                    int d = Clubs.DormOf(c); if (d >= 0) per[d]++;
                    if (!c.IsMob) names.Add((string.IsNullOrEmpty(c.shortName) ? c.displayName : c.shortName) + (c.InClub ? "★" : ""));
                }
                string counts = "古時計 " + per[0] + "・冒険者 " + per[1] + "・騎士 " + per[2] + "・魔法師 " + per[3];
                GUI.Label(new Rect(r.x + 14, y, 140, 24), Clubs.Names[k], small);
                GUI.Label(new Rect(r.x + 150, y + 2, r.width - 170, 24), counts + "　｜" + string.Join("・", names.ToArray()), tiny);
                y += 30;
            }
            GUI.Label(new Rect(r.x + 14, y + 4, r.width - 28, 24), "★は今活動している子", tiny);
        }

        void DrawDungeonTab(Rect r, float y)
        {
            var dd = DungeonDirector.Instance;
            if (dd == null) return;
            var run = dd.FRun;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 26), "篤の組：" + (run != null ? run.MemberNames() + "（探索中）" : (dd.fPicks.Count > 0 ? "篤・" + PickNames(dd) : "篤だけ")) + "　いちばん深く行った階：" + DungeonDirector.BestFloor + "階", small); y += 32;
            if (run == null && DungeonDirector.NeedTraining)
            {
                // 事前研修（新しい学園の訓練の施設。2026-11-01 famuru）
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "ダンジョン実習の前に、森の訓練の施設で事前研修を受ける（ダンジョンの再現。魔物は幻でけがはしない・戦利品はなし）", tiny); y += 26;
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "終えるとダンジョン実習に出発できる。誘った子もいっしょに受けられる（研修を終えたことはループで戻る）", tiny); y += 28;
                string why;
                bool ok = dd.CanStartTraining(out why);
                GUI.enabled = ok;
                if (GUI.Button(new Rect(r.x + 14, y, 220, 34), "事前研修へ出発", button)) dd.StartF(true);
                GUI.enabled = true;
                if (dd.fPicks.Count > 0 && GUI.Button(new Rect(r.x + 244, y, 160, 34), "組を解散する", smallButton)) dd.fPicks.Clear();
                if (!ok) GUI.Label(new Rect(r.x + 420, y + 6, r.width - 440, 24), why, tiny);
                y += 44;
            }
            else if (run == null)
            {
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "寮生やほかの寮の学生を右クリック →「ダンジョン実習の組に入れる」で3人まで誘える。準備室から出発（8:00〜16:30）" + (LoungeWorld.NewLayout ? "　事前研修：済み" : ""), tiny); y += 28;
                string why;
                bool ok = dd.CanStartNow(out why);
                GUI.enabled = ok;
                if (GUI.Button(new Rect(r.x + 14, y, 220, 34), "ダンジョン実習へ出発", button)) dd.StartF();
                GUI.enabled = true;
                if (dd.fPicks.Count > 0 && GUI.Button(new Rect(r.x + 244, y, 160, 34), "組を解散する", smallButton)) dd.fPicks.Clear();
                if (!ok) GUI.Label(new Rect(r.x + 420, y + 6, r.width - 440, 24), why, tiny);
                y += 44;
            }
            else
            {
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), (run.training ? "事前研修：" : "今：" + run.floor + "階・") + run.room + "つ目の部屋　体力 " + Mathf.RoundToInt(Mathf.Max(0f, run.hp)) + "/" + Mathf.RoundToInt(run.maxHp) + "　銀貨 " + run.silver + "・宝 " + run.items.Count + "個", small); y += 28;
                if (GUI.Button(new Rect(r.x + 14, y, 200, 30), "ダンジョンを見る", smallButton)) { var c0 = run.members.Count > 0 ? run.members[0] : null; if (c0 != null) world.FollowInDungeon(c0); }
                y += 38;
            }
            // ほかの組
            foreach (var o in dd.runs)
            {
                if (o.withF) continue;
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), o.title + "（" + o.MemberNames() + "）：" + o.floor + "階" + (o.onStage ? "・見られる" : ""), tiny);
                y += 24;
            }
            if (run != null)
            {
                y += 6;
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "探索の記録：", small); y += 26;
                int from = Mathf.Max(0, run.log.Count - 10);
                for (int i = from; i < run.log.Count; i++) { GUI.Label(new Rect(r.x + 24, y, r.width - 48, 22), run.log[i], tiny); y += 22; }
            }
        }

        static string PickNames(DungeonDirector dd)
        {
            var n = new List<string>();
            foreach (var c in dd.fPicks) n.Add(c.displayName);
            return string.Join("・", n.ToArray());
        }

        // ================= ダンジョンの選択（画面の下） =================
        Rect DungeonChoiceRect { get { return new Rect(VW / 2f - 380f, VirtualHeight - 300f, 760f, 200f); } }

        bool DungeonChoiceShown
        {
            get
            {
                var dd = DungeonDirector.Instance;
                var run = dd != null ? dd.FRun : null;
                return run != null && (run.phase == DungeonRun.Phase.Choice || run.phase == DungeonRun.Phase.Act || run.phase == DungeonRun.Phase.Walk) && run.room > 0;
            }
        }

        void DrawDungeonChoice()
        {
            if (!DungeonChoiceShown) return;
            var run = DungeonDirector.Instance.FRun;
            Rect r = DungeonChoiceRect;
            Deco(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 28, 26), (run.training ? "事前研修（訓練の施設）　体力 " : "ダンジョン実習　" + run.floor + "階　体力 ") + Mathf.RoundToInt(Mathf.Max(0f, run.hp)) + "/" + Mathf.RoundToInt(run.maxHp) + "　銀貨 " + run.silver + "・宝 " + run.items.Count, small);
            // 体力の棒
            var old = GUI.color;
            GUI.color = new Color(0.4f, 0.85f, 0.5f);
            GUI.DrawTexture(new Rect(r.x + 14, r.y + 36, (r.width - 28) * Mathf.Clamp01(run.hp / Mathf.Max(1f, run.maxHp)), 6), Texture2D.whiteTexture);
            GUI.color = old;
            float y = r.y + 48;
            if (run.phase == DungeonRun.Phase.Choice)
            {
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 26), run.evText, small); y += 34;
                float w = (r.width - 28 - (run.choices.Length - 1) * 8) / Mathf.Max(1, run.choices.Length);
                for (int i = 0; i < run.choices.Length; i++)
                    if (GUI.Button(new Rect(r.x + 14 + i * (w + 8), y, w, 40), run.choices[i], button) && run.picked < 0) run.picked = i;
                y += 48;
            }
            else
            {
                string last = run.log.Count > 0 ? run.log[run.log.Count - 1] : "";
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 26), run.phase == DungeonRun.Phase.Walk ? "次の部屋へ向かっている…" : "（" + (run.members.Count > 0 ? run.members[0].actionText : "") + "）", small); y += 30;
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 26), last, tiny); y += 30;
            }
            if (GUI.Button(new Rect(r.xMax - 150, r.yMax - 38, 136, 30), "ダンジョンを見る", smallButton) && run.members.Count > 0) world.FollowInDungeon(run.members[0]);
        }
    }
}
