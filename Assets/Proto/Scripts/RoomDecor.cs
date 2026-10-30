using System.Collections.Generic;
using UnityEngine;

namespace LifeSimProto
{
    /// <summary>家具の置き場（1部屋に1つずつ）。</summary>
    public enum DecorSlot { Window, Desk, Bed, Wall, Corner, Floor }

    /// <summary>
    /// 部屋の模様替え：購買部の「家具」の棚で買って寮生に贈ると、その子の部屋に置かれる（同じ置き場の物は入れかえ）。
    /// 家具は部屋にあるあいだ、欲求に少し効く（NeedMods.cs の "furn:"）。好きな物に合う家具は、とても喜ぶ。
    /// 見た目は仮の形（Claude がプログラムで組んだ箱や球）。絵と3Dはあとで（famuru 決定 2026-10-22）。
    /// 家具は周が変わると無くなる（持ち物と同じ）。篤の部屋には置けない。
    /// </summary>
    public static class RoomDecor
    {
        public class Def
        {
            public ItemKind kind;
            public DecorSlot slot;
            public string[] likes;   // この好きな物（CharProfile.likes）がある子はとても喜ぶ
        }

        public static readonly string[] SlotNames = { "窓辺", "机の上", "ベッド", "壁", "入口の角", "床" };
        public static int SlotCount { get { return SlotNames.Length; } }

        public static readonly Def[] All =
        {
            new Def { kind = ItemKind.FurnPlant,     slot = DecorSlot.Window, likes = new[] { "植物" } },
            new Def { kind = ItemKind.FurnMoonLamp,  slot = DecorSlot.Window, likes = new[] { "月", "夜の散歩" } },
            new Def { kind = ItemKind.FurnLamp,      slot = DecorSlot.Desk,   likes = new[] { "魔道具", "光る物" } },
            new Def { kind = ItemKind.FurnTeaSet,    slot = DecorSlot.Desk,   likes = new[] { "お茶会", "ハーブティー", "お菓子" } },
            new Def { kind = ItemKind.FurnMusicBox,  slot = DecorSlot.Desk,   likes = new[] { "甘いもの", "みんな" } },
            new Def { kind = ItemKind.FurnCookStand, slot = DecorSlot.Desk,   likes = new[] { "料理", "庶民の食べ物", "辛い料理" } },
            new Def { kind = ItemKind.FurnPlush,     slot = DecorSlot.Bed,    likes = new[] { "ふわふわ", "みんな" } },
            new Def { kind = ItemKind.FurnCushion,   slot = DecorSlot.Bed,    likes = new[] { "ふわふわ", "なでる", "編み物" } },
            new Def { kind = ItemKind.FurnTapestry,  slot = DecorSlot.Wall,   likes = new[] { "星", "占い", "月", "満月" } },
            new Def { kind = ItemKind.FurnToolRack,  slot = DecorSlot.Wall,   likes = new[] { "魔道具", "工具", "実験" } },
            new Def { kind = ItemKind.FurnMirror,    slot = DecorSlot.Corner, likes = new[] { "注目", "高い所" } },
            new Def { kind = ItemKind.FurnBookshelf, slot = DecorSlot.Corner, likes = new[] { "本" } },
            new Def { kind = ItemKind.FurnTreasure,  slot = DecorSlot.Corner, likes = new[] { "光る物", "木の実", "工具" } },
            new Def { kind = ItemKind.FurnStove,     slot = DecorSlot.Corner, likes = new[] { "暖炉", "ホットミルク" } },
            new Def { kind = ItemKind.FurnRug,       slot = DecorSlot.Floor,  likes = new[] { "編み物", "走る", "肉" } },
        };

        public static Def Get(ItemKind k) { foreach (var d in All) if (d.kind == k) return d; return null; }

        /// <summary>置いてある家具（記号・家具）。周が変わると消える。</summary>
        public static readonly List<KeyValuePair<string, ItemKind>> Placed = new List<KeyValuePair<string, ItemKind>>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll() { Placed.Clear(); }

        public static int CountIn(string letter) { int n = 0; foreach (var p in Placed) if (p.Key == letter) n++; return n; }

        public static List<ItemKind> In(string letter)
        {
            var r = new List<ItemKind>();
            foreach (var p in Placed) if (p.Key == letter) r.Add(p.Value);
            return r;
        }

        public static string SlotName(ItemKind k) { var d = Get(k); return d != null ? SlotNames[(int)d.slot] : ""; }

