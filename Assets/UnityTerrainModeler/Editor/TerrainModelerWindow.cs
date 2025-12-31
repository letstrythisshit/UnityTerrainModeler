using UnityEditor;
using UnityEngine;
using UnityTerrainModeler.Runtime;

namespace UnityTerrainModeler.Editor
{
    public class TerrainModelerWindow : EditorWindow
    {
        private TerrainModelerSettings settings;
        private TerrainModelerGenerator generator;

        [MenuItem("Tools/Unity Terrain Modeler")]
        public static void Open()
        {
            TerrainModelerWindow window = GetWindow<TerrainModelerWindow>();
            window.titleContent = new GUIContent("Terrain Modeler");
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Terrain Modeler", EditorStyles.boldLabel);
            settings = (TerrainModelerSettings)EditorGUILayout.ObjectField("Settings", settings, typeof(TerrainModelerSettings), false);
            generator = (TerrainModelerGenerator)EditorGUILayout.ObjectField("Generator", generator, typeof(TerrainModelerGenerator), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create Settings Asset"))
                {
                    CreateSettingsAsset();
                }

                if (GUILayout.Button("Create Generator"))
                {
                    CreateGenerator();
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(settings == null))
            {
                if (GUILayout.Button("Apply Biome Defaults"))
                {
                    settings.ApplyBiomeDefaults();
                    EditorUtility.SetDirty(settings);
                }
            }

            using (new EditorGUI.DisabledScope(settings == null || generator == null))
            {
                if (GUILayout.Button("Generate Terrain"))
                {
                    generator.settings = settings;
                    generator.Generate();
                }
            }
        }

        private void CreateSettingsAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Terrain Modeler Settings",
                "TerrainModelerSettings",
                "asset",
                "Select a location for the settings asset.");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            TerrainModelerSettings asset = CreateInstance<TerrainModelerSettings>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            settings = asset;
        }

        private void CreateGenerator()
        {
            Terrain terrain = FindObjectOfType<Terrain>();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Terrain Modeler", "No Terrain found in the scene.", "Ok");
                return;
            }

            TerrainModelerGenerator existing = terrain.GetComponent<TerrainModelerGenerator>();
            if (existing != null)
            {
                generator = existing;
                return;
            }

            generator = Undo.AddComponent<TerrainModelerGenerator>(terrain.gameObject);
            generator.settings = settings;
        }
    }
}
