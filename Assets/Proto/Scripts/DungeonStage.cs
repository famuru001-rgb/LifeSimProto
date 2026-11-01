using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// ダンジョン実習の場所（2026-11-01）。学園の東の離れた所に、部屋5つを一列に組み立てる（入口の魔法陣・出来事の部屋3つ・下への階段）。
    /// 魔物・宝箱・罠・泉は、組が部屋に着いたときに置く（DungeonDirector から呼ぶ）。寮杯の優勝杯もここ。
    /// </summary>
    public partial class LoungeWorld
    {
        public static Vector3 DungeonOrigin { get { return NewLayout ? new Vector3(400f, 0f, -22f) : new Vector3(130f, 0f, -22f); } } // 2026-11-01 家具のチャット：新しい学園（NewDorm.cs）では東が正門の外の街になるので、遠くの離れに
        public const float DungeonStep = 11f;     // 部屋の間隔（部屋8m＋通路3m）
        const float DRoomHalf = 4f, DCorrHalf = 1.2f, DWallH = 3.2f;

        Transform dungeonDyn;
        readonly List<GameObject> dungeonEventObjs = new List<GameObject>();
        GameObject dungeonChestLid;
        Material dStone, dFloor, dGlowBlue, dGlowRed, dGlowPurple;
        int dungeonFloorShown = -1;

        void BuildDungeonStage()
        {
            dStone = ProtoArt.Mat(new Color(0.36f, 0.34f, 0.40f), ProtoArt.StoneWall, new Vector2(1.5f, 1.5f), 0.1f);
            dFloor = ProtoArt.Mat(new Color(0.30f, 0.29f, 0.33f), ProtoArt.Flagstone, new Vector2(4f, 4f), 0.15f);
            dGlowBlue = ProtoArt.Glow(new Color(0.35f, 0.6f, 1f), 2.2f);
            dGlowRed = ProtoArt.Glow(new Color(1f, 0.3f, 0.25f), 2.0f);
            dGlowPurple = ProtoArt.Glow(new Color(0.7f, 0.35f, 1f), 2.0f);
            Vector3 o = DungeonOrigin;
            float len = DungeonStep * 4f + DRoomHalf * 2f;
            // 下の暗い地面と、部屋・通路の床
            Box("Dungeon_Ground", new Vector3(o.x, -0.14f, o.z + DungeonStep * 2f), new Vector3(40f, 0.1f, len + 30f), ProtoArt.Mat(new Color(0.12f, 0.12f, 0.14f)));
            Box("Dungeon_Floor", new Vector3(o.x, -0.05f, o.z + DungeonStep * 2f), new Vector3(DRoomHalf * 2f, 0.1f, len), dFloor);
            for (int i = 0; i < 5; i++)
            {
                Vector3 c = o + new Vector3(0f, 0f, i * DungeonStep);
                // 左右の壁
                Box("Dungeon_Wall", c + new Vector3(-DRoomHalf, DWallH / 2f, 0f), new Vector3(0.4f, DWallH, DRoomHalf * 2f + 0.4f), dStone);
                Box("Dungeon_Wall", c + new Vector3(DRoomHalf, DWallH / 2f, 0f), new Vector3(0.4f, DWallH, DRoomHalf * 2f + 0.4f), dStone);
                // 前後の壁（通路の口を空ける）
                float side = (DRoomHalf - DCorrHalf) / 2f + DCorrHalf;
                for (int k = 0; k < 2; k++)
                {
                    float z = k == 0 ? -DRoomHalf : DRoomHalf;
                    bool open = (k == 0 && i > 0) || (k == 1 && i < 4);
                    if (open)
                    {
                        Box("Dungeon_Wall", c + new Vector3(-side, DWallH / 2f, z), new Vector3(DRoomHalf - DCorrHalf, DWallH, 0.4f), dStone);
                        Box("Dungeon_Wall", c + new Vector3(side, DWallH / 2f, z), new Vector3(DRoomHalf - DCorrHalf, DWallH, 0.4f), dStone);
                    }
                    else Box("Dungeon_Wall", c + new Vector3(0f, DWallH / 2f, z), new Vector3(DRoomHalf * 2f + 0.4f, DWallH, 0.4f), dStone);
                }
                // 通路
                if (i < 4)
                {
                    float cz = c.z + DungeonStep / 2f;
                    float cl = DungeonStep - DRoomHalf * 2f;
                    Box("Dungeon_Wall", new Vector3(c.x - DCorrHalf - 0.2f, DWallH / 2f, cz), new Vector3(0.4f, DWallH, cl), dStone);
                    Box("Dungeon_Wall", new Vector3(c.x + DCorrHalf + 0.2f, DWallH / 2f, cz), new Vector3(0.4f, DWallH, cl), dStone);
                }
                // たいまつ（左右の壁）
                for (int k = -1; k <= 1; k += 2)
                {
                    Vector3 tp = c + new Vector3(k * (DRoomHalf - 0.35f), 2.1f, 1.5f);
                    Box("Dungeon_Torch", tp, new Vector3(0.12f, 0.35f, 0.12f), woodDark);
                    Box("Dungeon_TorchFire", tp + Vector3.up * 0.25f, new Vector3(0.16f, 0.2f, 0.16f), glowFire != null ? glowFire : glowLamp);
                    Lamp(tp + Vector3.up * 0.3f + new Vector3(-k * 0.4f, 0f, 0f), 1.8f, 8f, false, 0.15f);
                }
            }
            // 入口の魔法陣と、いちばん奥の階段
            Cyl("Dungeon_Circle", o + Vector3.up * 0.02f, new Vector3(3.2f, 0.01f, 3.2f), Quaternion.identity, dGlowBlue);
            Cyl("Dungeon_CircleIn", o + Vector3.up * 0.03f, new Vector3(2.6f, 0.01f, 2.6f), Quaternion.identity, dFloor);
            Vector3 st = o + new Vector3(0f, 0f, 4f * DungeonStep + 2.2f);
            for (int k = 0; k < 4; k++) Box("Dungeon_Stairs", st + new Vector3(0f, -0.06f - k * 0.02f, k * 0.35f), new Vector3(2.4f, 0.12f, 0.35f), ProtoArt.Mat(new Color(0.1f - k * 0.02f, 0.1f - k * 0.02f, 0.12f - k * 0.02f)));
            Label(o + Vector3.up * 3.6f, "ダンジョン実習（地下）");
            Label(st + Vector3.up * 2.4f, "下の階へ");
            AddArea("ダンジョン", Rect.MinMaxRect(o.x - DRoomHalf, o.z - DRoomHalf, o.x + DRoomHalf, o.z + 4f * DungeonStep + DRoomHalf), 0);
            dungeonDyn = new GameObject("DungeonDyn").transform;
            dungeonDyn.SetParent(root, false);
        }

        /// <summary>その階に入った（色を少し変える）。</summary>
        public void DungeonBuildFloor(int floor)
        {
            DungeonClearStage();
            dungeonFloorShown = floor;
            if (dFloor != null) dFloor.color = Color.Lerp(new Color(0.30f, 0.29f, 0.33f), new Color(0.24f, 0.18f, 0.28f), (floor - 1) / 4f);
        }

        public void DungeonClearStage()
        {
            foreach (var g in dungeonEventObjs) if (g != null) Destroy(g);
            dungeonEventObjs.Clear();
            dungeonChestLid = null;
        }

        GameObject DynBox(string n, Vector3 p, Vector3 s, Material m)
        {
            var g = Box(n, p, s, m, dungeonDyn);
            dungeonEventObjs.Add(g);
            return g;
        }

        GameObject DynCyl(string n, Vector3 p, Vector3 s, Material m, PrimitiveType t = PrimitiveType.Cylinder)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = n;
            g.transform.SetParent(dungeonDyn, false);
            g.transform.localPosition = p;
            g.transform.localScale = s;
            g.GetComponent<Renderer>().sharedMaterial = m;
            Destroy(g.GetComponent<Collider>());
            dungeonEventObjs.Add(g);
            return g;
        }

        /// <summary>部屋の出来事（魔物・罠・宝箱・泉）を置く。</summary>
        public void DungeonShowEvent(DungeonRun r)
        {
            DungeonClearStage();
            Vector3 c = DungeonDirector.RoomCenter(r.room) + new Vector3(0f, 0f, 1.5f);
            switch (r.ev)
            {
                case DungeonRun.Ev.Monster:
                    for (int i = 0; i < r.evCount; i++) MakeMonster(r.floor, c + new Vector3((i - (r.evCount - 1) / 2f) * 1.4f, 0f, 1.6f + (i % 2) * 0.5f));
                    break;
                case DungeonRun.Ev.Trap:
                    DynCyl("Dungeon_Trap", c + new Vector3(0f, 0.02f, 0.5f), new Vector3(2.2f, 0.01f, 2.2f), dGlowRed);
                    break;
                case DungeonRun.Ev.Chest:
                {
                    var b = DynBox("Dungeon_Chest", c + new Vector3(0f, 0.3f, 1.6f), new Vector3(0.9f, 0.6f, 0.6f), woodDark);
                    dungeonChestLid = DynBox("Dungeon_ChestLid", c + new Vector3(0f, 0.66f, 1.6f), new Vector3(0.95f, 0.14f, 0.65f), woodMid);
                    DynBox("Dungeon_ChestBand", c + new Vector3(0f, 0.32f, 1.28f), new Vector3(0.12f, 0.5f, 0.04f), brassMat != null ? brassMat : iron);
                    b.name = "Dungeon_Chest";
                    break;
                }
                case DungeonRun.Ev.Spring:
                    DynCyl("Dungeon_SpringRim", c + new Vector3(0f, 0.15f, 1.6f), new Vector3(2.0f, 0.15f, 2.0f), dStone);
                    DynCyl("Dungeon_Spring", c + new Vector3(0f, 0.31f, 1.6f), new Vector3(1.7f, 0.01f, 1.7f), dGlowBlue);
                    break;
                case DungeonRun.Ev.Altar:
                    DynBox("Dungeon_Altar", c + new Vector3(0f, 0.5f, 1.8f), new Vector3(1.6f, 1.0f, 0.9f), dStone);
                    DynCyl("Dungeon_Treasure", c + new Vector3(0f, 1.25f, 1.8f), new Vector3(0.45f, 0.45f, 0.45f), crystalMat != null ? crystalMat : dGlowPurple, PrimitiveType.Sphere);
                    break;
            }
        }

        /// <summary>魔物の形（階ごとに変わる：スライム・大コウモリ・骸骨の兵士・石の番人・影の魔物）。</summary>
        void MakeMonster(int floor, Vector3 p)
        {
            var holder = new GameObject("Dungeon_Monster");
            holder.transform.SetParent(dungeonDyn, false);
            holder.transform.localPosition = p;
            holder.transform.localRotation = Quaternion.LookRotation(Vector3.back);
            dungeonEventObjs.Add(holder);
            Transform h = holder.transform;
            System.Func<PrimitiveType, Vector3, Vector3, Material, GameObject> part = (t, lp, ls, m) =>
            {
                var g = GameObject.CreatePrimitive(t);
                g.transform.SetParent(h, false);
                g.transform.localPosition = lp; g.transform.localScale = ls;
                g.GetComponent<Renderer>().sharedMaterial = m;
                Destroy(g.GetComponent<Collider>());
                return g;
            };
            var eye = ProtoArt.Glow(new Color(1f, 0.9f, 0.3f), 3f);
            float bob = 0.08f;
            switch (Mathf.Clamp(floor, 1, 5))
            {
                case 1: // スライム
                    part(PrimitiveType.Sphere, new Vector3(0f, 0.4f, 0f), new Vector3(1.0f, 0.75f, 1.0f), ProtoArt.Glow(new Color(0.35f, 0.9f, 0.45f), 0.8f));
                    part(PrimitiveType.Sphere, new Vector3(-0.18f, 0.55f, 0.42f), Vector3.one * 0.12f, eye);
                    part(PrimitiveType.Sphere, new Vector3(0.18f, 0.55f, 0.42f), Vector3.one * 0.12f, eye);
                    break;
                case 2: // 大コウモリ
                    bob = 0.25f;
                    part(PrimitiveType.Sphere, new Vector3(0f, 1.7f, 0f), new Vector3(0.45f, 0.4f, 0.4f), ProtoArt.Mat(new Color(0.2f, 0.15f, 0.25f)));
                    part(PrimitiveType.Cube, new Vector3(-0.55f, 1.75f, 0f), new Vector3(0.7f, 0.04f, 0.35f), ProtoArt.Mat(new Color(0.15f, 0.1f, 0.2f)));
                    part(PrimitiveType.Cube, new Vector3(0.55f, 1.75f, 0f), new Vector3(0.7f, 0.04f, 0.35f), ProtoArt.Mat(new Color(0.15f, 0.1f, 0.2f)));
                    part(PrimitiveType.Sphere, new Vector3(-0.1f, 1.75f, 0.2f), Vector3.one * 0.07f, ProtoArt.Glow(new Color(1f, 0.2f, 0.2f), 3f));
                    part(PrimitiveType.Sphere, new Vector3(0.1f, 1.75f, 0.2f), Vector3.one * 0.07f, ProtoArt.Glow(new Color(1f, 0.2f, 0.2f), 3f));
                    break;
                case 3: // 骸骨の兵士
                {
                    var bone = ProtoArt.Mat(new Color(0.9f, 0.88f, 0.8f));
                    part(PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(0.28f, 0.45f, 0.2f), bone);
                    part(PrimitiveType.Sphere, new Vector3(0f, 1.55f, 0f), Vector3.one * 0.32f, bone);
                    part(PrimitiveType.Cube, new Vector3(-0.12f, 0.25f, 0f), new Vector3(0.08f, 0.5f, 0.08f), bone);
                    part(PrimitiveType.Cube, new Vector3(0.12f, 0.25f, 0f), new Vector3(0.08f, 0.5f, 0.08f), bone);
                    part(PrimitiveType.Cube, new Vector3(0.32f, 1.0f, 0.15f), new Vector3(0.05f, 0.9f, 0.05f), iron);
                    part(PrimitiveType.Sphere, new Vector3(-0.07f, 1.58f, 0.14f), Vector3.one * 0.06f, eye);
                    part(PrimitiveType.Sphere, new Vector3(0.07f, 1.58f, 0.14f), Vector3.one * 0.06f, eye);
                    bob = 0.03f;
                    break;
                }
                case 4: // 石の番人
                {
                    var rock = ProtoArt.Mat(new Color(0.5f, 0.48f, 0.45f), ProtoArt.StoneWall, Vector2.one, 0.05f);
                    part(PrimitiveType.Cube, new Vector3(0f, 1.0f, 0f), new Vector3(1.0f, 1.1f, 0.7f), rock);
                    part(PrimitiveType.Cube, new Vector3(0f, 1.8f, 0f), new Vector3(0.55f, 0.45f, 0.5f), rock);
                    part(PrimitiveType.Cube, new Vector3(-0.7f, 1.0f, 0f), new Vector3(0.35f, 1.0f, 0.35f), rock);
                    part(PrimitiveType.Cube, new Vector3(0.7f, 1.0f, 0f), new Vector3(0.35f, 1.0f, 0.35f), rock);
                    part(PrimitiveType.Cube, new Vector3(-0.25f, 0.25f, 0f), new Vector3(0.35f, 0.5f, 0.35f), rock);
                    part(PrimitiveType.Cube, new Vector3(0.25f, 0.25f, 0f), new Vector3(0.35f, 0.5f, 0.35f), rock);
                    part(PrimitiveType.Cube, new Vector3(0f, 1.85f, 0.26f), new Vector3(0.35f, 0.08f, 0.02f), ProtoArt.Glow(new Color(0.4f, 0.8f, 1f), 3f));
                    bob = 0.02f;
                    break;
                }
                default: // 影の魔物
                    part(PrimitiveType.Sphere, new Vector3(0f, 1.1f, 0f), new Vector3(1.0f, 1.4f, 1.0f), ProtoArt.Mat(new Color(0.08f, 0.04f, 0.12f)));
                    part(PrimitiveType.Sphere, new Vector3(0f, 1.1f, 0f), new Vector3(1.2f, 1.6f, 1.2f), ProtoArt.Glow(new Color(0.35f, 0.1f, 0.5f), 0.4f));
                    part(PrimitiveType.Sphere, new Vector3(-0.18f, 1.4f, 0.45f), Vector3.one * 0.12f, ProtoArt.Glow(new Color(0.9f, 0.3f, 1f), 3f));
                    part(PrimitiveType.Sphere, new Vector3(0.18f, 1.4f, 0.45f), Vector3.one * 0.12f, ProtoArt.Glow(new Color(0.9f, 0.3f, 1f), 3f));
                    bob = 0.15f;
                    break;
            }
            var b = holder.AddComponent<DungeonBob>();
            b.amount = bob;
        }

        /// <summary>出来事が片づいた：魔物は小さくなって消える。宝箱はふたが開く。</summary>
        public void DungeonClearEvent(bool monster)
        {
            if (monster)
            {
                foreach (var g in dungeonEventObjs) if (g != null && g.name == "Dungeon_Monster") g.AddComponent<DungeonShrink>();
            }
            else if (dungeonChestLid != null)
            {
                dungeonChestLid.transform.localPosition += new Vector3(0f, 0.25f, 0.3f);
                dungeonChestLid.transform.localRotation = Quaternion.Euler(-70f, 0f, 0f);
                var c = dungeonChestLid.transform.localPosition;
                DynCyl("Dungeon_Sparkle", c + new Vector3(0f, -0.1f, -0.2f), Vector3.one * 0.35f, ProtoArt.Glow(new Color(1f, 0.85f, 0.4f), 3f), PrimitiveType.Sphere).AddComponent<DungeonShrink>().time = 1.5f;
            }
        }

        /// <summary>罠にかかった：赤く光る。</summary>
        public void DungeonFlash()
        {
            foreach (var g in dungeonEventObjs) if (g != null && g.name == "Dungeon_Trap") g.transform.localScale = new Vector3(3.2f, 0.01f, 3.2f);
            var l = new GameObject("Dungeon_Flash").AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.3f, 0.2f); l.intensity = 4f; l.range = 8f;
            l.transform.SetParent(dungeonDyn, false);
            foreach (var g in dungeonEventObjs) if (g != null && g.name == "Dungeon_Trap") { l.transform.position = g.transform.position + Vector3.up; break; }
            l.gameObject.AddComponent<DungeonShrink>().time = 0.6f;
            dungeonEventObjs.Add(l.gameObject);
        }

        /// <summary>宝箱がミミックだった：紫になって跳ねる。</summary>
        public void DungeonMimic()
        {
            foreach (var g in dungeonEventObjs)
            {
                if (g == null || !g.name.StartsWith("Dungeon_Chest")) continue;
                g.GetComponent<Renderer>().sharedMaterial = dGlowPurple;
                g.name = "Dungeon_Monster";
                if (g.GetComponent<DungeonBob>() == null) g.AddComponent<DungeonBob>().amount = 0.15f;
            }
            dungeonChestLid = null;
        }

        /// <summary>篤の組がダンジョンに入ったら、カメラで篤を追いかける。</summary>
        public void FollowInDungeon(SimCharacter c)
        {
            var rig = mainCamera != null ? mainCamera.GetComponent<CameraRig>() : null;
            if (rig == null || c == null) return;
            rig.follow = c.transform;
            rig.pivot = c.transform.position + Vector3.up;
            rig.distance = 12f;
            rig.pitch = 52f;
            rig.yaw = 0f;
        }

        // ================= 寮杯の優勝杯 =================
        GameObject cupTrophy;

        /// <summary>談話室（暖炉の右）に優勝杯を飾る。</summary>
        public void ShowCupTrophy(bool on)
        {
            if (cupTrophy != null) { cupTrophy.SetActive(on); return; }
            if (!on) return;
            cupTrophy = new GameObject("CupTrophy");
            cupTrophy.transform.SetParent(root, false);
            Vector3 b = FireplaceSpot + new Vector3(2.7f, 0f, 1.2f);
            var gold = ProtoArt.Glow(new Color(1f, 0.8f, 0.3f), 1.2f);
            Box("Cup_Stand", b + Vector3.up * 0.45f, new Vector3(0.5f, 0.9f, 0.5f), woodDark, cupTrophy.transform);
            Cyl("Cup_Base", b + Vector3.up * 0.95f, new Vector3(0.28f, 0.05f, 0.28f), Quaternion.identity, gold, cupTrophy.transform);
            Cyl("Cup_Stem", b + Vector3.up * 1.08f, new Vector3(0.08f, 0.1f, 0.08f), Quaternion.identity, gold, cupTrophy.transform);
            Cyl("Cup_Bowl", b + Vector3.up * 1.32f, new Vector3(0.32f, 0.16f, 0.32f), Quaternion.identity, gold, cupTrophy.transform);
            Label(b + Vector3.up * 1.9f, "寮杯（" + HouseCup.OurName + "）");
        }
    }

    /// <summary>ふわふわ上下に動く（魔物）。</summary>
    public class DungeonBob : MonoBehaviour
    {
        public float amount = 0.08f;
        Vector3 basePos; float seed;
        void Start() { basePos = transform.localPosition; seed = Random.value * 10f; }
        void Update() { transform.localPosition = basePos + Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 3f + seed)) * amount; }
    }

    /// <summary>小さくなって消える。</summary>
    public class DungeonShrink : MonoBehaviour
    {
        public float time = 0.8f;
        float t; Vector3 s0;
        void Start() { s0 = transform.localScale; }
        void Update()
        {
            t += Time.deltaTime;
            transform.localScale = s0 * Mathf.Max(0f, 1f - t / time);
            var l = GetComponent<Light>(); if (l != null) l.intensity *= 0.9f;
            if (t >= time) Destroy(gameObject);
        }
    }
}
