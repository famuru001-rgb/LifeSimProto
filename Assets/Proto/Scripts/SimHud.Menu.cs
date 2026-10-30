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
        // エッチに誘う：場所と内容を選ぶ（2026-10-31 famuru「誘う時に部屋と内容を決められるようにしたい」）
        int menuInvite;             // 0 なし・1 場所を選ぶ・2 内容を選ぶ
        int invitePage;
        HPlan invitePlan;
        List<HPlaceInfo> invitePlaces;
        const int InvitePerPage = 9;
        Vector2 rDownPos;
        float rDownTime;
        bool rDown;

        const float MenuItemH = 34f;
        float MenuW { get { return menuInvite > 0 ? 400f : 300f; } }

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

        void CloseMenu() { menuChar = null; menuSelected = null; menuGift = false; menuInvite = 0; invitePlaces = null; }

        /// <summary>誘う相手（右クリックしたヒロイン、または篤を右クリックしたときに選んでいた子）。</summary>
        SimCharacter InviteTarget { get { var c = menuChar; if (c == null) return null; return c.IsTarget ? (menuSelected != null && !menuSelected.IsTarget ? menuSelected : null) : c; } }

        void OpenInvite()
        {
            var g = InviteTarget; var f = F;
            if (g == null || f == null) return;
            invitePlan = new HPlan();
            invitePlaces = f.InvitePlaces(g);
            invitePage = 0;
            menuInvite = 1;
            ClampMenu();
        }

        void ClampMenu()
        {
            float vw = Screen.width / Scale, vh = Screen.height / Scale;
            var r = MenuRect;
            if (r.xMax > vw - 4f) menuPos.x = Mathf.Max(4f, vw - 4f - r.width);
            if (r.yMax > vh - 4f) menuPos.y = Mathf.Max(4f, vh - 4f - r.height);
        }

        static string Next(string cur, string[] list, bool withNull)
        {
            int i = cur == null ? -1 : System.Array.IndexOf(list, cur);
            i++;
            if (i >= list.Length) return withNull ? null : list[0];
            return list[i];
        }

        /// <summary>場所と内容を選ぶ一覧。</summary>
        List<MenuItem> BuildInviteMenu()
        {
            var items = new List<MenuItem>();
            var g = InviteTarget; var f = F;
            if (g == null || f == null || invitePlan == null) { menuInvite = 0; return items; }
            var pl = invitePlan;
            if (menuInvite == 1)
            {
                items.Add(new MenuItem { label = "全部おまかせですぐ誘う", enabled = !g.InPrivate, act = () => { f.OrderInvite(g); world.Log("篤への指示：" + g.displayName + "をエッチに誘う"); CloseMenu(); } });
                items.Add(new MenuItem { label = "場所はおまかせ（内容を決める）", enabled = true, act = () => { pl.placeKey = null; pl.placeName = null; menuInvite = 2; ClampMenu(); } });
                var list = invitePlaces ?? new List<HPlaceInfo>();
                int pages = Mathf.Max(1, (list.Count + InvitePerPage - 1) / InvitePerPage);
                invitePage = Mathf.Clamp(invitePage, 0, pages - 1);
                for (int i = invitePage * InvitePerPage; i < Mathf.Min(list.Count, (invitePage + 1) * InvitePerPage); i++)
                {
                    var h = list[i];
                    items.Add(new MenuItem
                    {
                        label = h.name + "　" + Mathf.RoundToInt(h.dist) + "m" + (h.people > 0 ? "・" + h.people + "人いる" : ""),
                        enabled = true,
                        act = () => { pl.placeKey = h.key; pl.placeName = h.name; menuInvite = 2; ClampMenu(); }
                    });
                }
                if (pages > 1)
                {
                    int pg = invitePage;
                    items.Add(new MenuItem { label = "◀ 前へ（" + (pg + 1) + "／" + pages + "）", enabled = pg > 0, act = () => invitePage = pg - 1 });
                    items.Add(new MenuItem { label = "次へ ▶", enabled = pg < pages - 1, act = () => invitePage = pg + 1 });
                }
                items.Add(new MenuItem { label = "← もどる", enabled = true, act = () => { menuInvite = 0; } });
                return items;
            }
            // 内容
            items.Add(new MenuItem { label = "場所：" + (pl.placeName ?? "おまかせ") + "（変える）", enabled = true, act = () => { menuInvite = 1; ClampMenu(); } });
            items.Add(new MenuItem { label = "体位：" + (pl.pose ?? "おまかせ") + "　▶", enabled = pl.depth >= 2, act = () => pl.pose = Next(pl.pose, HPlan.Poses, true) });
            items.Add(new MenuItem { label = "どこまで：" + HPlan.DepthNames[pl.depth] + "　▶", enabled = true, act = () => pl.depth = (pl.depth + 2) % 3 });
            items.Add(new MenuItem { label = "人数：" + (pl.solo ? "二人きり" : "ほかの子も加わってよい") + "　▶", enabled = true, act = () => pl.solo = !pl.solo });
            items.Add(new MenuItem { label = "フィニッシュ：" + HPlan.FinishNames[pl.finish] + "　▶", enabled = pl.depth >= 2, act = () => pl.finish = (pl.finish + 1) % 3 });
            items.Add(new MenuItem
            {
                label = "この内容で" + g.displayName + "を誘う",
                enabled = !g.InPrivate && !g.IsSick,
                act = () => { f.OrderInvite(g, pl); world.Log("篤への指示：" + g.displayName + "をエッチに誘う（" + (pl.placeName ?? "場所はおまかせ") + "・" + pl.Summary() + "）"); CloseMenu(); }
            });
            items.Add(new MenuItem { label = "閉じる", enabled = true, act = CloseMenu });
            return items;
        }

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
            if (menuInvite > 0) return BuildInviteMenu();
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
            if (girl != null && girl.IsMob)
            {
                // 寮生以外の学生・先生：会話なし。エッチに誘うだけ（famuru 2026-10-30）
                var g = girl;
                items.Add(new MenuItem { label = g.displayName + "をエッチに誘う ▶", enabled = !g.InPrivate, act = OpenInvite });
            }
            else if (girl != null)
            {
                var g = girl;
                items.Add(new MenuItem { label = Story.Pending.Contains(g.letter) ? g.displayName + "に話しかける ★" : g.displayName + "に話しかける", enabled = true, act = () => { f.OrderTalk(g); world.Log("篤への指示：「" + g.displayName + "と話す」"); CloseMenu(); } });
                if (!c.IsTarget) items.Add(new MenuItem { label = "プレゼントする ▶", enabled = true, act = () => menuGift = true });
                items.Add(new MenuItem { label = g.displayName + "をエッチに誘う ▶", enabled = !g.IsSick, act = OpenInvite });
            }
            // ヒロインを選んでいるときに別のキャラを右クリック：篤が「〇〇に会いに行って」とお願いしに行く
            var s = menuSelected;
            if (s != null && !s.IsTarget && s != c && !c.IsTarget && !s.IsMob && !c.IsMob)
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
            string head = menuGift ? menuChar.displayName + "に渡す物" : menuChar.displayName + "（" + menuChar.StateName() + "）";
            if (menuInvite == 1) head = "どこでする？（近い順）";
            else if (menuInvite == 2) head = "どんなふうに？（押すと変わる）";
            GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, 26), head, small);
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
