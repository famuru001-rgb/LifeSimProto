using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 画面の左右・下の欄の出し入れ（famuru 決定 2026-10-24：欄ごとにしまえる・画面の端へ滑る・しまった状態は次に始めても覚えておく）。
    /// 開いているときは欄の右上（下の欄は右上）の小さなボタンでしまう。しまった欄は画面の端に名前の札が残り、押すと戻る。
    /// </summary>
    public partial class SimHud
    {
        enum SlideSide { Left, Right, Bottom }

        class SlidePanel
        {
            public readonly string key, name;
            public readonly SlideSide side;
            public bool hidden;
            public float t;               // 0＝出ている　1＝しまってある（滑っている途中は間の値）
            public SlidePanel(string key, string name, SlideSide side)
            {
                this.key = key; this.name = name; this.side = side;
            }
        }

        const float SlideSeconds = 0.2f;

        readonly SlidePanel slideDetail = new SlidePanel("detail", "キャラ", SlideSide.Left);
        readonly SlidePanel slideTable = new SlidePanel("table", "関係表", SlideSide.Right);
        readonly SlidePanel slideRomance = new SlidePanel("romance", "気持ち", SlideSide.Right);
        readonly SlidePanel slideLog = new SlidePanel("log", "できごと", SlideSide.Bottom);
        SlidePanel[] slideAll;
        SlidePanel[] SlideAll { get { return slideAll ?? (slideAll = new[] { slideDetail, slideTable, slideRomance, slideLog }); } }

        // ---- しまった状態を覚える（このパソコンに残る） ----
        static bool LoadHidden(string key)
        {
            try { return PlayerPrefs.GetInt("hud_hidden_" + key, 0) == 1; } catch { return false; }
        }
        static void SaveHidden(SlidePanel p)
        {
            try { PlayerPrefs.SetInt("hud_hidden_" + p.key, p.hidden ? 1 : 0); PlayerPrefs.Save(); } catch { }
        }

        /// <summary>始めたときに、前にしまった状態を読み込む（Start から呼ぶ）。</summary>
        void LoadSlides()
        {
            foreach (var p in SlideAll) { p.hidden = LoadHidden(p.key); p.t = p.hidden ? 1f : 0f; }
        }

        void ToggleSlide(SlidePanel p)
        {
            p.hidden = !p.hidden;
            SaveHidden(p);
        }

        /// <summary>滑る動き（毎フレーム。一時停止中も動くように実時間で進める）。</summary>
        void UpdateSlides()
        {
            float step = Time.unscaledDeltaTime / SlideSeconds;
            foreach (var p in SlideAll) p.t = Mathf.MoveTowards(p.t, p.hidden ? 1f : 0f, step);
        }

        static float Ease(float t) { return t * t * (3f - 2f * t); }

        /// <summary>左の欄は、立ち絵の分も含めて画面の外へ出す。</summary>
        float LeftExtra { get { return selected != null && Portrait(selected, "stand") != null ? 308f : 0f; } }

        /// <summary>欄の位置を、しまう具合に合わせてずらす。しまい切ると画面の外に出る。</summary>
        Rect Slid(Rect r, SlidePanel p)
        {
            float k = Ease(p.t);
            if (k <= 0f) return r;
            switch (p.side)
            {
                case SlideSide.Left: r.x -= k * (r.xMax + LeftExtra + 4f); break;
                case SlideSide.Right: r.x += k * (VW - r.x + 4f); break;
                default: r.y += k * (VirtualHeight - r.y + 4f); break;
            }
            return r;
        }

        // ---- ボタンと札 ----
        /// <summary>開いている欄の「しまう」ボタン（欄の右上）。</summary>
        static Rect HideButtonRect(Rect panelRect) { return new Rect(panelRect.xMax - 42, panelRect.y + 8, 32, 28); }

        static string HideArrow(SlidePanel p) { return p.side == SlideSide.Left ? "◀" : (p.side == SlideSide.Right ? "▶" : "▼"); }
        static string ShowArrow(SlidePanel p) { return p.side == SlideSide.Left ? "▶" : (p.side == SlideSide.Right ? "◀" : "▲"); }

        /// <summary>しまった欄の札（画面の端）。左右は名前を縦書きにする。</summary>
        Rect TagRect(SlidePanel p, Rect baseRect)
        {
            if (p.side == SlideSide.Bottom) return new Rect(baseRect.x + 14, VirtualHeight - 34, 140, 30);
            float h = 34f + p.name.Length * 20f;
            if (p.side == SlideSide.Left) return new Rect(2, baseRect.y + 8, 32, h);
            return new Rect(VW - 34, baseRect.y + 8, 32, h);
        }
        string TagText(SlidePanel p)
        {
            if (p.side == SlideSide.Bottom) return ShowArrow(p) + " " + p.name;
            return ShowArrow(p) + "\n" + string.Join("\n", System.Array.ConvertAll(p.name.ToCharArray(), ch => ch.ToString()));
        }

        bool SlideShown(SlidePanel p)
        {
            if (p == slideDetail) return selected != null;
            if (p == slideRomance) return world.romance != null && world.RomanceTarget != null;
            return true;
        }

        /// <summary>しまったあと札が出ているか（滑り終わってから出す）。</summary>
        static bool TagVisible(SlidePanel p) { return p.hidden && p.t >= 1f; }

        Rect BaseRectOf(SlidePanel p)
        {
            if (p == slideDetail) return DetailPanelBase;
            if (p == slideTable) return TablePanelBase;
            if (p == slideRomance) return RomancePanelBase;
            return LogPanelBase;
        }

        /// <summary>欄の「しまう」ボタンと、しまった欄の札を描く（欄を描いたあとに呼ぶ）。</summary>
        void DrawSlideControls()
        {
            foreach (var p in SlideAll)
            {
                if (!SlideShown(p)) continue;
                Rect b = BaseRectOf(p);
                if (TagVisible(p))
                {
                    Rect tr = TagRect(p, b);
                    if (helpOpen && tr.Overlaps(HelpPopup)) continue; // 操作の欄の下に隠れるボタンは押せないようにする
                    if (GUI.Button(tr, TagText(p), smallButton)) ToggleSlide(p);
                }
                else if (!p.hidden)
                {
                    Rect hr = HideButtonRect(Slid(b, p));
                    if (helpOpen && hr.Overlaps(HelpPopup)) continue;
                    if (GUI.Button(hr, HideArrow(p), smallButton)) ToggleSlide(p);
                }
            }
        }

        /// <summary>しまった欄の札の上か（床のクリックと間違えないように）。</summary>
        bool OverSlideTags(Vector2 v)
        {
            foreach (var p in SlideAll)
                if (SlideShown(p) && TagVisible(p) && TagRect(p, BaseRectOf(p)).Contains(v)) return true;
            return false;
        }
    }
}
