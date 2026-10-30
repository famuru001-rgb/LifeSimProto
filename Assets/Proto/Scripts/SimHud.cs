using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LifeSimProto
{
    /// <summary>
    /// 画面の表示（時計・速さ・名前と状態・吹き出し・関係表・記録・購買部）と、クリックでの操作。
    /// プレイヤーはFを操作する：Fを選んで床をクリックで移動。キャラを左クリックで選ぶ（そのキャラの画面）、右クリックで選択肢（SimHud.Menu.cs：
    /// 話しかける・プレゼント・エッチに誘う、ヒロインを選んでいるときは「〇〇に会いに行って」とお願い）。
    /// 試作なので Unity の簡易表示（OnGUI）で作っている。
    /// </summary>
    public partial class SimHud : MonoBehaviour
    {
        LoungeWorld world;
        CameraRig rig;
        SimCharacter selected;
        SimCharacter lastClicked;
        float lastClickTime;
        float speedBeforePause = 1f;
        bool shopOpen, examOpen, memoryOpen, saveOpen;
        Vector2 memoryScroll;

        GUIStyle panel, label, small, tiny, title, bubble, nameTag, cell, button, buttonOn, smallButton, worldLabel, clock, chip;
        Texture2D panelTex, bubbleTex, barBg;

        const float VirtualHeight = 1080f;
        float Scale { get { return Mathf.Max(0.55f, Screen.height / VirtualHeight); } }
        float VW { get { return Screen.width / Scale; } }

        Rect TopBar { get { return new Rect(12, 12, Mathf.Max(1900f, VW - 24f), 50); } }
        /// <summary>キャラの上に名前と行動・状態の札を出すか（最初は出さない。上のボタン・N キー。選んだキャラはいつも出す）。</summary>
        public static bool ShowNameTags = false;
        Rect MemoryPanel { get { return new Rect(VW / 2f - 470f, 120f, 940f, 820f); } }
        Rect ChoicePanel { get { return new Rect(VW / 2f - 300f, 380f, 600f, 170f); } }
        Rect ExamPanel { get { return new Rect(VW / 2f - 470f, 120f, 940f, 96f + Academics.ActiveCount * 30f + 70f); } }
        Rect InfoLine { get { return new Rect(12, 66, 1400, 32); } }
        // 左右・下の欄（しまうと画面の端へ滑る。SimHud.Slide.cs）。…Base はしまう前の位置
        Rect TablePanelBase { get { return new Rect(VW - 492, 108, 480, 400); } }
        Rect LogPanelBase { get { return new Rect(VW - 1260, VirtualHeight - 272, 760, 260); } }
        // 操作の説明：右上の歯車を押したときだけ、歯車の下に出す（famuru 決定 2026-10-24）
        bool helpOpen;
        Rect GearRect { get { return new Rect(VW - 12 - 46, 68, 46, 36); } }
        Rect HelpPopup { get { return new Rect(VW - 12 - 480, GearRect.yMax + 4, 480, 310); } }
        // 見る階のボタン（1階・2階・3階・屋上）：歯車の左に並べる（2026-10-25 寮を3階建てにした）
        static readonly string[] FloorButtonNames = { "1階", "2階", "3階", "屋上" };
        Rect FloorButtonsRect { get { return new Rect(GearRect.x - 8 - FloorButtonNames.Length * 62, GearRect.y, FloorButtonNames.Length * 62, GearRect.height); } }
        Rect DetailPanelBase { get { return new Rect(12, 108, 580, 960); } }
        Rect RomancePanelBase { get { return new Rect(VW - 492, 516, 480, 292); } }
        Rect TablePanel { get { return Slid(TablePanelBase, slideTable); } }
        Rect LogPanel { get { return Slid(LogPanelBase, slideLog); } }
        Rect DetailPanel { get { return Slid(DetailPanelBase, slideDetail); } }
        Rect RomancePanel { get { return Slid(RomancePanelBase, slideRomance); } }
        Rect ShopPanel { get { return new Rect(VW / 2f - 430f, 110f, 860f, 110f + ShopRowsMax * 44f); } }
        ItemShelf shopShelf = ItemShelf.Basic;
        static readonly string[] ShelfNames = { "食べ物・薬", "欲求の品", "プレゼント", "装飾品", "能力の装飾", "お守り", "寮生の私物", "家具" };
        static int ShopRowsMax
        {
            get
            {
                var n = new int[ShelfNames.Length];
                int m = 1;
                foreach (var d in ItemBook.All) { int i = (int)d.shelf; if (i < n.Length) { n[i]++; m = Mathf.Max(m, n[i]); } }
                return m;
            }
        }

        PlayerControl Player { get { return world != null ? world.player : null; } }
        SimCharacter F { get { return world != null ? world.RomanceTarget : null; } }

        public static SimHud Instance { get; private set; }

        void Start()
        {
            world = GetComponent<LoungeWorld>();
            Instance = this;
            LoadSlides();
        }

        void SetSpeed(float s)
        {
            if (s > 0f) speedBeforePause = s;
            world.SetUserSpeed(s);
        }

        // ================= 顔アイコン・立ち絵（Resources/Portraits/（記号）_face, _stand） =================
        readonly Dictionary<string, Texture2D> portraits = new Dictionary<string, Texture2D>();
        Texture2D Portrait(SimCharacter c, string kind)
        {
            string key = (string.IsNullOrEmpty(c.letter) ? c.displayName : c.letter) + "_" + kind;
            Texture2D t;
            if (!portraits.TryGetValue(key, out t)) { t = Resources.Load<Texture2D>("Portraits/" + key); portraits[key] = t; }
            return t;
        }
        Rect StandRect { get { return new Rect(DetailPanel.xMax + 8, 108, 300, 600); } }

        bool OverHud(Vector2 v)
        {
            if (OverSlideTags(v)) return true;
            if (TopBar.Contains(v) || TablePanel.Contains(v) || LogPanel.Contains(v) || RomancePanel.Contains(v)) return true;
            if (GearRect.Contains(v) || FloorButtonsRect.Contains(v) || (helpOpen && HelpPopup.Contains(v))) return true;
            if (selected != null && DetailPanel.Contains(v)) return true;
            if (selected != null && Portrait(selected, "stand") != null && StandRect.Contains(v)) return true;
            if (shopOpen && ShopPanel.Contains(v)) return true;
            if (examOpen && ExamPanel.Contains(v)) return true;
            if (memoryOpen && MemoryPanel.Contains(v)) return true;
            if (Loop.AskChoice && ChoicePanel.Contains(v)) return true;
            if (saveOpen && SavePanel.Contains(v)) return true;
            if (Story.IsActive) return true; // 物語の場面のあいだは、寮の中をクリックしない
            if (MenuOpen && MenuRect.Contains(v)) return true; // 右クリックの選択肢
            return false;
        }

        /// <summary>マウスが画面の欄の上にあるか（カメラのホイール操作を止めるのに使う）。</summary>
        public bool PointerOverHud()
        {
            var mouse = Mouse.current;
            if (mouse == null) return false;
            Vector2 mp = mouse.position.ReadValue();
            return !Story.IsActive && OverHud(new Vector2(mp.x / Scale, (Screen.height - mp.y) / Scale));
        }

        void Update()
        {
            if (world == null || world.mainCamera == null) return;
            if (rig == null) rig = world.mainCamera.GetComponent<CameraRig>();
            if (selected != null && !selected.Present) Select(null);
            UpdateSlides();

            if (world != null) foreach (var ch in world.residents) NeedRates(ch, NeedValues(ch)); // 欲求の変化を測っておく
            var kb = Keyboard.current;
            // 物語の場面のあいだは、スペースかエンターで次へ（ほかの操作はしない）
            if (Story.IsActive)
            {
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)) Story.Next(world);
                return;
            }
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame) SetSpeed(world.userSpeed > 0f ? 0f : speedBeforePause);
                var opts = world.speedOptions;
                if (opts != null)
                {
                    if (kb.digit1Key.wasPressedThisFrame && opts.Length > 0) SetSpeed(opts[0]);
                    if (kb.digit2Key.wasPressedThisFrame && opts.Length > 1) SetSpeed(opts[1]);
                    if (kb.digit3Key.wasPressedThisFrame && opts.Length > 2) SetSpeed(opts[2]);
                    if (kb.digit4Key.wasPressedThisFrame && opts.Length > 3) SetSpeed(opts[3]);
                }
                var dbg = world.debugSpeedOptions;
                if (dbg != null)
                {
                    if (kb.digit5Key.wasPressedThisFrame && dbg.Length > 0) SetSpeed(dbg[0]);
                    if (kb.digit6Key.wasPressedThisFrame && dbg.Length > 1) SetSpeed(dbg[1]);
                }
                if (kb.escapeKey.wasPressedThisFrame) { if (MenuOpen) CloseMenu(); else if (helpOpen) helpOpen = false; else if (saveOpen) saveOpen = false; else if (shopOpen) shopOpen = false; else if (examOpen) examOpen = false; else if (memoryOpen) memoryOpen = false; else Select(null); }
                if (kb.lKey.wasPressedThisFrame) { if (memoryOpen && memoryTab == MemoryTabH) memoryOpen = false; else { memoryOpen = true; memoryTab = MemoryTabH; hLoopView = Loop.Count; } }
                if (kb.pKey.wasPressedThisFrame) { saveOpen = !saveOpen; loadConfirm = -1; }
                if (kb.gKey.wasPressedThisFrame) examOpen = !examOpen;
                if (kb.mKey.wasPressedThisFrame) memoryOpen = !memoryOpen;
                if (kb.nKey.wasPressedThisFrame) ShowNameTags = !ShowNameTags;
                if (kb.tabKey.wasPressedThisFrame) CycleWallMode();
                if (kb.bKey.wasPressedThisFrame) shopOpen = !shopOpen;
            }

            var mouse = Mouse.current;
            UpdateRightClick(mouse); // 右クリック：選択肢（SimHud.Menu.cs）
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            Vector2 mp = mouse.position.ReadValue();
            Vector2 v = new Vector2(mp.x / Scale, (Screen.height - mp.y) / Scale);
            if (OverHud(v)) return;
            if (MenuOpen) { CloseMenu(); return; } // 選択肢の外をクリックしたら閉じるだけ

            SimCharacter hit = null;
            Ray ray = world.mainCamera.ScreenPointToRay(new Vector3(mp.x, mp.y, 0f));
            RaycastHit info;
            if (Physics.Raycast(ray, out info, 300f)) hit = info.collider.GetComponentInParent<SimCharacter>();

            float now = Time.unscaledTime;
            if (hit == null)
            {
                // 何もない所を左クリック：選ぶのをやめる（篤の移動は右クリック。famuru 2026-10-25）
                Select(null);
                return;
            }

            // 左クリックはキャラを選ぶだけ（そのキャラの画面が出る）。話しかける・お願いは右クリックの選択肢（famuru 2026-10-25）
            bool dbl = hit == lastClicked && now - lastClickTime < 0.35f;
            lastClicked = hit;
            lastClickTime = now;
            if (dbl && rig != null) rig.follow = hit.transform;
            Select(hit);
        }

        /// <summary>画面のクリック位置から、歩ける床の点を求める（2階を優先）。</summary>
        bool FloorPoint(Ray ray, out Vector3 p)
        {
            p = Vector3.zero;
            if (Mathf.Abs(ray.direction.y) < 0.0001f) return false;
            // 見えている階のうち、上の階から順に（隠れている階の床はクリックできない）
            int top = Mathf.Clamp(world.ViewFloor < 0 ? 3 : world.ViewFloor, 0, 3);
            for (int f = top; f >= 0; f--)
            {
                var nav = world.Nav(f);
                if (nav == null) continue;
                float t = (LoungeWorld.FloorY(f) - ray.origin.y) / ray.direction.y;
                if (t <= 0f) continue;
                Vector3 q = ray.origin + ray.direction * t;
                if (nav.IsFree(q)) { p = q; return true; }
                bool inDorm = q.x > -17f && q.x < 17f && q.z > -24f && q.z < 18f;
                bool inSchool = q.x > -27f && q.x < 44f && q.z > -130f && q.z < -24f; // 学校の本館（SchoolBuilder.cs。1.5倍）
                if (f == 0 && (inDorm || inSchool)) { p = nav.NearestFreePoint(q); return true; }
            }
            return false;
        }

        void Select(SimCharacter c)
        {
            if (c != selected) detailTopScroll = Vector2.zero; // 別のキャラを選んだら、左の欄の上（基本と行動）はいちばん上から
            if (selected != null) selected.selected = false;
            selected = c;
            if (selected != null) selected.selected = true;
        }

        void CycleWallMode()
        {
            world.wallMode = world.wallMode == WallMode.Auto ? WallMode.Low : (world.wallMode == WallMode.Low ? WallMode.All : WallMode.Auto);
        }

        static string WallModeName(WallMode m)
        {
            return m == WallMode.Auto ? "壁：自動" : (m == WallMode.Low ? "壁：低く" : "壁：全部");
        }

        // ================= 表示 =================
        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void InitStyles()
        {
            if (panel != null) return;
            panelTex = Solid(new Color(0.08f, 0.06f, 0.05f, 0.82f));
            bubbleTex = Solid(new Color(1f, 0.98f, 0.93f, 0.95f));
            barBg = Solid(new Color(1f, 1f, 1f, 0.15f));

            panel = new GUIStyle(GUI.skin.box);
            panel.normal.background = panelTex;
            panel.padding = new RectOffset(12, 12, 10, 10);

            label = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = false };
            label.normal.textColor = new Color(0.95f, 0.92f, 0.85f);
            clock = new GUIStyle(label) { fontSize = 22, fontStyle = FontStyle.Bold };
            clock.normal.textColor = new Color(1f, 0.88f, 0.6f);
            small = new GUIStyle(label) { fontSize = 16 };
            tiny = new GUIStyle(label) { fontSize = 14 };
            tiny.normal.textColor = new Color(0.85f, 0.82f, 0.75f);
            title = new GUIStyle(label) { fontSize = 22, fontStyle = FontStyle.Bold };
            title.normal.textColor = new Color(1f, 0.85f, 0.55f);
            cell = new GUIStyle(label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            cell.normal.textColor = Color.white;

            bubble = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            bubble.normal.background = bubbleTex;
            bubble.normal.textColor = new Color(0.1f, 0.08f, 0.06f);
            bubble.padding = new RectOffset(10, 10, 6, 6);

            nameTag = new GUIStyle(GUI.skin.box) { fontSize = 17, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            nameTag.normal.background = panelTex;
            nameTag.normal.textColor = Color.white;
            nameTag.padding = new RectOffset(8, 8, 3, 3);

            chip = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            chip.normal.textColor = new Color(0.08f, 0.06f, 0.05f);
            chip.padding = new RectOffset(6, 6, 1, 1);

            worldLabel = new GUIStyle(label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            worldLabel.normal.textColor = new Color(0.9f, 0.85f, 0.72f, 0.8f);

            button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            buttonOn = new GUIStyle(button);
            buttonOn.normal.textColor = new Color(1f, 0.85f, 0.3f);
            buttonOn.fontStyle = FontStyle.Bold;
            smallButton = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        }

        void OnGUI()
        {
            long tp = PerfProbe.Stamp();   // 重さを測る（PerfProbe.cs。2026-10-30 軽量化のチャット）
            try { OnGUICore(); } finally { PerfProbe.Add(PerfProbe.Hud, tp); }
        }

        void OnGUICore()
        {
            if (world == null || world.mainCamera == null || world.rel == null) return;
            InitStyles();
            float s = Scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, 1f));

            DrawWorldLabels(s);
            DrawCharacters(s);
            DrawTopBar();
            DrawInfoLine();
            DrawTable();
            DrawRomance();
            DrawLog();
            if (selected != null)
            {
                DrawDetail();
                var st = Portrait(selected, "stand");
                if (st != null) GUI.DrawTexture(StandRect, st, ScaleMode.ScaleToFit, true);
            }
            DrawSlideControls(); // 左右・下の欄の「しまう」ボタンと札（真ん中に開く画面より下に描く）
            DrawGear();
            DrawFloorButtons();
            if (helpOpen) DrawHelp();
            if (shopOpen) DrawShop();
            if (examOpen) DrawExam();
            if (memoryOpen) DrawMemory();
            if (Loop.AskChoice) DrawChoice();
            if (saveOpen) DrawSave();
            DrawMenu(); // 右クリックの選択肢
            if (Story.IsActive) DrawStory();
        }

        bool ToGui(Vector3 world3, float s, out Vector2 g)
        {
            Vector3 sp = world.mainCamera.WorldToScreenPoint(world3);
            g = new Vector2(sp.x / s, (Screen.height - sp.y) / s);
            return sp.z > 0f;
        }

        void DrawWorldLabels(float s)
        {
            Vector3 cam = world.mainCamera.transform.position;
            foreach (var kv in world.worldLabels)
            {
                if ((kv.Key - cam).sqrMagnitude > 40f * 40f) continue;
                if (world.ViewFloor >= 0 && Mathf.FloorToInt((kv.Key.y - 0.2f) / LoungeWorld.FloorH) > world.ViewFloor) continue; // 隠れている階の札は出さない
                Vector2 g;
                if (!ToGui(kv.Key, s, out g)) continue;
                GUI.Label(new Rect(g.x - 150, g.y - 12, 300, 24), kv.Value, worldLabel);
            }
        }

        /// <summary>状態を小さな札（色つき）で並べて描く。</summary>
        float DrawChips(float x, float y, List<KeyValuePair<string, Color>> tags, float h = 22f)
        {
            Color old = GUI.color;
            foreach (var t in tags)
            {
                Vector2 sz = chip.CalcSize(new GUIContent(t.Key));
                Rect r = new Rect(x, y, sz.x, h);
                GUI.color = t.Value;
                GUI.DrawTexture(r, ProtoArt.White);
                GUI.color = old;
                GUI.Label(r, t.Key, chip);
                x += sz.x + 4;
            }
            return x;
        }

        void DrawCharacters(float s)
        {
            var list = new List<SimCharacter>(world.characters);
            Vector3 cam = world.mainCamera.transform.position;
            list.Sort((a, b) => (b.transform.position - cam).sqrMagnitude.CompareTo((a.transform.position - cam).sqrMagnitude));
            foreach (var c in list)
            {
                if (!c.Present || !world.FloorVisible(c.transform.position.y)) continue;
                Vector2 g;
                if (!ToGui(c.transform.position + Vector3.up * c.HeadHeight, s, out g)) continue;

                Rect r;
                Color old = GUI.color;
                if ((!ShowNameTags || c.IsMob) && !c.selected) // 寮生以外の学生・先生の名前は、選んだときだけ
                {
                    // 名前と行動を出さない（選んだキャラだけ出す）。吹き出しの位置だけ決める
                    r = new Rect(g.x, g.y - 24f, 0f, 24f);
                    goto Bubble;
                }
                string tag = c.displayName + (c.IsTarget ? "（あなた）" : "") + "  " + c.actionText;
                Vector2 size = nameTag.CalcSize(new GUIContent(tag));
                r = new Rect(g.x - size.x / 2f, g.y - size.y, size.x, size.y);
                GUI.Box(r, tag, nameTag);
                var face = Portrait(c, "face");
                if (face != null) GUI.DrawTexture(new Rect(r.x - r.height - 2, r.y, r.height, r.height), face, ScaleMode.ScaleToFit, true);
                GUI.color = c.color;
                GUI.DrawTexture(new Rect(r.x, r.y, 6, r.height), ProtoArt.White);
                if (c.selected)
                {
                    GUI.color = new Color(1f, 0.85f, 0.2f);
                    GUI.DrawTexture(new Rect(r.x, r.yMax - 3, r.width, 3), ProtoArt.White);
                }
                GUI.color = old;

                // 状態異常の札（名前の右）
                var tags = c.StatusTagList();
                if (!c.IsTarget && Story.Pending.Contains(c.letter)) tags.Insert(0, new KeyValuePair<string, Color>("物語", new Color(1f, 0.85f, 0.4f)));
                if (tags.Count > 0) DrawChips(r.xMax + 4, r.y + 1, tags, r.height - 2);
            Bubble:

                if (!string.IsNullOrEmpty(c.bubble))
                {
                    Vector2 bs = bubble.CalcSize(new GUIContent(c.bubble));
                    Rect br = new Rect(g.x - bs.x / 2f, r.y - bs.y - 6, bs.x, bs.y);
                    GUI.Box(br, c.bubble, bubble);
                    GUI.color = new Color(1f, 0.98f, 0.93f, 0.95f);
                    GUI.DrawTexture(new Rect(g.x - 4, br.yMax, 8, 6), ProtoArt.White);
                    GUI.color = old;
                }
            }
        }

        void DrawTopBar()
        {
            Rect r = TopBar;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 10, 260, 30), world.Day + "日目 " + ScheduleMaker.Clock(world.Now) + "（" + world.TimeOfDayName() + "）", clock);
            float x = r.x + 280;
            float cur = world.userSpeed;
            if (GUI.Button(new Rect(x, r.y + 8, 110, 34), "一時停止", cur <= 0f ? buttonOn : button)) SetSpeed(0f);
            x += 118;
            foreach (var sp in world.speedOptions)
            {
                bool on = Mathf.Approximately(cur, sp);
                if (GUI.Button(new Rect(x, r.y + 8, 62, 34), sp + "倍", on ? buttonOn : button)) SetSpeed(sp);
                x += 68;
            }
            if (world.debugSpeedOptions != null)
            {
                var oldc = GUI.color;
                foreach (var sp in world.debugSpeedOptions)
                {
                    bool on = Mathf.Approximately(cur, sp);
                    GUI.color = new Color(1f, 0.75f, 0.6f); // デバッグ用は色を変える
                    if (GUI.Button(new Rect(x, r.y + 8, 62, 34), sp + "倍", on ? buttonOn : button)) SetSpeed(sp);
                    x += 68;
                }
                GUI.color = oldc;
            }
            x += 16;
            if (GUI.Button(new Rect(x, r.y + 8, 130, 34), WallModeName(world.wallMode), button)) CycleWallMode();
            x += 138;
            if (GUI.Button(new Rect(x, r.y + 8, 104, 34), ShowNameTags ? "名札：あり" : "名札：なし", ShowNameTags ? buttonOn : button)) ShowNameTags = !ShowNameTags;
            x += 108;
            x += 12; // 見る階のボタンは右上（歯車の左。DrawFloorButtons）
            if (Player != null)
            {
                GUI.Label(new Rect(x, r.y + 11, 130, 30), "銀貨 " + Player.money, clock);
                x += 130;
                if (GUI.Button(new Rect(x, r.y + 8, 100, 34), "購買部", shopOpen ? buttonOn : button)) shopOpen = !shopOpen;
                x += 108;
                if (F != null && GUI.Button(new Rect(x, r.y + 8, 96, 34), "篤を選ぶ", selected == F ? buttonOn : button)) Select(F);
                x += 100;
                if (GUI.Button(new Rect(x, r.y + 8, 80, 34), "成績", examOpen ? buttonOn : button)) examOpen = !examOpen;
                x += 88;
                if (GUI.Button(new Rect(x, r.y + 8, 90, 34), "思い出", memoryOpen && memoryTab != MemoryTabH ? buttonOn : button)) { if (memoryOpen && memoryTab != MemoryTabH) memoryOpen = false; else { memoryOpen = true; if (memoryTab == MemoryTabH) memoryTab = 0; } }
                x += 96;
                if (GUI.Button(new Rect(x, r.y + 8, 80, 34), "記録", memoryOpen && memoryTab == MemoryTabH ? buttonOn : button)) { if (memoryOpen && memoryTab == MemoryTabH) memoryOpen = false; else { memoryOpen = true; memoryTab = MemoryTabH; hLoopView = Loop.Count; } }
                x += 86;
                if (GUI.Button(new Rect(x, r.y + 8, 90, 34), "セーブ", saveOpen ? buttonOn : button)) { saveOpen = !saveOpen; loadConfirm = -1; }
            }
        }

        void DrawInfoLine()
        {
            Rect r = InfoLine;
            var parts = new List<string>();
            parts.Add(Calendar.DateText(world.Day, world.holidayWeekdays));
            if (world.todayNotes.Count > 0) parts.Add("今日：" + string.Join("／", world.todayNotes.ToArray()));
            int withA = 0, doneA = 0;
            foreach (var c in world.residents) if (c.hasAssignment) { withA++; if (c.assignProgress >= 100f) doneA++; }
            if (withA > 0) parts.Add("課題：" + doneA + "/" + withA + "人が終了");
            parts.Add("寮の評判 " + world.dormReputation);
            if (world.Blackout) parts.Add("停電中");
            if (world.SnackBonus) parts.Add("差し入れあり");
            if (world.autoFast) parts.Add("全員就寝中：" + world.allAsleepSpeed + "倍速");
            var away = new List<string>();
            var priv = new List<string>();
            foreach (var c in world.characters)
            {
                if (c.state == SimCharacter.State.Away) { if (!c.IsMob) away.Add(c.displayName); }
                else if (c.state == SimCharacter.State.Private) priv.Add(c.displayName);
            }
            if (away.Count > 0) parts.Add("外出中：" + string.Join("・", away.ToArray()));
            if (priv.Count > 0) parts.Add("エッチしている：" + string.Join("・", priv.ToArray()));
            string text = string.Join("　｜　", parts.ToArray());
            Vector2 size = small.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(r.x, r.y, size.x + 28, r.height), GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 5, size.x + 10, 24), text, small);
        }

        // 関係表と気持ちの欄は、課程ごとに見出しで切り替える（2026-10-30 ヒロインが34人になったため）
        static readonly string[] GroupTabs = { "冒険者", "騎士", "魔法師", "先生", "全員" };
        int groupTab;

        /// <summary>見出しのヒロイン（篤は withF のとき先頭）。</summary>
        List<SimCharacter> GroupMembers(int g, bool withF)
        {
            var l = new List<SimCharacter>();
            foreach (var c in world.residents)
            {
                if (c.IsTarget) { if (withF) l.Insert(0, c); continue; }
                string course = c.profile != null ? (c.profile.teacher ? "先生" : c.profile.course) : "冒険者";
                if (g >= GroupTabs.Length - 1 || course == GroupTabs[g]) l.Add(c);
            }
            return l;
        }

        void DrawGroupTabs(Rect r)
        {
            float tw = 62f;
            float tx = r.xMax - 12 - GroupTabs.Length * tw;
            for (int i = 0; i < GroupTabs.Length; i++)
                if (GUI.Button(new Rect(tx + i * tw, r.y + 8, tw - 3, 26), GroupTabs[i], groupTab == i ? buttonOn : smallButton)) groupTab = i;
        }

        void DrawTable()
        {
            Rect r = TablePanel;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 8, 180, 30), "関係表（好感度）", title);
            DrawGroupTabs(r);
            var members = GroupMembers(groupTab, true);
            int n = members.Count;
            float cw = Mathf.Min(n <= 6 ? 62 : 42, (r.width - 32f) / (n + 1)), ch = Mathf.Min(n <= 6 ? 38 : 28, (r.height - 46f - 40f) / (n + 1));
            bool tinyCells = cw < 26f || ch < 16f;
            float ox = r.x + 16, oy = r.y + 46;
            Color old = GUI.color;
            for (int i = 0; i < n; i++)
            {
                var ci = members[i];
                GUI.color = ci.color;
                GUI.DrawTexture(new Rect(ox + cw * (i + 1) + 1, oy + 1, cw - 2, Mathf.Min(4f, ch * 0.3f)), ProtoArt.White);
                GUI.DrawTexture(new Rect(ox + 1, oy + ch * (i + 1) + 1, Mathf.Min(4f, cw * 0.3f), ch - 2), ProtoArt.White);
                GUI.color = old;
                if (tinyCells) continue;
                var fi = Portrait(ci, "face");
                if (fi != null)
                {
                    GUI.DrawTexture(new Rect(ox + cw * (i + 1) + 2, oy + 2, cw - 4, ch - 4), fi, ScaleMode.ScaleToFit, true);
                    GUI.DrawTexture(new Rect(ox + 2, oy + ch * (i + 1) + 2, cw - 4, ch - 4), fi, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    string sn = string.IsNullOrEmpty(ci.shortName) ? ci.displayName : ci.shortName;
                    GUI.Label(new Rect(ox + cw * (i + 1), oy, cw, ch), sn, cell);
                    GUI.Label(new Rect(ox, oy + ch * (i + 1), cw, ch), sn, cell);
                }
            }
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float pad = tinyCells ? 0.5f : 2f;
                    Rect cr = new Rect(ox + cw * (j + 1) + pad, oy + ch * (i + 1) + pad, cw - pad * 2f, ch - pad * 2f);
                    if (i == j)
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.06f);
                        GUI.DrawTexture(cr, ProtoArt.White);
                        GUI.color = old;
                        continue;
                    }
                    int a = members[i].id, b = members[j].id;
                    float v = world.rel.Get(a, b);
                    int stage = Relationships.StageOf(v);
                    Color bg = stage == 2 ? new Color(0.75f, 0.3f, 0.5f) : (stage == 1 ? new Color(0.25f, 0.52f, 0.3f) : new Color(0.32f, 0.32f, 0.34f));
                    float since = world.simTime - world.rel.LastChange(a, b);
                    if (since < 2f) bg = Color.Lerp(new Color(1f, 0.85f, 0.2f), bg, since / 2f);
                    GUI.color = bg;
                    GUI.DrawTexture(cr, ProtoArt.White);
                    GUI.color = old;
                    if (!tinyCells) GUI.Label(cr, Mathf.RoundToInt(v).ToString(), cell);
                }
            }
            float ly = oy + ch * (n + 1) + 10;
            DrawLegend(new Rect(r.x + 16, ly, 140, 24), new Color(0.32f, 0.32f, 0.34f), "知り合い 0〜39");
            DrawLegend(new Rect(r.x + 170, ly, 140, 24), new Color(0.25f, 0.52f, 0.3f), "友達 40〜79");
            DrawLegend(new Rect(r.x + 312, ly, 150, 24), new Color(0.75f, 0.3f, 0.5f), "親友 80〜100");
        }

        void DrawRomance()
        {
            var rom = world.romance;
            var f = world.RomanceTarget;
            if (rom == null || f == null) return;
            Rect r = RomancePanel;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 8, 180, 30), f.displayName + "への気持ち", title);
            DrawGroupTabs(r);
            var members = GroupMembers(groupTab, false);
            float rowH = Mathf.Clamp((r.height - 52f) / Mathf.Max(1, members.Count), 8f, 26f);
            bool compact = rowH < 20f;
            float y = r.y + 44;
            Color old = GUI.color;
            foreach (var c in members)
            {
                int st = rom.Stage(c.id);
                float v = rom.love[c.id];
                GUI.color = c.color;
                GUI.DrawTexture(new Rect(r.x + 14, y + 2, 4, rowH - 4), ProtoArt.White);
                GUI.color = old;
                var rf = compact ? null : Portrait(c, "face");
                if (rf != null) GUI.DrawTexture(new Rect(r.x + 22, y, rowH, rowH), rf, ScaleMode.ScaleToFit, true);
                else GUI.Label(new Rect(r.x + 22, y - (compact ? 3 : 0), 60, rowH + 6), string.IsNullOrEmpty(c.shortName) ? c.displayName : c.shortName, tiny);
                Rect bar = new Rect(r.x + 84, y + rowH * 0.25f, 216, Mathf.Max(3f, rowH * 0.5f));
                GUI.DrawTexture(bar, barBg);
                Color bc = st == 3 ? new Color(1f, 0.35f, 0.55f) : (st == 2 ? new Color(0.95f, 0.5f, 0.65f) : (st == 1 ? new Color(0.85f, 0.65f, 0.75f) : new Color(0.55f, 0.55f, 0.6f)));
                float since = world.simTime - rom.flashTime[c.id];
                if (since < 2f) bc = Color.Lerp(new Color(1f, 0.85f, 0.2f), bc, since / 2f);
                GUI.color = bc;
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(v / 100f), bar.height), ProtoArt.White);
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                GUI.DrawTexture(new Rect(bar.x + bar.width * Romance.ConfessAt / 100f, bar.y - 2, 2, bar.height + 4), ProtoArt.White);
                GUI.color = old;
                string lab = (st == 3 ? "♥ " : "") + Romance.StageName(st) + "（" + Mathf.RoundToInt(v) + "）";
                if (st < 3 && world.Now < rom.cooldownUntil[c.id]) lab += " 告白後";
                GUI.Label(new Rect(r.x + 310, y - (compact ? 3 : 0), 160, rowH + 6), lab, compact ? tiny : small);
                y += rowH;
            }
        }

        void DrawLegend(Rect r, Color c, string text)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(r.x, r.y + 4, 16, 16), ProtoArt.White);
            GUI.color = old;
            GUI.Label(new Rect(r.x + 22, r.y, r.width, r.height), text, small);
        }

        // できごとの欄：見出しで種類を分ける（GameLog.cs）。ホイールでさかのぼれる。いちばん下を見ているあいだは新しい行についていく
        int logTab;
        string logHeroine;        // 「ヒロイン」の見出しで1人に絞っているとき、その子の記号（null は全員）
        Vector2 logScroll;
        bool logAtBottom = true;
        readonly List<LogEntry> logShown = new List<LogEntry>();

        void DrawLog()
        {
            Rect r = LogPanel;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 8, 140, 30), "できごと", title);

            // 見出し（右上に並べる）
            var tabs = GameLog.Tabs;
            float tw = 104f;
            float tx = r.xMax - 52 - tabs.Length * tw; // 右上は「しまう」ボタン
            for (int i = 0; i < tabs.Length; i++)
                if (GUI.Button(new Rect(tx + i * tw, r.y + 8, tw - 4, 30), tabs[i].name, logTab == i ? buttonOn : smallButton) && logTab != i)
                { logTab = i; logHeroine = null; logAtBottom = true; }
            float y = r.y + 44;

            // 「ヒロイン」の見出しでは、1人ずつ絞るボタン
            if (logTab == GameLog.HeroineTab)
            {
                var girls = new List<SimCharacter>();
                foreach (var c in world.residents) if (c != null && c != F && !string.IsNullOrEmpty(c.letter)) girls.Add(c);
                // 34人になったので、1行12人までで折り返す（2026-10-30）
                int perRow = 12;
                float bw = (r.width - 28f) / (perRow + 1);
                if (GUI.Button(new Rect(r.x + 14, y, bw - 3, 24), "全員", logHeroine == null ? buttonOn : smallButton) && logHeroine != null) { logHeroine = null; logAtBottom = true; }
                for (int i = 0; i < girls.Count; i++)
                {
                    var g = girls[i];
                    string nm = string.IsNullOrEmpty(g.shortName) ? g.displayName : g.shortName;
                    float bx = r.x + 14 + (i % perRow + 1) * bw, by = y + (i / perRow) * 26;
                    if (GUI.Button(new Rect(bx, by, bw - 3, 24), nm, logHeroine == g.letter ? buttonOn : smallButton) && logHeroine != g.letter)
                    { logHeroine = g.letter; logAtBottom = true; }
                }
                y += 26 * ((girls.Count + perRow - 1) / perRow) + 4;
            }

            // 見出しに合う行を集める
            logShown.Clear();
            var show = tabs[Mathf.Clamp(logTab, 0, tabs.Length - 1)].show;
            foreach (var e in world.log)
                if (show(e) && (logTab != GameLog.HeroineTab || logHeroine == null || e.About(logHeroine))) logShown.Add(e);

            const float lineH = 23f;
            var view = new Rect(r.x + 8, y, r.width - 16, r.yMax - 8 - y);
            float contentH = Mathf.Max(view.height, logShown.Count * lineH + 4f);
            var content = new Rect(0, 0, view.width - 20, contentH);
            float bottom = contentH - view.height;
            if (logAtBottom) logScroll.y = bottom; // 新しい行が来たら下までついていく
            logScroll = GUI.BeginScrollView(view, logScroll, content);
            if (logShown.Count == 0)
                GUI.Label(new Rect(6, 2, content.width - 6, 24), "まだ、この種類のできごとはありません", tiny);
            Color old = small.normal.textColor;
            float top = logScroll.y - lineH, end = logScroll.y + view.height;
            for (int i = 0; i < logShown.Count; i++)
            {
                float ly = 2f + i * lineH;
                if (ly < top || ly > end) continue; // 見えている行だけ描く
                small.normal.textColor = GameLog.ColorOf(logShown[i]);
                GUI.Label(new Rect(6, ly, content.width - 6, 24), logShown[i].text, small);
            }
            small.normal.textColor = old;
            GUI.EndScrollView();
            logAtBottom = logScroll.y >= bottom - 2f;
        }

        // 歯車の絵（文字の「⚙」は字体によって出ないので、絵を作って描く）
        Texture2D gearTex;
        Texture2D GearTexture()
        {
            if (gearTex != null) return gearTex;
            const int N = 64;
            gearTex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            float c = (N - 1) / 2f;
            for (int py = 0; py < N; py++)
                for (int px = 0; px < N; px++)
                {
                    float dx = px - c, dy = py - c;
                    float rr = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float outer = Mathf.Cos(8f * ang) > 0.25f ? 30f : 23f; // 歯が8つ
                    float a = Mathf.Clamp01(outer - rr + 0.5f) * Mathf.Clamp01(rr - 9f + 0.5f); // 真ん中は穴
                    gearTex.SetPixel(px, py, new Color(1f, 1f, 1f, a));
                }
            gearTex.Apply();
            return gearTex;
        }

        /// <summary>見る階のボタン。押すとその階を見る（上の階は隠れる）。今見ている階は色を変える。</summary>
        void DrawFloorButtons()
        {
            Rect r = FloorButtonsRect;
            for (int i = 0; i < FloorButtonNames.Length; i++)
            {
                bool on = world.ViewFloor == i;
                if (GUI.Button(new Rect(r.x + i * 62, r.y, 58, r.height), FloorButtonNames[i], on ? buttonOn : smallButton) && rig != null) rig.LookAtFloor(i);
            }
        }

        void DrawGear()
        {
            Rect g = GearRect;
            if (GUI.Button(g, GUIContent.none, helpOpen ? buttonOn : smallButton)) helpOpen = !helpOpen;
            Color old = GUI.color;
            GUI.color = helpOpen ? new Color(1f, 0.85f, 0.3f) : new Color(0.95f, 0.92f, 0.85f);
            float sz = g.height - 10f;
            GUI.DrawTexture(new Rect(g.center.x - sz / 2f, g.center.y - sz / 2f, sz, sz), GearTexture(), ScaleMode.ScaleToFit, true);
            GUI.color = old;
        }

        void DrawHelp()
        {
            Rect r = HelpPopup;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 8, 300, 30), "操作", title);
            string[] lines =
            {
                "右ドラッグ：視点を回す　ホイール：近づく/離れる",
                "WASD：移動　Q/E：回す　R/F：上下　中ドラッグ：平行移動",
                "キャラをクリック：選ぶ（何もない所で選ぶのをやめる）",
                "床を右クリック：篤が移動　キャラを右クリック：選択肢",
                "キャラを選ぶと、お願い・渡す のボタン",
                "ダブルクリック：追いかける（Escで解除）",
                "スペース：一時停止　1〜4：速さ　Tab：壁",
                "B：購買部　G：成績　M：思い出　L：記録　P：セーブ",
                "欄の右上の ◀ ▶ ▼：欄をしまう（画面の端の札で戻す）",
                "右上の 1階〜屋上：見る階（上の階は隠れる。R/F でも）",
            };
            float y = r.y + 40;
            foreach (var l in lines) { GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), l, small); y += 25; }
        }

        void DrawShop()
        {
            var pc = Player;
            if (pc == null) return;
            Rect r = ShopPanel;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 8, 400, 30), "購買部（銀貨 " + pc.money + "）", title);
            if (GUI.Button(new Rect(r.xMax - 92, r.y + 8, 80, 30), "閉じる", smallButton)) shopOpen = false;
            // 棚（種類）のタブ
            float tx = r.x + 14;
            for (int i = 0; i < ShelfNames.Length; i++)
            {
                if (GUI.Button(new Rect(tx, r.y + 44, 100, 30), ShelfNames[i], (int)shopShelf == i ? buttonOn : smallButton)) shopShelf = (ItemShelf)i;
                tx += 104;
            }
            float y = r.y + 86;
            foreach (var d in ItemBook.All)
            {
                if (d.shelf != shopShelf) continue;
                GUI.Label(new Rect(r.x + 14, y, 300, 24), d.name + "（" + d.price + "）", small);
                GUI.Label(new Rect(r.x + 14, y + 20, 600, 20), d.desc, tiny);
                if (!d.npcOnly) GUI.Label(new Rect(r.x + 540, y + 2, 70, 24), "所持 " + pc.Count(d.kind), small);
                if (d.npcOnly) GUI.Label(new Rect(r.xMax - 150, y + 6, 140, 24), "寮生だけが買う", tiny); // 篤は買えない
                else
                {
                    GUI.enabled = pc.money >= d.price;
                    if (GUI.Button(new Rect(r.xMax - 112, y + 2, 96, 34), "買う", button)) pc.Buy(d.kind);
                    GUI.enabled = true;
                }
                y += 44;
            }
        }

        /// <summary>ループを抜けたあとの選択。</summary>
        void DrawChoice()
        {
            Rect r = ChoicePanel;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 10, r.width - 28, 30), "ループを抜けた。これからどうする？", title);
            if (GUI.Button(new Rect(r.x + 20, r.y + 80, 270, 50), "このまま日常を続ける", button))
            { Loop.EndlessDays = true; Loop.AskChoice = false; world.Log("★ 終わりのない日常を続けることにした"); SetSpeed(speedBeforePause); }
            if (GUI.Button(new Rect(r.x + 310, r.y + 80, 270, 50), "もう一度最初から周回する", button))
            { Loop.AskChoice = false; Loop.Escaped = false; Loop.Rewind(world); }
        }

        /// <summary>成績表：いちばん新しい試験の、科目ごとの順位（1位はご機嫌になる）。</summary>
        void DrawExam()
        {
            Rect r = ExamPanel;
            GUI.Box(r, GUIContent.none, panel);
            string when = Academics.lastExamDay > 0 ? "（" + Academics.lastExamDay + "日目の試験）" : "（まだ試験がありません。土の日の午前に試験・18:00 に結果発表）";
            GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 120, 30), "成績表" + when, title);
            if (GUI.Button(new Rect(r.xMax - 92, r.y + 8, 80, 30), "閉じる", smallButton)) examOpen = false;
            float y = r.y + 46;
            // 31人になったので、上位10人と篤の順位だけ出す（2026-10-30）
            const int Top = 10;
            var fz = F;
            if (Academics.LastOverall.Count > 0)
            {
                var tot = new List<string>();
                for (int i = 0; i < Academics.LastOverall.Count; i++)
                {
                    var who = Academics.LastOverall[i].Key;
                    if (i >= Top && who != fz) continue;
                    tot.Add((i + 1) + "位 " + (string.IsNullOrEmpty(who.shortName) ? who.displayName : who.shortName) + "（" + Mathf.RoundToInt(Academics.LastOverall[i].Value) + "）");
                }
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "総合：" + string.Join("　", tot.ToArray()) + "（" + Academics.LastOverall.Count + "人）", tiny);
            }
            y += 30;
            if (!string.IsNullOrEmpty(Academics.LastDormRanking)) { GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "寮ごとの平均点：" + Academics.LastDormRanking, tiny); y += 30; }
            for (int s = 0; s < Academics.Count; s++)
            {
                if (!Academics.IsActive(s)) continue; // 予備の枠は出さない
                GUI.Label(new Rect(r.x + 14, y, 110, 26), Academics.Names[s], small);
                var list = Academics.LastExam[s];
                var parts = new List<string>();
                for (int i = 0; i < list.Count; i++)
                {
                    if (i >= Top && list[i].who != fz) continue;
                    parts.Add((i + 1) + "位 " + (string.IsNullOrEmpty(list[i].who.shortName) ? list[i].who.displayName : list[i].who.shortName) + list[i].mark + Mathf.RoundToInt(list[i].score));
                }
                GUI.Label(new Rect(r.x + 116, y + 2, r.width - 130, 26), parts.Count > 0 ? string.Join("　", parts.ToArray()) : "―", tiny);
                y += 30;
            }
        }

        static string FormatLeft(double minutes)
        {
            if (minutes >= 60) return Mathf.RoundToInt((float)(minutes / 60)) + "時間";
            return Mathf.Max(1, Mathf.RoundToInt((float)minutes)) + "分";
        }

        static string HobbyName(SimCharacter.HobbyKind h)
        {
            switch (h)
            {
                case SimCharacter.HobbyKind.Reading: return "読書";
                case SimCharacter.HobbyKind.Magic: return "魔法の練習";
                case SimCharacter.HobbyKind.Tea: return "お茶";
                default: return "散歩";
            }
        }

        static string Level(float v)
        {
            return v >= 0.7f ? "高い" : (v >= 0.4f ? "ふつう" : "低い");
        }

        // ---- 欲求の表示 ----
        // 表示の名前（順番は NeedValues と同じ）。NeedInverted が true の欲求は「100−値」で出す
        static readonly string[] NeedNames = { "心の満たされ", "体力", "楽しさ", "満腹度", "勉強", "趣味", "元気", "恋心", "性欲" };
        static readonly bool[] NeedInverted = { true, true, true, true, false, false, true, false, false };
        public static bool ShowHidden = false; // 隠しパラメータ（性欲のたまりなど）を出すか
        static readonly Color[] NeedColors =
        {
            new Color(0.45f, 0.6f, 0.95f), new Color(0.9f, 0.6f, 0.3f), new Color(0.6f, 0.85f, 0.4f), new Color(0.95f, 0.75f, 0.3f), new Color(0.5f, 0.75f, 0.9f),
            new Color(0.75f, 0.6f, 0.95f), new Color(0.55f, 0.55f, 0.8f), new Color(1f, 0.5f, 0.65f), new Color(0.85f, 0.4f, 0.55f)
        };
        static float[] NeedValues(SimCharacter c) { return new[] { c.lonely, c.tired, c.bored, c.hunger, c.study, c.hobbyNeed, c.sleepy, c.loveNeed, c.desire, c.Heat, c.vigor }; } // 9＝性欲のたまり（隠し）、10＝篤の精力

        class NeedSample { public double t; public float[] v; public float[] rate; }
        readonly Dictionary<SimCharacter, NeedSample> needSamples = new Dictionary<SimCharacter, NeedSample>();
        const double NeedSampleMinutes = 10; // この分数ごとに変化を測る

        /// <summary>欲求の変化（ゲーム内1時間あたり）。10分ごとに測り直す。</summary>
        float[] NeedRates(SimCharacter c, float[] cur)
        {
            NeedSample s;
            if (!needSamples.TryGetValue(c, out s) || world.Now < s.t)
            {
                s = new NeedSample { t = world.Now, v = (float[])cur.Clone(), rate = new float[cur.Length] };
                needSamples[c] = s;
                return s.rate;
            }
            double dt = world.Now - s.t;
            if (dt >= NeedSampleMinutes)
            {
                for (int i = 0; i < cur.Length; i++) s.rate[i] = (float)((cur[i] - s.v[i]) / dt * 60.0);
                s.v = (float[])cur.Clone();
                s.t = world.Now;
            }
            return s.rate;
        }

        /// <summary>短いゲージ＋今の値＋変化。</summary>
        void NeedBar(Rect r, string name, float value, float rate, Color c)
        {
            GUI.Label(new Rect(r.x, r.y - 3, 80, r.height + 4), name, small);
            Rect bar = new Rect(r.x + 80, r.y + 4, r.width - 80 - 150, r.height - 8);
            GUI.DrawTexture(bar, barBg);
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(value / 100f), bar.height), ProtoArt.White);
            GUI.color = old;
            int rv = Mathf.RoundToInt(rate);
            string rs = rv > 0 ? "+" + rv : (rv < 0 ? rv.ToString() : "±0");
            GUI.Label(new Rect(bar.xMax + 8, r.y - 3, 142, r.height + 4), Mathf.RoundToInt(value) + "　" + rs + "/時", small);
        }

        void Bar(Rect r, string name, float value, Color c)
        {
            GUI.Label(new Rect(r.x, r.y - 3, 90, r.height + 4), name, small);
            Rect bar = new Rect(r.x + 90, r.y + 3, r.width - 90, r.height - 6);
            GUI.DrawTexture(bar, barBg);
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(value / 100f), bar.height), ProtoArt.White);
            GUI.color = old;
        }
    }
}
