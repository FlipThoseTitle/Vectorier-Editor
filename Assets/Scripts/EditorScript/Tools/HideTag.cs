using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Vectorier.EditorScript.Tools
{
    public class HideTagWindow : EditorWindow
    {
        private const string MenuPath = "Vectorier/Tools/Hide Tags";
        private const string EditorPrefsPrefix = "Vectorier.EditorScript.Tools.HideTag.";

        private static readonly string[] Tags =
        {
            "Object",
            "Image",
            "Trigger",
            "Area",
            "Platform",
            "Trapezoid",
            "Spawn",
            "Camera",
            "Model",
            "Item",
            "Animation",
            "Particle",
            "Comment"
        };

        private static readonly string[] Layers =
        {
            "0.05",
            "0.1",
            "0.25",
            "0.5",
            "0.7",
            "1",
            "1.000001",
            "1.25",
            "1.5",
            "2"
        };

        private Vector2 _scrollPosition;
        private bool _isApplyingVisibility = false;

        [MenuItem(MenuPath, false, 40)]
        public static void ShowWindow()
        {
            HideTagWindow window = GetWindow<HideTagWindow>("Hide Tags");
            window.minSize = new Vector2(200, 400);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            SyncStatesWithHierarchy();
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Visibility Toggles", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);
            
            bool allTagsVisible = AreAllTagsVisible();
            EditorGUI.BeginChangeCheck();
            bool newAllTagsState = EditorGUILayout.ToggleLeft("All", allTagsVisible, EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck())
            {
                ToggleAllTags(newAllTagsState);
            }

            EditorGUILayout.Space(2);

            foreach (string tagName in Tags)
            {
                bool currentlyVisible = IsTagVisible(tagName);
                
                EditorGUI.BeginChangeCheck();
                bool newVisibleState = EditorGUILayout.ToggleLeft(tagName, currentlyVisible);
                
                if (EditorGUI.EndChangeCheck())
                {
                    ToggleTag(tagName, newVisibleState);
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Layers", EditorStyles.boldLabel);

            bool allLayersVisible = AreAllLayersVisible();
            EditorGUI.BeginChangeCheck();
            bool newAllLayersState = EditorGUILayout.ToggleLeft("All", allLayersVisible, EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck())
            {
                ToggleAllLayers(newAllLayersState);
            }

            EditorGUILayout.Space(2);

            foreach (string layerName in Layers)
            {
                bool currentlyVisible = IsLayerVisible(layerName);
                
                EditorGUI.BeginChangeCheck();
                bool newVisibleState = EditorGUILayout.ToggleLeft(layerName, currentlyVisible);
                
                if (EditorGUI.EndChangeCheck())
                {
                    ToggleLayer(layerName, newVisibleState);
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Refresh"))
            {
                SyncStatesWithHierarchy();
            }
        }

        private bool AreAllTagsVisible()
        {
            foreach (string tagName in Tags)
            {
                if (!IsTagVisible(tagName)) return false;
            }
            return true;
        }

        private bool AreAllLayersVisible()
        {
            foreach (string layerName in Layers)
            {
                if (!IsLayerVisible(layerName)) return false;
            }
            return true;
        }

        private void ToggleAllTags(bool makeVisible)
        {
            _isApplyingVisibility = true;

            foreach (string tagName in Tags)
            {
                SetTagVisible(tagName, makeVisible);
                GameObject[] matchingObjects = FindSceneObjectsWithTag(tagName);
                foreach (GameObject go in matchingObjects)
                {
                    if (go == null) continue;
                    if (makeVisible) SceneVisibilityManager.instance.Show(go, true);
                    else SceneVisibilityManager.instance.Hide(go, true);
                }
            }

            EditorApplication.RepaintHierarchyWindow();
            SceneView.RepaintAll();
            _isApplyingVisibility = false;
        }

        private void ToggleAllLayers(bool makeVisible)
        {
            _isApplyingVisibility = true;

            foreach (string layerName in Layers)
            {
                SetLayerVisible(layerName, makeVisible);
                GameObject[] matchingObjects = FindSceneObjectsWithLayer(layerName);
                foreach (GameObject go in matchingObjects)
                {
                    if (go == null) continue;
                    if (makeVisible) SceneVisibilityManager.instance.Show(go, true);
                    else SceneVisibilityManager.instance.Hide(go, true);
                }
            }

            EditorApplication.RepaintHierarchyWindow();
            SceneView.RepaintAll();
            _isApplyingVisibility = false;
        }

        private void ToggleTag(string tagName, bool makeVisible)
        {
            SetTagVisible(tagName, makeVisible);
            ApplyVisibilityForTag(tagName, makeVisible);
        }

        private void ToggleLayer(string layerName, bool makeVisible)
        {
            SetLayerVisible(layerName, makeVisible);
            ApplyVisibilityForLayer(layerName, makeVisible);
        }

        private void ApplyVisibilityForTag(string tagName, bool makeVisible)
        {
            _isApplyingVisibility = true; 

            GameObject[] matchingObjects = FindSceneObjectsWithTag(tagName);

            foreach (GameObject gameObject in matchingObjects)
            {
                if (gameObject == null) continue;

                if (makeVisible)
                {
                    SceneVisibilityManager.instance.Show(gameObject, true);
                }
                else
                {
                    SceneVisibilityManager.instance.Hide(gameObject, true);
                }
            }

            EditorApplication.RepaintHierarchyWindow();
            SceneView.RepaintAll();

            _isApplyingVisibility = false;
        }

        private void ApplyVisibilityForLayer(string layerName, bool makeVisible)
        {
            _isApplyingVisibility = true;

            GameObject[] matchingObjects = FindSceneObjectsWithLayer(layerName);

            foreach (GameObject gameObject in matchingObjects)
            {
                if (gameObject == null) continue;

                if (makeVisible)
                {
                    SceneVisibilityManager.instance.Show(gameObject, true);
                }
                else
                {
                    SceneVisibilityManager.instance.Hide(gameObject, true);
                }
            }

            EditorApplication.RepaintHierarchyWindow();
            SceneView.RepaintAll();

            _isApplyingVisibility = false;
        }

        private void OnHierarchyChanged()
        {
            if (!_isApplyingVisibility)
            {
                SyncStatesWithHierarchy();
            }
        }

        private void SyncStatesWithHierarchy()
        {
            bool UIUpdated = false;

            foreach (string tagName in Tags)
            {
                GameObject[] objects = FindSceneObjectsWithTag(tagName);
                if (objects.Length == 0) continue;

                bool allVisible = true;
                bool allInvisible = true;

                foreach (GameObject go in objects)
                {
                    if (go == null) continue;

                    if (SceneVisibilityManager.instance.IsHidden(go))
                    {
                        allVisible = false;
                    }
                    else
                    {
                        allInvisible = false;
                    }
                }

                bool savedState = IsTagVisible(tagName);

                if (allVisible && !savedState)
                {
                    SetTagVisible(tagName, true);
                    UIUpdated = true;
                }
                else if (allInvisible && savedState)
                {
                    SetTagVisible(tagName, false);
                    UIUpdated = true;
                }
            }

            foreach (string layerName in Layers)
            {
                GameObject[] objects = FindSceneObjectsWithLayer(layerName);
                if (objects.Length == 0) continue;

                bool allVisible = true;
                bool allInvisible = true;

                foreach (GameObject go in objects)
                {
                    if (go == null) continue;

                    if (SceneVisibilityManager.instance.IsHidden(go))
                    {
                        allVisible = false;
                    }
                    else
                    {
                        allInvisible = false;
                    }
                }

                bool savedState = IsLayerVisible(layerName);

                if (allVisible && !savedState)
                {
                    SetLayerVisible(layerName, true);
                    UIUpdated = true;
                }
                else if (allInvisible && savedState)
                {
                    SetLayerVisible(layerName, false);
                    UIUpdated = true;
                }
            }

            if (UIUpdated)
            {
                Repaint();
            }
        }

        private static GameObject[] FindSceneObjectsWithTag(string tagName)
        {
            List<GameObject> results = new List<GameObject>();
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (GameObject gameObject in allObjects)
            {
                if (gameObject == null) continue;

                if (EditorUtility.IsPersistent(gameObject)) continue;

                if (!gameObject.scene.IsValid()) continue;

                if (!string.Equals(gameObject.tag, tagName, StringComparison.Ordinal)) continue;

                results.Add(gameObject);
            }

            return results.ToArray();
        }

        private static GameObject[] FindSceneObjectsWithLayer(string layerName)
        {
            List<int> targetLayerIndexes = new List<int>();
            
            int primaryIndex = LayerMask.NameToLayer(layerName);
            if (primaryIndex != -1)
            {
                targetLayerIndexes.Add(primaryIndex);
            }

            if (layerName == "1")
            {
                int defaultIndex = LayerMask.NameToLayer("Default");
                if (defaultIndex != -1 && !targetLayerIndexes.Contains(defaultIndex))
                {
                    targetLayerIndexes.Add(defaultIndex);
                }
            }

            if (targetLayerIndexes.Count == 0) return new GameObject[0];

            HashSet<GameObject> results = new HashSet<GameObject>();
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

            foreach (GameObject gameObject in allObjects)
            {
                if (gameObject == null) continue;

                if (EditorUtility.IsPersistent(gameObject)) continue;

                if (!gameObject.scene.IsValid()) continue;

                if (!gameObject.CompareTag("Untagged"))
                {
                    GameObject rootTagged = GetRootTaggedObject(gameObject);
                    if (targetLayerIndexes.Contains(rootTagged.layer))
                    {
                        results.Add(rootTagged);
                    }
                }
            }

            GameObject[] resultArray = new GameObject[results.Count];
            int index = 0;
            foreach (GameObject go in results)
            {
                resultArray[index++] = go;
            }
            return resultArray;
        }

        private static GameObject GetRootTaggedObject(GameObject go)
        {
            GameObject rootTagged = go;
            Transform current = go.transform;
            while (current != null)
            {
                if (!current.gameObject.CompareTag("Untagged"))
                {
                    rootTagged = current.gameObject;
                }
                current = current.parent;
            }
            return rootTagged;
        }

        private static bool IsTagVisible(string tagName)
        {
            return EditorPrefs.GetBool(GetPrefsKey(tagName), true);
        }

        private static void SetTagVisible(string tagName, bool visible)
        {
            EditorPrefs.SetBool(GetPrefsKey(tagName), visible);
        }

        private static string GetPrefsKey(string tagName)
        {
            return EditorPrefsPrefix + Application.dataPath.GetHashCode() + "." + tagName;
        }

        private static bool IsLayerVisible(string layerName)
        {
            return EditorPrefs.GetBool(GetLayerPrefsKey(layerName), true);
        }

        private static void SetLayerVisible(string layerName, bool visible)
        {
            EditorPrefs.SetBool(GetLayerPrefsKey(layerName), visible);
        }

        private static string GetLayerPrefsKey(string layerName)
        {
            return EditorPrefsPrefix + Application.dataPath.GetHashCode() + ".Layer." + layerName;
        }
    }
}