using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;

// Custom inspector for tile assigning
namespace WFCPatternCreator
{
    [CustomEditor(typeof(Tile))]
    public class TileEditor : Editor
    {
        private Tile tile;
    
        readonly private static Dictionary<Tile, GameObject> previewObjects = new();
    
        private void OnEnable()
        {
            tile = (Tile)target;
        }
    
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
    
            EditorGUILayout.PropertyField(serializedObject.FindProperty("allowedZone"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("applyReverse"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("adjustYPosition"));
            if (tile.adjustYPosition) { EditorGUILayout.PropertyField(serializedObject.FindProperty("yPosAdjustment")); }

            if (GUILayout.Button("Fix Reverse Connections"))
            {
                FixAllReverseConnections();
                serializedObject.Update();
            }

            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);

            DrawNeighbourList("North Neighbours", "northNeighbours", Vector3.forward);
            DrawNeighbourList("South Neighbours", "southNeighbours", Vector3.back);
            DrawNeighbourList("East Neighbours",  "eastNeighbours",  Vector3.right);
            DrawNeighbourList("West Neighbours",  "westNeighbours",  Vector3.left);
            DrawNeighbourList("Up Neighbours",    "upNeighbours",    Vector3.up);
            DrawNeighbourList("Down Neighbours",  "downNeighbours",  Vector3.down);

            serializedObject.ApplyModifiedProperties();
        }


        private void DrawNeighbourList(string label, string propertyName, Vector3 directionOffset)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            HashSet<Tile> seen = new();
            SerializedProperty prop = serializedObject.FindProperty(propertyName);

            bool wasRemoved = false; // Add flag to track if we removed an item

            for (int i = 0; i < prop.arraySize; i++)
            {
                SerializedProperty element = prop.GetArrayElementAtIndex(i);
                Tile t = (Tile)element.objectReferenceValue;

                Color originalColor = GUI.color;
                if (t != null && seen.Contains(t))
                    GUI.color = Color.red;

                EditorGUILayout.BeginHorizontal();

                // Detect changes to handle reverse connections
                Tile previousTile = t;
                EditorGUI.BeginChangeCheck();
                element.objectReferenceValue = EditorGUILayout.ObjectField(element.objectReferenceValue, typeof(Tile), true, GUILayout.ExpandWidth(true));
                if (EditorGUI.EndChangeCheck())
                {
                    Tile newTile = (Tile)element.objectReferenceValue;

                    // Handle reverse neighbor assignment
                    if (tile.applyReverse)
                    {
                        // Remove old tile's reverse connection if it existed
                        if (previousTile != null && previousTile != newTile)
                            previousTile.RemoveNeighbourEditorOnly(tile, OppositeDirection(directionOffset));

                        // Add new tile's reverse connection
                        if (newTile != null && newTile != previousTile)
                            newTile.AddNeighbourEditorOnly(tile, OppositeDirection(directionOffset));
                    }

                    t = newTile;
                }

                if (t != null)
                {
                    if (GUILayout.Button("Visualize", GUILayout.Width(70)))
                    {
                        TogglePreview(t, tile.transform.position + directionOffset);
                    }
                }

                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    Tile removedTile = t;

                    // Remove preview if the tile exists
                    if (removedTile != null)
                    {
                        RemovePreview(removedTile);

                        // Remove reverse connection
                        if (tile.applyReverse)
                            removedTile.RemoveNeighbourEditorOnly(tile, OppositeDirection(directionOffset));
                    }

                    // Close layout before breaking
                    EditorGUILayout.EndHorizontal();
                    GUI.color = originalColor;

                    // Delete the array element
                    prop.DeleteArrayElementAtIndex(i);
                    serializedObject.ApplyModifiedProperties();

                    wasRemoved = true; // Set flag
                    break;
                }

                EditorGUILayout.EndHorizontal();

                if (t != null)
                    seen.Add(t);

                GUI.color = originalColor;
            }

