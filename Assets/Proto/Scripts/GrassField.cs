using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LifeSimProto
{
    /// <summary>
    /// 新しい学園の芝生の上の草（2026-11-02 家具のチャット。famuru「草地がのっぺり。アニメ的なリアリティを」）。
    /// 小さな草の束を島の原っぱに数万置き、GPU でまとめて描く（ゲームの物にはしない）。カメラの近く（約70m）だけ描き、遠くは根元へ縮めて消える。
    /// 草は LifeSim/Foliage（風でゆれる）。色は下の地面の色むらの画像から取るので、地面となじむ。
    /// </summary>
    public class GrassField : MonoBehaviour
    {
        class Chunk { public Bounds b; public Matrix4x4[] m; public MaterialPropertyBlock mpb; public int n; }
        readonly List<Chunk> chunks = new List<Chunk>();
        Mesh mesh; Material mat;
        Material[] subMats; // 部品（依頼30 の地面_v1 の草・花など）の材質。submesh ごと
        Matrix4x4 baseM = Matrix4x4.identity; // FBX の根元の回転など（部品の中の位置）
        float drawDist = DrawDist;
        ShadowCastingMode shadows = ShadowCastingMode.Off;
        readonly Plane[] planes = new Plane[6];
        const float ChunkSize = 12f, DrawDist = 75f;

        public void Build(List<Vector3> worldPos, List<float> yaw, List<float> scale, List<Color> colors, Material m)
        {
            mat = m; mat.enableInstancing = true;
            mesh = MakeClump();
            BuildChunks(worldPos, yaw, scale, colors);
        }

        /// <summary>部品の形（FBX）をたくさん並べる（依頼30 の地面_v1）。mats は submesh ごとの材質、baseMatrix は部品の中の位置と向き。</summary>
        public void BuildParts(Mesh m, Material[] mats, Matrix4x4 baseMatrix, List<Vector3> worldPos, List<float> yaw, List<float> scale, List<Color> colors, float drawDistance, bool castShadows)
        {
            mesh = m; subMats = mats; baseM = baseMatrix; drawDist = drawDistance;
            shadows = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            foreach (var x in mats) if (x != null) x.enableInstancing = true;
            mat = mats.Length > 0 ? mats[0] : null;
            BuildChunks(worldPos, yaw, scale, colors);
        }

        void BuildChunks(List<Vector3> worldPos, List<float> yaw, List<float> scale, List<Color> colors)
        {
            var map = new Dictionary<long, List<int>>();
            for (int i = 0; i < worldPos.Count; i++)
            {
                long k = ((long)Mathf.FloorToInt(worldPos[i].x / ChunkSize) << 32) ^ (uint)Mathf.FloorToInt(worldPos[i].z / ChunkSize);
                if (!map.TryGetValue(k, out var l)) map[k] = l = new List<int>();
                l.Add(i);
            }
            foreach (var kv in map)
            {
                var ids = kv.Value;
                for (int s = 0; s < ids.Count; s += 1000)
                {
                    int n = Mathf.Min(1000, ids.Count - s);
                    var c = new Chunk { m = new Matrix4x4[n], mpb = new MaterialPropertyBlock(), n = n };
                    var cols = new Vector4[n];
                    Bounds b = new Bounds(worldPos[ids[s]], Vector3.one);
                    for (int j = 0; j < n; j++)
                    {
                        int i = ids[s + j];
                        c.m[j] = Matrix4x4.TRS(worldPos[i], Quaternion.Euler(0f, yaw[i], 0f), Vector3.one * scale[i]) * baseM;
                        cols[j] = colors[i];
                        b.Encapsulate(worldPos[i]);
                    }
                    b.Expand(new Vector3(2f, 2f, 2f));
                    c.b = b;
                    c.mpb.SetVectorArray("_InstColor", cols);
                    chunks.Add(c);
                }
            }
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null || mesh == null) return;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            Vector3 cp = cam.transform.position;
            foreach (var c in chunks)
            {
                if ((c.b.ClosestPoint(cp) - cp).sqrMagnitude > drawDist * drawDist) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, c.b)) continue;
                if (subMats == null) Graphics.DrawMeshInstanced(mesh, 0, mat, c.m, c.n, c.mpb, ShadowCastingMode.Off, true, 0, null, LightProbeUsage.Off);
                else for (int s = 0; s < subMats.Length && s < mesh.subMeshCount; s++)
                        if (subMats[s] != null) Graphics.DrawMeshInstanced(mesh, s, subMats[s], c.m, c.n, c.mpb, shadows, true, 0, null, LightProbeUsage.Off);
            }
        }

        /// <summary>草の束：細い葉 9 枚（根元が暗く、先が明るい。頂点の色の a＝ゆれの強さ）。</summary>
        static Mesh MakeClump()
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var col = new List<Color>(); var t = new List<int>();
            var rnd = new System.Random(7);
            System.Func<float> R = () => (float)rnd.NextDouble();
            for (int k = 0; k < 9; k++)
            {
                float a = R() * Mathf.PI * 2f, r = Mathf.Sqrt(R()) * 0.26f;
                Vector3 b = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                float fy = R() * Mathf.PI * 2f;
                Vector3 side = new Vector3(Mathf.Cos(fy), 0f, Mathf.Sin(fy));
                Vector3 face = Vector3.Cross(side, Vector3.up);
                float h = 0.07f + R() * 0.09f, w = 0.035f + R() * 0.02f;
                Vector3 lean = (face * (R() - 0.5f) + new Vector3(b.x, 0f, b.z).normalized * 0.6f) * (0.04f + R() * 0.06f);
                int i0 = v.Count;
                v.Add(b - side * w * 0.5f); v.Add(b + side * w * 0.5f);
                v.Add(b - side * w * 0.36f + Vector3.up * h * 0.55f + lean * 0.35f); v.Add(b + side * w * 0.36f + Vector3.up * h * 0.55f + lean * 0.35f);
                v.Add(b + Vector3.up * h + lean);
                Vector3 nn = (Vector3.up * 0.6f + face * 0.4f).normalized;
                for (int q = 0; q < 5; q++) n.Add(nn);
                float shade = 0.92f + R() * 0.08f;
                // 根元は地面と同じ明るさ（少しだけ暗い）、先は少し明るく黄みに
                col.Add(new Color(0.86f * shade, 0.86f * shade, 0.86f * shade, 0f)); col.Add(new Color(0.86f * shade, 0.86f * shade, 0.86f * shade, 0f));
                col.Add(new Color(0.95f * shade, 0.96f * shade, 0.93f * shade, 0.45f)); col.Add(new Color(0.95f * shade, 0.96f * shade, 0.93f * shade, 0.45f));
                col.Add(new Color(1f * shade, 1f * shade, 0.95f * shade, 1f));
                t.AddRange(new[] { i0, i0 + 2, i0 + 1, i0 + 1, i0 + 2, i0 + 3, i0 + 2, i0 + 4, i0 + 3 });
            }
            var m = new Mesh { name = "GrassClump" };
            m.SetVertices(v); m.SetNormals(n); m.SetColors(col); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
