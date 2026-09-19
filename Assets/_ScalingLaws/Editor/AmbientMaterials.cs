using UnityEditor;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Photographed materials from ambientCG (CC0) on the Standard shader, for every room builder.
    ///
    /// One place, because the offices and the basement both want them and two copies of the
    /// texture setup would be two places for a normal map to be imported as a colour.
    ///
    /// The smoothness comes from each asset's roughness, turned into the alpha of a metallic map
    /// when the files were copied in, because that is where the Standard shader reads it.
    /// </summary>
    public static class AmbientMaterials
    {
        public const string Folder = "Assets/_ScalingLaws/Art/Textures/ambientCG";
        private const string MaterialFolder = "Assets/_ScalingLaws/Materials";

        /// <summary>
        /// A material from an ambientCG asset, or null when its textures are not in the project, so
        /// the caller can fall back to a plain colour.
        /// </summary>
        public static Material Get(string name, string asset, Color tint, float gloss, Vector2 tiling)
        {
            var colour = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{asset}_Color.jpg");

            if (colour == null)
            {
                return null;
            }

            var normalPath = $"{Folder}/{asset}_NormalGL.jpg";
            var glossPath = $"{Folder}/{asset}_MetallicSmoothness.png";

            Import(normalPath, TextureImporterType.NormalMap);
            Import(glossPath, TextureImporterType.Default);

            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("Standard");
            material.SetColor("_Color", tint);
            material.mainTexture = colour;
            material.mainTextureScale = tiling;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.5f);
            material.DisableKeyword("_EMISSION");

            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            SetKeyword(material, "_NORMALMAP", normal != null);

            var glossMap = AssetDatabase.LoadAssetAtPath<Texture2D>(glossPath);
            material.SetTexture("_MetallicGlossMap", glossMap);
            material.SetFloat("_GlossMapScale", gloss);
            SetKeyword(material, "_METALLICGLOSSMAP", glossMap != null);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetKeyword(Material material, string keyword, bool on)
        {
            if (on)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        private static void Import(string path, TextureImporterType type)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            if (importer.textureType == type && !importer.sRGBTexture && importer.wrapMode == TextureWrapMode.Repeat)
            {
                return;
            }

            importer.textureType = type;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
        }
    }
}
