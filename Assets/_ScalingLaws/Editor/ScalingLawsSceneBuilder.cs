using System.IO;
using ScalingLaws.Core;
using ScalingLaws.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScalingLaws.Editor
{
    /// <summary>
    /// Builds both scenes from code.
    ///
    /// Same rule as BakaBakeBakery: the scenes are generated, not hand edited. Anything done by hand
    /// in the scene view is lost the next time this runs. Every scene change belongs in here.
    ///
    /// The scenes are deliberately almost empty. A camera so nothing complains, and one object
    /// carrying a UIDocument plus its controller. The controllers build their own visual trees, so
    /// there is no prefab, no canvas and no hierarchy to keep in sync.
    ///
    /// Run it from the menu, or in batch mode:
    ///   -batchmode -executeMethod ScalingLaws.Editor.ScalingLawsSceneBuilder.BuildAll -quit
    /// </summary>
    public static class ScalingLawsSceneBuilder
    {
        private const string ScenesFolder = "Assets/_ScalingLaws/Scenes";
        private const string UiFolder = "Assets/_ScalingLaws/UI";
        private const string ThemeFolder = "Assets/UI Toolkit/UnityThemes";

        private const string PanelSettingsPath = UiFolder + "/ScalingLawsPanelSettings.asset";
        private const string OfficePrefabPath = "Assets/_ScalingLaws/Prefabs/OfficeRoom.prefab";
        private const string OfficeTargetPath = "Assets/_ScalingLaws/Resources/OfficeView.renderTexture";
        /// <summary>
        /// The stylesheet lives under Resources and nowhere else. There were briefly two copies, one
        /// here and one there, and the one the game actually loaded silently fell half a file behind
        /// the one being edited. One file, one path, no drift.
        /// </summary>
        private const string StyleSheetPath = "Assets/_ScalingLaws/Resources/ScalingLaws.uss";
        private const string RuntimeThemePath = ThemeFolder + "/UnityDefaultRuntimeTheme.tss";

        private static string MainMenuScenePath => $"{ScenesFolder}/{SceneFlow.MainMenuScene}.unity";
        private static string GameScenePath => $"{ScenesFolder}/{SceneFlow.GameScene}.unity";

        [MenuItem("Scaling Laws/Rebuild scenes")]
        public static void BuildAll()
        {
            EnsureFolder("Assets/UI Toolkit");
            EnsureFolder(ThemeFolder);
            EnsureFolder(ScenesFolder);

            var theme = EnsureRuntimeTheme();
            var panelSettings = EnsurePanelSettings(theme);
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);

            if (styleSheet == null)
            {
                Debug.LogError($"[Scaling Laws] Stylesheet missing at {StyleSheetPath}. Scenes will be unstyled.");
            }

            BuildScene<MainMenuController>(MainMenuScenePath, "MainMenu", panelSettings, styleSheet);
            BuildScene<GameShell>(GameScenePath, "GameShell", panelSettings, styleSheet);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Scaling Laws] Scenes rebuilt and added to build settings.");
        }

        private static void BuildScene<TController>(
            string path,
            string objectName,
            PanelSettings panelSettings,
            StyleSheet styleSheet)
            where TController : MonoBehaviour
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // UI Toolkit runtime panels render on their own, but a scene with no camera logs a
            // warning on every load and looks like a mistake.
            var cameraObject = new GameObject("Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.176f, 0.353f);
            camera.orthographic = true;
            cameraObject.tag = "MainCamera";

            var uiObject = new GameObject(objectName);
            var document = uiObject.AddComponent<UIDocument>();

            // Written through SerializedObject rather than through the property.
            //
            // The property assignment persisted for the menu scene and was empty in the game scene
            // every single time it was rebuilt, which is how the game shipped invisible for a week.
            // Whatever the setter does, it does not reliably survive the save on the second scene of
            // a run. The serialized field is the thing that actually gets written to disk, so that
            // is what gets set, exactly as the stylesheet already does.
            var serialized = new SerializedObject(document);
            var property = serialized.FindProperty("m_PanelSettings");
            if (property == null)
            {
                Debug.LogError("UIDocument has no m_PanelSettings field. Unity changed the format.");
            }
            else
            {
                property.objectReferenceValue = panelSettings;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var controller = uiObject.AddComponent<TController>();
            AssignStyleSheet(controller, styleSheet);

            if (typeof(TController) == typeof(GameShell))
            {
                AddOfficeStage();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);

            // The game scene once shipped with this reference empty. Nothing failed: the interface
            // was built exactly as normal and then drawn into no panel at all, so the screen was the
            // camera clear colour and the game looked hung. It is read back from what was actually
            // written rather than trusted from what was assigned.
            var written = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (written == null)
            {
                Debug.LogError($"{path} did not save.");
            }
            else if (new SerializedObject(document).FindProperty("m_PanelSettings")?.objectReferenceValue == null)
            {
                Debug.LogError($"{path} saved with no PanelSettings on its UIDocument. Nothing it "
                    + "builds will ever be drawn.");
            }
        }

        /// <summary>
        /// Puts the generated room in the game scene with its own camera, rendering into a texture
        /// the interface can show.
        ///
        /// A second camera into a render target rather than a second window: the office has to
        /// appear inside a UI panel alongside everything else, and UI Toolkit can show a texture but
        /// cannot show a camera. It also means the room is drawn only while the SITE screen is open,
        /// because a disabled camera costs nothing.
        ///
        /// The angle is the one every placement in the room assumes and must not be changed here.
        /// </summary>
        private static void AddOfficeStage()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OfficePrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("No office prefab yet. Run Scaling Laws > Build office room first.");
                return;
            }

            var target = AssetDatabase.LoadAssetAtPath<RenderTexture>(OfficeTargetPath);
            if (target == null)
            {
                target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32)
                {
                    name = "OfficeView",
                    antiAliasing = 4
                };

                AssetDatabase.CreateAsset(target, OfficeTargetPath);
            }

            var room = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            room.name = "OfficeRoom";

            // Far away from the interface camera so neither can ever see the other's geometry.
            room.transform.position = new Vector3(0f, -500f, 0f);

            var cameraObject = new GameObject("OfficeCamera");
            cameraObject.transform.SetParent(room.transform, false);

            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7.5f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.090f);
            camera.targetTexture = target;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            cameraObject.transform.rotation = Quaternion.Euler(30f, -45f, 0f);
            cameraObject.transform.position = new Vector3(6f, -500f + 3f, 4.5f)
                - cameraObject.transform.forward * 30f;

            var keyObject = new GameObject("OfficeKeyLight");
            keyObject.transform.SetParent(room.transform, false);

            var key = keyObject.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.1f;
            key.color = new Color(1.0f, 0.96f, 0.90f);
            key.shadows = LightShadows.Soft;
            keyObject.transform.rotation = Quaternion.Euler(45f, 200f, 0f);
        }

        /// <summary>
        /// The theme field is private and serialized, which is correct for a component nobody should
        /// be reaching into at runtime. The builder writes it through SerializedObject rather than
        /// widening the API just to make generation convenient.
        /// </summary>
        private static void AssignStyleSheet(MonoBehaviour controller, StyleSheet styleSheet)
        {
            if (styleSheet == null)
            {
                return;
            }

            var serialized = new SerializedObject(controller);
            var property = serialized.FindProperty("theme");
            if (property == null)
            {
                Debug.LogWarning($"[Scaling Laws] {controller.GetType().Name} has no serialized theme field.");
                return;
            }

            property.objectReferenceValue = styleSheet;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static PanelSettings EnsurePanelSettings(ThemeStyleSheet theme)
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;

            EditorUtility.SetDirty(settings);
            return settings;
        }

        /// <summary>
        /// The default runtime theme is a one line file that pulls in Unity's built-in control
        /// styling. Without it every TextField, Slider and DropdownField renders as an unstyled box.
        /// </summary>
        private static ThemeStyleSheet EnsureRuntimeTheme()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(RuntimeThemePath);
            if (existing != null)
            {
                return existing;
            }

            var found = AssetDatabase.FindAssets("t:ThemeStyleSheet");
            if (found.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(found[0]);
                var located = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
                if (located != null)
                {
                    return located;
                }
            }

            File.WriteAllText(RuntimeThemePath, "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(RuntimeThemePath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(RuntimeThemePath);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