        public static bool Liked(CharProfile p, ItemKind k)
        {
            var d = Get(k);
            if (p == null || d == null) return false;
            foreach (var t in d.likes) if (p.Likes(t)) return true;
            return false;
        }
    }

    public partial class LoungeWorld
    {
        readonly Dictionary<string, GameObject> decorObjects = new Dictionary<string, GameObject>();

        /// <summary>寮生に家具を贈る（SimCharacter.ApplyItem から）。</summary>
        public void GiveFurniture(SimCharacter c, SimCharacter giver, ItemDef d)
        {
            var def = RoomDecor.Get(d.kind);
            if (def == null) return;
            if (c.IsTarget || c.room == null)
            {
                Log("「" + d.name + "」は寮生の部屋にしか置けない（持ち物に戻した）");
                if (player != null) player.Refund(d.kind);
                return;
            }
            // 同じ置き場の家具は入れかえる
            for (int i = RoomDecor.Placed.Count - 1; i >= 0; i--)
            {
                var p = RoomDecor.Placed[i];
                var pd = RoomDecor.Get(p.Value);
                if (p.Key != c.letter || pd == null || pd.slot != def.slot) continue;
                var old = ItemBook.Get(p.Value);
                if (old != null && old.Mod != null) c.RemoveMod(old.Mod.id);
                RoomDecor.Placed.RemoveAt(i);
                if (old != null) Log(c.displayName + "の部屋の「" + old.name + "」を片づけた");
            }
            RoomDecor.Placed.Add(new KeyValuePair<string, ItemKind>(c.letter, d.kind));
            if (d.Mod != null) c.AddMod(d.Mod, 0f);
            ShowFurniture(c.letter, d.kind);

            bool liked = RoomDecor.Liked(c.profile, d.kind);
            if (giver != null && giver != c)
            {
                rel.Add(c.id, giver.id, liked ? 10f : 6f, simTime);
                if (giver.IsTarget) ChangeLove(c, liked ? 6f : 3f);
            }
            if (liked) { c.PositiveEvent(1f); c.ShowBubble(c.Say("like", "わあ…！ うれしい！"), 2.5f); }
            else c.ShowBubble(c.Say("thanks", "ありがとう"), 2f);
            Log((giver != null && giver != c ? giver.displayName + "が" : "") + c.displayName + "の部屋に「" + d.name + "」を贈った（" + RoomDecor.SlotName(d.kind) + "に置かれた）" + (liked ? "。とても喜んでいる！" : ""));
            if (RoomDecor.Placed.Count == 1) Album.Add(this, c.displayName + "の部屋に、はじめて家具を贈った（" + d.name + "）");
        }

        /// <summary>置いてある家具を全部作り直す（始め・読み込みのあと）。</summary>
        public void RebuildFurniture()
        {
            foreach (var kv in decorObjects) if (kv.Value != null) Destroy(kv.Value);
            decorObjects.Clear();
            foreach (var p in RoomDecor.Placed) ShowFurniture(p.Key, p.Value);
        }

