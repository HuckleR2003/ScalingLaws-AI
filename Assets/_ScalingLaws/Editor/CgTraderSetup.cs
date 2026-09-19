using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Sets up the three models the author downloaded from CGTrader: the big server rack, the
    /// medium one, and the television on its cabinet.
    ///
    /// **Local only.** CGTrader's royalty-free licence allows the models in the game and forbids
    /// redistributing the files, so the folder is gitignored like the Store packs, and every use of
    /// them falls back to something else on a clone without it.
    /// </summary>
    public static class CgTraderSetup
    {
        public const string Folder = "Assets/_ScalingLaws/Art/Models/CGTrader";

        public const string BigRack = Folder + "/ServerRackBig/ServerRackBig.fbx";
        public const string MediumRack = Folder + "/ServerRackMedium/ServerRackMedium.obj";
        public const string Television = Folder + "/ModernTV/ModernTV.obj";

        [MenuItem("Scaling Laws/Assets/Set up CGTrader models")]
        public static void SetUpAll()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                return;
            }

            // The big rack's textures travel inside the FBX: take them out, then import again so
            // the materials find them.
            if (AssetImporter.GetAtPath(BigRack) is ModelImporter big)
            {
                big.bakeAxisConversion = true;
                big.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                big.SaveAndReimport();

                var textures = Folder + "/ServerRackBig/Textures";
                Directory.CreateDirectory(textures);

                if (big.ExtractTextures(textures))
                {
                    AssetDatabase.Refresh();
                }

                big.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Local);
                big.SaveAndReimport();
            }

            foreach (var path in new[] { MediumRack, Television })
            {
                if (AssetImporter.GetAtPath(path) is ModelImporter importer)
                {
                    importer.bakeAxisConversion = true;
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                    importer.SaveAndReimport();
                }
            }

            Debug.Log("[CGTrader] models set up.");
        }


        /// <summary>Where the cleaned models are saved: a Resources folder inside the gitignored one.</summary>
        public const string ResourcesFolder = Folder + "/Resources/Racks";

        /// <summary>
        /// The three models cleaned of the author's staging and saved as prefabs the game can load:
        /// a big rack (the author's own, without its studio floor), a small rack (the big one cut in
        /// half), a medium rack, and the television on its cabinet without the room it was
        /// photographed in.
        /// </summary>
        [MenuItem("Scaling Laws/Assets/Build CGTrader prefabs")]
        public static void BuildPrefabs()
        {
            SetUpAll();
            Directory.CreateDirectory(ResourcesFolder);
            Directory.CreateDirectory(Folder + "/Meshes");

            // ---- the big rack, without the studio floor ----
            var big = Instantiate(BigRack);
            if (big != null)
            {
                foreach (var renderer in big.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.name == "Circle")
                    {
                        Object.DestroyImmediate(renderer.gameObject);
                    }
                }

                FixMaterials(big); Save(Normalise(big, "Rack_Big"), "Rack_Big");
            }

            // ---- the small rack: the big one with its middle metre taken out ----
            var small = Instantiate(BigRack);
            if (small != null)
            {
                foreach (var renderer in small.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.name == "Circle")
                    {
                        Object.DestroyImmediate(renderer.gameObject);
                    }
                }

                CutMiddle(small, cutFrom: 1.05f, cutHeight: 1.0f, "RackSmall");
                FixMaterials(small); Save(Normalise(small, "Rack_Small"), "Rack_Small");
            }

            // ---- the medium rack, as it came ----
            var medium = Instantiate(MediumRack);
            if (medium != null)
            {
                Save(Normalise(medium, "Rack_Medium"), "Rack_Medium");
            }

            // ---- the television, without the room it was photographed in ----
            var tv = Instantiate(Television);
            if (tv != null)
            {
                KeepMaterials(tv, new[] { "TV_IR", "TV_Screen", "TV_Frame", "TV_Sensor", "Chrome", "Cabinet_White", "Vase", "Vase_Red" }, "ModernTV");
                FixMaterials(tv); Save(Normalise(tv, "ModernTV"), "ModernTV");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[CGTrader] prefabs written to " + ResourcesFolder);
        }

        /// <summary>
        /// Replaces materials that did not survive the trip out of Blender: frosted glass arrives as
        /// opaque white, and the television's screen and frame as a flat grey.
        /// </summary>
        private static void FixMaterials(GameObject model)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;

                for (var index = 0; index < materials.Length; index++)
                {
                    var name = materials[index] == null ? string.Empty : materials[index].name.ToLowerInvariant();

                    if (name.Contains("glass"))
                    {
                        materials[index] = Shared("RackGlass", new Color(0.05f, 0.07f, 0.09f, 0.55f), 0.95f, transparent: true);
                    }
                    else if (name == "tv_screen")
                    {
                        materials[index] = Shared("TvScreen", new Color(0.02f, 0.02f, 0.025f), 0.95f, emission: new Color(0.02f, 0.05f, 0.09f));
                    }
                    else if (name == "tv_frame" || name == "tv_ir" || name == "tv_sensor")
                    {
                        materials[index] = Shared("TvFrame", new Color(0.04f, 0.04f, 0.045f), 0.6f);
                    }
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static Material Shared(string name, Color colour, float smoothness, bool transparent = false, Color? emission = null)
        {
            var path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_Color", colour);
            material.SetFloat("_Glossiness", smoothness);

            if (transparent)
            {
                material.SetFloat("_Mode", 3f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Instantiate(string path)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return model == null ? null : Object.Instantiate(model);
        }

        /// <summary>
        /// Puts a model's base on the floor and its middle on the origin, under a plain root, so the
        /// game can stand it on a square without knowing how the author built it.
        /// </summary>
        private static GameObject Normalise(GameObject model, string name)
        {
            var bounds = new Bounds();
            var first = true;

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            var root = new GameObject(name);
            model.transform.SetParent(root.transform, true);
            model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            return root;
        }

        private static void Save(GameObject root, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{ResourcesFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// Takes a horizontal band out of every mesh: vertices above the band move down by its height,
        /// parts entirely inside it are removed. How a tall cabinet becomes a short one without being
        /// squashed.
        /// </summary>
        private static void CutMiddle(GameObject model, float cutFrom, float cutHeight, string tag)
        {
            var cutTo = cutFrom + cutHeight;
            var index = 0;

            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<Renderer>();

                if (filter.sharedMesh == null || renderer == null)
                {
                    continue;
                }

                var b = renderer.bounds;

                if (b.min.y >= cutFrom && b.max.y <= cutTo)
                {
                    Object.DestroyImmediate(filter.gameObject);
                    continue;
                }

                if (b.max.y <= cutFrom)
                {
                    continue;
                }

                var mesh = Object.Instantiate(filter.sharedMesh);
                var vertices = mesh.vertices;
                var toLocal = filter.transform.worldToLocalMatrix;
                var toWorld = filter.transform.localToWorldMatrix;

                for (var v = 0; v < vertices.Length; v++)
                {
                    var world = toWorld.MultiplyPoint3x4(vertices[v]);

                    if (world.y > cutTo)
                    {
                        world.y -= cutHeight;
                    }
                    else if (world.y > cutFrom)
                    {
                        world.y = cutFrom;
                    }

                    vertices[v] = toLocal.MultiplyPoint3x4(world);
                }

                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, $"{Folder}/Meshes/{tag}_{index++}.asset");
                filter.sharedMesh = mesh;
            }
        }

        /// <summary>
        /// Keeps only the submeshes whose material is on the list. The television came as one mesh
        /// with the walls, window and floor of the author's render in it as further submeshes.
        /// </summary>
        private static void KeepMaterials(GameObject model, string[] keep, string tag)
        {
            var index = 0;

            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();

                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                var source = filter.sharedMesh;
                var materials = renderer.sharedMaterials;
                var mesh = new Mesh { indexFormat = source.indexFormat, name = source.name + "_kept" };

                var kept = new System.Collections.Generic.List<int[]>();
                var keptMaterials = new System.Collections.Generic.List<Material>();

                for (var sub = 0; sub < source.subMeshCount && sub < materials.Length; sub++)
                {
                    if (materials[sub] != null && System.Array.IndexOf(keep, materials[sub].name) >= 0)
                    {
                        kept.Add(source.GetTriangles(sub));
                        keptMaterials.Add(materials[sub]);
                    }
                }

                // Only the vertices the kept parts use, or the bounds stay the size of the room.
                var sourceVertices = source.vertices;
                var sourceNormals = source.normals;
                var sourceUv = source.uv;
                var remap = new System.Collections.Generic.Dictionary<int, int>();
                var vertices = new System.Collections.Generic.List<Vector3>();
                var normals = new System.Collections.Generic.List<Vector3>();
                var uv = new System.Collections.Generic.List<Vector2>();

                for (var sub = 0; sub < kept.Count; sub++)
                {
                    var triangles = kept[sub];

                    for (var t = 0; t < triangles.Length; t++)
                    {
                        if (!remap.TryGetValue(triangles[t], out var at))
                        {
                            at = vertices.Count;
                            remap[triangles[t]] = at;
                            vertices.Add(sourceVertices[triangles[t]]);
                            normals.Add(sourceNormals.Length > triangles[t] ? sourceNormals[triangles[t]] : Vector3.up);
                            uv.Add(sourceUv.Length > triangles[t] ? sourceUv[triangles[t]] : Vector2.zero);
                        }

                        triangles[t] = at;
                    }
                }

                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uv);
                mesh.subMeshCount = kept.Count;

                for (var sub = 0; sub < kept.Count; sub++)
                {
                    mesh.SetTriangles(kept[sub], sub);
                }

                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, $"{Folder}/Meshes/{tag}_{index++}.asset");
                filter.sharedMesh = mesh;
                renderer.sharedMaterials = keptMaterials.ToArray();
            }
        }

        /// <summary>Every renderer in the three models, with its size, to find the author's staging.</summary>
        public static void Dump()
        {
            foreach (var path in new[] { BigRack, MediumRack, Television })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (model == null)
                {
                    continue;
                }

                var placed = (GameObject)PrefabUtility.InstantiatePrefab(model);

                foreach (var renderer in placed.GetComponentsInChildren<Renderer>())
                {
                    var b = renderer.bounds;
                    Debug.Log($"[Dump] {Path.GetFileName(path)} | {renderer.name} | {b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00} at {b.center.x:0.00},{b.center.y:0.00},{b.center.z:0.00} | {string.Join(",", System.Linq.Enumerable.Select(renderer.sharedMaterials, m => m == null ? "-" : m.name))}");
                }

                Object.DestroyImmediate(placed);
            }
        }

        /// <summary>The three models side by side, for looking at.</summary>
        public static void Preview()
        {
            BuildPrefabs();

            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);

            var x = 0f;

            foreach (var path in new[] { ResourcesFolder + "/Rack_Big.prefab", ResourcesFolder + "/Rack_Medium.prefab",
                         ResourcesFolder + "/Rack_Small.prefab", ResourcesFolder + "/ModernTV.prefab",
                         PolyHavenImporter.Folder + "/modern_wooden_cabinet/modern_wooden_cabinet.fbx" })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (model == null)
                {
                    Debug.LogWarning("[CGTrader] missing " + path);
                    continue;
                }

                var placed = (GameObject)PrefabUtility.InstantiatePrefab(model);
                var bounds = new Bounds(placed.transform.position, Vector3.zero);

                foreach (var renderer in placed.GetComponentsInChildren<Renderer>())
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                Debug.Log($"[CGTrader] {Path.GetFileName(path)} size {bounds.size.x:0.00} x {bounds.size.y:0.00} x {bounds.size.z:0.00}");

                // Everything to about the height of a person, so the row can be compared.
                var scale = bounds.size.y > 0.01f ? Mathf.Clamp(1.8f / bounds.size.y, 0.001f, 1000f) : 1f;

                if (!path.Contains("Medium"))
                {
                    scale = 1f;
                }

                placed.transform.localScale *= scale;
                placed.transform.position = new Vector3(x, 0f, 0f) - new Vector3(0f, bounds.min.y * scale, 0f);
                x += Mathf.Max(bounds.size.x, bounds.size.z) * scale + 0.6f;
            }

            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.58f, 0.62f);
            cameraObject.transform.rotation = Quaternion.Euler(20f, -25f, 0f);
            cameraObject.transform.position = new Vector3(x / 2f, 1f, 0f) - cameraObject.transform.forward * 20f;
            camera.orthographicSize = Mathf.Max(2.2f, x * 0.3f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.5f);

            var target = new RenderTexture(1800, 700, 24) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var shot = new Texture2D(1800, 700, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, 1800, 700), 0, 0);
            RenderTexture.active = null;
            Directory.CreateDirectory("HubProof~");
            File.WriteAllBytes("HubProof~/new_models.png", shot.EncodeToPNG());
            Debug.Log("[CGTrader] wrote HubProof~/new_models.png");
        }
    }
}
