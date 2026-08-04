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
            document.panelSettings = panelSettings;

            var controller = uiObject.AddComponent<TController>();
            AssignStyleSheet(controller, styleSheet);

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
            else if (document.panelSettings == null)
            {
                Debug.LogError($"{path} saved with no PanelSettings on its UIDocument. Nothing it "
                    + "builds will ever be drawn.");
            }
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
