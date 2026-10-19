using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LifeSimProto
{
    /// <summary>
    /// 画面の表示（時計・速さ・頭の上の名前と吹き出し・関係表・記録）と、クリックでの指示。
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

        GUIStyle panel, label, small, title, bubble, nameTag, cell, button, buttonOn, worldLabel, clock;
        Texture2D panelTex, bubbleTex, barBg;

        const float VirtualHeight = 1080f;
        float Scale { get { return Mathf.Max(0.55f, Screen.height / VirtualHeight); } }
        float VW { get { return Screen.width / Scale; } }

        Rect TopBar { get { return new Rect(12, 12, 1240, 50); } }
        Rect InfoLine { get { return new Rect(12, 66, 1240, 32); } }
        Rect TablePanel { get { return new Rect(VW - 492, 108, 480, 372); } }
        Rect LogPanel { get { return new Rect(12, VirtualHeight - 272, 760, 260); } }
        Rect HelpPanel { get { return new Rect(VW - 492, VirtualHeight - 232, 480, 220); } }
        Rect DetailPanel { get { return new Rect(12, 108, 560, 690); } }
        Rect RomancePanel { get { return new Rect(VW - 492, 488, 480, 230); } }

        void Start()
        {
            world = GetComponent<LoungeWorld>();
        }

        void SetSpeed(float s)
        {
            if (s > 0f) speedBeforePause = s;
            world.SetUserSpeed(s);
        }

        bool OverHud(Vector2 v)
        {
            if (TopBar.Contains(v) || TablePanel.Contains(v) || LogPanel.Contains(v) || HelpPanel.Contains(v) || RomancePanel.Contains(v)) return true;
            if (selected != null && DetailPanel.Contains(v)) return true;
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
                if (kb.escapeKey.wasPressedThisFrame) Select(null);
                if (kb.tabKey.wasPressedThisFrame) CycleWallMode();
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
            if (hit == null) { Select(null); return; }

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
            world.Log("指示：" + selected.displayName + "に「" + hit.displayName + "に会いに行って」");
            selected.Order(hit);
            Select(null);
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

            worldLabel = new GUIStyle(label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            worldLabel.normal.textColor = new Color(0.9f, 0.85f, 0.72f, 0.8f);

            button = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            buttonOn = new GUIStyle(button);
            buttonOn.normal.textColor = new Color(1f, 0.85f, 0.3f);
            buttonOn.fontStyle = FontStyle.Bold;
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
            if (selected != null) DrawDetail();
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

                string tag = c.displayName + c.StatusTags() + "  " + c.actionText;
                Vector2 size = nameTag.CalcSize(new GUIContent(tag));
                Rect r = new Rect(g.x - size.x / 2f, g.y - size.y, size.x, size.y);
                Color old = GUI.color;
                GUI.Box(r, tag, nameTag);
                GUI.color = c.color;
                GUI.DrawTexture(new Rect(r.x, r.y, 6, r.height), ProtoArt.White);
                if (c.selected)
                {
                    GUI.color = new Color(1f, 0.85f, 0.2f);
                    GUI.DrawTexture(new Rect(r.x, r.yMax - 3, r.width, 3), ProtoArt.White);
                }
                GUI.color = old;

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
            x += 16;
            if (GUI.Button(new Rect(x, r.y + 8, 130, 34), WallModeName(world.wallMode), button)) CycleWallMode();
            x += 138;
            if (GUI.Button(new Rect(x, r.y + 8, 80, 34), "1階", button) && rig != null) rig.LookAtFloor(0);
            x += 88;
            if (GUI.Button(new Rect(x, r.y + 8, 80, 34), "2階", button) && rig != null) rig.LookAtFloor(1);
        }

        void DrawInfoLine()
        {
            Rect r = InfoLine;
            var parts = new List<string>();
            parts.Add(Calendar.DateText(world.Day, world.holidayWeekdays));
            if (world.todayNotes.Count > 0) parts.Add("今日：" + string.Join("／", world.todayNotes.ToArray()));
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
            if (priv.Count > 0) parts.Add("部屋で過ごしている：" + string.Join("・", priv.ToArray()));
            if (selected != null) parts.Add(selected.displayName + "を選択中：会わせたい相手をクリック");
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
            float cw = 62, ch = 38;
            float ox = r.x + 16, oy = r.y + 46;
            Color old = GUI.color;
            for (int i = 0; i < n; i++)
            {
                var ci = world.characters[i];
                GUI.color = ci.color;
                GUI.DrawTexture(new Rect(ox + cw * (i + 1) + 4, oy + 2, cw - 8, 4), ProtoArt.White);
                GUI.DrawTexture(new Rect(ox + 4, oy + ch * (i + 1) + 4, 4, ch - 8), ProtoArt.White);
                GUI.color = old;
                GUI.Label(new Rect(ox + cw * (i + 1), oy, cw, ch), ci.displayName, cell);
                GUI.Label(new Rect(ox, oy + ch * (i + 1), cw, ch), ci.displayName, cell);
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
                GUI.Label(new Rect(r.x + 24, y, 40, 26), c.displayName, small);
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
                string label = (st == 3 ? "♥ " : "") + Romance.StageName(st) + "（" + Mathf.RoundToInt(v) + "）";
                if (st < 3 && world.Now < rom.cooldownUntil[c.id]) label += " 告白後";
                GUI.Label(new Rect(r.x + 310, y, 160, 26), label, small);
                y += 34;
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
                "キャラをクリック → 相手をクリック：会いに行かせる",
                "キャラをダブルクリック：追いかける（Escで解除）",
                "スペース：一時停止/再開　1〜4：速さ　Tab：壁の表示",
                "全員が寝ている間は自動で早送り",
            };
            float y = r.y + 42;
            foreach (var l in lines) { GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), l, small); y += 28; }
        }

        void DrawDetail()
        {
            Rect r = DetailPanel;
            GUI.Box(r, GUIContent.none, panel);
            var c = selected;
            GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 28, 30), c.displayName + "（" + c.note + "）", title);
            string roomName = c.room != null ? "2階・" + c.room.name + "の部屋" : "なし";
            GUI.Label(new Rect(r.x + 14, r.y + 42, r.width - 28, 24), "今：" + c.actionText, small);
            GUI.Label(new Rect(r.x + 14, r.y + 66, r.width - 28, 24), "部屋：" + roomName, small);
            GUI.Label(new Rect(r.x + 14, r.y + 90, r.width - 28, 24), "話したい度 " + Level(c.sociability) + "　気まぐれ " + Level(c.whim) + "　真面目さ " + Level(c.diligence), small);
            string loveText = c.IsTarget ? "（恋愛の相手）" : "恋：" + Romance.StageName(world.romance.Stage(c.id)) + "（" + Mathf.RoundToInt(world.romance.love[c.id]) + "）";
            GUI.Label(new Rect(r.x + 14, r.y + 114, r.width - 28, 24), "趣味：" + HobbyName(c.hobby) + "　" + loveText, small);

            float y = r.y + 146;
            Bar(new Rect(r.x + 14, y, r.width - 28, 22), "さみしさ", c.lonely, new Color(0.45f, 0.6f, 0.95f)); y += 26;
            Bar(new Rect(r.x + 14, y, r.width - 28, 22), "疲れ", c.tired, new Color(0.9f, 0.6f, 0.3f)); y += 26;
            Bar(new Rect(r.x + 14, y, r.width - 28, 22), "退屈", c.bored, new Color(0.6f, 0.85f, 0.4f)); y += 26;
            Bar(new Rect(r.x + 14, y, r.width - 28, 22), "空腹", c.hunger, new Color(0.95f, 0.75f, 0.3f)); y += 26;
            Bar(new Rect(r.x + 14, y, r.width - 28, 22), "勉強", c.study, new Color(0.5f, 0.75f, 0.9f)); y += 26;
            Bar(new Rect(r.x + 14, y, r.width - 28, 22), "趣味", c.hobbyNeed, new Color(0.75f, 0.6f, 0.95f)); y += 26;
            Bar(new Rect(r.x + 14, y, r.width - 28, 22), "睡眠", c.sleepy, new Color(0.55f, 0.55f, 0.8f)); y += 26;
            if (!c.IsTarget) { Bar(new Rect(r.x + 14, y, r.width - 28, 22), "恋心", c.loveNeed, new Color(1f, 0.5f, 0.65f)); y += 26; }
            Bar(new Rect(r.x + 14, y, r.width - 28, 22), "性欲", c.desire, new Color(0.85f, 0.4f, 0.55f)); y += 26;

            y += 8;
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), "今日の予定", small);
            y += 24;
            int shown = 0;
            foreach (var it in c.schedule)
            {
                if (shown >= 3) break;
                string mark = it.done ? "済 " : "　 ";
                string time = ScheduleMaker.Clock(it.start) + (it.kind == TaskKind.Class ? "〜" + ScheduleMaker.Clock(it.end) : "");
                if (it.done && it.end < world.Now && it.kind != TaskKind.Bed) continue;
                GUI.Label(new Rect(r.x + 24, y, r.width - 38, 22), mark + time + "　" + it.Label, small);
                y += 22;
                shown++;
            }

            y += 4;
            if (c.statuses.Count > 0)
            {
                foreach (var st in c.statuses)
                {
                    var def = Statuses.Get(st.kind);
                    double left = st.until - world.Now;
                    string remain = left > 1440 * 2 ? "" : (left >= 60 ? "あと" + Mathf.RoundToInt((float)(left / 60)) + "時間" : "あと" + Mathf.RoundToInt((float)left) + "分");
                    GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "◆" + def.name + "：" + def.desc + "（" + remain + "）", small);
                    y += 22;
                }
            }
            GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22), "今週の勉強量：" + Mathf.RoundToInt(c.weekStudy), small);
            y += 26;
            var others = new List<SimCharacter>(world.characters);
            others.Remove(c);
            others.Sort((a, b) => world.rel.Get(c.id, b.id).CompareTo(world.rel.Get(c.id, a.id)));
            foreach (var o in others)
            {
                float v = world.rel.Get(c.id, o.id);
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 22),
                    o.displayName + "：" + Relationships.StageName(Relationships.StageOf(v)) + "（" + Mathf.RoundToInt(v) + "）", small);
                y += 21;
            }
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
            GUI.Label(new Rect(r.x, r.y - 2, 90, r.height + 4), name, small);
            Rect bar = new Rect(r.x + 90, r.y + 3, r.width - 90, r.height - 6);
            GUI.DrawTexture(bar, barBg);
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(value / 100f), bar.height), ProtoArt.White);
            GUI.color = old;
        }
    }
}