        void ShowFurniture(string letter, ItemKind k)
        {
            var def = RoomDecor.Get(k);
            var c = characters.Find(x => x.letter == letter);
            if (def == null || c == null || c.room == null || c.room.bed == null) return;
            string key = letter + ":" + def.slot;
            GameObject old;
            if (decorObjects.TryGetValue(key, out old) && old != null) Destroy(old);

            var r = c.room;
            float s = Mathf.Sign(r.bed.forward.x);
            if (s == 0f) s = 1f;
            float corr = s > 0f ? r.rect.xMin : r.rect.xMax;
            float cz = r.rect.center.y, y = r.bed.position.y;
            System.Func<float, float> X = u => corr + s * u; // 廊下の壁から部屋の奥への距離 u（0〜5.4）
            float dzD = cz + 1.25f + 0.15f * s; // 机の上（本番の机は cz+1.25。ランプの反対側。家具 2026-10-26）

            var holder = new GameObject("Decor_" + letter + "_" + def.slot);
            holder.transform.SetParent(root, true);
            decorObjects[key] = holder;
            var t = holder.transform;

            // 本番の家具（家具_v3）があればそれを置く（家具 2026-10-26）
            string fm = null; Vector3 fp = Vector3.zero; float fy = 0f;
            switch (k)
            {
                case ItemKind.FurnPlant: fm = "Furn_DecorPlant"; fp = new Vector3(X(5.7f), y, cz + 0.7f); fy = -90f * s; break;
                case ItemKind.FurnMoonLamp: fm = "Furn_DecorMoonLamp"; fp = new Vector3(X(5.7f), y, cz + 0.7f); fy = -90f * s; break;
                case ItemKind.FurnLamp: fm = "Furn_DecorMagicLamp"; fp = new Vector3(X(5.55f), y + 0.76f, dzD); fy = -90f * s; break;
                case ItemKind.FurnTeaSet: fm = "Furn_DecorTeaSet"; fp = new Vector3(X(5.55f), y + 0.76f, dzD); fy = -90f * s; break;
                case ItemKind.FurnMusicBox: fm = "Furn_DecorMusicBox"; fp = new Vector3(X(5.55f), y + 0.76f, dzD); fy = -90f * s; break;
                case ItemKind.FurnCookStand: fm = "Furn_DecorCookStand"; fp = new Vector3(X(5.55f), y + 0.76f, dzD); fy = -90f * s; break;
                case ItemKind.FurnPlush: fm = "Furn_DecorPlush"; fp = new Vector3(X(5.2f), y + 0.54f, cz - 0.95f); fy = -90f * s; break;
                case ItemKind.FurnCushion: fm = "Furn_DecorCushion"; fp = new Vector3(X(5.2f), y + 0.54f, cz - 0.35f); fy = -90f * s; break;
                case ItemKind.FurnTapestry: fm = "Furn_DecorTapestry"; fp = new Vector3(X(4.2f), y + 1.25f, cz - 1.65f); fy = 0f; break;
                case ItemKind.FurnToolRack: fm = "Furn_DecorToolRack"; fp = new Vector3(X(4.2f), y + 1.3f, cz - 1.65f); fy = 0f; break;
                case ItemKind.FurnMirror: fm = "Furn_DecorMirror"; fp = new Vector3(X(0.4f), y, cz - 1.15f); fy = 90f * s; break;
                case ItemKind.FurnBookshelf: fm = "Furn_DecorBookshelf"; fp = new Vector3(X(0.4f), y, cz - 1.15f); fy = 90f * s; break;
                case ItemKind.FurnTreasure: fm = "Furn_DecorTreasure"; fp = new Vector3(X(0.4f), y, cz - 1.15f); fy = 90f * s; break;
                case ItemKind.FurnStove: fm = "Furn_DecorStove"; fp = new Vector3(X(0.45f), y, cz - 1.15f); fy = 90f * s; break;
                case ItemKind.FurnRug: fm = "Furn_DecorRug"; fp = new Vector3(X(2.3f), y + 0.005f, cz + 0.1f); fy = 0f; break;
            }
            if (fm != null)
            {
                var fgo = PlaceFurn(fm, null, fp, fy);
                if (fgo != null) { fgo.transform.SetParent(t, true); return; }
            }

            switch (k)
            {
                case ItemKind.FurnPlant: // 鉢植えの花（窓辺）
                    DecorPrim(PrimitiveType.Cylinder, t, new Vector3(X(5.7f), y + 0.17f, cz + 0.7f), new Vector3(0.32f, 0.17f, 0.32f), new Color(0.72f, 0.42f, 0.3f));
                    DecorPrim(PrimitiveType.Sphere, t, new Vector3(X(5.7f), y + 0.55f, cz + 0.7f), new Vector3(0.5f, 0.5f, 0.5f), new Color(0.35f, 0.6f, 0.3f));
                    for (int i = 0; i < 4; i++)
                        DecorPrim(PrimitiveType.Sphere, t, new Vector3(X(5.7f) + Mathf.Cos(i * 1.6f) * 0.17f, y + 0.7f + (i % 2) * 0.08f, cz + 0.7f + Mathf.Sin(i * 1.6f) * 0.17f), Vector3.one * 0.11f, i % 2 == 0 ? new Color(0.95f, 0.55f, 0.65f) : new Color(1f, 0.85f, 0.4f));
                    break;
                case ItemKind.FurnMoonLamp: // 月の常夜灯（窓辺）
                    DecorPrim(PrimitiveType.Cylinder, t, new Vector3(X(5.7f), y + 0.3f, cz + 0.7f), new Vector3(0.08f, 0.3f, 0.08f), new Color(0.55f, 0.5f, 0.6f));
                    DecorGlow(t, new Vector3(X(5.7f), y + 0.72f, cz + 0.7f), 0.3f, new Color(0.8f, 0.85f, 1f), 1.8f);
                    break;
                case ItemKind.FurnLamp: // 魔法のランプ（机の上）
                    DecorPrim(PrimitiveType.Cylinder, t, new Vector3(X(5.55f), y + 0.8f, dzD), new Vector3(0.1f, 0.05f, 0.1f), new Color(0.6f, 0.45f, 0.25f));
                    DecorGlow(t, new Vector3(X(5.55f), y + 0.93f, dzD), 0.14f, new Color(0.5f, 0.9f, 1f), 2.5f);
                    break;
                case ItemKind.FurnTeaSet: // ティーセット（机の上）
                    DecorPrim(PrimitiveType.Cylinder, t, new Vector3(X(5.55f), y + 0.79f, dzD), new Vector3(0.3f, 0.01f, 0.3f), new Color(0.85f, 0.8f, 0.7f));
                    DecorPrim(PrimitiveType.Sphere, t, new Vector3(X(5.55f), y + 0.86f, dzD), new Vector3(0.13f, 0.11f, 0.13f), new Color(0.95f, 0.95f, 0.98f));
                    DecorPrim(PrimitiveType.Cylinder, t, new Vector3(X(5.55f) + 0.1f, y + 0.81f, dzD + 0.1f), new Vector3(0.05f, 0.03f, 0.05f), new Color(0.95f, 0.95f, 0.98f));
                    break;
                case ItemKind.FurnMusicBox: // オルゴール（机の上）
                    DecorPrim(PrimitiveType.Cube, t, new Vector3(X(5.55f), y + 0.82f, dzD), new Vector3(0.18f, 0.1f, 0.13f), new Color(0.55f, 0.3f, 0.25f));
                    DecorPrim(PrimitiveType.Cube, t, new Vector3(X(5.55f), y + 0.88f, dzD), new Vector3(0.19f, 0.02f, 0.14f), new Color(0.9f, 0.75f, 0.35f));
                    break;
                case ItemKind.FurnCookStand: // 料理の本立て（机の上）
                    for (int i = 0; i < 3; i++)
                        DecorPrim(PrimitiveType.Cube, t, new Vector3(X(5.55f), y + 0.88f, dzD - 0.07f + i * 0.07f), new Vector3(0.2f, 0.24f, 0.05f), i == 0 ? new Color(0.85f, 0.4f, 0.3f) : (i == 1 ? new Color(0.95f, 0.8f, 0.4f) : new Color(0.45f, 0.65f, 0.4f)));
                    break;
                case ItemKind.FurnPlush: // ぬいぐるみ（ベッド）
                    DecorPrim(PrimitiveType.Sphere, t, new Vector3(X(5.15f), y + 0.66f, cz - 0.95f), new Vector3(0.26f, 0.24f, 0.22f), new Color(0.92f, 0.78f, 0.6f));
                    DecorPrim(PrimitiveType.Sphere, t, new Vector3(X(5.15f), y + 0.85f, cz - 0.95f), new Vector3(0.2f, 0.18f, 0.18f), new Color(0.92f, 0.78f, 0.6f));
                    DecorPrim(PrimitiveType.Sphere, t, new Vector3(X(5.15f), y + 0.95f, cz - 1.03f), Vector3.one * 0.07f, new Color(0.92f, 0.78f, 0.6f));
                    DecorPrim(PrimitiveType.Sphere, t, new Vector3(X(5.15f), y + 0.95f, cz - 0.87f), Vector3.one * 0.07f, new Color(0.92f, 0.78f, 0.6f));
                    break;
                case ItemKind.FurnCushion: // ふわふわのクッション（ベッド）
                    DecorPrim(PrimitiveType.Sphere, t, new Vector3(X(5.15f), y + 0.62f, cz - 0.95f), new Vector3(0.45f, 0.16f, 0.45f), new Color(0.75f, 0.7f, 0.95f));
                    break;
                case ItemKind.FurnTapestry: // 星図のタペストリー（壁）
                    DecorPrim(PrimitiveType.Cube, t, new Vector3(X(4.2f), y + 1.65f, cz - 1.45f), new Vector3(1.1f, 0.85f, 0.02f), new Color(0.15f, 0.18f, 0.38f));
                    for (int i = 0; i < 6; i++)
                        DecorGlow(t, new Vector3(X(4.2f) + (i * 0.37f % 1f - 0.45f), y + 1.4f + (i * 0.23f % 0.55f), cz - 1.43f), 0.035f, new Color(1f, 0.95f, 0.7f), 1.5f);
                    break;
                case ItemKind.FurnToolRack: // 工具の壁掛け（壁）
                    DecorPrim(PrimitiveType.Cube, t, new Vector3(X(4.2f), y + 1.6f, cz - 1.46f), new Vector3(1.0f, 0.6f, 0.03f), new Color(0.55f, 0.4f, 0.28f));
                    for (int i = 0; i < 4; i++)
                        DecorPrim(PrimitiveType.Cube, t, new Vector3(X(4.2f) + (i - 1.5f) * 0.22f, y + 1.6f, cz - 1.43f), new Vector3(0.05f, 0.35f, 0.03f), new Color(0.6f, 0.62f, 0.66f));
                    break;
                case ItemKind.FurnMirror: // 姿見の鏡（入口の角）
                    DecorPrim(PrimitiveType.Cube, t, new Vector3(X(0.35f), y + 0.85f, cz - 1.2f), new Vector3(0.08f, 1.6f, 0.55f), new Color(0.75f, 0.6f, 0.35f));
                    DecorPrim(PrimitiveType.Cube, t, new Vector3(X(0.4f), y + 0.88f, cz - 1.2f), new Vector3(0.02f, 1.4f, 0.45f), new Color(0.75f, 0.85f, 0.95f));
                    break;
                case ItemKind.FurnBookshelf: // 小さな本棚（入口の角）
                    DecorPrim(PrimitiveType.Cube, t, new Vector3(X(0.32f), y + 0.6f, cz - 1.15f), new Vector3(0.4f, 1.2f, 0.6f), new Color(0.5f, 0.36f, 0.25f));
                    for (int row = 0; row < 3; row++)
                        for (int i = 0; i < 4; i++)
                            DecorPrim(PrimitiveType.Cube, t, new Vector3(X(0.45f), y + 0.25f + row * 0.38f, cz - 1.38f + i * 0.13f), new Vector3(0.22f, 0.28f, 0.09f), Color.HSVToRGB((row * 4 + i) * 0.13f % 1f, 0.45f, 0.7f));
                    break;
                case ItemKind.FurnTreasure: // 宝物の飾り棚（入口の角）
                    DecorPrim(PrimitiveType.Cube, t, new Vector3(X(0.32f), y + 0.5f, cz - 1.15f), new Vector3(0.4f, 1.0f, 0.6f), new Color(0.45f, 0.3f, 0.22f));
                    for (int i = 0; i < 5; i++)
                        DecorGlow(t, new Vector3(X(0.5f), y + 0.35f + (i % 3) * 0.3f, cz - 1.35f + i * 0.1f), 0.06f, Color.HSVToRGB(i * 0.19f % 1f, 0.5f, 1f), 1.2f);
                    break;
                case ItemKind.FurnStove: // 小さな暖炉ストーブ（入口の角）
                    DecorPrim(PrimitiveType.Cylinder, t, new Vector3(X(0.4f), y + 0.35f, cz - 1.15f), new Vector3(0.45f, 0.35f, 0.45f), new Color(0.25f, 0.24f, 0.26f));
                    DecorPrim(PrimitiveType.Cylinder, t, new Vector3(X(0.4f), y + 1.2f, cz - 1.15f), new Vector3(0.1f, 0.5f, 0.1f), new Color(0.25f, 0.24f, 0.26f));
                    DecorGlow(t, new Vector3(X(0.62f), y + 0.3f, cz - 1.15f), 0.15f, new Color(1f, 0.55f, 0.2f), 2.5f);
                    break;
                case ItemKind.FurnRug: // 円いラグ（床）
                    DecorPrim(PrimitiveType.Cylinder, t, new Vector3(X(2.3f), y + 0.012f, cz + 0.1f), new Vector3(1.6f, 0.006f, 1.3f), new Color(0.8f, 0.5f, 0.45f));
                    DecorPrim(PrimitiveType.Cylinder, t, new Vector3(X(2.3f), y + 0.016f, cz + 0.1f), new Vector3(1.1f, 0.006f, 0.85f), new Color(0.95f, 0.85f, 0.65f));
                    break;
            }
        }

        static GameObject DecorPrim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = "Decor";
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = ProtoArt.Mat(color);
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        static void DecorGlow(Transform parent, Vector3 pos, float size, Color color, float intensity)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "DecorGlow";
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<Renderer>().sharedMaterial = ProtoArt.Glow(color, intensity);
            Destroy(go.GetComponent<Collider>());
        }
    }
}
