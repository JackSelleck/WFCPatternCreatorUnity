using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using System.IO;

namespace WFCPatternCreator
{
    /// <summary>
    /// Tool to make tile rotations easier
    /// </summary>
    public class ModelRotator : EditorWindow
    {
        private string folderPath = "Assets/Models";
        private List<Object> excludedModels = new();

        [MenuItem("Tools/Tile Rotator", priority = 1)]
        public static void ShowWindow()
        {
            GetWindow<ModelRotator>("Model Rotator");
        }

        void OnGUI()
        {
            GUILayout.Label("Duplicate and Rotate Models", EditorStyles.boldLabel);
            folderPath = EditorGUILayout.TextField("Folder Path:", folderPath);

            GUILayout.Space(8);
            GUILayout.Label("Excluded Models (drag assets here):", EditorStyles.boldLabel);

            DrawExclusionList();

            GUILayout.Space(10);
            if (GUILayout.Button("Duplicate & Rotate"))
            {
                GetModelsAndPrefabs();
            }
        }

        void DrawExclusionList()
        {
            // Draw the drop area
            var dropArea = GUILayoutUtility.GetRect(0, 80, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag models here to exclude");

            Event evt = Event.current;
            if (dropArea.Contains(evt.mousePosition))
            {
                if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (Object obj in DragAndDrop.objectReferences)
                        {
                            if (!excludedModels.Contains(obj))
                                excludedModels.Add(obj);
                        }
                    }
                    evt.Use();
                }
            }

            // Show excluded items
            for (int i = excludedModels.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                excludedModels[i] = EditorGUILayout.ObjectField(excludedModels[i], typeof(Object), false);

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    excludedModels.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }

            if (excludedModels.Count == 0)
            {
                GUILayout.Label("None", EditorStyles.miniLabel);
            }
        }

        // Dont know if people will use direct models or prefabs of models so just look for both
        void GetModelsAndPrefabs()
        {
            if (!Directory.Exists(folderPath))
            {
                Debug.LogError($"Folder not found: {folderPath}");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });
            string[] prefabAssets = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });

            if (guids.Length == 0 && prefabAssets.Length == 0)
            {
                Debug.LogWarning("No models or prefabs in folder");
                return;
            }

            // Batch asset operations to prevent Inspector updates
            AssetDatabase.StartAssetEditing();
            try
            {
                if (guids.Length != 0)
                {
                    ProcessModels(guids);
                }
                if (prefabAssets.Length != 0)
                {
                    ProcessPrefabs(prefabAssets);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
                Debug.Log("Finished duplicating and rotating models.");
            }
        }

        void ProcessModels(string[] prefabAssets)
        {
            foreach (string guid in prefabAssets)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (model == null) continue;

                // Skip excluded models
                if (excludedModels.Contains(model))
                {
                    Debug.Log($"Skipped {model.name}");
                    continue;
                }

                CreateRotatedPrefab(model, assetPath, "West", 90);
                CreateRotatedPrefab(model, assetPath, "South", 180);
                CreateRotatedPrefab(model, assetPath, "East", 270);
            }
        }

        void ProcessPrefabs(string[] prefabAssets)
        {
            foreach (string guid in prefabAssets)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                // Skip excluded models
                if (excludedModels.Contains(prefab))
                {
                    Debug.Log($"Skipped {prefab.name}");
                    continue;
                }

                CreateRotatedPrefab(prefab, assetPath, "West", 90);
                CreateRotatedPrefab(prefab, assetPath, "South", 180);
                CreateRotatedPrefab(prefab, assetPath, "East", 270);
            }
        }

        void CreateRotatedPrefab(GameObject model, string assetPath, string suffix, float yRotation)
        {
            GameObject root = new($"{suffix}_{model.name}");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(root.transform, false);
            instance.transform.localRotation = Quaternion.Euler(0, yRotation, 0);
            Undo.AddComponent<Tile>(root);
            EditorUtility.SetDirty(root);

            string dir = Path.GetDirectoryName(assetPath);
            string newName = $"{suffix}_{model.name}.prefab";
            string newPath = Path.Combine(dir, newName).Replace("\\", "/");

            PrefabUtility.SaveAsPrefabAsset(root, newPath);
            DestroyImmediate(root);

            Debug.Log($"Created rotated prefab: {newPath}");
        }
    }
}