            // Only draw drop area if didn't remove anything to avoid layout errors
            if (!wasRemoved)
            {
                // Drop area for adding new tiles by dragging
                Rect dropArea = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));

                Event evt = Event.current;

                // Check if dragging over the area
                bool isDragging = DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0;
                bool isHovering = dropArea.Contains(evt.mousePosition) && isDragging;

                // Visual styling for drop area
                GUIStyle boxStyle = new(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };

                if (isHovering)
                {
                    GUI.backgroundColor = Color.green;
                    GUI.Box(dropArea, "Release to Add Tile", boxStyle);
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.Box(dropArea, "Drag Tile Here to Add", boxStyle);
                }

                // Handle drag and drop events
                switch (evt.type)
                {
                    case EventType.DragUpdated:
                        if (dropArea.Contains(evt.mousePosition))
                        {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            evt.Use();
                            Repaint();
                        }
                        break;

                    case EventType.DragPerform:
                        if (dropArea.Contains(evt.mousePosition))
                        {
                            DragAndDrop.AcceptDrag();

                            foreach (Object draggedObject in DragAndDrop.objectReferences)
                            {
                                Tile draggedTile = draggedObject as Tile;
                                if (draggedTile == null)
                                {
                                    GameObject go = draggedObject as GameObject;
                                    if (go != null)
                                        draggedTile = go.GetComponent<Tile>();
                                }

                                if (draggedTile != null)
                                {
                                    prop.arraySize++;
                                    SerializedProperty newElement = prop.GetArrayElementAtIndex(prop.arraySize - 1);
                                    newElement.objectReferenceValue = draggedTile;

                                    if (tile.applyReverse)
                                    {
                                        draggedTile.AddNeighbourEditorOnly(tile, OppositeDirection(directionOffset));
                                    }
                                }
                            }

                            tile.RemoveEmptyNeighbours();
                            serializedObject.ApplyModifiedProperties();
                            evt.Use();
                            Repaint();
                            serializedObject.Update();
                        }
                        break;
                }
            }

            EditorGUILayout.Space();
        }

        private void TogglePreview(Tile neighbor, Vector3 targetPosition)
        {
            // Remove existing previews for other neighbors
            List<Tile> keys = new(previewObjects.Keys);
            foreach (Tile key in keys)
            {
                if (key != neighbor)
                    RemovePreview(key);
            }
            // If clicked same object to preview
            if (previewObjects.ContainsKey(neighbor))
            {
                // Do nuffin
            }
            else
            {
                // Create new preview GameObject
                GameObject preview = new($"{neighbor.name}_Preview");
                preview.transform.position = targetPosition;

                // visuals for the object
                MeshFilter mf = neighbor.GetComponent<MeshFilter>();
                MeshRenderer mr = neighbor.GetComponent<MeshRenderer>();
                if (mf == null) { mf = neighbor.GetComponentInChildren<MeshFilter>(); }
                if (mr == null) { mr = neighbor.GetComponentInChildren<MeshRenderer>(); }
                if (mf != null && mr != null)
                {
                    // Get meshes rotation
                    preview.transform.rotation = mf.transform.rotation;
                    preview.transform.localScale = mf.transform.lossyScale;

                    MeshFilter pmf = preview.AddComponent<MeshFilter>();
                    pmf.sharedMesh = mf.sharedMesh;
                    MeshRenderer pmr = preview.AddComponent<MeshRenderer>();
                    pmr.sharedMaterials = mr.sharedMaterials;
                    pmr.shadowCastingMode = ShadowCastingMode.Off;
                    pmr.receiveShadows = false;
                }

                // Mark as editor-only
                preview.hideFlags = HideFlags.DontSave;

                previewObjects[neighbor] = preview;
            }
        }

        private void RemovePreview(Tile neighbor)
        {
            if (!previewObjects.ContainsKey(neighbor))
            {
                return;
            }
            GameObject go = previewObjects[neighbor];
            if (go != null)
                GameObject.DestroyImmediate(go);
            previewObjects.Remove(neighbor);
        }

        private void FixAllReverseConnections()
        {
            if (!tile.applyReverse)
            {
                EditorUtility.DisplayDialog("Apply Reverse Disabled",
                    "Apply Reverse is currently disabled. Enable it first to fix connections.", "OK");
                return;
            }

            int fixedCount = 0;

            // For each direction, ensure all neighbors have the reverse connection
            fixedCount += EnsureReverseConnections(tile.northNeighbours, Tile.NeighbourDirection.South);
            fixedCount += EnsureReverseConnections(tile.southNeighbours, Tile.NeighbourDirection.North);
            fixedCount += EnsureReverseConnections(tile.eastNeighbours, Tile.NeighbourDirection.West);
            fixedCount += EnsureReverseConnections(tile.westNeighbours, Tile.NeighbourDirection.East);
            fixedCount += EnsureReverseConnections(tile.upNeighbours, Tile.NeighbourDirection.Down);
            fixedCount += EnsureReverseConnections(tile.downNeighbours, Tile.NeighbourDirection.Up);

            if (fixedCount > 0)
            {
                Debug.Log($"Fixed {fixedCount} reverse connection(s) for {tile.name}");
                EditorUtility.SetDirty(tile);
            }
            else
            {
                Debug.Log($"All reverse connections are already correct for {tile.name}");
            }
        }

        private int EnsureReverseConnections(Tile[] neighbors, Tile.NeighbourDirection reverseDirection)
        {
            if (neighbors == null || neighbors.Length == 0)
                return 0;

            int fixedCount = 0;

            foreach (Tile neighbor in neighbors)
            {
                if (neighbor == null)
                    continue;

                // Get the neighbor's array in the reverse direction
                Tile[] reverseArray = neighbor.GetArrayByDirection(reverseDirection);

                // Check if this tile is in the neighbor's reverse array
                if (!tile.Contains(reverseArray, tile))
                {
                    // Add this tile to the neighbor's reverse array
                    neighbor.AddNeighbourEditorOnly(tile, reverseDirection);
                    fixedCount++;
                    Debug.Log($"Added reverse connection: {neighbor.name}, {tile.name} ({reverseDirection})");
                }
            }

            return fixedCount;
        }

        private Tile.NeighbourDirection OppositeDirection(Vector3 dir)
        {
            if (dir == Vector3.forward) return Tile.NeighbourDirection.South;  // North to South
            if (dir == Vector3.back) return Tile.NeighbourDirection.North;     // South to North
            if (dir == Vector3.right) return Tile.NeighbourDirection.West;     // East to West
            if (dir == Vector3.left) return Tile.NeighbourDirection.East;      // West to East
            if (dir == Vector3.up) return Tile.NeighbourDirection.Down;        // Up to Down
            else return Tile.NeighbourDirection.Up;
        }

        private void OnDisable()
        {
            foreach (var kv in previewObjects)
                if (kv.Value != null)
                    DestroyImmediate(kv.Value);
    
            previewObjects.Clear();
        }
    }
}