using System.IO;
using UnityEditor;
using UnityEngine;

namespace LifeSimProto.EditorTools
{
    /// <summary>
    /// 仮モデル（Resources/ProtoModels）と動き（Resources/Motions）の読み込み設定を自動で行う。
    /// ・どちらも Humanoid（人型）として読み込む → Mixamo の動きを仮モデルに当てられる
    /// ・動きはくり返し再生、その場で動く（移動はゲーム側で行う）
    /// メニュー「LifeSim/仮モデルと動きを読み込み直す」で、設定をやり直せる。
    /// </summary>
    public class ProtoModelImport : AssetPostprocessor
    {
        const string ModelDir = "Assets/Proto/Resources/ProtoModels";
        const string MotionDir = "Assets/Proto/Resources/Motions";
        const string CharDir = "Assets/Proto/Resources/Chars";   // 本番キャラ（Codex 制作）

        public override uint GetVersion() { return 11; }

        static bool IsModel(string p) { p = p.Replace('\\', '/'); return p.StartsWith(ModelDir) || p.StartsWith(CharDir); }
        static bool IsMotion(string p) { return p.Replace('\\', '/').StartsWith(MotionDir); }

        void OnPreprocessModel()
        {
            if (!IsModel(assetPath) && !IsMotion(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            if (IsModel(assetPath))
            {
                mi.importAnimation = false;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.importBlendShapes = true;   // 表情（シェイプキー）
                mi.importAnimation = false;
                // 2026-10-26 famuru：リア（I）は顔の影をなめらかにした法線を、表情の分もモデルのまま使う（自動で作り直すと硬い影が戻る）
                if (Path.GetFileNameWithoutExtension(assetPath) == "CharI")
                {
                    mi.importNormals = ModelImporterNormals.Import;
                    mi.importBlendShapeNormals = ModelImporterNormals.Import;
                }
            }
            else
            {
                mi.importAnimation = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
            }
        }

        // 本番キャラのテクスチャ：名前が _Normal で終わるものは法線マップ、_ORM は色ではないデータとして読む
        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.StartsWith(CharDir)) return;
            var ti = (TextureImporter)assetImporter;
            string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
            if (n.EndsWith("_normal")) ti.textureType = TextureImporterType.NormalMap;
            else if (n.EndsWith("_orm")) ti.sRGBTexture = false;
        }

        // 本番キャラの材質：金属っぽく見えないように、金属度の画像を外して金属度0・なめらかさ控えめにする
        // （Blender の ORM 画像が金属度として割り当てられると、肌がまだらな灰色に見えるため）
        void OnPostprocessMaterial(Material m)
        {
            if (!assetPath.Replace('\\', '/').StartsWith(CharDir)) return;
            if (m.HasProperty("_MetallicGlossMap")) m.SetTexture("_MetallicGlossMap", null);
            m.DisableKeyword("_METALLICSPECGLOSSMAP");
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.25f);
        }

        void OnPreprocessAnimation()
        {
            if (!IsMotion(assetPath)) return;
            var mi = (ModelImporter)assetImporter;
            string file = Path.GetFileNameWithoutExtension(assetPath);
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = file;
                // 「_Down」「_Up」で終わる動き（座る・立つ）は1回だけ。ほかはくり返し（2026-10-30 モーション）
                c.loopTime = !(file.EndsWith("_Down") || file.EndsWith("_Up"));
                c.lockRootRotation = true;      // 向きはゲーム側で決める
                c.lockRootHeightY = true;       // 高さは元の動きのまま（座る動きで腰が下がる）
                c.lockRootPositionXZ = false;   // 前に進む動きは体に焼き込まない（ゲーム側で移動するので、その場で動く）
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
            }
            mi.clipAnimations = clips;
        }

        [MenuItem("LifeSim/確認用の写真を撮る（再生中）")]
        static void TakePhotos()
        {
            if (!Application.isPlaying) { Debug.LogWarning("再生中に使ってください"); return; }
            new GameObject("PhotoBooth").AddComponent<LifeSimProto.ProtoPhotoBooth>();
        }

        [MenuItem("LifeSim/仮モデルと動きを読み込み直す")]
        static void ReimportAll()
        {
            foreach (var dir in new[] { ModelDir, MotionDir, CharDir })
            {
                if (!AssetDatabase.IsValidFolder(dir)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { dir }))
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("仮モデルと動きを読み込み直しました");
        }
    }
}
