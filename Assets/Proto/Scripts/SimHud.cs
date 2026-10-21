using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LifeSimProto
{
    /// <summary>
    /// 画面の表示（時計・速さ・名前と状態・吹き出し・関係表・記録・購買部）と、クリックでの操作。
    /// プレイヤーはFを操作する：Fを選んで床をクリックで移動、キャラをクリックで話しかける。
    /// ほかのキャラを選んで相手をクリックすると、Fが「〇〇に会いに行って」とお願いしに行く。
    /// 試作なので Unity の簡易表示（OnGUI）で作っている。
    /// </summary>
    public class SimHud : MonoBehaviour
    {
        LoungeWorld world;
        CameraRig rig;
        SimCharacter selected;
        SimCharacter lastClicked;
        float lastClickTime;
        float speedBeforePause = 1f;
        bool shopOpen, examOpen, memoryOpen;
        Vector2 memoryScroll;

        GUIStyle panel, label, small, tiny, title, bubble, nameTag, cell, button, buttonOn, smallButton, worldLabel, clock, chip;
        Texture2D panelTex, bubbleTex, barBg;

        const float VirtualHeight = 1080f;
        float Scale { get { return Mathf.Max(0.55f, Screen.height / VirtualHeight); } }
        float VW { get { return Screen.width / Scale; } }

        Rect TopBar { get { return new Rect(12, 12, 1780, 50); } }
        Rect MemoryPanel { get { return new Rect(VW / 2f - 470f, 120f, 940f, 820f); } }
        Rect ChoicePanel { get { return new Rect(VW / 2f - 300f, 380f, 600f, 170f); } }
        Rect ExamPanel { get { return new Rect(VW / 2f - 470f, 120f, 940f, 96f + Academics.Count * 30f + 40f); } }
        Rect InfoLine { get { return new Rect(12, 66, 1400, 32); } }
        Rect TablePanel { get { return new Rect(VW - 492, 108, 480, 400); } }
        Rect LogPanel { get { return new Rect(VW - 1260, VirtualHeight - 272, 760, 260); } }
        Rect HelpPanel { get { return new Rect(VW - 492, VirtualHeight - 262, 480, 250); } }
        Rect DetailPanel { get { return new Rect(12, 108, 580, 960); } }
        Rect RomancePanel { get { return new Rect(VW - 492, 516, 480, 292); } }
        Rect ShopPanel { get { return new Rect(VW / 2f - 380f, 110f, 760f, 110f + ShopRowsMax * 44f); } }
        ItemShelf shopShelf = ItemShelf.Basic;
        static readonly string[] ShelfNames = { "食べ物・薬", "欲求の品", "プレゼント", "装飾品", "能力の装飾", "お守り", "寮生の私物" };
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

        void Start()
        {
            world = GetComponent<LoungeWorld>();
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
            if (TopBar.Contains(v) || TablePanel.Contains(v) || LogPanel.Contains(v) || HelpPanel.Contains(v) || RomancePanel.Contains(v)) return true;
            if (selected != null && DetailPanel.Contains(v)) return true;
            if (selected != null && Portrait(selected, "stand") != null && StandRect.Contains(v)) return true;
            if (shopOpen && ShopPanel.Contains(v)) return true;
            if (examOpen && ExamPanel.Contains(v)) return true;
            if (memoryOpen && MemoryPanel.Contains(v)) return true;
            if (Loop.AskChoice && ChoicePanel.Contains(v)) return true;
            return false;
        }

        void Update()
        {
            if (world == null || world.mainCamera == null) return;
            if (rig == null) rig = world.mainCamera.GetComponent<CameraRig>();
            if (selected != null && !selected.Present) Select(null);

            var kb = Keyboard.current;
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
                if (kb.escapeKey.wasPressedThisFrame) { if (shopOpen) shopOpen = false; else if (examOpen) examOpen = false; else if (memoryOpen) memoryOpen = false; else Select(null); }
                if (kb.gKey.wasPressedThisFrame) examOpen = !examOpen;
                if (kb.mKey.wasPressedThisFrame) memoryOpen = !memoryOpen;
                if (kb.tabKey.wasPressedThisFrame) CycleWallMode();
                if (kb.bKey.wasPressedThisFrame) shopOpen = !shopOpen;
            }

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            Vector2 mp = mouse.position.ReadValue();
            Vector2 v = new Vector2(mp.x / Scale, (Screen.height - mp.y) / Scale);
            if (OverHud(v)) return;

            SimCharacter hit = null;
            Ray ray = world.mainCamera.ScreenPointToRay(new Vector3(mp.x, mp.y, 0f));
            RaycastHit info;
            if (Physics.Raycast(ray, out info, 300f)) hit = info.collider.GetComponentInParent<SimCharacter>();

            float now = Time.unscaledTime;
            if (hit == null)
            {
                // Fを選んでいるときは、床のクリックで移動
                Vector3 p;
                if (selected != null && selected.IsTarget && FloorPoint(ray, out p)) { selected.OrderMove(p); return; }
                Select(null);
                return;
            }

            bool dbl = hit == lastClicked && now - lastClickTime < 0.35f;
            lastClicked = hit;
            lastClickTime = now;
            if (dbl)
            {
                if (rig != null) rig.follow = hit.transform;
                Select(hit);
                return;
            }
            if (selected == null || selected == hit)
            {
                Select(hit);
                return;
            }
            if (selected.IsTarget)
            {
                // F → だれかに話しかける
                world.Log("篤への指示：「" + hit.displayName + "と話す」");
                selected.OrderTalk(hit);
                return;
            }
            if (hit.IsTarget)
            {
                world.Log("篤への指示：「" + selected.displayName + "と話す」");
                hit.OrderTalk(selected);
                Select(null);
                return;
            }
            // ほかのキャラ → 相手：Fが「〇〇に会いに行って」とお願いしに行く
            world.Log("篤への指示：" + selected.displayName + "に「" + hit.displayName + "に会いに行って」とお願いする");
            selected.Order(hit);
            Select(null);
        }

        /// <summary>画面のクリック位置から、歩ける床の点を求める（2階を優先）。</summary>
        bool FloorPoint(Ray ray, out Vector3 p)
        {
            p = Vector3.zero;
            if (Mathf.Abs(ray.direction.y) < 0.0001f) return false;
            float t2 = (LoungeWorld.Floor3Y - ray.origin.y) / ray.direction.y;
            if (t2 > 0f && world.nav2 != null)
            {
                Vector3 q = ray.origin + ray.direction * t2;
                if (world.nav2.IsFree(q)) { p = q; return true; } // 屋上の天文台
            }
            float t1 = (LoungeWorld.Floor2Y - ray.origin.y) / ray.direction.y;
            if (t1 > 0f)
            {
                Vector3 q = ray.origin + ray.direction * t1;
                if (world.nav1.IsFree(q)) { p = q; return true; }
            }
            float t0 = -ray.origin.y / ray.direction.y;
            if (t0 > 0f)
            {
                Vector3 q = ray.origin + ray.direction * t0;
                if (world.nav0.IsFree(q)) { p = q; return true; }
                if (q.x > -15f && q.x < 15f && q.z > -18f && q.z < 8f) { p = world.nav0.NearestFreePoint(q); return true; }
            }
            return false;
        }

        void Select(SimCharacter c)
        {
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
            DrawHelp();
            if (selected != null)
            {
                DrawDetail();
                var st = Portrait(selected, "stand");
                if (st != null) GUI.DrawTexture(StandRect, st, ScaleMode.ScaleToFit, true);
            }
            if (shopOpen) DrawShop();
            if (examOpen) DrawExam();
            if (memoryOpen) DrawMemory();
            if (Loop.AskChoice) DrawChoice();
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
                if (!c.Present) continue;
                Vector2 g;
                if (!ToGui(c.transform.position + Vector3.up * c.HeadHeight, s, out g)) continue;

                string tag = c.displayName + (c.IsTarget ? "（あなた）" : "") + "  " + c.actionText;
                Vector2 size = nameTag.CalcSize(new GUIContent(tag));
                Rect r = new Rect(g.x - size.x / 2f, g.y - size.y, size.x, size.y);
                Color old = GUI.color;
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
                if (tags.Count > 0) DrawChips(r.xMax + 4, r.y + 1, tags, r.height - 2);

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
                if (GUI.Button(new Rect(x, r.y + 8, 76, 34), sp + "倍", on ? buttonOn : button)) SetSpeed(sp);
                x += 84;
            }
            if (world.debugSpeedOptions != null)
            {
                var oldc = GUI.color;
                foreach (var sp in world.debugSpeedOptions)
                {
                    bool on = Mathf.Approximately(cur, sp);
                    GUI.color = new Color(1f, 0.75f, 0.6f); // デバッグ用は色を変える
                    if (GUI.Button(new Rect(x, r.y + 8, 76, 34), sp + "倍", on ? buttonOn : button)) SetSpeed(sp);
                    x += 84;
                }
                GUI.color = oldc;
            }
            x += 16;
            if (GUI.Button(new Rect(x, r.y + 8, 130, 34), WallModeName(world.wallMode), button)) CycleWallMode();
            x += 138;
            if (GUI.Button(new Rect(x, r.y + 8, 70, 34), "1階", button) && rig != null) rig.LookAtFloor(0);
            x += 76;
            if (GUI.Button(new Rect(x, r.y + 8, 70, 34), "2階", button) && rig != null) rig.LookAtFloor(1);
            x += 90;
            if (Player != null)
            {
                GUI.Label(new Rect(x, r.y + 11, 130, 30), "銀貨 " + Player.money, clock);
                x += 130;
                if (GUI.Button(new Rect(x, r.y + 8, 100, 34), "購買部", shopOpen ? buttonOn : button)) shopOpen = !shopOpen;
                x += 108;
                if (F != null && GUI.Button(new Rect(x, r.y + 8, 100, 34), "篤を選ぶ", selected == F ? buttonOn : button)) Select(F);
                x += 108;
                if (GUI.Button(new Rect(x, r.y + 8, 80, 34), "成績", examOpen ? buttonOn : button)) examOpen = !examOpen;
                x += 88;
                if (GUI.Button(new Rect(x, r.y + 8, 100, 34), "思い出", memoryOpen ? buttonOn : button)) memoryOpen = !memoryOpen;
            }
        }

        void DrawInfoLine()
        {
            Rect r = InfoLine;
            var parts = new List<string>();
            parts.Add(Calendar.DateText(world.Day, world.holidayWeekdays));
            if (world.todayNotes.Count > 0) parts.Add("今日：" + string.Join("／", world.todayNotes.ToArray()));
            int withA = 0, doneA = 0;
            foreach (var c in world.characters) if (c.hasAssignment) { withA++; if (c.assignProgress >= 100f) doneA++; }
            if (withA > 0) parts.Add("課題：" + doneA + "/" + withA + "人が終了");
            parts.Add("寮の評判 " + world.dormReputation);
            if (world.Blackout) parts.Add("停電中");
            if (world.SnackBonus) parts.Add("差し入れあり");
            if (world.autoFast) parts.Add("全員就寝中：" + world.allAsleepSpeed + "倍速");
            var away = new List<string>();
            var priv = new List<string>();
            foreach (var c in world.characters)
            {
                if (c.state == SimCharacter.State.Away) away.Add(c.displayName);
                else if (c.state == SimCharacter.State.Private) priv.Add(c.displayName);
            }
            if (away.Count > 0) parts.Add("外出中：" + string.Join("・", away.ToArray()));
            if (priv.Count > 0) parts.Add("エッチしている：" + string.Join("・", priv.ToArray()));
            string text = string.Join("　｜　", parts.ToArray());
            Vector2 size = small.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(r.x, r.y, size.x + 28, r.height), GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 5, size.x + 10, 24), text, small);
        }

        void DrawTable()
        {
            Rect r = TablePanel;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 8, 300, 30), "関係表（好感度）", title);
            int n = world.characters.Count;
            float cw = n <= 6 ? 62 : 42, ch = n <= 6 ? 38 : 28;
            float ox = r.x + 16, oy = r.y + 46;
            Color old = GUI.color;
            for (int i = 0; i < n; i++)
            {
                var ci = world.characters[i];
                GUI.color = ci.color;
                GUI.DrawTexture(new Rect(ox + cw * (i + 1) + 4, oy + 2, cw - 8, 4), ProtoArt.White);
                GUI.DrawTexture(new Rect(ox + 4, oy + ch * (i + 1) + 4, 4, ch - 8), ProtoArt.White);
                GUI.color = old;
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
                    Rect cr = new Rect(ox + cw * (j + 1) + 2, oy + ch * (i + 1) + 2, cw - 4, ch - 4);
                    if (i == j)
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.06f);
                        GUI.DrawTexture(cr, ProtoArt.White);
                        GUI.color = old;
                        continue;
                    }
                    int a = world.characters[i].id, b = world.characters[j].id;
                    float v = world.rel.Get(a, b);
                    int stage = Relationships.StageOf(v);
                    Color bg = stage == 2 ? new Color(0.75f, 0.3f, 0.5f) : (stage == 1 ? new Color(0.25f, 0.52f, 0.3f) : new Color(0.32f, 0.32f, 0.34f));
                    float since = world.simTime - world.rel.LastChange(a, b);
                    if (since < 2f) bg = Color.Lerp(new Color(1f, 0.85f, 0.2f), bg, since / 2f);
                    GUI.color = bg;
                    GUI.DrawTexture(cr, ProtoArt.White);
                    GUI.color = old;
                    GUI.Label(cr, Mathf.RoundToInt(v).ToString(), cell);
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
            GUI.Label(new Rect(r.x + 14, r.y + 8, 400, 30), f.displayName + "への気持ち", title);
            float y = r.y + 44;
            Color old = GUI.color;
            foreach (var c in world.characters)
            {
                if (c.IsTarget) continue;
                int st = rom.Stage(c.id);
                float v = rom.love[c.id];
                GUI.color = c.color;
                GUI.DrawTexture(new Rect(r.x + 14, y + 4, 4, 18), ProtoArt.White);
                GUI.color = old;
                var rf = Portrait(c, "face");
                if (rf != null) GUI.DrawTexture(new Rect(r.x + 22, y, 26, 26), rf, ScaleMode.ScaleToFit, true);
                else GUI.Label(new Rect(r.x + 22, y, 40, 26), string.IsNullOrEmpty(c.shortName) ? c.displayName : c.shortName, tiny);
                Rect bar = new Rect(r.x + 60, y + 6, 240, 14);
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
                GUI.Label(new Rect(r.x + 310, y, 160, 26), lab, small);
                y += 26;
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

        void DrawLog()
        {
            Rect r = LogPanel;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 8, 300, 30), "できごと", title);
            int lines = 9;
            int start = Mathf.Max(0, world.log.Count - lines);
            float y = r.y + 42;
            for (int i = start; i < world.log.Count; i++)
            {
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), world.log[i], small);
                y += 23;
            }
        }

        void DrawHelp()
        {
            Rect r = HelpPanel;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 8, 300, 30), "操作", title);
            string[] lines =
            {
                "右ドラッグ：視点を回す　ホイール：近づく/離れる",
                "WASD：移動　Q/E：回す　R/F：上下　中ドラッグ：平行移動",
                "篤を選ぶ → 床をクリック：移動／キャラをクリック：話す",
                "キャラを選ぶ → 相手をクリック：篤がお願いしに行く",
                "キャラを選ぶと、お願い・渡す のボタン",
                "ダブルクリック：追いかける（Escで解除）",
                "スペース：一時停止　1〜4：速さ　Tab：壁　B：購買部　G：成績　M：思い出",
            };
            float y = r.y + 42;
            foreach (var l in lines) { GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), l, small); y += 28; }
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

        /// <summary>思い出：周の数・篤の魔法・アルバム・キャラ図鑑（ループしても残る）。</summary>
        void DrawMemory()
        {
            Rect r = MemoryPanel;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 120, 30), "思い出（" + Loop.Count + "周目）　ループしても残る", title);
            if (GUI.Button(new Rect(r.xMax - 92, r.y + 8, 80, 30), "閉じる", smallButton)) memoryOpen = false;
            float y = r.y + 46;
            var magic = new List<string>();
            foreach (var sp in FMagic.All) magic.Add(FMagic.Has(sp.subject) ? sp.name : "？（" + Academics.Names[sp.needSubject] + " " + Mathf.RoundToInt(sp.need) + "で開く）");
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "篤の魔法：" + string.Join("／", magic.ToArray()), tiny); y += 24;
            var fx = world.RomanceTarget;
            if (fx != null) { GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "篤の能力（持ち越し）：" + fx.AbilityText(), tiny); y += 24; }
            var gates = new List<string>();
            foreach (var g in PlaceGates.All) gates.Add(g.place + (Loop.OpenedPlaces.Contains(g.place) ? "（開いた）" : "（" + PlaceGates.Text(g) + "）"));
            for (int k = 0; k < gates.Count; k += 2)
            {
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), (k == 0 ? "場所：" : "　　　") + string.Join("　", gates.GetRange(k, Mathf.Min(2, gates.Count - k)).ToArray()), tiny);
                y += 22;
            }
            y += 6;

            GUI.Label(new Rect(r.x + 14, y, 300, 24), "キャラ図鑑（会話で埋まる）", small); y += 26;
            foreach (var c in world.characters)
            {
                if (c.IsTarget || c.profile == null) continue;
                var known = new List<string>();
                foreach (var t in c.profile.likes) known.Add(Loop.Known(c, "好き", t) ? t : "？");
                var bad = new List<string>();
                foreach (var t in c.profile.dislikes) bad.Add(Loop.Known(c, "苦手", t) ? t : "？");
                bool bd = false;
                foreach (var z in Loop.Zukan) if (z.StartsWith(c.letter + ":誕生日:")) { bd = true; break; }
                GUI.Label(new Rect(r.x + 24, y, r.width - 40, 22), c.displayName + "（" + c.profile.race + "）　好き：" + string.Join("・", known.ToArray()) + "　苦手：" + string.Join("・", bad.ToArray()) + "　誕生日：" + (bd ? "わかった" : "？"), tiny);
                y += 22;
            }
            y += 8;
            GUI.Label(new Rect(r.x + 14, y, 300, 24), "アルバム（" + Album.Entries.Count + "）", small); y += 26;
            Rect view = new Rect(r.x + 14, y, r.width - 28, r.yMax - y - 12);
            Rect content = new Rect(0, 0, view.width - 20, Mathf.Max(view.height, Album.Entries.Count * 22f));
            memoryScroll = GUI.BeginScrollView(view, memoryScroll, content);
            for (int i = Album.Entries.Count - 1, k = 0; i >= 0; i--, k++)
                GUI.Label(new Rect(4, k * 22f, content.width - 8, 22), Album.Entries[i], tiny);
            GUI.EndScrollView();
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
            if (Academics.LastOverall.Count > 0)
            {
                var tot = new List<string>();
                for (int i = 0; i < Academics.LastOverall.Count; i++)
                    tot.Add((i + 1) + "位 " + Academics.LastOverall[i].Key.displayName + "（" + Mathf.RoundToInt(Academics.LastOverall[i].Value) + "）");
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "総合：" + string.Join("　", tot.ToArray()), tiny);
            }
            y += 30;
            for (int s = 0; s < Academics.Count; s++)
            {
                GUI.Label(new Rect(r.x + 14, y, 100, 26), Academics.Names[s], small);
                var list = Academics.LastExam[s];
                var parts = new List<string>();
                for (int i = 0; i < list.Count; i++)
                    parts.Add((i + 1) + "位 " + list[i].who.displayName + list[i].mark + Mathf.RoundToInt(list[i].score));
                GUI.Label(new Rect(r.x + 116, y + 2, r.width - 130, 26), parts.Count > 0 ? string.Join("　", parts.ToArray()) : "―", tiny);
                y += 30;
            }
        }

        void DrawDetail()
        {
            Rect r = DetailPanel;
            var c = selected;
            float y = r.y + 8;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 30), c.displayName + "（" + c.note + "）" + (c.IsTarget ? "　あなたが操作" : ""), title);
            y += 34;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "今：" + c.actionText, small); y += 22;
            string roomName = c.room != null ? "2階・" + c.room.name + "の部屋" : "なし";
            string loveText = c.IsTarget ? "（恋愛の相手）" : "恋：" + Romance.StageName(world.romance.Stage(c.id)) + "（" + Mathf.RoundToInt(world.romance.love[c.id]) + "）";
            string hob = c.profile != null && !string.IsNullOrEmpty(c.profile.hobbyText) ? c.profile.hobbyText : HobbyName(c.hobby);
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "部屋：" + roomName + "　趣味：" + hob + "　" + loveText, small); y += 22;
            if (c.profile != null)
            {
                var pf = c.profile;
                string likeText = (pf.likes.Length > 0 ? "好き：" + string.Join("・", pf.likes) : "") + (pf.dislikes.Length > 0 ? "　苦手：" + string.Join("・", pf.dislikes) : "")
                    + (pf.traitText.Length > 0 ? "　特徴：" + pf.traitText : "");
                if (likeText.Length > 0) { GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), likeText, tiny); y += 20; }
                // 取っている科目と学力（★得意 ▽苦手）
                var parts = new List<string>();
                foreach (int sj in pf.enrolled)
                    parts.Add(Academics.Names[sj] + (pf.IsStrong(sj) ? "★" : (pf.IsWeak(sj) ? "▽" : "")) + Mathf.RoundToInt(c.skill[sj]));
                for (int k = 0; k < parts.Count; k += 3)
                {
                    GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), (k == 0 ? "科目：" : "　　　") + string.Join("　", parts.GetRange(k, Mathf.Min(3, parts.Count - k)).ToArray()), tiny);
                    y += 20;
                }
            }
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "能力：" + c.AbilityText(), small); y += 22;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "話したい度 " + Level(c.sociability) + "　気まぐれ " + Level(c.whim) + "　真面目さ " + Level(c.diligence), small); y += 22;
            string assign = c.hasAssignment ? "　課題（" + SimCharacter.SubjectName(c.assignSubject) + "）" + Mathf.RoundToInt(c.assignProgress) + "%（締切 " + ScheduleMaker.Clock(c.assignDeadline) + "）" : "";
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "学力 " + Mathf.RoundToInt(c.academic) + "　魔力のたまり " + Mathf.RoundToInt(c.mana) + "　今週の勉強 " + Mathf.RoundToInt(c.weekStudy) + assign, small); y += 28;

            // 状態異常
            if (c.statuses.Count == 0) { GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "状態：ふつう", small); y += 24; }
            foreach (var st in c.statuses)
            {
                var def = StatusBook.Get(st.line);
                var one = new List<KeyValuePair<string, Color>> { new KeyValuePair<string, Color>(def.NameOf(st.stage), def.Color) };
                float x = DrawChips(r.x + 14, y, one);
                string remain = st.until > 0 ? "（あと" + FormatLeft(st.until - world.Now) + "）" : "";
                GUI.Label(new Rect(x + 4, y, r.xMax - x - 18, 22), def.EffectOf(st.stage).desc + remain, tiny);
                y += 26;
            }
            // 身につけている物・効いているアイテム
            var worn = new List<string>();
            foreach (var a in c.ItemMods) worn.Add(a.mod.name + (a.until > 0 ? "（あと" + FormatLeft(a.until - world.Now) + "）" : "（身につけている）"));
            if (worn.Count > 0) { GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "効いている物：" + string.Join("／", worn.ToArray()), tiny); y += 24; }
            y += 4;

            Bar(new Rect(r.x + 14, y, r.width - 28, 20), "さみしさ", c.lonely, new Color(0.45f, 0.6f, 0.95f)); y += 23;
            Bar(new Rect(r.x + 14, y, r.width - 28, 20), "疲れ", c.tired, new Color(0.9f, 0.6f, 0.3f)); y += 23;
            Bar(new Rect(r.x + 14, y, r.width - 28, 20), "退屈", c.bored, new Color(0.6f, 0.85f, 0.4f)); y += 23;
            Bar(new Rect(r.x + 14, y, r.width - 28, 20), "空腹", c.hunger, new Color(0.95f, 0.75f, 0.3f)); y += 23;
            Bar(new Rect(r.x + 14, y, r.width - 28, 20), "勉強", c.study, new Color(0.5f, 0.75f, 0.9f)); y += 23;
            Bar(new Rect(r.x + 14, y, r.width - 28, 20), "趣味", c.hobbyNeed, new Color(0.75f, 0.6f, 0.95f)); y += 23;
            Bar(new Rect(r.x + 14, y, r.width - 28, 20), "睡眠", c.sleepy, new Color(0.55f, 0.55f, 0.8f)); y += 23;
            if (!c.IsTarget) { Bar(new Rect(r.x + 14, y, r.width - 28, 20), "恋心", c.loveNeed, new Color(1f, 0.5f, 0.65f)); y += 23; }
            Bar(new Rect(r.x + 14, y, r.width - 28, 20), "性欲", c.desire, new Color(0.85f, 0.4f, 0.55f)); y += 27;

            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "今日の予定", small);
            y += 22;
            int shown = 0;
            foreach (var it in c.schedule)
            {
                if (shown >= 3) break;
                if (it.done && it.end < world.Now && it.kind != TaskKind.Bed) continue;
                string time = ScheduleMaker.Clock(it.start) + (it.kind == TaskKind.Class ? "〜" + ScheduleMaker.Clock(it.end) : "");
                GUI.Label(new Rect(r.x + 24, y, r.width - 38, 20), (it.done ? "済 " : "　 ") + time + "　" + it.Label, tiny);
                y += 20;
                shown++;
            }
            y += 10;

            if (c.IsTarget) DrawFActions(r, ref y);
            else DrawGirlActions(r, c, ref y);
        }

        void DrawFActions(Rect r, ref float y)
        {
            var pc = Player;
            var f = selected;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "篤の行動（銀貨 " + (pc != null ? pc.money : 0) + "）", title); y += 32;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "床をクリックで移動・キャラをクリックで話しかける", tiny); y += 24;
            if (GUI.Button(new Rect(r.x + 14, y, 180, 32), "寮の手伝い（+20）", smallButton)) f.OrderWork();
            if (GUI.Button(new Rect(r.x + 202, y, 200, 32), "街のアルバイト（+70）", smallButton)) f.OrderPartTime();
            if (GUI.Button(new Rect(r.x + 410, y, 150, 32), "購買部を開く", smallButton)) shopOpen = true;
            y += 40;
            // 篤の魔法（自分に・まわりに使うもの。1日1回ずつ）
            float mx = r.x + 14;
            bool anyMagic = false;
            foreach (var sp in FMagic.All)
            {
                if (!FMagic.Has(sp.subject) || sp.subject == 6 || sp.subject == 7) continue;
                anyMagic = true;
                GUI.enabled = !FMagic.UsedToday(sp.subject, world.Day);
                if (GUI.Button(new Rect(mx, y, 180, 32), "魔法：" + sp.name, smallButton)) FMagic.Use(world, f, sp.subject, null);
                GUI.enabled = true;
                mx += 188;
            }
            if (anyMagic) y += 40;
            if (pc == null) return;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "持ち物（自分に使う）", small); y += 24;
            float x = r.x + 14;
            bool any = false;
            foreach (var d in ItemBook.All)
            {
                int n = pc.Count(d.kind);
                if (n <= 0) continue;
                any = true;
                if (!d.selfUse) { GUI.Label(new Rect(x, y + 4, 170, 24), d.name + "×" + n, tiny); }
                else if (GUI.Button(new Rect(x, y, 170, 30), d.name + "×" + n + " 使う", smallButton))
                {
                    if (pc.Take(d.kind)) f.ApplyItem(d.kind, f);
                }
                x += 178;
                if (x > r.xMax - 180) { x = r.x + 14; y += 34; }
            }
            if (!any) GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "なし（購買部で買える）", tiny);
        }

        void DrawGirlActions(Rect r, SimCharacter c, ref float y)
        {
            var pc = Player;
            var f = F;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "篤から", title); y += 32;
            if (f == null) return;
            if (GUI.Button(new Rect(r.x + 14, y, 150, 32), "話しかける", smallButton)) { f.OrderTalk(c); world.Log("篤への指示：「" + c.displayName + "と話す」"); }
            if (GUI.Button(new Rect(r.x + 172, y, 180, 32), "お願い：勉強して", smallButton)) { f.OrderRequest(c, RequestKind.Study, null); world.Log("篤への指示：" + c.displayName + "に「勉強して」とお願いする"); }
            if (GUI.Button(new Rect(r.x + 360, y, 180, 32), "お願い：休んで", smallButton)) { f.OrderRequest(c, RequestKind.Rest, null); world.Log("篤への指示：" + c.displayName + "に「休んで」とお願いする"); }
            y += 38;
            int ts = c.TeachSubjectFor(f);
            GUI.enabled = ts >= 0;
            if (GUI.Button(new Rect(r.x + 14, y, 300, 32), ts >= 0 ? "お願い：" + Academics.Names[ts] + "を教えて" : "お願い：勉強を教えて（教われる科目なし）", smallButton))
            { f.OrderRequest(c, RequestKind.Teach, null); world.Log("篤への指示：" + c.displayName + "に「勉強を教えて」とお願いする"); }
            GUI.enabled = true;
            // 篤の魔法（相手に使うもの。近くにいるときだけ）
            bool near = f.Present && c.Present && (f.transform.position - c.transform.position).sqrMagnitude < 9f;
            float mx = r.x + 322;
            foreach (int sj in new[] { 6, 7 })
            {
                if (!FMagic.Has(sj)) continue;
                var sp = System.Array.Find(FMagic.All, q => q.subject == sj);
                GUI.enabled = near && !FMagic.UsedToday(sj, world.Day) && (sj != 6 || c.GetStage(StatusLine.Cold) > 0);
                if (GUI.Button(new Rect(mx, y, 120, 32), sp.name, smallButton)) FMagic.Use(world, f, sj, c);
                GUI.enabled = true;
                mx += 126;
            }
            y += 38;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "ほかのキャラをクリック → 「その子に会いに行って」とお願い", tiny); y += 24;
            if (pc != null) GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "最近のお願い：" + pc.RecentRequests(c) + "回（多いと断られやすい）", tiny);
            y += 28;
            if (pc == null) return;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "渡す（篤が持っていく）", small); y += 24;
            float x = r.x + 14;
            bool any = false;
            foreach (var d in ItemBook.All)
            {
                int n = pc.Count(d.kind);
                if (n <= 0) continue;
                any = true;
                GUI.enabled = pc.CanUseToday(d.kind);
                if (GUI.Button(new Rect(x, y, 170, 30), d.name + "×" + n + " 渡す", smallButton))
                {
                    if (pc.Take(d.kind))
                    {
                        world.Log("篤への指示：" + c.displayName + "に「" + d.name + "」を渡す");
                        f.OrderGive(c, d.kind);
                    }
                }
                GUI.enabled = true;
                x += 178;
                if (x > r.xMax - 180) { x = r.x + 14; y += 34; }
            }
            if (!any) GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "持ち物なし（購買部で買える）", tiny);
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
