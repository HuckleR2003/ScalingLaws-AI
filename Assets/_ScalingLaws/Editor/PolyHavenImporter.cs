using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Gives the Poly Haven models (CC0) their materials.
    ///
    /// The FBX files arrive pointing at textures that are not in the project: Poly Haven ships its
    /// roughness and normals as EXR, which is heavy and which the Standard shader cannot read as a
    /// smoothness map anyway. The download script converted them into three files per material
    /// (`_Color.jpg`, `_NormalGL.jpg`, `_MetallicSmoothness.png`), and this makes a Standard material
    /// from those and remaps the model's own material slot onto it, by name. Safe to run again.
    /// </summary>
    public static class PolyHavenImporter
    {
        public const string Folder = "Assets/_ScalingLaws/Art/Models/PolyHaven";

        [MenuItem("Scaling Laws/Assets/Set up Poly Haven models")]
        public static void SetUpAll()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                return;
            }

            foreach (var directory in Directory.GetDirectories(Folder))
            {
                var asset = Path.GetFileName(directory);
                var fbxPath = $"{Folder}/{asset}/{asset}.fbx";

                if (AssetImporter.GetAtPath(fbxPath) is not ModelImporter importer)
                {
                    continue;
                }

                // Blender's axes baked into the mesh, so the model stands up with an ordinary root.
                importer.bakeAxisConversion = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.SaveAndReimport();

                var prefixes = Directory.GetFiles(directory, "*_Color.jpg")
                    .Select(file => Path.GetFileName(file).Replace("_Color.jpg", string.Empty))
                    .ToList();

                var slots = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Material>().Select(m => m.name).Distinct().ToList();

                foreach (var slot in slots)
                {
                    // The slot is named after its part (`legs`, `pillow`, `cart`), sometimes with the
                    // asset's name in front; the textures always carry the asset's name.
                    var prefix = prefixes.FirstOrDefault(p => p == slot)
                                 ?? prefixes.FirstOrDefault(p => p.EndsWith("_" + slot))
                                 ?? prefixes.FirstOrDefault(p => slot.EndsWith(p.Substring(asset.Length).TrimStart('_')) && p.Length > asset.Length)
                                 ?? (prefixes.Count == 1 ? prefixes[0] : null);

                    if (prefix == null)
                    {
                        Debug.LogWarning($"[PolyHaven] {asset}: no textures for slot '{slot}'.");
                        continue;
                    }

                    var material = MaterialFor($"{Folder}/{asset}", prefix);
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), material);
                }

                importer.SaveAndReimport();
                Debug.Log($"[PolyHaven] {asset}: {slots.Count} slot(s), textures {string.Join(", ", prefixes)}.");
            }
        }

        private static Material MaterialFor(string folder, string prefix)
        {
            var path = $"{folder}/{prefix}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = prefix };
                AssetDatabase.CreateAsset(material, path);
            }

            Texture2D Load(string suffix, TextureImporterType type, bool srgb)
            {
                var texturePath = $"{folder}/{prefix}_{suffix}";

                if (AssetImporter.GetAtPath(texturePath) is TextureImporter texture
                    && (texture.textureType != type || texture.sRGBTexture != srgb))
                {
                    texture.textureType = type;
                    texture.sRGBTexture = srgb;
                    texture.SaveAndReimport();
                }

                return AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            }

            material.SetTexture("_MainTex", Load("Color.jpg", TextureImporterType.Default, true));

            var normal = Load("NormalGL.jpg", TextureImporterType.NormalMap, false);

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            var gloss = Load("MetallicSmoothness.png", TextureImporterType.Default, false);

            if (gloss != null)
            {
                material.SetTexture("_MetallicGlossMap", gloss);
                material.SetFloat("_GlossMapScale", 1f);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }

            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
