using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LifeSimProto
{
    /// <summary>
    /// 画面の表示（頭の上の名前と吹き出し・関係表・時間の速さ・記録）と、クリックでの指示。
    /// 試作なので Unity の簡易表示（OnGUI）で作っている。
    /// </summary>
    public class SimHud : MonoBehaviour
    {
        LoungeWorld world;
        CameraRig rig;
        SimCharacter selected;
        SimCharacter lastClicked;
        float lastClickTime;
        float currentSpeed = 1f;
        float speedBeforePause = 1f;

        GUIStyle panel, label, small, title, bubble, nameTag, cell, button, buttonOn, worldLabel;
        Texture2D panelTex, bubbleTex, barBg;

        const float VirtualHeight = 1080f;
        float Scale { get { return Mathf.Max(0.55f, Screen.height / VirtualHeight); } }
        float VW { get { return Screen.width / Scale; } }

        Rect TopBar { get { return new Rect(12, 12, 660, 50); } }
        Rect TablePanel { get { return new Rect(VW - 492, 12, 480, 372); } }
        Rect LogPanel { get { return new Rect(12, VirtualHeight - 272, 660, 260); } }
        Rect HelpPanel { get { return new Rect(VW - 492, VirtualHeight - 202, 480, 190); } }
        Rect DetailPanel { get { return new Rect(12, 72, 400, 330); } }

        void Start()
        {
            world = GetComponent<LoungeWorld>();
            Time.timeScale = 1f;
        }

        void SetSpeed(float s)
        {
            if (s > 0f) speedBeforePause = s;
            currentSpeed = s;
            Time.timeScale = s;
        }

        bool OverHud(Vector2 v)
        {
            if (TopBar.Contains(v) || TablePanel.Contains(v) || LogPanel.Contains(v) || HelpPanel.Contains(v)) return true;
            if (selected != null && DetailPanel.Contains(v)) return true;
            return false;
        }

        void Update()
        {
            if (world == null || world.mainCamera == null) return;
            if (rig == null) rig = world.mainCamera.GetComponent<CameraRig>();

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame) SetSpeed(currentSpeed > 0f ? 0f : speedBeforePause);
                if (kb.digit1Key.wasPressedThisFrame) SetSpeed(1f);
                if (kb.digit2Key.wasPressedThisFrame) SetSpeed(2f);
                if (kb.digit3Key.wasPressedThisFrame) SetSpeed(4f);
                if (kb.escapeKey.wasPressedThisFrame) Select(null);
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
            // 選んでいるキャラに「〇〇に会いに行って」と指示する
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

            worldLabel = new GUIStyle(label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            worldLabel.normal.textColor = new Color(0.85f, 0.8f, 0.7f, 0.85f);

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
            DrawTable();
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
            foreach (var kv in world.worldLabels)
            {
                Vector2 g;
                if (!ToGui(kv.Key, s, out g)) continue;
                GUI.Label(new Rect(g.x - 150, g.y - 12, 300, 24), kv.Value, worldLabel);
            }
        }

        void DrawCharacters(float s)
        {
            // 遠いキャラから順に描く（近いキャラの表示が上に来るように）
            var list = new List<SimCharacter>(world.characters);
            Vector3 cam = world.mainCamera.transform.position;
            list.Sort((a, b) => (b.transform.position - cam).sqrMagnitude.CompareTo((a.transform.position - cam).sqrMagnitude));
            foreach (var c in list)
            {
                Vector2 g;
                if (!ToGui(c.transform.position + Vector3.up * c.HeadHeight, s, out g)) continue;

                string tag = c.displayName + "  " + c.actionText;
                Vector2 size = nameTag.CalcSize(new GUIContent(tag));
                Rect r = new Rect(g.x - size.x / 2f, g.y - size.y, size.x, size.y);
                Color old = GUI.color;
                GUI.Box(r, tag, nameTag);
                // キャラの色の帯
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
            int t = Mathf.FloorToInt(world.simTime);
            GUI.Label(new Rect(r.x + 14, r.y + 11, 160, 30), string.Format("経過 {0:00}:{1:00}", t / 60, t % 60), label);
            float x = r.x + 170;
            float[] speeds = { 0f, 1f, 2f, 4f };
            string[] names = { "一時停止", "1倍", "2倍", "4倍" };
            for (int i = 0; i < speeds.Length; i++)
            {
                float w = i == 0 ? 110 : 70;
                bool on = Mathf.Approximately(currentSpeed, speeds[i]);
                if (GUI.Button(new Rect(x, r.y + 8, w, 34), names[i], on ? buttonOn : button)) SetSpeed(speeds[i]);
                x += w + 8;
            }
            string hint = selected == null ? "キャラをクリックで選択" : selected.displayName + "を選択中：会わせたい相手をクリック";
            GUI.Label(new Rect(x + 6, r.y + 13, 240, 30), hint, small);
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
                "WASD：移動　Q/E：回す　中ドラッグ：平行移動",
                "キャラをクリック → 相手をクリック：会いに行かせる",
                "キャラをダブルクリック：追いかける（Escで解除）",
                "スペース：一時停止/再開　1・2・3：1倍・2倍・4倍",
            };
            float y = r.y + 42;
            foreach (var l in lines) { GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24), l, small); y += 27; }
        }

        void DrawDetail()
        {
            Rect r = DetailPanel;
            GUI.Box(r, GUIContent.none, panel);
            var c = selected;
            GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 28, 30), c.displayName + "（" + c.note + "）", title);
            GUI.Label(new Rect(r.x + 14, r.y + 42, r.width - 28, 24), "今：" + c.actionText, small);
            GUI.Label(new Rect(r.x + 14, r.y + 68, r.width - 28, 24), "人と話したい度 " + Level(c.sociability) + "　気まぐれ度 " + Level(c.whim), small);
            Bar(new Rect(r.x + 14, r.y + 100, r.width - 28, 22), "さみしさ", c.lonely, new Color(0.45f, 0.6f, 0.95f));
            Bar(new Rect(r.x + 14, r.y + 128, r.width - 28, 22), "疲れ", c.tired, new Color(0.9f, 0.6f, 0.3f));
            Bar(new Rect(r.x + 14, r.y + 156, r.width - 28, 22), "退屈", c.bored, new Color(0.6f, 0.85f, 0.4f));

            // 好感度の高い順
            var others = new List<SimCharacter>(world.characters);
            others.Remove(c);
            others.Sort((a, b) => world.rel.Get(c.id, b.id).CompareTo(world.rel.Get(c.id, a.id)));
            float y = r.y + 190;
            foreach (var o in others)
            {
                float v = world.rel.Get(c.id, o.id);
                GUI.Label(new Rect(r.x + 14, y, r.width - 28, 24),
                    o.displayName + "：" + Relationships.StageName(Relationships.StageOf(v)) + "（" + Mathf.RoundToInt(v) + "）", small);
                y += 23;
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
