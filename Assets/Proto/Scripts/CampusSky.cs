using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LifeSimProto
{
    /// <summary>
    /// 新しい学園の空・雲・遠くの山・湖の水・道の灯り（2026-11-02 家具のチャット。famuru「屋外を全体的にクオリティアップ」）。
    /// CampusPreview が新しい学園で遊ぶときだけ足す。時刻と天気（Calendar）で空の色・雲の色・山の色・灯りが変わる。
    /// ・空：カメラを包む大きな球（内側に色の段々と星の画像）。雲：高い所に半透明の板をいくつか、ゆっくり流れる
    /// ・山：学園のまわりの湖の向こうに、2重の山なみ（遠いほど空の色に近い）
    /// ・湖：さざ波の凹凸の画像を流して、きらめかせる
    /// </summary>
    public class CampusSky : MonoBehaviour
    {
        public static readonly Vector3 Center = new Vector3(16f, 0f, -61.5f); // 学園の真ん中（ゲームの座標）
        const float DomeR = 1100f;

        Transform dome;
        Material domeMat, cloudMat, mountNear, mountFar, water;
        Texture2D skyTex;
        readonly List<Transform> clouds = new List<Transform>();
        readonly List<Light> lamps = new List<Light>();
        readonly List<Material> lampGlows = new List<Material>();
        Color[] skyPix;
        float nextSky;
        Vector2 waterOff;
        bool waterToon; // LifeSim/Water（アニメ調の水）を使っているか
        bool depthAsked;

        public void SetWater(Material m) { water = m; }
        public void AddLamp(Light l, Material glow) { if (l != null) lamps.Add(l); if (glow != null) lampGlows.Add(glow); }

        // ================= 光（2026-11-02 famuru「全くダメ。参考に作り直して」→ 屋外の作り方.md の 2. 光と色） =================
        // 太陽は強く暖かい色・影の中は空の青（環境光を空・地平線・地面の3色に）。DormBuilder.UpdateLighting のあとに上書きする（新しい学園のときだけ）。
        // 値は Logs\campus_light.txt（「名前 = 値」）で遊びながら変えられる。決まったら下の初めの値に書き写す。
        class LightTune
        {
            public float sunI = 2.0f, sunPitch = 44f, sunYaw = -38f;
            public Color sunCol = new Color(1f, 0.9f, 0.74f);
            public Color ambSky = new Color(0.56f, 0.68f, 0.92f), ambEq = new Color(0.62f, 0.66f, 0.66f), ambGround = new Color(0.34f, 0.36f, 0.26f);
            public float ambI = 0.85f;
            public float exposure = 0.32f, contrast = 28f, saturation = 4f, temperature = 6f;
            public float cloudyFactor = 0.62f, cloudyAmb = 1.05f;
        }
        readonly LightTune tune = new LightTune();
        Light sun;
        Tonemapping postTm; ColorAdjustments postCa; WhiteBalance postWb; ShadowsMidtonesHighlights postSmh;
        System.DateTime tuneStamp;
        float nextTune;

        void ReadTune()
        {
            string f = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../Logs/campus_light.txt"));
            try
            {
                if (!System.IO.File.Exists(f)) return;
                var st = System.IO.File.GetLastWriteTimeUtc(f);
                if (st == tuneStamp) return;
                tuneStamp = st;
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                foreach (var raw in System.IO.File.ReadAllLines(f))
                {
                    var kv = raw.Split('=');
                    if (kv.Length != 2) continue;
                    string k = kv[0].Trim();
                    var v = kv[1].Trim().Split(new[] { ' ', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (v.Length == 0) continue;
                    float a = float.Parse(v[0], ci);
                    Color c = v.Length >= 3 ? new Color(a, float.Parse(v[1], ci), float.Parse(v[2], ci)) : Color.white;
                    switch (k)
                    {
                        case "sunI": tune.sunI = a; break;
                        case "sunPitch": tune.sunPitch = a; break;
                        case "sunYaw": tune.sunYaw = a; break;
                        case "sunCol": tune.sunCol = c; break;
                        case "ambSky": tune.ambSky = c; break;
                        case "ambEq": tune.ambEq = c; break;
                        case "ambGround": tune.ambGround = c; break;
                        case "ambI": tune.ambI = a; break;
                        case "exposure": tune.exposure = a; break;
                        case "contrast": tune.contrast = a; break;
                        case "saturation": tune.saturation = a; break;
                        case "temperature": tune.temperature = a; break;
                        case "cloudyFactor": tune.cloudyFactor = a; break;
                        case "cloudyAmb": tune.cloudyAmb = a; break;
                    }
                }
                Debug.Log("[CampusSky] 光の値を読みなおした");
            }
            catch (System.Exception e) { Debug.LogWarning("[CampusSky] campus_light.txt：" + e.Message); }
        }

        void LateUpdate()
        {
            if (Time.unscaledTime >= nextTune) { nextTune = Time.unscaledTime + 1f; ReadTune(); }
            if (sun == null)
            {
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional && l.name == "Sun") { sun = l; break; }
            }
            var wld = LoungeWorld.Instance;
            float m = wld != null ? wld.MinuteOfDay : 600f;
            Weather w = wld != null ? Calendar.WeatherOf(wld.Day) : Weather.Sunny;
            float day = DayAmount(m);
            bool morning = m < 12f * 60f;
            float sunset = morning ? Mathf.Clamp01(1f - Mathf.Abs(m - 390f) / 90f) : Mathf.Clamp01(1f - Mathf.Abs(m - 1050f) / 110f);
            float wf = w == Weather.Sunny ? 1f : (w == Weather.Cloudy ? tune.cloudyFactor : (w == Weather.Rain ? 0.4f : 0.55f));
            float af = w == Weather.Sunny ? 1f : tune.cloudyAmb;
            if (sun != null)
            {
                Color sc = Color.Lerp(tune.sunCol, new Color(1f, 0.62f, 0.38f), sunset * 0.8f);
                sun.color = Color.Lerp(new Color(0.5f, 0.6f, 1f), sc, day);
                sun.intensity = Mathf.Lerp(0.15f, tune.sunI * wf, day);
                // 朝は東・夕方は西から少し低く（夕方の長い影）。真昼は sunPitch
                float t = Mathf.InverseLerp(6f * 60f, 18f * 60f, m);
                float pitch = Mathf.Lerp(tune.sunPitch - 12f, tune.sunPitch, Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI));
                sun.transform.rotation = Quaternion.Euler(pitch, tune.sunYaw + (t - 0.5f) * 50f, 0f);
                sun.shadowStrength = Mathf.Lerp(0.9f, 0.75f, 1f - wf);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            Color nSky = new Color(0.1f, 0.12f, 0.24f), nEq = new Color(0.09f, 0.1f, 0.17f), nGr = new Color(0.05f, 0.05f, 0.07f);
            Color dSky = tune.ambSky * tune.ambI * af, dEq = tune.ambEq * tune.ambI * af, dGr = tune.ambGround * tune.ambI * af;
            Color warm = new Color(1f, 0.82f, 0.72f);
            RenderSettings.ambientSkyColor = Color.Lerp(nSky, Color.Lerp(dSky, dSky * warm, sunset * 0.5f), day);
            RenderSettings.ambientEquatorColor = Color.Lerp(nEq, Color.Lerp(dEq, dEq * warm, sunset * 0.6f), day);
            RenderSettings.ambientGroundColor = Color.Lerp(nGr, dGr, day);
            if (postCa != null)
            {
                postCa.postExposure.Override(tune.exposure);
                postCa.contrast.Override(tune.contrast);
                postCa.saturation.Override(Mathf.Lerp(tune.saturation - 10f, tune.saturation, wf));
                postWb.temperature.Override(tune.temperature);
            }
        }

        void Start()
        {
            BuildDome();
            BuildClouds();
            BuildPost();
            mountNear = BuildMountains("Mountains_Near", 560f, 70f, 40f, 120f, 11);
            mountFar = BuildMountains("Mountains_Far", 820f, 110f, 90f, 230f, 23);
            if (water != null) SetupWater(water);
            UpdateSky(true);
        }

        // ================= 空 =================
        static Shader UnlitShader { get { return Shader.Find("Universal Render Pipeline/Unlit"); } }

        void BuildDome()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SkyDome";
            Destroy(go.GetComponent<Collider>());
            dome = go.transform;
            dome.SetParent(transform, false);
            dome.localScale = Vector3.one * DomeR * 2f;
            skyTex = new Texture2D(256, 128, TextureFormat.RGB24, false) { wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            skyPix = new Color[256 * 128];
            var skyShader = Shader.Find("LifeSim/SkyDome"); // 霞をかけない空の球（Resources\Shaders\LifeSimSkyDome.shader）
            domeMat = new Material(skyShader != null ? skyShader : UnlitShader);
            domeMat.SetTexture("_BaseMap", skyTex);
            domeMat.SetColor("_BaseColor", Color.white);
            domeMat.SetFloat("_Cull", 0f);
            domeMat.renderQueue = 1000; // いちばん先に描く（ほかの物の後ろ）
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = domeMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>空の色（上・地平線）と星の明るさ。</summary>
        static void SkyColors(float m, Weather w, out Color top, out Color hor, out float stars, out Color cloud)
        {
            Color dayTop = new Color(0.30f, 0.52f, 0.86f), dayHor = new Color(0.74f, 0.84f, 0.95f);
            Color setTop = new Color(0.30f, 0.33f, 0.62f), setHor = new Color(1.0f, 0.60f, 0.38f);
            Color nightTop = new Color(0.02f, 0.03f, 0.08f), nightHor = new Color(0.07f, 0.09f, 0.18f);
            float day = DayAmount(m);
            bool morning = m < 12f * 60f;
            float sunset = morning ? Mathf.Clamp01(1f - Mathf.Abs(m - 360f) / 70f) : Mathf.Clamp01(1f - Mathf.Abs(m - 1080f) / 80f); // 6:00 と 18:00 ごろに夕焼け・朝焼け
            top = Color.Lerp(nightTop, dayTop, day); hor = Color.Lerp(nightHor, dayHor, day);
            top = Color.Lerp(top, setTop, sunset * 0.8f); hor = Color.Lerp(hor, setHor, sunset * 0.85f);
            cloud = Color.Lerp(new Color(0.12f, 0.13f, 0.18f), Color.white, day);
            cloud = Color.Lerp(cloud, new Color(1f, 0.7f, 0.55f), sunset * 0.7f);
            float gray = w == Weather.Rain ? 0.65f : (w == Weather.Cloudy ? 0.45f : (w == Weather.Snow ? 0.5f : 0f));
            if (gray > 0f)
            {
                Color g1 = Color.Lerp(new Color(0.04f, 0.04f, 0.06f), new Color(0.55f, 0.58f, 0.62f), day);
                top = Color.Lerp(top, g1, gray); hor = Color.Lerp(hor, g1 * 1.15f, gray);
                cloud = Color.Lerp(cloud, g1 * 1.1f, gray * 0.7f);
            }
            stars = (1f - day) * (1f - gray);
        }

        static float DayAmount(float m)
        {
            if (m < 330f) return 0f;
            if (m < 480f) return Mathf.InverseLerp(330f, 480f, m);
            if (m < 1020f) return 1f;
            if (m < 1170f) return 1f - Mathf.InverseLerp(1020f, 1170f, m);
            return 0f;
        }

        static float Hash(int x, int y) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return (h & 0xffffff) / 16777215f; } }

        void UpdateSky(bool force)
        {
            var wld = LoungeWorld.Instance;
            float m = wld != null ? wld.MinuteOfDay : 600f;
            Weather w = wld != null ? Calendar.WeatherOf(wld.Day) : Weather.Sunny;
            Color top, hor, cloud; float stars;
            SkyColors(m, w, out top, out hor, out stars, out cloud);
            for (int y = 0; y < 128; y++)
            {
                float v = y / 127f;                 // 0 下・1 上（球の UV）
                float alt = Mathf.Clamp01((v - 0.5f) * 2f); // 地平線から上
                Color c = v < 0.5f ? hor * 0.85f : Color.Lerp(hor, top, Mathf.Pow(alt, 0.55f));
                for (int x = 0; x < 256; x++)
                {
                    Color p = c;
                    if (stars > 0.02f && v > 0.55f)
                    {
                        float s = Hash(x, y);
                        if (s > 0.985f) p += Color.white * stars * (s - 0.985f) * 55f * Mathf.Clamp01((v - 0.55f) * 5f);
                    }
                    skyPix[y * 256 + x] = p;
                }
            }
            skyTex.SetPixels(skyPix);
            skyTex.Apply(false);
            if (cloudMat != null) { var cc = cloud; cc.a = 0.85f; cloudMat.SetColor("_BaseColor", cc); }
            float day = DayAmount(m);
            // 山：遠いほど地平線の色に近い
            Color mBase = new Color(0.30f, 0.44f, 0.36f);
            if (mountNear != null) mountNear.SetColor("_BaseColor", Color.Lerp(mBase, hor, 0.12f));
            if (mountFar != null) mountFar.SetColor("_BaseColor", Color.Lerp(mBase, hor, 0.35f));
            // 遠くの霞（地平線の色。学園の中＝カメラから 90m までは かからない）
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 1500f;
            RenderSettings.fogColor = Color.Lerp(hor, top, 0.15f);
            if (water != null && waterToon)
            {
                water.SetColor("_SkyColor", Color.Lerp(hor, top, 0.3f));
                water.SetColor("_ShallowColor", Color.Lerp(new Color(0.08f, 0.14f, 0.2f, 0.6f), new Color(0.40f, 0.68f, 0.66f, 0.55f), day));
                water.SetColor("_DeepColor", Color.Lerp(new Color(0.03f, 0.05f, 0.09f, 0.96f), new Color(0.15f, 0.41f, 0.6f, 0.95f), day)); // 沖は暗い紺→明るい深い青（2026-11-08）
                water.SetFloat("_Night", 1f - day);
            }
            else if (water != null) water.SetColor("_BaseColor", Color.Lerp(new Color(0.02f, 0.04f, 0.07f), new Color(0.16f, 0.34f, 0.46f), day));
            // 道の灯り：夕方〜朝
            bool on = m >= 17.5f * 60f || m < 6.5f * 60f || w == Weather.Rain && day < 0.8f;
            foreach (var l in lamps) if (l != null) l.enabled = on;
            foreach (var g in lampGlows) if (g != null) g.SetColor("_BaseColor", on ? new Color(2.2f, 1.6f, 0.9f) : new Color(0.35f, 0.3f, 0.25f));
        }

        // ================= 雲 =================
        void BuildClouds()
        {
            var tex = new Texture2D(256, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[256 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 256; x++)
                {
                    float u = x / 255f, v = y / 127f;
                    float n = Mathf.PerlinNoise(u * 5f, v * 3f) * 0.6f + Mathf.PerlinNoise(u * 13f + 7f, v * 9f + 3f) * 0.4f;
                    float edge = Mathf.Clamp01(1f - (Mathf.Pow((u - 0.5f) * 2f, 2f) + Mathf.Pow((v - 0.5f) * 2f, 2f)));
                    float a = Mathf.Clamp01((n * edge - 0.18f) * 3f);
                    px[y * 256 + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply(false);
            cloudMat = new Material(UnlitShader);
            cloudMat.SetTexture("_BaseMap", tex);
            cloudMat.SetFloat("_Surface", 1f); cloudMat.SetFloat("_Blend", 0f);
            cloudMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            cloudMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            cloudMat.SetFloat("_ZWrite", 0f); cloudMat.SetFloat("_Cull", 0f);
            cloudMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            cloudMat.renderQueue = 2900;
            var rnd = new System.Random(5);
            for (int i = 0; i < 14; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Cloud";
                Destroy(q.GetComponent<Collider>());
                var r = q.GetComponent<Renderer>();
                r.sharedMaterial = cloudMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                var t = q.transform;
                t.SetParent(transform, false);
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, dist = 150f + (float)rnd.NextDouble() * 550f;
                t.position = Center + new Vector3(Mathf.Cos(ang) * dist, 180f + (float)rnd.NextDouble() * 120f, Mathf.Sin(ang) * dist);
                t.rotation = Quaternion.Euler(90f, (float)rnd.NextDouble() * 360f, 0f);
                float s = 120f + (float)rnd.NextDouble() * 160f;
                t.localScale = new Vector3(s, s * 0.5f, 1f);
                clouds.Add(t);
            }
        }

        // ================= 山 =================
        Material BuildMountains(string name, float r0, float depth, float hMin, float hMax, int seed)
        {
            const int N = 220;
            var verts = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i <= N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float n = Mathf.PerlinNoise(seed + i * 0.09f, seed * 0.37f) * 0.55f + Mathf.PerlinNoise(seed * 2f + i * 0.31f, 1.7f) * 0.3f + Mathf.PerlinNoise(seed * 3f + i * 0.9f, 4.1f) * 0.15f;
                float h = Mathf.Lerp(hMin, hMax, n);
                float rr = r0 + Mathf.PerlinNoise(i * 0.05f, seed) * 60f;
                verts.Add(Center + d * rr + Vector3.down * 4f);
                verts.Add(Center + d * (rr + depth * 0.5f) + Vector3.up * h);
                verts.Add(Center + d * (rr + depth) + Vector3.down * 4f);
            }
            for (int i = 0; i < N; i++)
            {
                int a = i * 3, b = (i + 1) * 3;
                tris.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1, a + 1, a + 2, b + 1, b + 1, a + 2, b + 2 });
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); // 日の光で山肌に陰影
            m.SetFloat("_Cull", 0f);
            m.SetFloat("_Smoothness", 0f);
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return m;
        }

        // ================= 湖 =================
        void SetupWater(Material m)
        {
            const int S = 256;
            var h = new float[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = x / (float)S * Mathf.PI * 2f, v = y / (float)S * Mathf.PI * 2f;
                    h[y * S + x] = WaveH(x / (float)S, y / (float)S); // 向きのばらばらな波を重ねる（くり返しが目立たない。2026-11-08）
                }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = h[y * S + (x + 1) % S] - h[y * S + (x + S - 1) % S];
                    float dy = h[((y + 1) % S) * S + x] - h[((y + S - 1) % S) * S + x];
                    Vector3 n = new Vector3(-dx * 1.2f, -dy * 1.2f, 1f).normalized;
                    px[y * S + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            tex.SetPixels(px); tex.Apply(true);
            m.SetTexture("_BumpMap", tex);
            m.SetFloat("_BumpScale", 0.6f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTextureScale("_BumpMap", new Vector2(300f, 300f));
            m.SetColor("_BaseColor", new Color(0.16f, 0.34f, 0.46f));
            m.SetFloat("_Smoothness", 0.8f);
            m.SetFloat("_EnvironmentReflections", 0f); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); // まわりの景色の映り込みは切る（遠くの水面が白く光るため）。日の光のきらめきは残す
            // アニメ調の水（深さで色が変わる・岸の泡・くっきりしたきらめき。2026-11-02 famuru「水面がのっぺり」）
            var ws = Shader.Find("LifeSim/Water");
            if (ws != null)
            {
                m.shader = ws;
                m.renderQueue = -1;
                m.SetTexture("_NormalTex", tex);
                m.SetTexture("_NoiseTex", MakeNoise());
                m.SetFloat("_FoamDist", 0.9f); // 岸へ寄せる泡の帯が見える幅（2026-11-08）
                waterToon = true;
            }
        }

        /// <summary>くり返せる波の高さ：向きと細かさのちがう波を 16 本重ねる（整数の波数なので画像のはしでつながる）。</summary>
        static float WaveH(float u, float v)
        {
            if (waveK == null)
            {
                var rnd = new System.Random(11);
                waveK = new Vector4[16];
                for (int i = 0; i < 16; i++)
                {
                    int kx, ky;
                    do { kx = rnd.Next(-11, 12); ky = rnd.Next(-11, 12); } while (kx * kx + ky * ky < 9);
                    float k = Mathf.Sqrt(kx * kx + ky * ky);
                    waveK[i] = new Vector4(kx, ky, (float)rnd.NextDouble() * 6.2831f, 0.9f / k);
                }
            }
            float h = 0f;
            foreach (var w in waveK) h += w.w * Mathf.Sin((w.x * u + w.y * v) * 6.2831f + w.z);
            return h * 1.6f;
        }
        static Vector4[] waveK;

        static Texture2D MakeNoise()
        {
            const int S = 256;
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    // くり返せる雲のような模様（4方向の周期の和）
                    float u = x / (float)S, v = y / (float)S, n = 0f, amp = 0.5f;
                    for (int o = 1; o <= 4; o++)
                    {
                        float f = 2f * o;
                        n += amp * (Mathf.Sin((u * f + Mathf.Sin(v * 6.2831f * o) * 0.15f) * 6.2831f) * Mathf.Sin((v * f + Mathf.Cos(u * 6.2831f * o) * 0.15f) * 6.2831f) * 0.5f + 0.5f);
                        amp *= 0.55f;
                    }
                    n /= 1.2f;
                    px[y * S + x] = new Color(n, n, n, 1f);
                }
            var t = new Texture2D(S, S, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat };
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                if (cam.farClipPlane < DomeR * 1.2f) cam.farClipPlane = DomeR * 1.2f;
                if (!depthAsked && waterToon)
                {
                    // 水の深さと岸の泡のために、カメラに奥行きの画像を作ってもらう
                    var ad = cam.GetUniversalAdditionalCameraData();
                    if (ad != null) { ad.requiresDepthOption = CameraOverrideOption.On; ad.requiresColorOption = CameraOverrideOption.On; } // 屈折には色の画像も
                    depthAsked = true;
                }
                if (dome != null) dome.position = cam.transform.position;
            }
            float dt = Time.deltaTime;
            foreach (var c in clouds)
            {
                c.position += new Vector3(1.2f, 0f, 0.5f) * dt;
                // 遠くへ出た雲は反対がわへ戻す。高さはそのまま（前は戻すたびに下がり、雲が湖の水面まで沈んで白い点に見えた。2026-11-08）
                Vector3 d = c.position - Center; d.y = 0f;
                if (d.magnitude > 760f) c.position = Center - d.normalized * 700f + Vector3.up * (c.position.y - Center.y);
            }
            if (water != null && !waterToon) { waterOff += new Vector2(0.006f, 0.004f) * dt; water.SetTextureOffset("_BumpMap", waterOff); }
            if (Time.unscaledTime >= nextSky) { nextSky = Time.unscaledTime + 1f; UpdateSky(false); }
        }

        // ================= 画面の仕上げ（色合い・光のにじみ。新しい学園のときだけ） =================
        void BuildPost()
        {
            var go = new GameObject("CampusPost");
            go.transform.SetParent(transform, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;
            var prof = ScriptableObject.CreateInstance<VolumeProfile>();
            // 原神は ACES を使わない（色相が変わる）→ Neutral。明るさ・コントラスト・彩度は LightTune で（屋外の作り方.md）
            postTm = prof.Add<Tonemapping>(true); postTm.mode.Override(TonemappingMode.Neutral);
            postCa = prof.Add<ColorAdjustments>(true); postCa.postExposure.Override(tune.exposure); postCa.contrast.Override(tune.contrast); postCa.saturation.Override(tune.saturation);
            postWb = prof.Add<WhiteBalance>(true); postWb.temperature.Override(tune.temperature); postWb.tint.Override(2f);
            postSmh = prof.Add<ShadowsMidtonesHighlights>(true);
            postSmh.shadows.Override(new Vector4(0.93f, 0.97f, 1.1f, 0f)); // 影は少し青く
            postSmh.highlights.Override(new Vector4(1.06f, 1.02f, 0.94f, 0f)); // 明るい所は少し暖かく
            var bl = prof.Add<Bloom>(true); bl.intensity.Override(0.4f); bl.threshold.Override(0.9f); bl.scatter.Override(0.65f);
            var vg = prof.Add<Vignette>(true); vg.intensity.Override(0.24f); vg.smoothness.Override(0.45f);
            vol.sharedProfile = prof;
        }
    }

    /// <summary>学園の模様に、凹凸（_Normal）とつや（_MS）の画像があれば付ける（依頼24。無ければ何もしない）。2026-11-02 家具のチャット</summary>
    public static class TilePBR
    {
        public static Material Apply(Material m, string tile)
        {
            if (m == null || string.IsNullOrEmpty(tile)) return m;
            string dir = LoungeWorld.NewLayout && Resources.Load<Texture2D>("TilesV2/" + tile + "_Normal") != null ? "TilesV2/" : "Tiles/"; // 依頼24 の作り直し
            var n = Resources.Load<Texture2D>(dir + tile + "_Normal");
            if (n != null && m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            var ms = Resources.Load<Texture2D>(dir + tile + "_MS");
            if (ms != null && m.HasProperty("_MetallicGlossMap")) { m.SetTexture("_MetallicGlossMap", ms); m.SetFloat("_Smoothness", 1f); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            if (dir == "TilesV2/")
            {
                // 依頼24 の報告：模型の並べ方（UV）は前の大きさのまま。角の石は 3×2m→2×1m、崖は 4m→10m の模様なので、くり返しの数で合わせる
                if (tile == "Tile_StoneTrim") m.mainTextureScale = new Vector2(1.5f, 2f);
                else if (tile == "Tile_Cliff") m.mainTextureScale = new Vector2(0.4f, 0.4f);
            }
            return m;
        }
        public static Texture2D Tex(string tile, string fallback = null)
        {
            Texture2D t = LoungeWorld.NewLayout ? Resources.Load<Texture2D>("TilesV2/" + tile) : null;
            if (t == null) t = Resources.Load<Texture2D>("Tiles/" + tile);
            if (t == null && fallback != null) t = Resources.Load<Texture2D>("Tiles/" + fallback);
            return t;
        }
    }
}
