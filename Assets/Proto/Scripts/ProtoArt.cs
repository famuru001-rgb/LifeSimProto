using UnityEngine;

namespace LifeSimProto
{
    /// <summary>
    /// 試作用の見た目（材質と、プログラムで作る石・木の模様）。外部の素材は使わない。
    /// </summary>
    public static class ProtoArt
    {
        static Shader litShader;
        static Shader unlitShader;
        static Texture2D stoneWallTex, flagstoneTex, woodPlankTex, woodFineTex, fabricTex, whiteTex;

        static Shader Lit
        {
            get
            {
                if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null) litShader = Shader.Find("Standard");
                return litShader;
            }
        }

        static Shader Unlit
        {
            get
            {
                if (unlitShader == null) unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (unlitShader == null) unlitShader = Lit;
                return unlitShader;
            }
        }

        public static Texture2D White
        {
            get
            {
                if (whiteTex == null)
                {
                    whiteTex = new Texture2D(2, 2);
                    whiteTex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                    whiteTex.Apply();
                }
                return whiteTex;
            }
        }

        public static Texture2D StoneWall { get { if (stoneWallTex == null) stoneWallTex = MakeBlocks(256, 64, 32, new Color(0.8f, 0.73f, 0.62f), 0.3f, new Color(0.56f, 0.5f, 0.42f), 11); return stoneWallTex; } }
        public static Texture2D Flagstone { get { if (flagstoneTex == null) flagstoneTex = MakeBlocks(256, 128, 64, new Color(0.78f, 0.71f, 0.6f), 0.25f, new Color(0.55f, 0.49f, 0.41f), 23); return flagstoneTex; } }
        public static Texture2D WoodPlank { get { if (woodPlankTex == null) woodPlankTex = MakeWood(256, 32, 41); return woodPlankTex; } }
        public static Texture2D WoodFine { get { if (woodFineTex == null) woodFineTex = MakeWood(128, 128, 57); return woodFineTex; } }
        public static Texture2D Fabric { get { if (fabricTex == null) fabricTex = MakeFabric(128, 71); return fabricTex; } }

        // 2026-10-30（軽量化のチャット）：同じ色・模様・繰り返し・つやの材質は1つを使い回す（寮と学園で材質が1600種類以上になり、描く回数が増えていた）。
        // 使い回した材質は、あとから色などを変えないこと（変えたいときは MatNew で自分用を作る）。
        static readonly System.Collections.Generic.Dictionary<string, Material> matCache = new System.Collections.Generic.Dictionary<string, Material>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearMatCache() { matCache.Clear(); }

        public static Material Mat(Color color, Texture2D tex = null, Vector2? tiling = null, float smoothness = 0.15f)
        {
            Vector2 tl = tiling ?? Vector2.one;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            string key = color.r.ToString("0.####", ci) + "," + color.g.ToString("0.####", ci) + "," + color.b.ToString("0.####", ci) + "," + color.a.ToString("0.####", ci)
                + "|" + (tex != null ? tex.GetHashCode().ToString(ci) : "-") + "|" + tl.x.ToString("0.####", ci) + "," + tl.y.ToString("0.####", ci) + "|" + smoothness.ToString("0.####", ci);
            Material m;
            if (matCache.TryGetValue(key, out m) && m != null) return m;
            m = MatNew(color, tex, tiling, smoothness);
            matCache[key] = m;
            return m;
        }

        /// <summary>使い回さない材質（あとで色や両面などを変えるとき）。</summary>
        public static Material MatNew(Color color, Texture2D tex = null, Vector2? tiling = null, float smoothness = 0.15f)
        {
            var m = new Material(Lit);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (tex != null)
            {
                Vector2 t = tiling ?? Vector2.one;
                if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", t); }
                if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", tex); m.SetTextureScale("_MainTex", t); }
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            return m;
        }

        /// <summary>自分で光る材質（窓・炎・ランプ用）。</summary>
        public static Material Glow(Color color, float intensity)
        {
            var m = new Material(Unlit);
            Color c = color * intensity;
            c.a = 1f;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }

        static float Hash01(int n)
        {
            unchecked
            {
                uint h = (uint)n * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (h & 0xffff) / 65535f;
            }
        }

        /// <summary>石積み・石畳の模様（つなぎ目のない繰り返し）。</summary>
        static Texture2D MakeBlocks(int size, int bw, int bh, Color baseColor, float vary, Color mortar, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                int row = y / bh;
                int offset = (row % 2) * (bw / 2);
                for (int x = 0; x < size; x++)
                {
                    int xx = (x + offset) % size;
                    int col = xx / bw;
                    bool isMortar = (y % bh) < 3 || (xx % bw) < 3;
                    float n = Mathf.PerlinNoise(x * 0.07f + seed, y * 0.07f + seed * 0.5f);
                    float fine = Mathf.PerlinNoise(x * 0.4f + seed, y * 0.4f);
                    Color c;
                    if (isMortar)
                    {
                        c = mortar * (0.9f + 0.2f * fine);
                    }
                    else
                    {
                        float h = Hash01(row * 131 + col * 17 + seed * 7);
                        float tint = Hash01(row * 71 + col * 29 + seed);
                        c = baseColor * (1f - vary * 0.5f + vary * h);
                        c = Color.Lerp(c, new Color(c.r * 1.05f, c.g, c.b * 0.92f), tint);
                        c *= 0.85f + 0.2f * n + 0.08f * fine;
                    }
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>板張りの木目模様。</summary>
        static Texture2D MakeWood(int size, int plankWidth, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            var px = new Color[size * size];
            Color baseColor = new Color(0.72f, 0.5f, 0.28f); // 蜂蜜色の木（依頼11 の絵に合わせた）
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int plank = x / plankWidth;
                    float ph = Hash01(plank * 37 + seed);
                    int endOffset = (int)(ph * size);
                    bool seam = (x % plankWidth) < 2 || ((y + endOffset) % size) < 2;
                    float grain = Mathf.Sin((x * 0.9f + Mathf.PerlinNoise(x * 0.05f + seed, y * 0.02f) * 25f) * 0.8f) * 0.5f + 0.5f;
                    float n = Mathf.PerlinNoise(x * 0.03f + plank * 3.1f, y * 0.01f + seed);
                    Color c = baseColor * (0.75f + 0.35f * ph);
                    c *= 0.85f + 0.12f * grain + 0.1f * n;
                    if (seam) c *= 0.55f;
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>布の模様（ソファ・じゅうたん用）。</summary>
        static Texture2D MakeFabric(int size, int seed)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float weave = ((x + y) % 4 < 2) ? 1f : 0.9f;
                    float n = Mathf.PerlinNoise(x * 0.2f + seed, y * 0.2f);
                    float v = weave * (0.88f + 0.12f * n);
                    px[y * size + x] = new Color(v, v, v, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
