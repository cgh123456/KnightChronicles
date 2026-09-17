#if UNITY_EDITOR
using UnityEditor;

namespace KnightChronicles.Editor
{
    public sealed class ArtTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Resources/Art/"))return;
            if(!assetPath.Contains("/World/")&&!assetPath.Contains("/Enemies/")&&!assetPath.Contains("KnightVariant_"))return;
            var importer=(TextureImporter)assetImporter;
            importer.maxTextureSize=4096;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
            importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
        }
    }
}
#endif
