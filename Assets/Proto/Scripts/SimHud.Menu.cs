using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LifeSimProto
{
    /// <summary>
    /// キャラを右クリックしたときの選択肢（2026-10-25 famuru）。
    /// ・左クリック：キャラを選ぶ（そのキャラの画面が出る）だけ。何もない所で選ぶのをやめる
    /// ・床を右クリック：篤がそこへ移動
    /// ・右クリック（押してすぐ離す）：選択肢が出る。篤が話しかける・プレゼントする・エッチに誘う／ヒロインを選んでいるときは「〇〇に会いに行って」とお願い
    /// ・右ボタンを押したまま動かすのは、今までどおりカメラを回す
    /// </summary>
    public partial class SimHud
    {
        SimCharacter menuChar;      // 右クリックしたキャラ（null なら閉じている）
        SimCharacter menuSelected;  // 右クリックしたときに選んでいたキャラ
        Vector2 menuPos;
        bool menuGift;              // プレゼントの品物を出している
        Vector2 rDownPos;
        float rDownTime;
        bool rDown;

        const float MenuW = 300f, MenuItemH = 34f;

        struct MenuItem { public string label; public System.Action act; public bool enabled; }

        Rect MenuRect
        {
            get
            {
                int n = Mathf.Max(1, BuildMenu().Count);
                return new Rect(menuPos.x, menuPos.y, MenuW, 36f + n * (MenuItemH + 4f) + 6f);
            }
        }

        bool MenuOpen { get { return menuChar != null; } }

        void CloseMenu() { menuChar = null; menuSelected = null; menuGift = false; }

        /// <summary>右ボタン：押してすぐ離したら（ほとんど動かさずに）右クリック。Update から呼ぶ。</summary>
        void UpdateRightClick(Mouse mouse)
        {
            if (mouse == null) return;
            Vector2 mp = mouse.position.ReadValue();
            if (mouse.rightButton.wasPressedThisFrame) { rDown = true; rDownPos = mp; rDownTime = Time.unscaledTime; }
            if (!mouse.rightButton.wasReleasedThisFrame || !rDown) return;
            rDown = false;
            if ((mp - rDownPos).magnitude > 8f || Time.unscaledTime - rDownTime > 0.45f) return; // カメラを回した
            Vector2 v = new Vector2(mp.x / Scale, (Screen.height - mp.y) / Scale);
            if (MenuOpen && MenuRect.Contains(v)) return;
            if (OverHud(v)) { CloseMenu(); return; }
            SimCharacter hit = null;
            Ray ray = world.mainCamera.ScreenPointToRay(new Vector3(mp.x, mp.y, 0f));
            RaycastHit info;
            if (Physics.Raycast(ray, out info, 300f)) hit = info.collider.GetComponentInParent<SimCharacter>();
            if (F == null) { CloseMenu(); return; }
            if (hit == null)
            {
                // 何もない所（床）を右クリック：篤がそこへ移動（famuru 2026-10-25）
                CloseMenu();
                Vector3 p;
                if (FloorPoint(ray, out p)) F.OrderMove(p);
                return;
            }
            menuChar = hit;
            menuSelected = selected;
            menuGift = false;
            float vw = Screen.width / Scale, vh = Screen.height / Scale;
            menuPos = v + new Vector2(8f, 8f);
            var r = MenuRect;
            if (r.xMax > vw - 4f) menuPos.x = vw - 4f - r.width;
            if (r.yMax > vh - 4f) menuPos.y = Mathf.Max(4f, vh - 4f - r.height);
        }

        List<MenuItem> BuildMenu()
        {
            var items = new List<MenuItem>();
            var f = F;
            var c = menuChar;
            if (f == null || c == null) return items;
            if (menuGift)
            {
                var pc = Player;
                if (pc != null)
                    foreach (var d in ItemBook.All)
                    {
                        int n = pc.Count(d.kind);
                        if (n <= 0) continue;
                        var dk = d;
                        items.Add(new MenuItem
                        {
                            label = d.name + "×" + n + (d.furniture && RoomDecor.Liked(c.profile, d.kind) ? " ♪" : ""),
                            enabled = pc.CanUseToday(d.kind),
                            act = () => { if (pc.Take(dk.kind)) { world.Log("篤への指示：" + c.displayName + "に「" + dk.name + "」を渡す"); f.OrderGive(c, dk.kind); } CloseMenu(); }
                        });
                    }
                if (items.Count == 0) items.Add(new MenuItem { label = "持ち物なし（購買部で買える）", enabled = false });
                items.Add(new MenuItem { label = "← もどる", enabled = true, act = () => menuGift = false });
                return items;
            }
            // 篤を右クリック：選んでいるヒロインと話す・誘う
            SimCharacter girl = c.IsTarget ? (menuSelected != null && !menuSelected.IsTarget ? menuSelected : null) : c;
            if (girl != null)
            {
                var g = girl;
                items.Add(new MenuItem { label = Story.Pending.Contains(g.letter) ? g.displayName + "に話しかける ★" : g.displayName + "に話しかける", enabled = true, act = () => { f.OrderTalk(g); world.Log("篤への指示：「" + g.displayName + "と話す」"); CloseMenu(); } });
                if (!c.IsTarget) items.Add(new MenuItem { label = "プレゼントする ▶", enabled = true, act = () => menuGift = true });
                items.Add(new MenuItem { label = g.displayName + "をエッチに誘う", enabled = !g.IsSick, act = () => { f.OrderInvite(g); world.Log("篤への指示：" + g.displayName + "をエッチに誘う"); CloseMenu(); } });
            }
            // ヒロインを選んでいるときに別のキャラを右クリック：篤が「〇〇に会いに行って」とお願いしに行く
            var s = menuSelected;
            if (s != null && !s.IsTarget && s != c && !c.IsTarget)
            {
                items.Add(new MenuItem { label = s.displayName + "に「" + c.displayName + "に会いに行って」とお願い", enabled = true, act = () => { world.Log("篤への指示：" + s.displayName + "に「" + c.displayName + "に会いに行って」とお願いする"); s.Order(c); CloseMenu(); } });
            }
            items.Add(new MenuItem { label = "閉じる", enabled = true, act = CloseMenu });
            return items;
        }

        void DrawMenu()
        {
            if (!MenuOpen) return;
            if (!menuChar.Present) { CloseMenu(); return; }
            var items = BuildMenu();
            var r = MenuRect;
            GUI.Box(r, GUIContent.none, panel);
            GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, 26), menuGift ? menuChar.displayName + "に渡す物" : menuChar.displayName + "（" + menuChar.StateName() + "）", small);
            float y = r.y + 36f;
            foreach (var it in items)
            {
                GUI.enabled = it.enabled;
                if (GUI.Button(new Rect(r.x + 8, y, r.width - 16, MenuItemH), it.label, smallButton) && it.act != null) it.act();
                GUI.enabled = true;
                y += MenuItemH + 4f;
            }
        }
    }
}
