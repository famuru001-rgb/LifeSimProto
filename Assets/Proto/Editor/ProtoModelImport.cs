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

        public override uint GetVersion() { return 4; }

        static bool IsModel(string p) { return p.Replace('\\', '/').StartsWith(ModelDir); }
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
                mi.importBlendShapes = false;
            }
            else
            {
                mi.importAnimation = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
            }
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
                c.loopTime = true;
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
            foreach (var dir in new[] { ModelDir, MotionDir })
            {
                if (!AssetDatabase.IsValidFolder(dir)) continue;
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { dir }))
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            }
            Debug.Log("仮モデルと動きを読み込み直しました");
        }
    }
}
