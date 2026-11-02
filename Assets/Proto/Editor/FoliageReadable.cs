using UnityEditor;
using UnityEngine;

namespace LifeSimProto.EditorTools
{
    /// <summary>
    /// 屋外の木・茂み・生け垣の模型を「中身を読める」設定にする（ゲームで葉の面の向きを丸く直すため。CampusPreview.LeafNormals）。
    /// 2026-11-02 家具のチャット。開いたときに設定が違う物だけ直して読み込み直す（ほかの模型には触らない）。
    /// </summary>
    [InitializeOnLoad]
    static class FoliageReadable
    {
        static readonly string[] Names = { "Tree", "Tree_Oak", "Tree_Pine", "Tree_Blossom", "Bush_Round", "Bush_Flower", "Hedge", "FlowerPatch", "FlowerBed" };

        static FoliageReadable() { EditorApplication.delayCall += Fix; }

        static void Fix()
        {
            foreach (var n in Names)
            {
                string p = "Assets/Proto/Resources/Furniture/Furn_" + n + ".fbx";
                var mi = AssetImporter.GetAtPath(p) as ModelImporter;
                if (mi == null || mi.isReadable) continue;
                mi.isReadable = true;
                mi.SaveAndReimport();
                Debug.Log("[FoliageReadable] 読める設定にした：" + p);
            }
        }
    }
}
