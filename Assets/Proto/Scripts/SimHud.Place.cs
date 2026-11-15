using UnityEngine;
using UnityEngine.InputSystem;

namespace LifeSimProto
{
    /// <summary>
    /// 設置の仕組み 段1 の画面（2026-11-15。Placement.cs）。
    /// ・置く場面：品物の形がマウスについてくる。床の枠が緑なら置ける、赤なら置けない（理由を下の欄に出す）
    ///   左クリックで置く・右クリック（ドラッグしない）か「回す」で90度ずつ回す・Esc か「やめる」で終わる
    /// ・置いた物を左クリック：右上に小さな欄（動かす・回す・しまう）
    /// ・購買部の「置く家具」の棚で買う。篤の「持ち物」から「置く」
    /// </summary>
    public partial class SimHud
    {
        PlaceBook.Def placing;
        int placeYaw;
        Vector3 placePos;
        bool placeHasPos;
        string placeWhy;
        GameObject placeGhost, placeFrame;
        PlaceBook.PlacedItem placeMovingFrom;   // 動かしている途中の、もとの置き場所（やめたら戻す）
        Vector2 placeRightDown; bool placeRightHeld;
        PlacedMarker pickedPlaced;
        static Material placeOkMat, placeNgMat;
        bool shopPlaceTab;
        bool placeFrozen;   // 試験用（命令 place ghost）：マウスでなく決めた所に形を出す
        Vector2 shopPlaceScroll;

        float VH { get { return Screen.height / Scale; } }
        Rect PlaceBar { get { return new Rect(VW / 2f - 340f, VH - 170f, 680f, 104f); } }
        Rect PlacedPanel { get { return new Rect(VW - 12f - 330f, 110f, 330f, 116f); } }
        public bool Placing { get { return placing != null; } }

        bool OverPlaceUi(Vector2 v)
        {
            if (placing != null && PlaceBar.Contains(v)) return true;
            if (pickedPlaced != null && PlacedPanel.Contains(v)) return true;
            return false;
        }

