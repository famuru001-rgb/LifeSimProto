using UnityEngine;

namespace LifeSimProto
{
    /// <summary>生活のできごとで、篤がどうするかの選択肢（画面の下。2026-11-02）。選ばずにいると、できごとはそのまま進んで終わる。</summary>
    public partial class SimHud
    {
        Rect LifeChoiceRect
        {
            get
            {
                float y = VirtualHeight - (DungeonChoiceShown ? 420f : 220f);
                return new Rect(VW / 2f - 330f, y, 660f, 110f);
            }
        }

        bool LifeChoiceShown
        {
            get { var d = LifeEventDirector.Instance; return d != null && d.ChoiceRun != null; }
        }

        void DrawLifeChoice()
        {
            var d = LifeEventDirector.Instance;
            var run = d != null ? d.ChoiceRun : null;
            if (run == null) return;
            Rect r = LifeChoiceRect;
            GUI.Box(r, GUIContent.none, panel);
            string who = run.main.displayName + (run.partner != null && !run.partnerIsF ? "と" + run.partner.displayName : "");
            GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 28, 26), run.def.name + "（" + who + "）　篤はどうする？", small);
            int n = run.def.choices.Length;
            float w = (r.width - 28 - (n - 1) * 8) / Mathf.Max(1, n);
            for (int i = 0; i < n; i++)
                if (GUI.Button(new Rect(r.x + 14 + i * (w + 8), r.y + 42, w, 40), run.def.choices[i], button)) d.Choose(run, i);
            GUI.Label(new Rect(r.x + 14, r.y + 86, r.width - 28, 20), "選ばなければ、そのまま見守る", tiny);
        }
    }
}
