using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 浴場・脱衣所・露天風呂の家具（依頼34 追記2＝浴場_v3。洗い場8・ロッカー8・湯船8席・岩風呂）。家具のチャット 2026-11-15。
    /// 置き場所はチャッピーの配置見本（浴場_v3\検証\layout.json）どおり：X＝部屋の左（西）から、Y＝手前（−z）から。
    /// 家具の向きの角度は見本の値から 180−角度 で変える（見本の正面は −Y、ゲームの正面は +Z）。
    /// </summary>
    public partial class LoungeWorld
    {
        const string BathF = "Bath/";
        static bool BathV3On { get { return HasFurn(BathF + "Furn_Bath_Tub"); } }
        /// <summary>湯船・岩風呂は床より 0.22m 沈むので、その形だけ床をくりぬく。</summary>
        static Rect BathTubHole { get { return Rect.MinMaxRect(-WingOut, BackZ + 1f, -WingOut + 4f, BackZ + 6f); } }
        static Rect SpaTubHole { get { return Rect.MinMaxRect(-WingOut - 4.5f + 0.9f + 0f, BackOut + 0.4f, -WingOut - 4.5f + 0.9f + 4.4f, BackOut + 4.2f); } }

        readonly List<KeyValuePair<string, GameObject>> bathPlaced = new List<KeyValuePair<string, GameObject>>();

        GameObject Pl(string asset, float ox, float oz, float X, float Y, float Z, float a)
        {
            var go = PlaceFurn(BathF + "Furn_" + asset, null, new Vector3(ox + X, Z, oz + Y), 180f - a);
            if (go != null) bathPlaced.Add(new KeyValuePair<string, GameObject>(asset, go));
            return go;
        }

        void BBlock(float cx, float cz, float w, float d, float a)
        {
            bool sw = Mathf.Abs(Mathf.Sin(a * Mathf.Deg2Rad)) > 0.7f;
            float bw = sw ? d : w, bd = sw ? w : d;
            blockers[0].Add(new Rect(cx - bw / 2f, cz - bd / 2f, bw, bd));
        }

        /// <summary>浴場の床（湯船のところは穴）。</summary>
        void BathFloor(Material m)
        {
            var h = BathTubHole;
            Slab("Floor_Bath", 0, Rect.MinMaxRect(-WingOut, BackZ, -5f, h.yMin), m);
            Slab("Floor_Bath", 0, Rect.MinMaxRect(-WingOut, h.yMax, -5f, BackOut), m);
            Slab("Floor_Bath", 0, Rect.MinMaxRect(h.xMax, h.yMin, -5f, h.yMax), m);
        }

        /// <summary>浴場と脱衣所の家具・湯船の席・洗い場。</summary>
        void NBathV3()
        {
            bathPlaced.Clear();
            // ---- 浴場（x −12〜−5・z 5.5〜15.5） ----
            Pl("Bath_Tub", -WingOut, BackZ, 2f, 3.5f, 0f, 0f);
            Pl("Bath_LionSpout", -WingOut, BackZ, 0f, 3.5f, 0.42f, 90f);
            Pl("Bath_WashStation", -WingOut, BackZ, 6.7f, 3.5f, 0f, -90f);
            BBlock(-WingOut + 6.7f, BackZ + 3.5f, 1.000f, 0.600f, -90f);
            Pl("Bath_WashStation", -WingOut, BackZ, 6.7f, 4.85f, 0f, -90f);
            BBlock(-WingOut + 6.7f, BackZ + 4.85f, 1.000f, 0.600f, -90f);
            Pl("Bath_WashStation", -WingOut, BackZ, 6.7f, 6.2f, 0f, -90f);
            BBlock(-WingOut + 6.7f, BackZ + 6.2f, 1.000f, 0.600f, -90f);
            Pl("Bath_WashStation", -WingOut, BackZ, 6.7f, 7.55f, 0f, -90f);
            BBlock(-WingOut + 6.7f, BackZ + 7.55f, 1.000f, 0.600f, -90f);
            Pl("Bath_WashStation", -WingOut, BackZ, 6.7f, 8.9f, 0f, -90f);
            BBlock(-WingOut + 6.7f, BackZ + 8.9f, 1.000f, 0.600f, -90f);
            Pl("Bath_WashStation", -WingOut, BackZ, 3.9f, 9.7f, 0f, 0f);
            BBlock(-WingOut + 3.9f, BackZ + 9.7f, 1.000f, 0.600f, 0f);
            Pl("Bath_WashStation", -WingOut, BackZ, 4.9f, 9.7f, 0f, 0f);
            BBlock(-WingOut + 4.9f, BackZ + 9.7f, 1.000f, 0.600f, 0f);
            Pl("Bath_WashStation", -WingOut, BackZ, 5.9f, 9.7f, 0f, 0f);
            BBlock(-WingOut + 5.9f, BackZ + 9.7f, 1.000f, 0.600f, 0f);
            Pl("Bath_FootMat", -WingOut, BackZ, 6.65f, 2.5f, 0f, 90f);
            Pl("Bath_FootMat", -WingOut, BackZ, 2.4f, 9.65f, 0f, 0f);
            Pl("Bath_PlantPot", -WingOut, BackZ, 0.4f, 9.3f, 0f, 0f);
            BBlock(-WingOut + 0.4f, BackZ + 9.3f, 0.600f, 0.600f, 0f);
            Pl("Bath_BucketStack", -WingOut, BackZ, 0.55f, 7.5f, 0f, 0f);
            BBlock(-WingOut + 0.55f, BackZ + 7.5f, 0.700f, 0.500f, 0f);
            Pl("Bath_TowelHook", -WingOut, BackZ, 7f, 1.2f, 1.1f, -90f);
            Pl("Bath_WallLantern", -WingOut, BackZ, 0f, 7.5f, 1.9f, 90f);
            Pl("Bath_WallLantern", -WingOut, BackZ, 7f, 1.4f, 1.9f, -90f);
            Pl("Bath_WallLantern", -WingOut, BackZ, 4.5f, 10f, 2.2f, 0f);
            // ---- 脱衣所（x −5〜−2.5） ----
            Pl("Dress_Locker", -5f, BackZ, 0.275f, 3.45f, 0f, 90f);
            BBlock(-5f + 0.275f, BackZ + 3.45f, 0.700f, 0.550f, 90f);
            Pl("Dress_Locker", -5f, BackZ, 0.275f, 4.15f, 0f, 90f);
            BBlock(-5f + 0.275f, BackZ + 4.15f, 0.700f, 0.550f, 90f);
            Pl("Dress_Locker", -5f, BackZ, 0.275f, 4.85f, 0f, 90f);
            BBlock(-5f + 0.275f, BackZ + 4.85f, 0.700f, 0.550f, 90f);
            Pl("Dress_Locker", -5f, BackZ, 0.275f, 5.55f, 0f, 90f);
            BBlock(-5f + 0.275f, BackZ + 5.55f, 0.700f, 0.550f, 90f);
            Pl("Dress_Locker", -5f, BackZ, 0.275f, 6.25f, 0f, 90f);
            BBlock(-5f + 0.275f, BackZ + 6.25f, 0.700f, 0.550f, 90f);
            Pl("Dress_Locker", -5f, BackZ, 0.275f, 6.95f, 0f, 90f);
            BBlock(-5f + 0.275f, BackZ + 6.95f, 0.700f, 0.550f, 90f);
            Pl("Dress_Locker", -5f, BackZ, 0.275f, 7.6499999999999995f, 0f, 90f);
            BBlock(-5f + 0.275f, BackZ + 7.6499999999999995f, 0.700f, 0.550f, 90f);
            Pl("Dress_Locker", -5f, BackZ, 0.275f, 8.35f, 0f, 90f);
            BBlock(-5f + 0.275f, BackZ + 8.35f, 0.700f, 0.550f, 90f);
            Pl("Dress_BasketShelf", -5f, BackZ, 0.225f, 0.95f, 0f, 90f);
            BBlock(-5f + 0.225f, BackZ + 0.95f, 1.600f, 0.450f, 90f);
            Pl("Dress_Bench", -5f, BackZ, 0.85f, 4.1f, 0f, 90f);
            BBlock(-5f + 0.85f, BackZ + 4.1f, 1.800f, 0.385f, 90f);
            Pl("Dress_Bench", -5f, BackZ, 0.85f, 6.1f, 0f, 90f);
            BBlock(-5f + 0.85f, BackZ + 6.1f, 1.800f, 0.385f, 90f);
            Pl("Dress_Bench", -5f, BackZ, 0.85f, 8.1f, 0f, 90f);
            BBlock(-5f + 0.85f, BackZ + 8.1f, 1.800f, 0.385f, 90f);
            Pl("Dress_Vanity", -5f, BackZ, 2.25f, 4.4f, 0f, -90f);
            BBlock(-5f + 2.25f, BackZ + 4.4f, 1.200f, 0.500f, -90f);
            Pl("Dress_WaterTable", -5f, BackZ, 2.25f, 6.0f, 0f, -90f);
            BBlock(-5f + 2.25f, BackZ + 6.0f, 0.600f, 0.400f, -90f);
            Pl("Dress_Scale", -5f, BackZ, 2.2f, 7.1f, 0f, -90f);
            BBlock(-5f + 2.2f, BackZ + 7.1f, 0.500f, 0.600f, -90f);
            Pl("Dress_MagicFan", -5f, BackZ, 2.275f, 9.1f, 0f, 0f);
            BBlock(-5f + 2.275f, BackZ + 9.1f, 0.455f, 0.455f, 0f);
            Pl("Bath_FootMat", -5f, BackZ, 0.35f, 2.5f, 0f, 90f);
            Pl("Bath_FootMat", -5f, BackZ, 2.15f, 8.1f, 0f, 90f);
            Pl("Dress_Duckboard", -5f, BackZ, 1.5f, 3.7f, 0f, 0f);
            Pl("Dress_Duckboard", -5f, BackZ, 1.5f, 5f, 0f, 0f);
            Pl("Dress_Duckboard", -5f, BackZ, 1.5f, 6.3f, 0f, 0f);
            Pl("Dress_Duckboard", -5f, BackZ, 1.5f, 7.6f, 0f, 0f);
            Pl("Dress_Duckboard", -5f, BackZ, 1.5f, 8.9f, 0f, 0f);
            Pl("Dress_HangingLamp", -5f, BackZ, 1.25f, 2f, 3.4f, 0f);
            Pl("Dress_HangingLamp", -5f, BackZ, 1.25f, 5f, 3.4f, 0f);
            Pl("Dress_HangingLamp", -5f, BackZ, 1.25f, 8f, 3.4f, 0f);
            blockers[0].Add(BathTubHole);   // 湯船（人は席へ直接入る）
            BathAfterPlace("浴場");
        }

        /// <summary>置いた家具から、席・洗い場・明かりをつくる。</summary>
        void BathAfterPlace(string area)
        {
            var washStands = new List<Vector3>();
            Vector3 washLook = Vector3.zero;
            foreach (var kv in bathPlaced)
            {
                var go = kv.Value;
                if (kv.Key == "Bath_Tub")
                {
                    Vector3 tc = root.InverseTransformPoint(go.transform.position);
                    for (int k = 1; k <= 8; k++)
                    {
                        Vector3 p = Mark(go, "Seat_" + k, tc);
                        Vector3 fwd = new Vector3(tc.x - p.x, 0f, tc.z - p.z); if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.right; fwd.Normalize();
                        seats.Add(new Seat { kind = SeatKind.Bath, label = "湯船", area = "浴場", position = p, forward = fwd, approach = new Vector3(tc.x + 2.6f, 0f, p.z) });
                    }
                }
                else if (kv.Key == "Bath_WashStation")
                {
                    Vector3 s = Mark(go, "Seat_1", go.transform.position), l = Mark(go, "Look", go.transform.position);
                    Vector3 dir = new Vector3(s.x - l.x, 0f, s.z - l.z); if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward; dir.Normalize();
                    washStands.Add(new Vector3(s.x + dir.x * 0.5f, 0f, s.z + dir.z * 0.5f));
                    washLook = new Vector3(l.x, 1.3f, l.z);
                }
                else if (kv.Key == "Spa_Bench")
                {
                    for (int k = 1; k <= 2; k++)
                    {
                        Vector3 p = Mark(go, "Seat_" + k, go.transform.position);
                        Vector3 f = go.transform.position - p; f.y = 0f;
                        // 長いすの座る向きは、いすの正面（Seat の向き）。向きの目じるしが無いので、いすの前（置いた向き）へ
                        Vector3 fwd = go.transform.forward; fwd.y = 0f; fwd.Normalize();
                        seats.Add(new Seat { kind = SeatKind.Sit, label = "露天風呂の長いす", area = "浴場", position = new Vector3(p.x, 0f, p.z), forward = fwd, approach = new Vector3(p.x, 0f, p.z) + fwd * 0.7f });
                    }
                }
                else if (kv.Key == "Spa_HotStone" || kv.Key == "Spa_Lantern")
                {
                    Vector3 lp = Mark(go, "Light_1", go.transform.position + Vector3.up * 0.8f);
                    Lamp(lp, 1.2f, 4f);
                }
            }
            if (washStands.Count > 0) pois.Add(MakePoi("洗い場で体を洗っている", washLook, washStands.ToArray()));
        }

        /// <summary>露天風呂（浴場の奥・南東のすみ）。岩風呂・塀・庭の小物。</summary>
        void NSpaV3()
        {
            float x0 = -WingOut - 4.5f, x1 = -6.5f, z0 = BackOut, z1 = BackOut + 4.5f;
            bathPlaced.Clear();
            // 床（岩風呂のところは穴）
            var h = SpaTubHole;
            Box("Spa_Floor", new Vector3((x0 + h.xMin) / 2f, -0.04f, (z0 + z1) / 2f), new Vector3(h.xMin - x0, 0.08f, z1 - z0), flagstone);
            Box("Spa_Floor", new Vector3((h.xMax + x1) / 2f, -0.04f, (z0 + z1) / 2f), new Vector3(x1 - h.xMax, 0.08f, z1 - z0), flagstone);
            Box("Spa_Floor", new Vector3((h.xMin + h.xMax) / 2f, -0.04f, (z0 + h.yMin) / 2f), new Vector3(h.xMax - h.xMin, 0.08f, h.yMin - z0), flagstone);
            Box("Spa_Floor", new Vector3((h.xMin + h.xMax) / 2f, -0.04f, (h.yMax + z1) / 2f), new Vector3(h.xMax - h.xMin, 0.08f, z1 - h.yMax), flagstone);
            Pl("Spa_RockTub", -WingOut - 4.5f, BackOut, 3.1f, 2.3f, 0f, 0f);
            Pl("Spa_Fence", -WingOut - 4.5f, BackOut, 1f, 4.5f, 0f, 0f);
            Pl("Spa_Fence", -WingOut - 4.5f, BackOut, 3f, 4.5f, 0f, 0f);
            Pl("Spa_Fence", -WingOut - 4.5f, BackOut, 5f, 4.5f, 0f, 0f);
            Pl("Spa_Fence", -WingOut - 4.5f, BackOut, 7f, 4.5f, 0f, 0f);
            Pl("Spa_Fence", -WingOut - 4.5f, BackOut, 9f, 4.5f, 0f, 0f);
            Pl("Spa_Fence", -WingOut - 4.5f, BackOut, 0f, 1.1f, 0f, 90f);
            Pl("Spa_Fence", -WingOut - 4.5f, BackOut, 0f, 3.3f, 0f, 90f);
            Pl("Spa_Fence", -WingOut - 4.5f, BackOut, 10f, 1.1f, 0f, 90f);
            Pl("Spa_Fence", -WingOut - 4.5f, BackOut, 10f, 3.3f, 0f, 90f);
            Pl("Spa_FencePost", -WingOut - 4.5f, BackOut, 0f, 0f, 0f, 0f);
            Pl("Spa_FencePost", -WingOut - 4.5f, BackOut, 0f, 4.5f, 0f, 0f);
            Pl("Spa_FencePost", -WingOut - 4.5f, BackOut, 10f, 0f, 0f, 0f);
            Pl("Spa_FencePost", -WingOut - 4.5f, BackOut, 10f, 4.5f, 0f, 0f);
            Pl("Spa_Spout", -WingOut - 4.5f, BackOut, 3.3f, 3.6f, 0.42f, 0f);
            Pl("Spa_HotStone", -WingOut - 4.5f, BackOut, 2.1f, 3.75f, 0.1f, 0f);
            Pl("Spa_Bench", -WingOut - 4.5f, BackOut, 7.1f, 3.9f, 0f, 0f);
            BBlock(-WingOut - 4.5f + 7.1f, BackOut + 3.9f, 1.400f, 0.440f, 0f);
            Pl("Spa_TowelStand", -WingOut - 4.5f, BackOut, 9f, 3.8f, 0f, 0f);
            BBlock(-WingOut - 4.5f + 9f, BackOut + 3.8f, 0.800f, 0.450f, 0f);
            Pl("Spa_WashSpot", -WingOut - 4.5f, BackOut, 9.2f, 1.5f, 0f, -90f);
            BBlock(-WingOut - 4.5f + 9.2f, BackOut + 1.5f, 0.900f, 0.700f, -90f);
            Pl("Spa_Plants", -WingOut - 4.5f, BackOut, 0.45f, 3.7f, 0f, 0f);
            BBlock(-WingOut - 4.5f + 0.45f, BackOut + 3.7f, 1.200f, 0.800f, 0f);
            Pl("Spa_SteppingStones", -WingOut - 4.5f, BackOut, 5.8f, 1.2f, 0f, 0f);
            Pl("Spa_SteppingStones", -WingOut - 4.5f, BackOut, 6.9f, 1.5f, 0f, 0f);
            Pl("Spa_SteppingStones", -WingOut - 4.5f, BackOut, 8f, 1.8f, 0f, 0f);
            Pl("Spa_Pebbles", -WingOut - 4.5f, BackOut, 5.1f, 3f, 0f, 0f);
            Pl("Spa_Pebbles", -WingOut - 4.5f, BackOut, 5f, 1.2f, 0f, 0f);
            Pl("Spa_Pebbles", -WingOut - 4.5f, BackOut, 1.1f, 0.5f, 0f, 0f);
            Pl("Spa_Lantern", -WingOut - 4.5f, BackOut, 0.4f, 0.3f, 0f, 0f);
            BBlock(-WingOut - 4.5f + 0.4f, BackOut + 0.3f, 0.400f, 0.400f, 0f);
            Pl("Spa_Lantern", -WingOut - 4.5f, BackOut, 9.6f, 4.1f, 0f, 0f);
            BBlock(-WingOut - 4.5f + 9.6f, BackOut + 4.1f, 0.400f, 0.400f, 0f);
            // 岩風呂の席（6人。まわりから入る）
            Vector3 pc = new Vector3((h.xMin + h.xMax) / 2f, 0f, (h.yMin + h.yMax) / 2f);
            foreach (var kv in bathPlaced)
            {
                if (kv.Key != "Spa_RockTub") continue;
                for (int k = 1; k <= 6; k++)
                {
                    Vector3 p = Mark(kv.Value, "Seat_" + k, pc);
                    Vector3 fwd = new Vector3(pc.x - p.x, 0f, pc.z - p.z); if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.right; fwd.Normalize();
                    Vector3 outDir = -fwd;
                    seats.Add(new Seat { kind = SeatKind.Bath, label = "露天風呂", area = "浴場", position = p, forward = fwd, approach = new Vector3(h.xMax + 0.9f, 0f, pc.z) });   // 浴場の側（東）の入る側のへり
                }
            }
            blockers[0].Add(SpaTubHole);
            BathAfterPlace("浴場");
            Label(new Vector3(pc.x, 2.2f, pc.z), "露天風呂");
            AddArea("浴場", Rect.MinMaxRect(x0, z0, x1, z1), 0);
        }
    }
}
