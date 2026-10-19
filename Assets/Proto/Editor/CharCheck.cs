using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LifeSimProto.EditorTools
{
    /// <summary>
    /// 本番キャラ（Resources/Chars/（記号）/Char（記号）.fbx）を「キャラモデル仕様.md」に合わせて検品し、
    /// LifeSimProto\Logs\char_check.txt に書く。メニュー「LifeSim/本番キャラを検品する」またはClaudeBridgeの checkchars。
    /// </summary>
    public static class CharCheck
    {
        const string CharDir = "Assets/Proto/Resources/Chars";
        static readonly string[] NeedKeys = { "Blink", "Smile", "MouthOpen", "Sad", "Angry", "Surprised" };
        static readonly string[] NeedBones = { "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
                                               "RightShoulder", "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase",
                                               "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase" };

        [MenuItem("LifeSim/本番キャラを検品する")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("本番キャラの検品 " + System.DateTime.Now);
            if (!AssetDatabase.IsValidFolder(CharDir)) { sb.AppendLine("Chars フォルダがありません"); Write(sb); return; }
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { CharDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CheckOne(path, sb);
            }
            Write(sb);
        }

        static void Write(StringBuilder sb)
        {
            string p = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/char_check.txt"));
            File.WriteAllText(p, sb.ToString(), Encoding.UTF8);
            Debug.Log("検品結果：" + p);
        }

        static string Base(string n) { int k = n.LastIndexOf(':'); return k >= 0 ? n.Substring(k + 1) : n; }

        static void CheckOne(string path, StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("■ " + path);
            var problems = new List<string>();
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) { sb.AppendLine("  読み込めません"); return; }

            // 人型
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            bool human = avatar != null && avatar.isValid && avatar.isHuman;
            sb.AppendLine("  人型（Humanoid）：" + (human ? "OK" : "NG"));
            if (!human) problems.Add("Humanoid として読み込めない（骨の名前・つながり・T字の姿勢を確認）");
            if (mi != null) sb.AppendLine("  読み込み設定：" + mi.animationType);

            // 骨
            var all = go.GetComponentsInChildren<Transform>(true);
            var names = new HashSet<string>(all.Select(t => Base(t.name)));
            var missing = NeedBones.Where(b => !names.Contains(b)).ToList();
            sb.AppendLine("  骨の数：" + all.Length + "（必須の骨で足りないもの：" + (missing.Count == 0 ? "なし" : string.Join(", ", missing.ToArray())) + "）");
            if (missing.Count > 0) problems.Add("必須の骨が足りない：" + string.Join(", ", missing.ToArray()));

            // 揺れ
            var springs = all.Where(t => Base(t.name).StartsWith("Spring_") && !Base(t.name).EndsWith("_End")).ToList();
            var noEnd = springs.Where(t => !t.Cast<Transform>().Any(c => Base(c.name).StartsWith("Spring_"))).Select(t => Base(t.name)).ToList();
            sb.AppendLine("  揺れの骨：" + springs.Count + "本" + (noEnd.Count > 0 ? "（先に続く骨がない：" + string.Join(", ", noEnd.ToArray()) + "）" : ""));
            if (noEnd.Count > 0) problems.Add("揺れの鎖の最後に _End の骨がない：" + string.Join(", ", noEnd.ToArray()));
            if (springs.Count > 40) problems.Add("揺れの骨が40本を超えている（" + springs.Count + "本）");

            // 高さ
            var top = all.FirstOrDefault(t => Base(t.name) == "HeadTop_End") ?? all.FirstOrDefault(t => Base(t.name) == "Head");
            if (top != null) sb.AppendLine("  頭の高さ（骨）：" + (top.position.y - go.transform.position.y).ToString("0.00") + "m");

            // メッシュ・表情・服・材質
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var mats = new HashSet<Material>();
            var keysFound = new HashSet<string>();
            var outfits = new SortedDictionary<string, int>();
            int alwaysTris = 0, maxBones = 0;
            var noTex = new List<string>();
            var unknown = new List<string>();
            foreach (var r in smrs)
            {
                var m = r.sharedMesh;
                if (m == null) continue;
                int tris = 0;
                for (int i = 0; i < m.subMeshCount; i++) tris += (int)m.GetIndexCount(i) / 3;
                string n = r.gameObject.name;
                if (n.StartsWith("Outfit_"))
                {
                    string rest = n.Substring(7);
                    int k = rest.IndexOf('_');
                    string key = k >= 0 ? rest.Substring(0, k) : rest;
                    int v; outfits.TryGetValue(key, out v); outfits[key] = v + tris;
                }
                else
                {
                    alwaysTris += tris;
                    if (!(n.StartsWith("Body_") || n.StartsWith("Hair_") || n.StartsWith("Acc_") || n.StartsWith("Face_"))) unknown.Add(n);
                }
                for (int i = 0; i < m.blendShapeCount; i++) keysFound.Add(m.GetBlendShapeName(i));
                var bpv = m.GetBonesPerVertex();
                for (int i = 0; i < bpv.Length; i++) if (bpv[i] > maxBones) maxBones = bpv[i];
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    mats.Add(mat);
                    if (mat.mainTexture == null) noTex.Add(mat.name);
                }
            }
            sb.AppendLine("  メッシュ：" + smrs.Length + "（" + string.Join(", ", smrs.Select(r => r.gameObject.name).ToArray()) + "）");
            sb.AppendLine("  いつも表示する部分の三角形：" + alwaysTris);
            foreach (var kv in outfits) sb.AppendLine("  服「" + kv.Key + "」の三角形：" + kv.Value + "（合計 " + (alwaysTris + kv.Value) + "）");
            if (!outfits.ContainsKey("Uniform")) problems.Add("制服（Outfit_Uniform_）がない");
            foreach (var kv in outfits) if (alwaysTris + kv.Value > 50000) problems.Add("服「" + kv.Key + "」を着たときの三角形が50,000を超える（" + (alwaysTris + kv.Value) + "）");
            if (unknown.Count > 0) problems.Add("名前の頭が決まりにないメッシュ（いつも表示になる）：" + string.Join(", ", unknown.ToArray()));
            var missingKeys = NeedKeys.Where(k => !keysFound.Contains(k)).ToList();
            sb.AppendLine("  表情（シェイプキー）：" + string.Join(", ", keysFound.ToArray()));
            if (missingKeys.Count > 0) problems.Add("表情のシェイプキーが足りない：" + string.Join(", ", missingKeys.ToArray()));
            sb.AppendLine("  材質：" + mats.Count + "個" + (noTex.Count > 0 ? "（テクスチャなし：" + string.Join(", ", noTex.Distinct().ToArray()) + "）" : ""));
            if (mats.Count > 8) problems.Add("材質が8個を超えている（" + mats.Count + "個）");
            if (noTex.Count > 0) problems.Add("テクスチャが見つからない材質：" + string.Join(", ", noTex.Distinct().ToArray()));
            sb.AppendLine("  1頂点あたりの骨の数（最大）：" + maxBones);
            if (maxBones > 4) problems.Add("1頂点に5本以上の骨が影響している（最大" + maxBones + "本。ゲームでは4本までに切られる）");

            sb.AppendLine(problems.Count == 0 ? "  → 決まりどおり" : "  → 直す所：");
            foreach (var p in problems) sb.AppendLine("    ・" + p);
        }
    }
}