        public void BeginPlace(PlaceBook.Def d, PlaceBook.PlacedItem movingFrom = null)
        {
            EndPlace(false);
            placing = d;
            placeMovingFrom = movingFrom;
            placeYaw = movingFrom != null ? movingFrom.yaw : 0;
            placeHasPos = false;
            placeWhy = null;
            pickedPlaced = null;
            shopOpen = false;
            Select(null);
            var pf = Resources.Load<GameObject>("Furniture/" + d.model);
            if (pf != null)
            {
                placeGhost = Instantiate(pf);
                placeGhost.name = "PlaceGhost";
                foreach (var c in placeGhost.GetComponentsInChildren<Collider>()) Destroy(c);
                foreach (var r in placeGhost.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                placeGhost.SetActive(false);
            }
            placeFrame = new GameObject("PlaceFrame");
            for (int i = 0; i < 4; i++)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(bar.GetComponent<Collider>());
                bar.transform.SetParent(placeFrame.transform, false);
                bar.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            placeFrame.SetActive(false);
            world.Log("置く場所を選ぶ：「" + d.name + "」（左クリックで置く・右クリックで回す・Esc でやめる）");
        }

        /// <summary>置く場面を終える。cancel なら、動かしている途中の物をもとの所へ戻す。</summary>
        void EndPlace(bool cancel)
        {
            if (cancel && placeMovingFrom != null && placing != null)
            {
                var m = placeMovingFrom;
                if (world.Place(m.name, new Vector3(m.x, m.y, m.z), m.yaw) != null) world.Log("「" + m.name + "」をもとの所に戻せなかった（持ち物に入れた）");
            }
            placing = null;
            placeMovingFrom = null;
            placeFrozen = false;
            if (placeGhost != null) Destroy(placeGhost);
            if (placeFrame != null) Destroy(placeFrame);
            placeGhost = null; placeFrame = null;
        }

        static Material FrameMat(bool ok)
        {
            if (placeOkMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                placeOkMat = new Material(sh) { name = "PlaceOk" };
                placeNgMat = new Material(sh) { name = "PlaceNg" };
                placeOkMat.SetColor("_BaseColor", new Color(0.35f, 1f, 0.45f));
                placeNgMat.SetColor("_BaseColor", new Color(1f, 0.3f, 0.3f));
            }
            return ok ? placeOkMat : placeNgMat;
        }

        /// <summary>Update から：置く場面のあいだ、マウスとキーを全部ここで使う。</summary>
        void UpdatePlacing(Mouse mouse, Keyboard kb)
        {
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { EndPlace(true); return; }
            if (mouse == null) return;
            Vector2 mp = mouse.position.ReadValue();
            Vector2 v = new Vector2(mp.x / Scale, (Screen.height - mp.y) / Scale);
            // 右クリック（ドラッグしない）で回す。ドラッグはカメラを回す
            if (mouse.rightButton.wasPressedThisFrame) { placeRightDown = mp; placeRightHeld = true; }
            if (placeRightHeld && mouse.rightButton.wasReleasedThisFrame)
            {
                placeRightHeld = false;
                if ((mp - placeRightDown).magnitude < 6f) placeYaw = (placeYaw + 90) % 360;
            }
            if (!OverHud(v) && !placeFrozen)
            {
                Ray ray = world.mainCamera.ScreenPointToRay(new Vector3(mp.x, mp.y, 0f));
                int f = world.ViewFloor >= 0 ? world.ViewFloor : (F != null ? LoungeWorld.FloorOf(F.transform.position.y) : 0);
                f = Mathf.Clamp(f, 0, 3);
                float fy = LoungeWorld.FloorY(f);
                if (Mathf.Abs(ray.direction.y) > 1e-4f)
                {
                    float t = (fy - ray.origin.y) / ray.direction.y;
                    if (t > 0f)
                    {
                        Vector3 q = ray.origin + ray.direction * t;
                        q.x = Mathf.Round(q.x * 8f) / 8f;
                        q.z = Mathf.Round(q.z * 8f) / 8f;
                        q.y = fy;
                        placePos = q;
                        placeHasPos = true;
                    }
                }
            }
            if (placeHasPos)
            {
                AreaInfo area;
                placeWhy = world.CheckPlace(placing, placePos, placeYaw, out area);
                ShowGhost(placeWhy == null);
                if (mouse.leftButton.wasPressedThisFrame && !OverHud(v))
                {
                    if (placeWhy != null) { world.Log("置けない：" + placeWhy); return; }
                    var d = placing;
                    string err = world.Place(d.name, placePos, placeYaw);
                    if (err != null) { world.Log("置けない：" + err); return; }
                    placeMovingFrom = null;
                    if (PlaceBook.Count(d.name) <= 0) EndPlace(false);   // まだ持っていれば続けて置ける
                }
            }
        }

        void ShowGhost(bool ok)
        {
            Rect local; float h;
            if (!world.PlaceFootprint(placing.model, out local, out h)) return;
            if (placeGhost != null)
            {
                placeGhost.SetActive(true);
                var pf = Resources.Load<GameObject>("Furniture/" + placing.model);
                placeGhost.transform.position = placePos;
                placeGhost.transform.rotation = Quaternion.Euler(0f, placeYaw, 0f) * (pf != null ? pf.transform.localRotation : Quaternion.identity);
            }
            if (placeFrame != null)
            {
                placeFrame.SetActive(true);
                Rect fp = LoungeWorld.TurnedFootprint(local, placeYaw, placePos);
                float y = placePos.y + 0.03f, t = 0.06f;
                var mat = FrameMat(ok);
                var bars = placeFrame.transform;
                SetBar(bars.GetChild(0), new Vector3(fp.center.x, y, fp.yMin), new Vector3(fp.width + t, 0.02f, t), mat);
                SetBar(bars.GetChild(1), new Vector3(fp.center.x, y, fp.yMax), new Vector3(fp.width + t, 0.02f, t), mat);
                SetBar(bars.GetChild(2), new Vector3(fp.xMin, y, fp.center.y), new Vector3(t, 0.02f, fp.height + t), mat);
                SetBar(bars.GetChild(3), new Vector3(fp.xMax, y, fp.center.y), new Vector3(t, 0.02f, fp.height + t), mat);
            }
        }

        static void SetBar(Transform b, Vector3 c, Vector3 s, Material m)
        {
            b.position = c; b.localScale = s; b.rotation = Quaternion.identity;
            var r = b.GetComponent<Renderer>();
            if (r.sharedMaterial != m) r.sharedMaterial = m;
        }

        /// <summary>試験用（命令 place ghost）：決めた所に置く場面の形を出す（マウスは使わない）。</summary>
        public string DebugGhost(PlaceBook.Def d, Vector3 pos, int yaw)
        {
            BeginPlace(d);
            placeFrozen = true;
            placePos = pos; placeYaw = yaw; placeHasPos = true;
            AreaInfo area;
            placeWhy = world.CheckPlace(d, pos, yaw, out area);
            return placeWhy;
        }

        public void DebugEndPlace() { EndPlace(false); }

        /// <summary>置いた物をクリックしたか（キャラに当たらなかったとき）。</summary>
        bool TryPickPlaced(Ray ray)
        {
            RaycastHit h;
            if (!Physics.Raycast(ray, out h, 300f, 1 << 2, QueryTriggerInteraction.Ignore)) { pickedPlaced = null; return false; }
            var m = h.collider.GetComponentInParent<PlacedMarker>();
            pickedPlaced = m;
            return m != null;
        }

        void DrawPlacing()
        {
            if (placing != null)
            {
                Rect r = PlaceBar;
                Deco(r, GUIContent.none, panel);
                GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 28, 26), "「" + placing.name + "」を置く（持っている数 " + PlaceBook.Count(placing.name) + "）　左クリックで置く・右クリックで回す", small);
                string msg = !placeHasPos ? "置きたい所にマウスを動かす" : (placeWhy == null ? "置ける" : "置けない：" + placeWhy);
                var old = GUI.color;
                GUI.color = placeHasPos && placeWhy == null ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 0.6f, 0.6f);
                GUI.Label(new Rect(r.x + 14, r.y + 36, r.width - 28, 24), msg, small);
                GUI.color = old;
                if (GUI.Button(new Rect(r.x + 14, r.y + 64, 130, 32), "左に回す", smallButton)) placeYaw = (placeYaw + 270) % 360;
                if (GUI.Button(new Rect(r.x + 150, r.y + 64, 130, 32), "右に回す", smallButton)) placeYaw = (placeYaw + 90) % 360;
                if (GUI.Button(new Rect(r.xMax - 144, r.y + 64, 130, 32), "やめる", smallButton)) EndPlace(true);
            }
            if (pickedPlaced != null)
            {
                if (pickedPlaced.item == null || !PlaceBook.Placed.Contains(pickedPlaced.item)) { pickedPlaced = null; return; }
                var it = pickedPlaced.item;
                Rect r = PlacedPanel;
                Deco(r, GUIContent.none, panel);
                GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 28, 26), it.name + "（" + it.area + "）", small);
                var d = PlaceBook.Get(it.name);
                if (d != null) GUI.Label(new Rect(r.x + 14, r.y + 34, r.width - 28, 34), d.FullDesc, tiny);
                float bx = r.x + 14;
                if (GUI.Button(new Rect(bx, r.y + 70, 72, 32), "動かす", smallButton) && d != null)
                {
                    var from = it;
                    world.Unplace(it);
                    BeginPlace(d, from);
                    return;
                }
                bx += 78;
                if (GUI.Button(new Rect(bx, r.y + 70, 72, 32), "回す", smallButton))
                {
                    world.Unplace(it);
                    string err = world.Place(it.name, new Vector3(it.x, it.y, it.z), it.yaw + 90);
                    if (err != null) { world.Log("回せない：" + err); world.Place(it.name, new Vector3(it.x, it.y, it.z), it.yaw); }
                    pickedPlaced = null;
                    foreach (var m in world.PlacedObjects) if (m != null && m.item != null && m.item.name == it.name && Mathf.Abs(m.item.x - it.x) < 0.01f && Mathf.Abs(m.item.z - it.z) < 0.01f) pickedPlaced = m;
                    return;
                }
                bx += 78;
                if (GUI.Button(new Rect(bx, r.y + 70, 72, 32), "しまう", smallButton)) { world.Unplace(it); world.Log("「" + it.name + "」をしまった（持ち物に戻した）"); pickedPlaced = null; return; }
                bx += 78;
                if (GUI.Button(new Rect(bx, r.y + 70, 72, 32), "閉じる", smallButton)) pickedPlaced = null;
            }
        }

        /// <summary>購買部の「置く家具」の棚。</summary>
        void DrawShopPlace(Rect r)
        {
            var pc = Player;
            float top = r.y + 86;
            Rect view = new Rect(r.x + 8, top, r.width - 16, r.yMax - top - 8);
            int n = 0;
            foreach (var d in PlaceBook.All) if (d.InShop) n++;
            Rect content = new Rect(0, 0, view.width - 20, Mathf.Max(view.height, n * 44f + 30f));
            shopPlaceScroll = GUI.BeginScrollView(view, shopPlaceScroll, content);
            float y = 0;
            GUI.Label(new Rect(6, y, content.width, 24), "買ったら篤の「持ち物」から「置く」。置いた物は周が変わっても残る", tiny); y += 26;
            foreach (var d in PlaceBook.All)
            {
                if (!d.InShop) continue;
                GUI.Label(new Rect(6, y, 320, 24), d.name + "（" + d.price + "）", small);
                GUI.Label(new Rect(6, y + 20, content.width - 240, 20), d.FullDesc, tiny);
                GUI.Label(new Rect(content.width - 230, y + 2, 100, 24), "所持 " + PlaceBook.Count(d.name), small);
                GUI.enabled = pc != null && pc.money >= d.price;
                if (GUI.Button(new Rect(content.width - 110, y + 2, 96, 34), "買う", button))
                {
                    pc.money -= d.price;
                    PlaceBook.Give(d.name);
                    world.Log("購買部で「" + d.name + "」を買った（銀貨−" + d.price + "）");
                }
                GUI.enabled = true;
                y += 44;
            }
            GUI.EndScrollView();
        }

        /// <summary>篤の「持ち物」の欄の下：置く物。</summary>
        void PlaceItemsGui(float w, ref float y)
        {
            y += 6;
            GUI.Label(new Rect(4, y, w, 22), "置く物（家具・道具。置いた物 " + PlaceBook.Placed.Count + "こ）", small); y += 24;
            float x = 4;
            bool any = false;
            foreach (var d in PlaceBook.All)
            {
                int n = PlaceBook.Count(d.name);
                if (n <= 0) continue;
                any = true;
                if (GUI.Button(new Rect(x, y, 170, 30), d.name + "×" + n + " 置く", smallButton)) BeginPlace(d);
                x += 178;
                if (x > w - 170) { x = 4; y += 34; }
            }
            if (x > 4) y += 34;
            if (!any) { GUI.Label(new Rect(4, y, w, 22), "なし（購買部の「置く家具」の棚で買える）", tiny); y += 24; }
        }
    }
}
