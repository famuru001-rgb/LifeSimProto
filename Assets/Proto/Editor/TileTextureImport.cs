using UnityEditor;

namespace LifeSimProto
{
    /// <summary>
    /// 学園の模様（Resources\Tiles・Resources\Furniture\Textures）の画像の読み方（2026-11-02 家具のチャット。依頼24 の凹凸・つやの画像）。
    /// ・名前が _Normal で終わる画像は凹凸（法線）の画像として読む
    /// ・名前が _MS で終わる画像（赤＝金属っぽさ・透明度＝つや）は色ではないデータとして読む
    /// </summary>
    public class TileTextureImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Proto/Resources/Tiles/") && !p.Contains("/Proto/Resources/TilesV2/") && !p.Contains("/Proto/Resources/Furniture/Textures/") && !(p.Contains("/Proto/Resources/Furniture/") && p.Contains("/Textures/"))) return; // Courtyard2・Plants2 の Textures も（2026-11-06）
            var ti = (TextureImporter)assetImporter;
            string n = System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
            if (n.EndsWith("_normal")) ti.textureType = TextureImporterType.NormalMap;
            else if (n.EndsWith("_ms")) { ti.sRGBTexture = false; ti.alphaIsTransparency = false; }
        }
    }
}
