using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 新しい場所6か所（依頼11の絵を採用：2026-10-21 famuru 決定）。
    /// 2026-10-25 寮の作り直しで場所を移した：台所は1階の奥（食堂の北）、図書室は3階の奥（南）、天文台は寮の屋上、
    /// 中庭（温室・工房の小屋）は寮の奥（食堂の裏口から出る）。
    /// 開く前は見えるが入れない（扉に鍵・鎖）。開く条件は Loop.cs の PlaceGates。開いた場所はループしても開いたまま。
    /// 開いた場所では、実技の授業（中庭：飛行術・剣術、工房：錬成、温室：薬草学）が見える。
    /// </summary>
    public partial class LoungeWorld
    {
        public const string Courtyard = "中庭", Greenhouse = "温室", Library = "図書室", Workshop = "工房", Observatory = "屋上の天文台", Kitchen = "台所";
        const float CY0 = 5.4f, CY1 = 23.2f;
        // 中庭・温室・工房は、前の位置で組み立ててから寮の奥へずらす（大きさは今のまま）。前の寮：談話室の奥行きの半分 5m
        const float OldHalfD = 5f;
        static readonly Vector3 YardOff = new Vector3(0f, 0f, 13f);          // 中庭・温室・工房：寮の奥（z 18 から先）

        class PlaceDoor
        {
            public string place;
            public int floor;
            public Rect nav;
            public GameObject locked, open;
        }

        readonly List<PlaceDoor> placeDoors = new List<PlaceDoor>();
        readonly Dictionary<string, List<Seat>> placeSeats = new Dictionary<string, List<Seat>>();
        readonly Dictionary<string, List<Poi>> placePois = new Dictionary<string, List<Poi>>();
        readonly HashSet<string> placesAdded = new HashSet<string>();
        static readonly Dictionary<string, Vector3> PlaceFocus = new Dictionary<string, Vector3>
        {
            { Courtyard, new Vector3(2.8f, 0f, 14.5f) + YardOff }, { Greenhouse, new Vector3(-7.5f, 0f, 19.5f) + YardOff }, { Library, new Vector3(-8.5f, Floor3Y, 13.5f) },
            { Workshop, new Vector3(8.5f, 0f, 19.8f) + YardOff }, { Observatory, new Vector3(0f, RoofY, -0.6f) }, { Kitchen, new Vector3(14.0f, 0f, 14.6f) },
        };
        int appliedPlaceCount = -1;
        int seatMark, poiMark;
        Material grassMat, glassMat, brassMat, terraMat, leafMat, leafLightMat, lavenderMat, greenLampMat, crystalMat, crystalBlueMat, slateMat, paperMat, rugGoldMat;

        public bool PlaceOpen(string place) { return place == Kitchen || Loop.OpenedPlaces.Contains(place); } // 台所は最初から開いている（famuru 2026-10-26）
        public static Vector3 FocusOf(string place) { Vector3 v; return PlaceFocus.TryGetValue(place, out v) ? v : Vector3.zero; }

        /// <summary>実技の授業が見える場所（開いていれば）。</summary>
        public static string ClassPlace(int subject)
        {
            switch (subject)
            {
                case Academics.Flight: case Academics.Sword: return Courtyard; // 剣術はのちに本館の体育館へ
                case Academics.Alchemy: return Workshop;
                case Academics.Herb: return Greenhouse;
                default: return null;
            }
        }

        // ================= 組み立て =================
        void CreatePlaceMaterials()
        {
            if (grassMat != null) return; // 学校の本館（先に作る）で、もう作ってある
            grassMat = ProtoArt.Mat(new Color(0.55f, 0.72f, 0.38f), ProtoArt.Fabric, new Vector2(14f, 10f), 0.05f);
            var tg = TileTex("Grass"); if (tg != null) { grassMat = ProtoArt.Mat(Color.white, tg, Vector2.one, 0.05f); tileMeters[grassMat] = 4f; } // 本番の模様
            glassMat = ProtoArt.Mat(new Color(0.78f, 0.9f, 0.92f), null, null, 0.95f);
            brassMat = ProtoArt.Mat(new Color(0.85f, 0.65f, 0.3f), null, null, 0.7f);
            terraMat = ProtoArt.Mat(new Color(0.78f, 0.45f, 0.3f), null, null, 0.2f);
            leafMat = ProtoArt.Mat(new Color(0.36f, 0.55f, 0.25f), ProtoArt.Fabric, new Vector2(2f, 2f), 0.1f);
            leafLightMat = ProtoArt.Mat(new Color(0.62f, 0.75f, 0.35f), ProtoArt.Fabric, new Vector2(2f, 2f), 0.1f);
            lavenderMat = ProtoArt.Mat(new Color(0.62f, 0.52f, 0.82f), null, null, 0.1f);
            greenLampMat = ProtoArt.Glow(new Color(0.45f, 0.9f, 0.45f), 1.6f);
            crystalMat = ProtoArt.Glow(new Color(0.75f, 0.45f, 1f), 1.8f);
            crystalBlueMat = ProtoArt.Glow(new Color(0.45f, 0.75f, 1f), 1.8f);
            slateMat = ProtoArt.Mat(new Color(0.42f, 0.44f, 0.5f), ProtoArt.Flagstone, new Vector2(2f, 2f), 0.2f);
            paperMat = ProtoArt.Mat(new Color(0.95f, 0.9f, 0.78f));
            rugGoldMat = ProtoArt.Mat(new Color(0.8f, 0.62f, 0.3f), ProtoArt.Fabric, new Vector2(4f, 4f), 0.05f);
        }

        void BuildPlaces()
        {
            CreatePlaceMaterials();
            // 区画は小さい場所から先に（AreaAt は最初に当たった区画を返す）
            BuildLibrary();
            Shifted(BuildGreenhouse, YardOff);
            Shifted(BuildWorkshop, YardOff);
            BuildKitchen();
            Shifted(BuildCourtyard, YardOff);
            BuildObservatory();
        }

        /// <summary>場所を前の位置で組み立ててから、作った物・壁・席・見どころ・区画・名札・扉・ふさがる所をまとめて off だけずらす。</summary>
        void Shifted(System.Action build, Vector3 off)
        {
            int c0 = root.childCount, w0 = walls.Count, b0 = blockers[0].Count, s0 = seats.Count, p0 = pois.Count;
            int a0 = areaList.Count, l0 = worldLabels.Count, d0 = placeDoors.Count;
            var keys0 = new HashSet<string>(placeSeats.Keys);
            build();
            for (int i = c0; i < root.childCount; i++) root.GetChild(i).position += off;
            Vector2 o2 = new Vector2(off.x, off.z);
            for (int i = w0; i < walls.Count; i++) { walls[i].a += o2; walls[i].b += o2; }
            for (int i = b0; i < blockers[0].Count; i++) { var r = blockers[0][i]; r.position += o2; blockers[0][i] = r; }
            for (int i = s0; i < seats.Count; i++) ShiftSeat(seats[i], off);
            for (int i = p0; i < pois.Count; i++) ShiftPoi(pois[i], off);
            foreach (var kv in placeSeats) if (!keys0.Contains(kv.Key)) foreach (var st in kv.Value) ShiftSeat(st, off);
            foreach (var kv in placePois) if (!keys0.Contains(kv.Key)) foreach (var pp in kv.Value) ShiftPoi(pp, off);
            for (int i = a0; i < areaList.Count; i++) { var r = areaList[i].rect; r.position += o2; areaList[i].rect = r; }
            for (int i = l0; i < worldLabels.Count; i++) worldLabels[i] = new KeyValuePair<Vector3, string>(worldLabels[i].Key + off, worldLabels[i].Value);
            for (int i = d0; i < placeDoors.Count; i++) { var r = placeDoors[i].nav; r.position += o2; placeDoors[i].nav = r; }
        }

        static void ShiftSeat(Seat st, Vector3 off) { st.position += off; st.approach += off; }

        static void ShiftPoi(Poi p, Vector3 off)
        {
            p.lookAt += off;
            if (p.slots != null) for (int i = 0; i < p.slots.Length; i++) p.slots[i] += off;
        }

        static Rect Off(Rect r, Vector3 off) { r.position += new Vector2(off.x, off.z); return r; }

        void BeginPlace() { seatMark = seats.Count; poiMark = pois.Count; }

        /// <summary>この場所の席と見どころを、開くまでよけておく。</summary>
        void EndPlace(string place)
        {
            var sl = seats.GetRange(seatMark, seats.Count - seatMark);
            seats.RemoveRange(seatMark, seats.Count - seatMark);
            var pl = pois.GetRange(poiMark, pois.Count - poiMark);
            pois.RemoveRange(poiMark, pois.Count - poiMark);
            foreach (var p in pl) p.place = place;
            placeSeats[place] = sl;
            placePois[place] = pl;
        }

        /// <summary>開いた場所の扉を開け、席と見どころを使えるようにする（閉まっている場所の扉は通れない）。</summary>
        void ApplyPlaces()
        {
            appliedPlaceCount = Loop.OpenedPlaces.Count;
            foreach (var d in placeDoors)
            {
                bool open = PlaceOpen(d.place);
                var nav = Nav(d.floor);
                if (open) nav.Open(d.nav); else nav.Block(d.nav);
                if (d.locked != null) d.locked.SetActive(!open);
                if (d.open != null) d.open.SetActive(open);
            }
            foreach (var kv in placeSeats)
            {
                if (!PlaceOpen(kv.Key) || placesAdded.Contains(kv.Key)) continue;
                placesAdded.Add(kv.Key);
                seats.AddRange(kv.Value);
                List<Poi> pl;
                if (placePois.TryGetValue(kv.Key, out pl)) pois.AddRange(pl);
            }
        }

        void UpdatePlaces()
        {
            if (Loop.OpenedPlaces.Count != appliedPlaceCount) ApplyPlaces();
        }

        /// <summary>場所が開いたときの扉。locked：鍵と鎖の扉、open：開いた扉。</summary>
        void PlaceDoorAt(string place, int floor, Vector3 c, bool alongX, float width, float height, Rect nav, Material doorMat, bool hingeHigh = false)
        {
            float hs = hingeHigh ? -1f : 1f; // hingeHigh：ちょうつがいを +側に（開く向きを左右逆に）
            float y = FloorY(floor);
            var locked = new GameObject("Door_Locked_" + place).transform;
            locked.SetParent(root, false);
            Vector3 size = alongX ? new Vector3(width, height, 0.1f) : new Vector3(0.1f, height, width);
            string lmName = ArchOr("Furn_LockedDoor");
            bool lm = PlaceFurn(lmName, locked, new Vector3(c.x, y, c.z), alongX ? 0f : 90f, false, DoorScale(lmName, width, height)) != null; // 本番の部品（鎖と錠前つき）
            if (!lm) {
            Box("Door_Panel", new Vector3(c.x, y + height / 2f, c.z), size, doorMat, locked);
            // 鉄の帯と鎖・錠前
            for (int i = 0; i < 2; i++)
            {
                float by = y + height * (0.25f + 0.5f * i);
                Box("Door_Band", new Vector3(c.x, by, c.z), alongX ? new Vector3(width, 0.08f, 0.14f) : new Vector3(0.14f, 0.08f, width), iron, locked);
            }
            float tilt = Mathf.Atan2(height * 0.5f, width) * Mathf.Rad2Deg;
            for (int k = -1; k <= 1; k += 2)
            {
                var ch = Box("Door_Chain", new Vector3(c.x, y + height * 0.5f, c.z), alongX ? new Vector3(width * 1.05f, 0.05f, 0.18f) : new Vector3(0.18f, 0.05f, width * 1.05f), iron, locked);
                ch.transform.localRotation = alongX ? Quaternion.Euler(0f, 0f, tilt * k) : Quaternion.Euler(tilt * k, 0f, 0f);
            }
            Box("Door_Lock", new Vector3(c.x, y + height * 0.47f, c.z), new Vector3(0.22f, 0.26f, 0.22f), brassMat, locked);
            }
            // 開いた扉（壁ぎわに開いている）
            var open = new GameObject("Door_Open_" + place).transform;
            open.SetParent(root, false);
            Vector3 hinge = alongX ? new Vector3(c.x - hs * width / 2f, y, c.z) : new Vector3(c.x, y, c.z - hs * width / 2f);
            Vector3 off = alongX ? new Vector3(hs * 0.05f, height / 2f, -width / 2f) : new Vector3(-width / 2f, height / 2f, hs * 0.05f);
            string odName = ArchOr("Furn_Door");
            if (PlaceFurn(odName, open, hinge + off - Vector3.up * (height / 2f), alongX ? 90f : 0f, false, DoorScale(odName, width, height)) == null) // 開いた扉（壁ぎわ）
                Box("Door_PanelOpen", hinge + off, alongX ? new Vector3(0.1f, height, width) : new Vector3(width, height, 0.1f), doorMat, open);
            placeDoors.Add(new PlaceDoor { place = place, floor = floor, nav = nav, locked = locked.gameObject, open = open.gameObject });
        }

        // ---- 小物 ----
        void Tree(Vector3 p, float h)
        {
            if (PlaceFurn("Furn_Tree", null, p, (p.x * 37f + p.z * 11f) % 360f, false, Vector3.one * (h / 4f)) != null) { blockers[FloorOf(p.y)].Add(new Rect(p.x - 0.3f, p.z - 0.3f, 0.6f, 0.6f)); return; } // 本番の家具（高さ4m を合わせる）
            Cyl("Tree_Trunk", p + Vector3.up * (h * 0.3f), new Vector3(0.28f, h * 0.3f, 0.28f), Quaternion.identity, woodDark);
            Leaf(p + Vector3.up * (h * 0.72f), h * 0.55f, leafMat);
            Leaf(p + new Vector3(0.35f, h * 0.88f, 0.2f), h * 0.4f, leafLightMat);
            Leaf(p + new Vector3(-0.3f, h * 0.82f, -0.25f), h * 0.38f, leafMat);
            blockers[0].Add(new Rect(p.x - 0.3f, p.z - 0.3f, 0.6f, 0.6f));
        }

        GameObject Leaf(Vector3 p, float d, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Leaf";
            go.transform.SetParent(root, false);
            go.transform.localPosition = p;
            go.transform.localScale = new Vector3(d, d * 0.85f, d);
            go.GetComponent<Renderer>().sharedMaterial = m;
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        void Pot(Vector3 p, float s, Material plant)
        {
            if (PlaceFurn("Furn_FlowerPot", null, p, 0f, false, Vector3.one * (s / 0.5f)) != null) return; // 本番の家具（直径0.5m を大きさに合わせる）
            Cyl("Pot", p + Vector3.up * (s * 0.5f), new Vector3(s, s * 0.5f, s), Quaternion.identity, terraMat);
            Leaf(p + Vector3.up * (s * 1.2f), s * 1.2f, plant);
        }

        void LampPost(Vector3 p)
        {
            if (PlaceFurn("Furn_LampPost", null, p, 0f) == null)
            {
            Cyl("LampPost", p + Vector3.up * 1.1f, new Vector3(0.1f, 1.1f, 0.1f), Quaternion.identity, iron);
            Box("LampPost_Head", p + Vector3.up * 2.3f, new Vector3(0.28f, 0.34f, 0.28f), glowLamp);
            }
            Lamp(p + Vector3.up * 2.2f, 2.2f, 7f);
            blockers[0].Add(new Rect(p.x - 0.15f, p.z - 0.15f, 0.3f, 0.3f));
        }

        void SimpleBench(string label, string area, Vector3 c, float yaw, Material cushion)
        {
            var t = new GameObject(label).transform;
            t.SetParent(root, false);
            t.localPosition = c;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (PlaceFurn(cushion != null ? "Furn_BenchBackCushion" : "Furn_BenchBack", t, Vector3.zero, 0f) == null)
            {
            Box("Bench_Seat", new Vector3(0f, 0.42f, 0f), new Vector3(1.5f, 0.08f, 0.45f), woodMid, t);
            Box("Bench_Back", new Vector3(0f, 0.72f, -0.2f), new Vector3(1.5f, 0.5f, 0.06f), woodMid, t);
            Box("Bench_LegL", new Vector3(-0.65f, 0.2f, 0f), new Vector3(0.08f, 0.4f, 0.4f), woodDark, t);
            Box("Bench_LegR", new Vector3(0.65f, 0.2f, 0f), new Vector3(0.08f, 0.4f, 0.4f), woodDark, t);
            if (cushion != null) Box("Bench_Cushion", new Vector3(0f, 0.48f, 0.02f), new Vector3(1.35f, 0.06f, 0.4f), cushion, t);
            }
            AddSeat(t, label, area, new Vector3(-0.35f, 0f, 0f), new Vector3(-0.35f, 0f, 0.7f));
            AddSeat(t, label, area, new Vector3(0.35f, 0f, 0f), new Vector3(0.35f, 0f, 0.7f));
            AddFootprint(t, 1.5f, 0.5f, c.y);
        }

        /// <summary>棚の中身（seed）に合う本番の家具。無いときは null（今までの箱）。</summary>
        static string ShelfModel(int seed)
        {
            switch (seed)
            {
                case 101: case 103: return "Furn_HerbShelf";   // 温室の薬草
                case 307: return "Furn_DishShelf";              // 台所の食器
                case 211: return "Furn_TreasureShelf";          // 工房の宝物
                case 213: return "Furn_StoreShelf";             // 工房の道具
                case 401: return "Furn_Bookshelf";              // 篤の部屋の本棚
            }
            return null;
        }

        void ShelfRow(Vector3 c, float yaw, float w, float h, int tiers, Material item, Material item2, int seed, string forceModel = null)
        {
            var t = new GameObject("Shelf").transform;
            t.SetParent(root, false);
            t.localPosition = c;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float d = 0.45f;
            // 本番の家具の棚（中身で4種。幅と高さに合わせて伸ばす。正面は −z）
            string sm = forceModel ?? ShelfModel(seed);
            float smh = sm == "Furn_HerbShelf" || sm == "Furn_MedicineShelf" ? 1.8f : (sm == "Furn_Bookshelf" ? 2.6f : (sm == "Furn_LowShelf" ? 0.9f : 2.0f));
            float smw = sm == "Furn_Bookshelf" ? 2.6f : (sm == "Furn_MedicineShelf" ? 1.2f : 2.0f);
            if (sm != null && Resources.Load<GameObject>("Furniture/" + sm) != null)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(w / smw)); float tw = w / n; // 長い棚は並べる
                for (int i = 0; i < n; i++) PlaceFurn(sm, t, new Vector3(-w / 2f + tw * (i + 0.5f), 0f, 0f), 180f, false, new Vector3(tw / smw, h / smh, 1f));
                Vector3 qa = t.TransformPoint(new Vector3(-w / 2f, 0f, -d / 2f)), qb = t.TransformPoint(new Vector3(w / 2f, 0f, d / 2f));
                blockers[FloorOf(c.y)].Add(Rect.MinMaxRect(Mathf.Min(qa.x, qb.x), Mathf.Min(qa.z, qb.z), Mathf.Max(qa.x, qb.x), Mathf.Max(qa.z, qb.z)));
                return;
            }
            Box("Shelf_Back", new Vector3(0f, h / 2f, 0.2f), new Vector3(w, h, 0.06f), woodDark, t);
            Box("Shelf_SideL", new Vector3(-w / 2f, h / 2f, 0f), new Vector3(0.07f, h, d), woodDark, t);
            Box("Shelf_SideR", new Vector3(w / 2f, h / 2f, 0f), new Vector3(0.07f, h, d), woodDark, t);
            var rnd = new System.Random(seed);
            for (int i = 0; i < tiers; i++)
            {
                float y = 0.1f + i * (h - 0.2f) / Mathf.Max(1, tiers - 1);
                Box("Shelf_Board", new Vector3(0f, y, 0f), new Vector3(w, 0.05f, d), woodMid, t);
                if (i == tiers - 1 && tiers > 1) break;
                for (float x = -w / 2f + 0.2f; x < w / 2f - 0.15f; x += 0.32f)
                {
                    if (rnd.NextDouble() < 0.2) continue;
                    float s = 0.14f + (float)rnd.NextDouble() * 0.08f;
                    var m = rnd.NextDouble() < 0.5 ? item : item2;
                    var go = Box("Shelf_Item", new Vector3(x, y + 0.03f + s / 2f, -0.02f), new Vector3(s, s, s), m, t);
                    if (rnd.NextDouble() < 0.5) go.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                }
            }
            Vector3 a = t.TransformPoint(new Vector3(-w / 2f, 0f, -d / 2f));
            Vector3 b = t.TransformPoint(new Vector3(w / 2f, 0f, d / 2f));
            var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
            blockers[FloorOf(c.y)].Add(r);
        }

        Poi ClassPoi(string label, int subject, Vector3 lookAt, float x0, float z0, float dx, float dz, int cols, int rows, float y = 0f)
        {
            var slots = new List<Vector3>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    slots.Add(new Vector3(x0 + c * dx, y, z0 + r * dz));
            var p = MakePoi(label, lookAt, slots.ToArray());
            p.subject = subject;
            p.classOnly = true;
            return p;
        }

        // ================= 図書室（3階の奥・南。x −17〜0、z 9〜18） =================
        void BuildLibrary()
        {
            BeginPlace();
            float x0 = -17f, x1 = 0f, z0 = 9f, z1 = 18f, y = FloorY(2);
            // 本棚（奥の壁と南の壁）とはしご
            Bookshelf(new Vector3(-13.5f, y, z1 - 0.45f), 3.2f, 0f, 2.7f);
            Bookshelf(new Vector3(-9.8f, y, z1 - 0.45f), 3.2f, 0f, 2.7f);
            Bookshelf(new Vector3(-6.1f, y, z1 - 0.45f), 3.2f, 0f, 2.7f);
            Bookshelf(new Vector3(x0 + 0.45f, y, 13.2f), 4.0f, -90f, 2.7f);
            if (PlaceFurn("Furn_Ladder", null, new Vector3(-9.8f, y, z1 - 0.95f), 180f) == null) // 上を本棚に立てかける
            {
            BoxRot("Ladder_RailL", new Vector3(-10.05f, y + 1.3f, z1 - 0.85f), new Vector3(0.06f, 2.6f, 0.06f), Quaternion.Euler(-12f, 0f, 0f), brassMat);
            BoxRot("Ladder_RailR", new Vector3(-9.55f, y + 1.3f, z1 - 0.85f), new Vector3(0.06f, 2.6f, 0.06f), Quaternion.Euler(-12f, 0f, 0f), brassMat);
            for (int i = 0; i < 6; i++) Box("Ladder_Rung", new Vector3(-9.8f, y + 0.3f + i * 0.38f, z1 - 1.1f + i * 0.08f), new Vector3(0.5f, 0.04f, 0.05f), brassMat);
            }
            // 閲覧机と緑のランプ
            float tx = -8.5f, tz = 13.2f;
            if (PlaceFurn("Furn_LibraryRug", null, new Vector3(tx, y, tz), 0f) == null)
            {
            Box("Library_Rug", new Vector3(tx, y + 0.012f, tz), new Vector3(3.6f, 0.02f, 2.6f), rugMat);
            }
            if (PlaceFurn("Furn_LibraryTable", null, new Vector3(tx, y, tz), 0f) == null)
            {
            Box("Library_Table", new Vector3(tx, y + 0.75f, tz), new Vector3(2.2f, 0.08f, 1.0f), woodMid);
            for (int i = 0; i < 4; i++)
                Box("Library_TableLeg", new Vector3(tx + ((i % 2 == 0) ? -1.0f : 1.0f), y + 0.37f, tz + ((i < 2) ? -0.4f : 0.4f)), new Vector3(0.1f, 0.74f, 0.1f), woodDark);
            }
            for (int k = -1; k <= 1; k += 2)
            {
                if (PlaceFurn("Furn_GreenLamp", null, new Vector3(tx + 0.55f * k, y + 0.79f, tz), 0f) != null) continue;
                Box("GreenLamp_Base", new Vector3(tx + 0.55f * k, y + 0.88f, tz), new Vector3(0.06f, 0.2f, 0.06f), brassMat);
                Box("GreenLamp_Shade", new Vector3(tx + 0.55f * k, y + 1.0f, tz), new Vector3(0.36f, 0.1f, 0.2f), greenLampMat);
            }
            Box("Library_Books", new Vector3(tx + 0.5f, y + 0.83f, tz - 0.2f), new Vector3(0.3f, 0.08f, 0.22f), fabricRed);
            Box("Library_OpenBook", new Vector3(tx - 0.4f, y + 0.8f, tz + 0.15f), new Vector3(0.4f, 0.02f, 0.28f), paperMat);
            blockers[2].Add(new Rect(tx - 1.1f, tz - 0.5f, 2.2f, 1.0f));
            Chair("図書室の机", Library, new Vector3(tx - 0.5f, y, tz - 0.8f), 0f);
            Chair("図書室の机", Library, new Vector3(tx + 0.5f, y, tz - 0.8f), 0f);
            Chair("図書室の机", Library, new Vector3(tx - 0.5f, y, tz + 0.8f), 180f);
            Chair("図書室の机", Library, new Vector3(tx + 0.5f, y, tz + 0.8f), 180f);
            // 窓辺の読書席（勉強部屋との壁ぎわ）
            SimpleBench("窓辺の読書席", Library, new Vector3(x1 - 0.45f, y, 13.5f), -90f, fabricRed);
            SimpleBench("窓辺の読書席", Library, new Vector3(-3.5f, y, z1 - 0.5f), 180f, fabricRed);
            Lamp(new Vector3(tx, y + 2.5f, tz), 2.5f, 7f);
            Lamp(new Vector3(-3.5f, y + 2.5f, 14f), 2.0f, 6f);
            Lamp(new Vector3(-14f, y + 2.5f, 13f), 2.0f, 6f);
            pois.Add(MakePoi("図書室の本を選んでいる", new Vector3(-13.5f, y + 1.5f, z1), new Vector3(-14.0f, y, z1 - 1.3f), new Vector3(-13.0f, y, z1 - 1.3f)));
            pois.Add(MakePoi("図書室の本を選んでいる", new Vector3(-6.1f, y + 1.5f, z1), new Vector3(-6.6f, y, z1 - 1.3f), new Vector3(-5.6f, y, z1 - 1.3f)));
            Label(new Vector3(-8.5f, y + 2.4f, 10.0f), "図書室");
            AddArea(Library, Rect.MinMaxRect(x0, z0, x1, z1), 2);
            PlaceDoorAt(Library, 2, new Vector3(-9.8f, 0f, z0), true, 1.0f, 2.3f, DoorNav(-9.8f, z0, true), woodDark);
            EndPlace(Library);
        }

        // ================= 温室（中庭の左奥） =================
        void BuildGreenhouse()
        {
            BeginPlace();
            float x0 = -11f, x1 = -4f, z0 = 17f, z1 = 22.5f;
            Box("Greenhouse_Floor", new Vector3((x0 + x1) / 2f, 0.0f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.06f, z1 - z0), flagstone);
            // 低い石の土台
            float bh = 0.6f, bt = 0.3f;
            var sm = StoneFor(7f, bh);
            Box("GH_BaseBack", new Vector3((x0 + x1) / 2f, bh / 2f, z1), new Vector3(x1 - x0 + bt, bh, bt), sm);
            Box("GH_BaseL", new Vector3(x0, bh / 2f, (z0 + z1) / 2f), new Vector3(bt, bh, z1 - z0), sm);
            Box("GH_BaseR", new Vector3(x1, bh / 2f, (z0 + z1) / 2f), new Vector3(bt, bh, z1 - z0), sm);
            Box("GH_BaseF1", new Vector3((x0 - 8f) / 2f, bh / 2f, z0), new Vector3(-8f - x0, bh, bt), sm);
            Box("GH_BaseF2", new Vector3((-7f + x1) / 2f, bh / 2f, z0), new Vector3(x1 + 7f, bh, bt), sm);
            // ガラス（奥と左右）と鉄の骨組み
            Box("GH_GlassBack", new Vector3((x0 + x1) / 2f, 1.75f, z1), new Vector3(x1 - x0, 2.3f, 0.04f), glassMat);
            Box("GH_GlassL", new Vector3(x0, 1.75f, (z0 + z1) / 2f), new Vector3(0.04f, 2.3f, z1 - z0), glassMat);
            Box("GH_GlassR", new Vector3(x1, 1.75f, (z0 + z1) / 2f), new Vector3(0.04f, 2.3f, z1 - z0), glassMat);
            for (float x = x0; x <= x1 + 0.01f; x += 1.75f)
            {
                Box("GH_Post", new Vector3(x, 1.5f, z0), new Vector3(0.08f, 3.0f, 0.08f), iron);
                Box("GH_Post", new Vector3(x, 1.5f, z1), new Vector3(0.08f, 3.0f, 0.08f), iron);
                ceilingRenderers.Add(BoxRot("GH_Rafter", new Vector3(x, 3.4f, (z0 + z1) / 2f), new Vector3(0.07f, 0.07f, z1 - z0 + 0.3f), Quaternion.identity, iron).GetComponent<Renderer>());
            }
            for (float z = z0; z <= z1 + 0.01f; z += 1.375f)
            {
                Box("GH_Post", new Vector3(x0, 1.5f, z), new Vector3(0.08f, 3.0f, 0.08f), iron);
                Box("GH_Post", new Vector3(x1, 1.5f, z), new Vector3(0.08f, 3.0f, 0.08f), iron);
            }
            ceilingRenderers.Add(Box("GH_Ridge", new Vector3((x0 + x1) / 2f, 3.45f, (z0 + z1) / 2f), new Vector3(x1 - x0 + 0.2f, 0.1f, 0.1f), iron).GetComponent<Renderer>());
            // 薬草の棚（左と奥）
            ShelfRow(new Vector3(-10.6f, 0f, 19.6f), -90f, 4.4f, 1.8f, 3, terraMat, leafLightMat, 101);
            ShelfRow(new Vector3(-8.2f, 0f, 22.1f), 0f, 3.2f, 1.6f, 3, terraMat, lavenderMat, 103);
            for (int i = 0; i < 5; i++) Pot(new Vector3(-10.5f + i * 0.35f, 0f, 17.45f), 0.25f, i % 2 == 0 ? leafMat : lavenderMat);
            for (int i = 0; i < 4; i++) Leaf(new Vector3(-10.6f, 1.95f, 18f + i * 1.0f), 0.35f, i % 2 == 0 ? lavenderMat : leafLightMat);
            // 作業台とじょうろ
            if (PlaceFurn("Furn_GHWorkbench", null, new Vector3(-5.0f, 0f, 21.2f), 180f) == null) // 上の鉢や道具も入っている
            {
            Box("GH_Workbench", new Vector3(-5.0f, 0.8f, 21.2f), new Vector3(1.6f, 0.08f, 0.7f), woodMid);
            Box("GH_WorkbenchBody", new Vector3(-5.0f, 0.4f, 21.2f), new Vector3(1.5f, 0.76f, 0.6f), woodDark);
            Cyl("WateringCan", new Vector3(-5.4f, 1.0f, 21.2f), new Vector3(0.22f, 0.15f, 0.22f), Quaternion.identity, brassMat);
            Pot(new Vector3(-4.7f, 0.84f, 21.2f), 0.2f, leafLightMat);
            }
            blockers[0].Add(new Rect(-5.8f, 20.85f, 1.6f, 0.7f));
            // ハーブティーのテーブル
            if (PlaceFurn("Furn_RoundRug", null, new Vector3(-5.2f, 0.02f, 18.6f), 0f) == null)
            {
            Cyl("GH_Rug", new Vector3(-5.2f, 0.035f, 18.6f), new Vector3(1.9f, 0.01f, 1.9f), Quaternion.identity, rugMat);
            }
            if (PlaceFurn("Furn_TeaTable", null, new Vector3(-5.2f, 0f, 18.6f), 0f) == null) // ティーセットも入っている
            {
            Cyl("GH_TeaTable", new Vector3(-5.2f, 0.7f, 18.6f), new Vector3(0.8f, 0.03f, 0.8f), Quaternion.identity, woodMid);
            Cyl("GH_TeaTableLeg", new Vector3(-5.2f, 0.35f, 18.6f), new Vector3(0.12f, 0.35f, 0.12f), Quaternion.identity, woodDark);
            Cyl("Teacup", new Vector3(-5.35f, 0.78f, 18.6f), new Vector3(0.1f, 0.04f, 0.1f), Quaternion.identity, fabricWhite);
            Cyl("Teacup", new Vector3(-5.05f, 0.78f, 18.65f), new Vector3(0.1f, 0.04f, 0.1f), Quaternion.identity, fabricWhite);
            }
            blockers[0].Add(new Rect(-5.6f, 18.2f, 0.8f, 0.8f));
            Chair("温室のテーブル", Greenhouse, new Vector3(-6.0f, 0f, 18.6f), 90f);
            Chair("温室のテーブル", Greenhouse, new Vector3(-4.4f, 0f, 18.6f), -90f);
            Lamp(new Vector3(-7.5f, 2.6f, 19.8f), 1.8f, 6f);
            pois.Add(MakePoi("温室で薬草の世話をしている", new Vector3(-8.2f, 0.9f, 22.1f), new Vector3(-9.0f, 0f, 21.35f), new Vector3(-7.5f, 0f, 21.35f), new Vector3(-9.85f, 0f, 20.6f)));
            pois[pois.Count - 1].subject = Academics.Herb;
            pois.Add(ClassPoi("薬草学の授業を受けている", Academics.Herb, new Vector3(-10.6f, 1f, 19.6f), -9.9f, 17.9f, 0.8f, 1.2f, 4, 2));
            Label(new Vector3(-7.5f, 2.6f, 17.2f), "温室");
            AddArea(Greenhouse, Rect.MinMaxRect(x0, z0, x1, z1), 0);
            PlaceDoorAt(Greenhouse, 0, new Vector3(-7.5f, 0f, z0), true, 1.0f, 2.3f, Rect.MinMaxRect(-7.9f, 16.6f, -7.1f, 17.5f), iron);
            EndPlace(Greenhouse);
        }

        // ================= 工房（中庭の右奥） =================
        void BuildWorkshop()
        {
            BeginPlace();
            float x0 = 5f, x1 = 12f, z0 = 17f, z1 = 22.5f, h = 3.2f, t = 0.35f;
            Box("Workshop_Floor", new Vector3((x0 + x1) / 2f, 0.0f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.06f, z1 - z0), flagstone);
            var front = Wall(new Vector2(x0 - 0.2f, z0), new Vector2(x1 + 0.2f, z0), 0f, h, t, new Vector4(8.0f - (x0 - 0.2f), 9.0f - (x0 - 0.2f), 0f, 2.4f));
            var back = Wall(new Vector2(x0 - 0.2f, z1), new Vector2(x1 + 0.2f, z1), 0f, h, t);
            var left = Wall(new Vector2(x0, z0), new Vector2(x0, z1), 0f, h, t);
            var right = Wall(new Vector2(x1, z0), new Vector2(x1, z1), 0f, h, t);
            Window(left, new Vector3(x0, 0f, 20.6f), Vector3.right, 1.1f, 0.9f, 1.5f);
            Window(back, new Vector3(7.0f, 0f, z1), Vector3.back, 1.1f, 1.0f, 1.6f);
            // 工具の壁と作業台
            if (PlaceFurn("Furn_ToolBoard", null, new Vector3(x0 + 0.195f, 1.1f, 18.9f), 90f) == null) // 壁に掛ける物（東を向く）
            {
            Box("ToolBoard", new Vector3(x0 + 0.22f, 1.6f, 18.9f), new Vector3(0.05f, 1.0f, 1.8f), woodMid);
            for (int i = 0; i < 6; i++) Box("Tool", new Vector3(x0 + 0.27f, 1.6f + (i % 2) * 0.15f, 18.2f + i * 0.28f), new Vector3(0.04f, 0.5f, 0.06f), iron);
            }
            if (PlaceFurn("Furn_WSBench", null, new Vector3(5.8f, 0f, 19.2f), 90f) == null) // 部屋の中（+x）を向く
            {
            Box("WS_Bench", new Vector3(5.8f, 0.85f, 19.2f), new Vector3(0.9f, 0.08f, 2.4f), woodMid);
            Box("WS_BenchBody", new Vector3(5.8f, 0.42f, 19.2f), new Vector3(0.8f, 0.8f, 2.3f), woodDark);
            Box("WS_Gadget", new Vector3(5.8f, 0.98f, 18.8f), new Vector3(0.25f, 0.18f, 0.25f), brassMat);
            Cyl("WS_Gear", new Vector3(5.8f, 0.95f, 19.6f), new Vector3(0.3f, 0.02f, 0.3f), Quaternion.identity, brassMat);
            }
            blockers[0].Add(new Rect(5.3f, 18.0f, 1.0f, 2.4f));
            // 真ん中の机
            if (PlaceFurn("Furn_WSTable", null, new Vector3(8.5f, 0f, 20.2f), 180f) == null) // 椅子（−z）側が正面
            {
            Box("WS_Table", new Vector3(8.5f, 0.75f, 20.2f), new Vector3(1.4f, 0.08f, 0.9f), woodMid);
            Box("WS_TableBody", new Vector3(8.5f, 0.37f, 20.2f), new Vector3(1.3f, 0.74f, 0.8f), woodDark);
            Box("WS_Paper", new Vector3(8.4f, 0.8f, 20.2f), new Vector3(0.5f, 0.01f, 0.35f), paperMat);
            }
            blockers[0].Add(new Rect(7.8f, 19.75f, 1.4f, 0.9f));
            Chair("工房の椅子", Workshop, new Vector3(8.1f, 0f, 19.35f), 0f);
            Chair("工房の椅子", Workshop, new Vector3(8.9f, 0f, 19.35f), 0f);
            // 錬成の炉と釜
            if (PlaceFurn("Furn_Furnace", null, new Vector3(10.9f, 0f, 21.85f), 180f) == null)
                Box("Furnace", new Vector3(10.9f, 0.8f, 21.85f), new Vector3(1.6f, 1.6f, 0.9f), mortarDark);
            back.upper.Add(Box("Furnace_Chimney", new Vector3(10.9f, 2.5f, 22.05f), new Vector3(0.8f, 1.8f, 0.5f), mortarDark).GetComponent<Renderer>());
            var wch = PlaceFurn("Furn_Chimney", null, new Vector3(10.9f, 3.4f, 22.05f), 180f); // 煙突の頭
            if (wch != null) foreach (var r in wch.GetComponentsInChildren<Renderer>()) back.upper.Add(r);
            var glow = Box("Furnace_Fire", new Vector3(10.9f, 0.55f, 21.38f), new Vector3(0.8f, 0.5f, 0.05f), glowFire);
            glow.AddComponent<FlameWobble>().seed = 3.3f;
            if (PlaceFurn("Furn_Cauldron", null, new Vector3(10.2f, 0f, 21.0f), 180f) == null)
            {
            var cauldron = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cauldron.name = "Cauldron";
            cauldron.transform.SetParent(root, false);
            cauldron.transform.localPosition = new Vector3(10.2f, 0.45f, 21.0f);
            cauldron.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
            cauldron.GetComponent<Renderer>().sharedMaterial = iron;
            Destroy(cauldron.GetComponent<Collider>());
            Cyl("Cauldron_Brew", new Vector3(10.2f, 0.74f, 21.0f), new Vector3(0.6f, 0.01f, 0.6f), Quaternion.identity, ProtoArt.Glow(new Color(0.5f, 1f, 0.6f), 1.2f));
            }
            var fire = Lamp(new Vector3(10.6f, 1.2f, 20.9f), 3f, 6f, false, 0.2f);
            fire.color = new Color(1f, 0.6f, 0.3f);
            fire.GetComponent<Flicker>().isFire = true;
            blockers[0].Add(new Rect(9.7f, 20.5f, 2.2f, 2.0f));
            // 宝物の棚（光る石・木の実）と箱
            ShelfRow(new Vector3(11.65f, 0f, 18.6f), 90f, 2.2f, 1.9f, 4, crystalMat, crystalBlueMat, 211);
            ShelfRow(new Vector3(7.6f, 0f, 22.1f), 0f, 2.4f, 2.0f, 4, woodMid, brassMat, 213);
            Lamp(new Vector3(7.5f, 2.6f, 19.2f), 1.8f, 6f);
            var tinker = MakePoi("工房で魔道具をいじっている", new Vector3(5.8f, 1f, 19.2f), new Vector3(6.75f, 0f, 18.7f), new Vector3(6.75f, 0f, 19.8f));
            tinker.hobby = (int)SimCharacter.HobbyKind.Magic;
            pois.Add(tinker);
            var brew = MakePoi("錬成の釜をかき混ぜている", new Vector3(10.2f, 0.7f, 21.0f), new Vector3(9.4f, 0f, 20.6f), new Vector3(9.9f, 0f, 20.05f));
            brew.subject = Academics.Alchemy;
            pois.Add(brew);
            pois.Add(MakePoi("宝物の棚を眺めている", new Vector3(11.65f, 1.2f, 18.6f), new Vector3(10.95f, 0f, 18.3f), new Vector3(10.95f, 0f, 19.0f)));
            pois.Add(ClassPoi("錬成の授業を受けている", Academics.Alchemy, new Vector3(10.2f, 0.7f, 21.0f), 6.7f, 17.6f, 0.75f, 0.0f, 2, 1));
            var cls = pois[pois.Count - 1];
            cls.slots = new[] { new Vector3(6.7f, 0f, 17.6f), new Vector3(7.45f, 0f, 17.6f), new Vector3(9.55f, 0f, 17.6f), new Vector3(10.3f, 0f, 17.6f),
                                new Vector3(11.05f, 0f, 17.6f), new Vector3(6.7f, 0f, 20.9f), new Vector3(7.45f, 0f, 21.4f), new Vector3(9.4f, 0f, 19.2f) };
            cls.occupants = new SimCharacter[cls.slots.Length];
            Label(new Vector3(8.5f, 2.6f, 17.2f), "工房");
            AddArea(Workshop, Rect.MinMaxRect(x0, z0, x1, z1), 0);
            PlaceDoorAt(Workshop, 0, new Vector3(8.5f, 0f, z0), true, 1.0f, 2.4f, Rect.MinMaxRect(8.1f, 16.6f, 8.9f, 17.5f), woodDark);
            EndPlace(Workshop);
        }

        // ================= 台所（1階の奥・北。x 11〜17、z 9〜18＝6m×9m。famuru 2026-10-26 作り直し：食堂との壁を6mくりぬいて配膳カウンター、
        // 奥の壁にかまど（扉をよけて東へ）と大きな窯、東の壁に大鍋の炉・薪・調理台・流し、南の壁に食器棚・チーズとパンの棚・樽、真ん中にアイランドと鍋かけ。最初から開いている） =================
        void BuildKitchen()
        {
            BeginPlace();
            float x0 = KitchenX0, x1 = 17f, z0 = 9f, z1 = 18f;
            float wi = WallT / 2f; // 壁の内側まで
            Box("Kitchen_Floor", new Vector3((x0 + x1) / 2f, -0.05f, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.1f, z1 - z0), flagstone);
            // --- 奥の壁（z1）：扉の前をあけて、大鍋の炉・大きな窯（かまどはなくした。famuru 2026-10-26） ---
            float kx = 15.85f;
            var kiln = PlaceFurn("Furn_BigKiln", null, new Vector3(kx, 0f, z1 - wi - 0.7f), 180f); // パンを焼く大きな窯（前のパン焼きのかまどの代わり）
            if (kiln != null)
            {
                Vector3 kp = MarkPos(kiln, "Fire", new Vector3(kx, 1.25f, z1 - wi - 0.7f - 0.22f)); // 窯の中（ドームの口の奥）
                var kg = Box("Kiln_Glow", kp + Vector3.up * 0.05f, new Vector3(0.6f, 0.18f, 0.5f), glowEmber);
                kg.AddComponent<FlameWobble>().seed = 7.3f;
                blockers[0].Add(Rect.MinMaxRect(kx - 1.05f, z1 - wi - 1.45f, x1, z1));
            }
            else
            {
                if (PlaceFurn("Furn_Oven", null, new Vector3(kx, 0f, z1 - 0.6f), 180f) == null)
                    Box("Oven", new Vector3(kx, 0.75f, z1 - 0.6f), new Vector3(1.2f, 1.5f, 0.9f), mortarDark);
                Box("Oven_Glow", new Vector3(kx, 0.75f, z1 - 1.07f), new Vector3(0.6f, 0.4f, 0.05f), glowEmber);
                blockers[0].Add(Rect.MinMaxRect(kx - 0.7f, z1 - 1.1f, kx + 0.7f, z1));
            }
            float px = 13.95f, pz = z1 - wi - 0.6f; // 大鍋の炉（窯の横。大鍋は薪の火にかける）
            var pit = PlaceFurn("Furn_PotFirePit", null, new Vector3(px, 0f, pz), 180f);
            if (pit != null)
            {
                PlaceFurn("Furn_StockPot", null, MarkPos(pit, "Pot", new Vector3(px, 0.55f, pz)), 180f); // 鍋の底を目印 Pot に
                Vector3 fp = MarkPos(pit, "Fire", new Vector3(px, 0.3f, pz));
                var pg = Box("Pit_Fire", fp + Vector3.up * 0.1f, new Vector3(0.6f, 0.22f, 0.5f), glowFire);
                pg.AddComponent<FlameWobble>().seed = 3.7f;
            }
            else
            {
                Box("Pit_Stone", new Vector3(px, 0.15f, pz), new Vector3(1.4f, 0.3f, 1.0f), mortarDark);
                var pg = Box("Pit_Fire", new Vector3(px, 0.4f, pz), new Vector3(0.6f, 0.22f, 0.5f), glowFire);
                pg.AddComponent<FlameWobble>().seed = 3.7f;
            }
            blockers[0].Add(Rect.MinMaxRect(px - 0.85f, pz - 0.65f, px + 0.85f, z1));
            var kfire = Lamp(new Vector3(14.9f, 1.2f, z1 - 1.6f), 3f, 6f, false, 0.2f);
            kfire.color = new Color(1f, 0.6f, 0.3f);
            kfire.GetComponent<Flicker>().isFire = true;
            // --- 東の壁（x1。正面は −x。手前から）：水の樽（2つ目）・調理台・流し・大きな薪の山・香辛料の棚（famuru 2026-10-26） ---
            float ex = x1 - wi;
            bool deep = Resources.Load<GameObject>("Furniture/Furn_KitchenCounterDeep") != null; // 奥行き1.5倍の台（チャッピー依頼19）
            float cd = deep ? 1.0f : 0.65f;
            PlaceFurn("Furn_BarrelWater", null, new Vector3(ex - 0.35f, 0f, 9.75f), 0f);
            bool kCounter = PlaceFurn(deep ? "Furn_KitchenCounterDeep" : "Furn_KitchenCounter", null, new Vector3(ex - cd / 2f, 0f, 11.2f), -90f) != null;
            PlaceFurn(deep ? "Furn_KitchenSinkDeep" : "Furn_KitchenSinkCounter", null, new Vector3(ex - cd / 2f, 0f, 13.2f), -90f);
            if (!kCounter) Box("Kitchen_Counter", new Vector3(ex - cd / 2f, 0.45f, 12.2f), new Vector3(cd, 0.9f, 4.0f), woodDark);
            PlaceFurn("Furn_Firewood", null, new Vector3(ex - 0.375f, 0f, 15.0f), -90f, false, Vector3.one * 1.5f); // 前の大鍋の所に1.5倍で
            PlaceFurn("Furn_SpiceShelf", null, new Vector3(ex, 1.35f, 11.2f), -90f);
            blockers[0].Add(Rect.MinMaxRect(ex - 0.7f, z0 + wi, x1, 10.1f));
            blockers[0].Add(Rect.MinMaxRect(ex - cd - 0.05f, 10.1f, x1, 14.25f));
            blockers[0].Add(Rect.MinMaxRect(ex - 0.8f, 14.2f, x1, 15.8f));
            // --- 南の壁（z0。正面は +z）：ぶどう酒の樽・食器棚（半分の幅）・チーズとパンの棚・水と小麦粉の樽・干した束 ---
            float sz = z0 + wi;
            PlaceFurn("Furn_BarrelWine", null, new Vector3(11.5f, 0f, sz + 0.3f), 0f);
            if (PlaceFurn("Furn_DishShelf", null, new Vector3(12.95f, 0f, sz + 0.225f), 0f, false, new Vector3(0.9f, 1f, 1f)) == null)
                ShelfRow(new Vector3(12.95f, 0f, sz + 0.3f), 0f, 1.8f, 2.0f, 4, fabricWhite, terraMat, 307);
            PlaceFurn("Furn_CheeseBreadShelf", null, new Vector3(14.5f, 0f, sz + 0.225f), 0f);
            PlaceFurn("Furn_BarrelWater", null, new Vector3(15.4f, 0f, sz + 0.3f), 0f);
            PlaceFurn("Furn_BarrelFlour", null, new Vector3(15.93f, 0f, sz + 0.3f), 0f);
            PlaceFurn("Furn_HangingHerbs", null, new Vector3(14.5f, 2.05f, sz), 0f);
            PlaceFurn("Furn_HangingHerbs", null, new Vector3(15.65f, 1.5f, sz), 0f);
            blockers[0].Add(Rect.MinMaxRect(x0 + wi, sz, ex - 0.65f, sz + 0.65f));
            // --- 真ん中：アイランド（両側から使う）と天井から吊るす鍋かけ、食材のかご ---
            float ix = 14.0f, iz = 12.6f;
            bool kIsland = PlaceFurn("Furn_KitchenIslandBig", null, new Vector3(ix, 0f, iz), 90f) != null;
            if (!kIsland)
            {
            Box("Kitchen_Island", new Vector3(ix, 0.88f, iz), new Vector3(1.2f, 0.08f, 2.8f), woodMid);
            Box("Kitchen_IslandBody", new Vector3(ix, 0.42f, iz), new Vector3(1.1f, 0.84f, 2.7f), woodDark);
            }
            var rack = PlaceFurn("Furn_PotRack", null, new Vector3(ix, 4.0f, iz), 90f); // 原点は吊るす上端
            // 上から見ても見えるように、天井の物（シャンデリアなど）と違って隠さない
            blockers[0].Add(new Rect(ix - 0.65f, iz - 1.45f, 1.3f, 2.9f));
            PlaceFurn("Furn_BasketVeg", null, new Vector3(13.45f, 0f, 10.5f), 0f);
            PlaceFurn("Furn_BasketFruit", null, new Vector3(14.1f, 0f, 10.5f), 0f);
            PlaceFurn("Furn_BasketBread", null, new Vector3(14.75f, 0f, 10.5f), 0f);
            blockers[0].Add(Rect.MinMaxRect(13.1f, 10.25f, 15.1f, 10.75f));
            Lamp(new Vector3(12.6f, 3.0f, iz), 2.2f, 6f);
            // --- 料理する所（アイランドの両側）・洗い物・大鍋・配膳 ---
            var cook = MakePoi("台所で料理をしている", new Vector3(ix, 0.9f, iz),
                new Vector3(ix - 1.15f, 0f, iz - 0.9f), new Vector3(ix - 1.15f, 0f, iz), new Vector3(ix - 1.15f, 0f, iz + 0.9f),
                new Vector3(ix + 1.15f, 0f, iz - 0.9f), new Vector3(ix + 1.15f, 0f, iz), new Vector3(ix + 1.15f, 0f, iz + 0.9f));
            cook.kitchen = true;
            pois.Add(cook);
            pois.Add(MakePoi("台所で洗い物をしている", new Vector3(ex - cd / 2f, 0.9f, 13.2f), new Vector3(ex - cd - 0.45f, 0f, 13.2f)));
            pois.Add(MakePoi("大鍋を煮込んでいる", new Vector3(px, 0.9f, pz), new Vector3(px, 0f, pz - 1.15f)));
            pois.Add(MakePoi("料理を配っている", new Vector3(x0, 1.0f, 12.8f), new Vector3(x0 + 0.95f, 0f, 11.3f), new Vector3(x0 + 0.95f, 0f, 14.3f)));
            Label(new Vector3(ix, 2.6f, z0 + 0.8f), "台所");
            AddArea(Kitchen, Rect.MinMaxRect(x0, z0, x1, z1), 0);
            PlaceDoorAt(Kitchen, 0, new Vector3(x0, 0f, KitchenDoorZ), false, 1.0f, 2.4f, DoorNav(x0, KitchenDoorZ, false), woodDark, true); // 開く向きは北側（famuru 2026-10-26）
            EndPlace(Kitchen);
        }

        /// <summary>上が丸い鉄の扉（Furn_ArchDoor・Furn_ArchLockedDoor。依頼19）があればそちらを使う。</summary>
        static string ArchOr(string name)
        {
            string a = name.Replace("Furn_", "Furn_Arch");
            return Resources.Load<GameObject>("Furniture/" + a) != null ? a : name;
        }

        /// <summary>扉の大きさ合わせ。上が丸い扉は枠（DoorFrame）と同じ倍率（Wall の枠の置き方に合わせる）、四角い扉は 1.0×2.3 を穴に合わせる。</summary>
        static Vector3 DoorScale(string model, float w, float h)
        {
            return model.Contains("Arch") ? new Vector3((w + 0.2f) / 1.2f, (h + 0.15f) / 2.5f, 1f) : new Vector3(w / 1.0f, h / 2.3f, 1f);
        }

        /// <summary>目印の位置（寮の中の座標）。見つからなければ fallback（報告にある目印の位置から計算した値）。</summary>
        Vector3 MarkPos(GameObject go, string name, Vector3 fallback)
        {
            var t = FurnMark(go, name);
            Debug.Log("[家具] 目印 " + (go != null ? go.name : "-") + "/" + name + (t != null ? " あり " + root.InverseTransformPoint(t.position) : " なし（決めた位置）"));
            return t != null ? root.InverseTransformPoint(t.position) : fallback;
        }

        /// <summary>本番の家具の目印（Fire・Pot など。Blender の空の物）を探す。</summary>
        static Transform FurnMark(GameObject go, string name)
        {
            if (go == null) return null;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == name || t.name.StartsWith(name + ".")) return t;
            return null;
        }

        // ================= 中庭（寮の裏） =================
        void BuildCourtyard()
        {
            BeginPlace();
            Box("Courtyard_Grass", new Vector3(0f, -0.03f, (CY0 + CY1) / 2f), new Vector3(27f, 0.04f, CY1 - CY0), grassMat);
            // まわりの低い石の塀
            var sm = StoneFor(18f, 1.4f);
            Box("Yard_WallL", new Vector3(-13.6f, 0.7f, (CY0 + CY1) / 2f), new Vector3(0.4f, 1.4f, CY1 - CY0), sm);
            Box("Yard_WallR", new Vector3(13.6f, 0.7f, (CY0 + CY1) / 2f), new Vector3(0.4f, 1.4f, CY1 - CY0), sm);
            Box("Yard_WallB", new Vector3(0f, 0.7f, CY1), new Vector3(27.6f, 1.4f, 0.4f), sm);
            if (Resources.Load<GameObject>("Furniture/Furn_WallCap") != null)
                for (int i = 0; i < 14; i++) PlaceFurn("Furn_WallCap", null, new Vector3(-13.9f + (i + 0.5f) * (27.8f / 14f), 1.39f, CY1), 0f, false, new Vector3(27.8f / 14f / 2f, 1f, 1f)); // 本番の部品の笠石
            else Box("Yard_WallCap", new Vector3(0f, 1.45f, CY1), new Vector3(27.8f, 0.12f, 0.5f), mortarDark);
            // 食堂の裏口からの小道・円い広場（行事の屋台を並べられる）・小屋への道
            Box("Yard_Path", new Vector3(2.8f, -0.005f, 8.6f), new Vector3(2.6f, 0.04f, 6.4f), flagstone);
            // 屋根のある通路（食堂の裏口から広場の手前まで。幅2.4m。famuru 2026-10-25「食堂の裏口を広い通路に」）
            for (int k = -1; k <= 1; k += 2)
                for (int j = 0; j < 3; j++)
                {
                    float px = 2.8f + k * 1.5f, pz = 6.2f + j * 1.7f;
                    if (PlaceFurn("Furn_Pillar", null, new Vector3(px, 0f, pz), 0f, false, new Vector3(0.18f / 0.4f, 2.7f / 4.5f, 0.18f / 0.4f)) == null)
                        Box("Passage_Post", new Vector3(px, 1.35f, pz), new Vector3(0.18f, 2.7f, 0.18f), woodDark);
                    blockers[0].Add(new Rect(px - 0.15f, pz - 0.15f, 0.3f, 0.3f));
                }
            ceilingRenderers.Add(Box("Passage_Roof", new Vector3(2.8f, 2.78f, 7.5f), new Vector3(3.4f, 0.1f, 4.2f), mortarDark).GetComponent<Renderer>());
            Lamp(new Vector3(2.8f, 2.5f, 7.5f), 1.4f, 5f);
            Cyl("Plaza", new Vector3(2.8f, -0.005f, 14.5f), new Vector3(6.4f, 0.025f, 6.4f), Quaternion.identity, flagstone);
            Cyl("Plaza_Ring", new Vector3(2.8f, 0.012f, 14.5f), new Vector3(3.4f, 0.01f, 3.4f), Quaternion.identity, rugMat);
            Cyl("Plaza_Center", new Vector3(2.8f, 0.016f, 14.5f), new Vector3(2.6f, 0.01f, 2.6f), Quaternion.identity, flagstone);
            Cyl("Plaza_Star", new Vector3(2.8f, 0.02f, 14.5f), new Vector3(0.9f, 0.01f, 0.9f), Quaternion.identity, rugGoldMat);
            YardPath(new Vector2(0.4f, 16.5f), new Vector2(-7.5f, 16.8f));
            YardPath(new Vector2(5.2f, 16.5f), new Vector2(8.5f, 16.8f));
            YardPath(new Vector2(2.8f, 17.7f), new Vector2(2.8f, 22.6f));
            // ベンチ（広場を向く）
            SimpleBench("中庭のベンチ", Courtyard, new Vector3(-1.3f, 0f, 14.5f), 90f, null);
            SimpleBench("中庭のベンチ", Courtyard, new Vector3(6.9f, 0f, 14.5f), -90f, null);
            SimpleBench("中庭のベンチ", Courtyard, new Vector3(2.8f, 0f, 18.4f), 180f, null);
            // 植樹祭の若木（花壇と低い柵）
            Vector2[] saplings = { new Vector2(0.3f, 11.9f), new Vector2(5.3f, 11.9f) };
            foreach (var s in saplings)
            {
                if (PlaceFurn("Furn_FlowerBed", null, new Vector3(s.x, 0f, s.y), 0f) != null)
                    PlaceFurn("Furn_Sapling", null, new Vector3(s.x, 0.25f, s.y), 0f);
                else
                {
                Box("FlowerBed", new Vector3(s.x, 0.12f, s.y), new Vector3(1.6f, 0.24f, 1.0f), woodDark);
                Box("FlowerBed_Soil", new Vector3(s.x, 0.25f, s.y), new Vector3(1.45f, 0.04f, 0.85f), fabricBrown);
                for (int i = 0; i < 3; i++) Leaf(new Vector3(s.x - 0.5f + i * 0.5f, 0.38f, s.y), 0.3f, i == 1 ? lavenderMat : leafLightMat);
                Cyl("Sapling", new Vector3(s.x, 0.7f, s.y), new Vector3(0.06f, 0.45f, 0.06f), Quaternion.identity, woodDark);
                Leaf(new Vector3(s.x, 1.2f, s.y), 0.55f, leafLightMat);
                }
                blockers[0].Add(new Rect(s.x - 0.8f, s.y - 0.5f, 1.6f, 1.0f));
            }
            LampPost(new Vector3(0.9f, 0f, 12.6f));
            LampPost(new Vector3(4.7f, 0f, 12.6f));
            LampPost(new Vector3(-8.6f, 0f, 16.3f));
            LampPost(new Vector3(9.6f, 0f, 16.3f));
            // 飛行術：浮かぶ輪・ほうき立て
            Box("FlightLine", new Vector3(-7f, 0.005f, 14.6f), new Vector3(9f, 0.01f, 0.08f), fabricWhite);
            FloatRing(new Vector3(-10.5f, 2.4f, 14.6f), 0.8f);
            FloatRing(new Vector3(-7.3f, 3.1f, 14.6f), 0.65f);
            FloatRing(new Vector3(-4.2f, 2.2f, 14.6f), 0.75f);
            var rack = new GameObject("BroomRack").transform;
            rack.SetParent(root, false);
            rack.localPosition = new Vector3(-12.6f, 0f, 16.0f);
            if (PlaceFurn("Furn_BroomRack", rack, Vector3.zero, 90f) == null) // 長い辺は z
            {
            Box("Rack_Frame", new Vector3(0f, 0.9f, 0f), new Vector3(0.3f, 0.08f, 1.8f), woodDark, rack);
            Box("Rack_LegA", new Vector3(0f, 0.45f, -0.85f), new Vector3(0.1f, 0.9f, 0.1f), woodDark, rack);
            Box("Rack_LegB", new Vector3(0f, 0.45f, 0.85f), new Vector3(0.1f, 0.9f, 0.1f), woodDark, rack);
            for (int i = 0; i < 5; i++)
            {
                Cyl("Broom_Stick", new Vector3(0.05f, 0.75f, -0.7f + i * 0.35f), new Vector3(0.04f, 0.6f, 0.04f), Quaternion.Euler(0f, 0f, 8f), woodMid, rack);
                Box("Broom_Head", new Vector3(0.12f, 0.22f, -0.7f + i * 0.35f), new Vector3(0.18f, 0.35f, 0.16f), rugGoldMat, rack);
            }
            }
            blockers[0].Add(new Rect(-12.85f, 15.0f, 0.5f, 2.0f));
            // 剣術：マット・打ち込み台・杭
            if (PlaceFurn("Furn_TrainingMat", null, new Vector3(9.0f, 0f, 10.8f), 0f) == null)
            {
            Box("Mat1", new Vector3(9.0f, 0.03f, 10.8f), new Vector3(2.6f, 0.06f, 1.8f), fabricRed);
            }
            if (PlaceFurn("Furn_TrainingMat", null, new Vector3(11.2f, 0f, 12.4f), 0f, false, new Vector3(1.6f / 2.6f, 1f, 1.2f / 1.8f)) == null)
            {
            Box("Mat2", new Vector3(11.2f, 0.03f, 12.4f), new Vector3(1.6f, 0.06f, 1.2f), fabricRed);
            }
            var dummy = new GameObject("TrainingDummy").transform;
            dummy.SetParent(root, false);
            dummy.localPosition = new Vector3(12.6f, 0f, 10.6f);
            if (PlaceFurn("Furn_Dummy", dummy, Vector3.zero, 0f) == null)
            {
            Cyl("Dummy_Post", new Vector3(0f, 0.85f, 0f), new Vector3(0.3f, 0.85f, 0.3f), Quaternion.identity, woodMid, dummy);
            Box("Dummy_Arm", new Vector3(0f, 1.2f, 0f), new Vector3(1.0f, 0.12f, 0.12f), woodDark, dummy);
            Box("Dummy_Arm2", new Vector3(0f, 0.85f, 0f), new Vector3(0.12f, 0.12f, 0.8f), woodDark, dummy);
            Cyl("Dummy_Target", new Vector3(-0.17f, 1.45f, 0f), new Vector3(0.3f, 0.02f, 0.3f), Quaternion.Euler(0f, 0f, 90f), fabricRed, dummy);
            }
            blockers[0].Add(new Rect(12.3f, 10.3f, 0.6f, 0.6f));
            for (int i = 0; i < 3; i++)
            {
                if (PlaceFurn("Furn_TrainingPost", null, new Vector3(10.4f + i * 0.9f, 0f, 14.4f), 0f) == null)
                {
                Cyl("Post", new Vector3(10.4f + i * 0.9f, 0.35f, 14.4f), new Vector3(0.18f, 0.35f, 0.18f), Quaternion.identity, woodDark);
                }
                blockers[0].Add(new Rect(10.3f + i * 0.9f, 14.3f, 0.2f, 0.2f));
            }
            // 木（まわり）
            Vector3[] trees =
            {
                new Vector3(-12.6f, 0f, 19.5f), new Vector3(-12.4f, 0f, 22.3f), new Vector3(-2.7f, 0f, 22.2f), new Vector3(0.2f, 0f, 21.6f),
                new Vector3(5.6f, 0f, 21.8f) - new Vector3(1.6f, 0f, 0f), new Vector3(12.8f, 0f, 12.6f), new Vector3(12.8f, 0f, 15.3f), new Vector3(-2.6f, 0f, 12.6f)
            };
            foreach (var p in trees) Tree(p, 3.2f + (p.x % 1f + 1f) * 0.3f);
            // 見どころ（だれでも）・自主練習・授業の場所
            pois.Add(MakePoi("中庭をぶらぶらしている", new Vector3(2.8f, 1f, 14.5f), new Vector3(1.2f, 0f, 13.3f), new Vector3(4.4f, 0f, 15.8f), new Vector3(1.0f, 0f, 15.9f)));
            pois[pois.Count - 1].outdoor = true;
            pois.Add(MakePoi("若木を眺めている", new Vector3(0.3f, 1f, 11.9f), new Vector3(0.3f, 0f, 12.9f), new Vector3(5.3f, 0f, 12.9f)));
            pois[pois.Count - 1].outdoor = true;
            var fly = MakePoi("ほうきで飛ぶ練習をしている", new Vector3(-7.3f, 3f, 14.6f), new Vector3(-11.4f, 0f, 13.9f), new Vector3(-5.6f, 0f, 13.6f));
            fly.subject = Academics.Flight; fly.outdoor = true;
            pois.Add(fly);
            var kick = MakePoi("打ち込み台で剣術の練習をしている", new Vector3(12.6f, 1.1f, 10.6f), new Vector3(11.8f, 0f, 10.7f), new Vector3(12.6f, 0f, 11.5f));
            kick.subject = Academics.Sword; kick.outdoor = true;
            pois.Add(kick);
            var flyClass = ClassPoi("飛行術の授業を受けている", Academics.Flight, new Vector3(-7.3f, 2.5f, 14f), -11.6f, 15.6f, 1.3f, 0f, 7, 1);
            flyClass.outdoor = true;
            pois.Add(flyClass);
            var bodyClass = ClassPoi("剣術の授業を受けている", Academics.Sword, new Vector3(9.0f, 0.5f, 10.8f), 7.6f, 10.2f, 0.85f, 2.4f, 4, 2);
            bodyClass.outdoor = true;
            pois.Add(bodyClass);
            Label(new Vector3(2.8f, 2.2f, 11.2f), "中庭");
            AddArea(Courtyard, Rect.MinMaxRect(-13.4f, CY0, 13.4f, CY1), 0);
            PlaceDoorAt(Courtyard, 0, new Vector3(2.8f, 0f, OldHalfD + 0.2f), true, 2.4f, 2.8f, Rect.MinMaxRect(1.75f, 4.6f, 3.85f, 5.9f), woodDark);
            EndPlace(Courtyard);
        }

        void YardPath(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
            var go = Box("Yard_Path", new Vector3((a.x + b.x) / 2f, -0.004f, (a.y + b.y) / 2f), new Vector3(1.3f, 0.04f, d.magnitude), flagstone);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void FloatRing(Vector3 c, float r)
        {
            if (PlaceFurn("Furn_FlyRing", null, c, 90f, false, Vector3.one * (r / 0.8f)) != null) return; // 宙に浮かぶ物（輪は ±x を向く）
            int n = 16;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Vector3 p = c + new Vector3(0f, Mathf.Sin(a) * r, Mathf.Cos(a) * r);
                var seg = Box("FlyRing", p, new Vector3(0.08f, 0.08f, r * 0.42f), ringMat);
                seg.transform.localRotation = Quaternion.Euler(-a * Mathf.Rad2Deg, 0f, 0f);
            }
        }

        // ================= 屋上の天文台（寮の屋上の真ん中。3階の階段の上の扉に鍵） =================
        void BuildObservatory()
        {
            BeginPlace();
            float x0 = -6f, x1 = 6f, z0 = -7f, z1 = 3f, y = FloorY(3);
            Cyl("Terrace_Compass", new Vector3(0f, y + 0.005f, -2f), new Vector3(3.2f, 0.01f, 3.2f), Quaternion.identity, rugMat);
            Cyl("Terrace_CompassIn", new Vector3(0f, y + 0.01f, -2f), new Vector3(2.6f, 0.01f, 2.6f), Quaternion.identity, flagstone);
            // 望遠鏡
            var tel = new GameObject("Telescope").transform;
            tel.SetParent(root, false);
            tel.localPosition = new Vector3(0f, y, -2f);
            if (PlaceFurn("Furn_Telescope", tel, Vector3.zero, 0f) == null)
            {
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f;
                Cyl("Tripod_Leg", Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.6f, 0.25f), new Vector3(0.05f, 0.65f, 0.05f), Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(20f, 0f, 0f), woodDark, tel);
            }
            Cyl("Telescope_Tube", new Vector3(0.05f, 1.45f, 0.25f), new Vector3(0.16f, 0.75f, 0.16f), Quaternion.Euler(55f, 20f, 0f), brassMat, tel);
            }
            blockers[3].Add(new Rect(-0.45f, -2.45f, 0.9f, 0.9f));
            // 星図の机
            if (PlaceFurn("Furn_StarChartTable", null, new Vector3(2.6f, y, -3.4f), 0f) == null) // 星図も入っている
            {
            Box("StarChart_Table", new Vector3(2.6f, y + 0.75f, -3.4f), new Vector3(1.2f, 0.06f, 0.8f), woodMid);
            Box("StarChart_Body", new Vector3(2.6f, y + 0.37f, -3.4f), new Vector3(1.1f, 0.74f, 0.7f), woodDark);
            Box("StarChart", new Vector3(2.6f, y + 0.79f, -3.4f), new Vector3(0.7f, 0.01f, 0.55f), ProtoArt.Glow(new Color(0.25f, 0.35f, 0.75f), 1.0f));
            }
            blockers[3].Add(new Rect(2.0f, -3.8f, 1.2f, 0.8f));
            // 毛布とクッション（星見の夜に座る）
            if (PlaceFurn("Furn_TerraceRug", null, new Vector3(-3.0f, y, -4.6f), 0f) == null)
            {
            Box("Terrace_Rug", new Vector3(-3.0f, y + 0.01f, -4.6f), new Vector3(2.2f, 0.02f, 2.6f), rugMat);
            }
            var cushionSpots = new[] { new Vector3(-3.6f, y, -5.2f), new Vector3(-2.4f, y, -5.2f), new Vector3(-3.6f, y, -4.0f), new Vector3(-2.4f, y, -4.0f) };
            foreach (var p in cushionSpots)
            {
                if (PlaceFurn("Furn_Cushion", null, p, 0f, false, new Vector3(1.1f, 1.25f, 1.1f)) == null)
                    Box("Cushion", p + Vector3.up * 0.05f, new Vector3(0.55f, 0.1f, 0.55f), fabricGreen);
                seats.Add(new Seat
                {
                    kind = SeatKind.Sit, floorSeat = true, label = "天文台のクッション", area = Observatory,
                    position = p, forward = new Vector3(0f, 0f, 1f), approach = p + new Vector3(0.6f, 0f, 0f)
                });
            }
            if (PlaceFurn("Furn_Lantern", null, new Vector3(-3.0f, y + 0.03f, -4.6f), 0f) == null)
            {
            Box("Lantern", new Vector3(-3.0f, y + 0.2f, -4.6f), new Vector3(0.18f, 0.3f, 0.18f), glowLamp);
            }
            SimpleBench("天文台のベンチ", Observatory, new Vector3(0f, y, 1.8f), 180f, fabricRed);
            Pot(new Vector3(-5.2f, y, 2.2f), 0.4f, leafMat);
            Pot(new Vector3(5.2f, y, 2.2f), 0.4f, leafMat);
            blockers[3].Add(new Rect(-5.5f, 1.9f, 0.6f, 0.6f));
            blockers[3].Add(new Rect(4.9f, 1.9f, 0.6f, 0.6f));
            Lamp(new Vector3(0f, y + 1.8f, 0f), 1.8f, 7f);
            Lamp(new Vector3(-3.0f, y + 0.8f, -4.6f), 1.2f, 4f);
            var star = MakePoi("望遠鏡で星を見ている", new Vector3(0.2f, y + 1.9f, -1.5f), new Vector3(0f, y, -2.75f), new Vector3(-0.75f, y, -2.2f));
            star.outdoor = true; star.stars = true;
            pois.Add(star);
            pois.Add(MakePoi("星図を広げている", new Vector3(2.6f, y + 0.8f, -3.4f), new Vector3(2.6f, y, -4.2f)));
            pois[pois.Count - 1].outdoor = true;
            Label(new Vector3(0f, y + 2.4f, 0f), "屋上の天文台");
            AddArea(Observatory, Rect.MinMaxRect(x0, z0, x1, z1), 3);
            // 鍵：3階の真ん中の踊り場（屋上への階段の下）。開くまで、吹き抜けのまわりの通路から踊り場へ入れない
            PlaceDoorAt(Observatory, 2, new Vector3((StairB0 + StairA0) / 2f, 0f, BandZ0), true, StairA0 - StairB0, 2.2f, Rect.MinMaxRect(StairB0, BandZ0 - 0.5f, StairA0, BandZ0 + 0.3f), woodDark);
            EndPlace(Observatory);
        }

        /// <summary>新しい場所の歩ける所（1階。扉は閉まっている。開くと ApplyPlaces で通れる）。</summary>
        void OpenPlacesNav()
        {
            var n0 = navs[0];
            n0.Open(Rect.MinMaxRect(KitchenX0 + 0.25f, 9.25f, 16.75f, 17.75f));              // 台所
            n0.Open(Off(Rect.MinMaxRect(-10.75f, 17.25f, -4.25f, 22.25f), YardOff));        // 温室
            n0.Open(Off(Rect.MinMaxRect(5.25f, 17.25f, 11.75f, 22.25f), YardOff));          // 工房
            n0.Open(Off(Rect.MinMaxRect(-13.15f, 5.65f, 13.35f, 16.55f), YardOff));         // 中庭（手前と奥の広い所）
            n0.Open(Off(Rect.MinMaxRect(-3.8f, 16.5f, 4.8f, 22.95f), YardOff));             // 中庭（小屋のあいだ）
            n0.Open(Off(Rect.MinMaxRect(12.15f, 16.5f, 13.35f, 22.95f), YardOff));          // 中庭（工房の右）
            n0.Open(Off(Rect.MinMaxRect(-13.15f, 16.5f, -11.25f, 22.95f), YardOff));        // 中庭（温室の左）
        }

        /// <summary>3階の図書室（扉は ApplyPlaces で開け閉め）。</summary>
        void OpenPlacesNav2F()
        {
            navs[2].Open(Rect.MinMaxRect(-16.75f, 9.25f, -0.25f, 17.75f));
        }

        public static readonly string[] PlaceFloor0Names = { "Courtyard_Grass", "Kitchen_Floor", "Greenhouse_Floor", "Workshop_Floor", "Yard_Path", "Plaza", "Plaza_Ring", "Plaza_Center", "Plaza_Star" };
    }

    /// <summary>
    /// 新しい場所の確認写真（ゲームのカメラで、場所ごとに斜め上から）。Claude が外から合図して撮る（ClaudeBridge の placeshots）。
    /// 保存先：ゲーム本体\仮モデル\Unity確認\場所_（名前）.png
    /// </summary>
    public class PlaceShots : MonoBehaviour
    {
        IEnumerator Start()
        {
            var world = LoungeWorld.Instance;
            if (world == null || world.mainCamera == null) { Destroy(gameObject); yield break; }
            var rig = world.mainCamera.GetComponent<CameraRig>();
            if (rig == null) { Destroy(gameObject); yield break; }
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../仮モデル/Unity確認"));
            Directory.CreateDirectory(dir);
            var keep = new float[] { rig.pitch, rig.yaw, rig.distance };
            Vector3 keepPivot = rig.pivot;
            var keepFollow = rig.follow;
            System.Func<string, float, Vector4> At = (place, kind) => { var f = LoungeWorld.FocusOf(place); return new Vector4(f.x, f.y + 0.8f, f.z, kind); };
            var shots = new List<KeyValuePair<string, Vector4>>
            {
                new KeyValuePair<string, Vector4>("全体", new Vector4(0f, 1f, 4f, 0f)),
                new KeyValuePair<string, Vector4>("中庭", At(LoungeWorld.Courtyard, 1f)),
                new KeyValuePair<string, Vector4>("温室", At(LoungeWorld.Greenhouse, 2f)),
                new KeyValuePair<string, Vector4>("工房", At(LoungeWorld.Workshop, 2f)),
                new KeyValuePair<string, Vector4>("図書室", At(LoungeWorld.Library, 2f)),
                new KeyValuePair<string, Vector4>("台所", At(LoungeWorld.Kitchen, 2f)),
                new KeyValuePair<string, Vector4>("天文台", At(LoungeWorld.Observatory, 2f)),
            };
            foreach (var s in shots)
            {
                rig.follow = null;
                rig.pivot = new Vector3(s.Value.x, s.Value.y, s.Value.z);
                rig.yaw = 0f;
                rig.pitch = s.Value.w == 0f ? 55f : 50f;
                rig.distance = s.Value.w == 0f ? 46f : (s.Value.w == 1f ? 26f : 13f);
                for (int f = 0; f < 4; f++) yield return null;
                var mc = world.mainCamera;
                var prevT = mc.targetTexture;
                var rt = new RenderTexture(1280, 720, 24);
                mc.targetTexture = rt;
                mc.Render();
                mc.targetTexture = prevT;
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(dir, "場所_" + s.Key + ".png"), tex.EncodeToPNG());
                Destroy(tex);
                rt.Release();
            }
            rig.pitch = keep[0]; rig.yaw = keep[1]; rig.distance = keep[2]; rig.pivot = keepPivot; rig.follow = keepFollow;
            Debug.Log("場所の写真を保存しました：" + dir);
            Destroy(gameObject);
        }
    }
}
