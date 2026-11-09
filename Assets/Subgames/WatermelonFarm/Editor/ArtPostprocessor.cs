#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace WatermelonFarm.EditorTools
{
    // 自动把 Assets/Resources/Art 下的 PNG 配成「Sprite / 透明 / 不压缩 / 无 Mipmap」，
    // 这样无需任何手工导入设置，Resources.Load<Sprite> 就能直接拿到带 alpha 的精灵。
    public sealed class ArtPostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Subgames/WatermelonFarm/Resources/Art/")) return;
            TextureImporter ti = assetImporter as TextureImporter;
            if (ti == null) return;

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100f;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.maxTextureSize = 2048;
            // 运行时需要按地块实际宽高比重采样土壤贴图并重新生成圆角
            ti.isReadable = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.sRGBTexture = true;
        }
    }
}
#endif
