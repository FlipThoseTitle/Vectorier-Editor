using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vectorier.Component;

namespace Vectorier.EditorScript.Tools
{
    public static class HierarchySort
    {
        private const string UndoName = "Hierarchy Sort";

        // ---------------------------------------------------------------------
        // Configuration
        // ---------------------------------------------------------------------

        // Tag -> group name. The order here is also the sibling order of the groups
        // and the tag priority used when sorting children inside an "Object".
        private static readonly (string Tag, string Group)[] SortGroups =
        {
            ("Object",    "Objects"),
            ("Image",     "Images"),
            ("Trigger",   "Triggers"),
            ("Area",      "Areas"),
            ("Platform",  "Platforms"),
            ("Trapezoid", "Trapezoids"),
            ("Model",     "Models"),
            ("Item",      "Items"),
            ("Animation", "Animations"),
            ("Particle",  "Particle"),
            ("Comment",   "Comment"),
        };

        // Unity layer name + value -> sub-group name under "Images" (in display order).
        private static readonly (string LayerName, double Value, string Group)[] ImageLayers =
        {
            ("Background",         0.05,    "Background"),
            ("Far Background",     0.1,     "Far Background"),
            ("Midground",          0.25,    "Midground"),
            ("Backdrop",           0.5,     "Backdrop"),
            ("Near Backdrop",      0.7,     "Near Backdrop"),
            ("Gameplay (Default)", 1.0,     "Gameplay"),
            ("Overlay",            1.00001, "Overlay"),
            ("Foreground",         1.25,    "Foreground"),
            ("Near Foreground",    1.5,     "Near Foreground"),
            ("Frontmost",          2.0,     "Frontmost"),
        };

        private const string DefaultLayerName = "Default";
        private const string DefaultLayerGroup = "Gameplay";

        private const string CameraTag = "Camera";
        private const string SpawnTag = "Spawn";
        private const string ObjectTag = "Object";
        private const string ImageTag = "Image";
        private const string HunterCameraName = "CameraHunterMode";

        private static readonly HashSet<string> SpawnNames = new HashSet<string>
        {
            "DefaultSpawn",
            "PlayerSpawn",
            "HunterSpawn",
        };

        private static readonly IComparer<string> NaturalComparer =
            Comparer<string>.Create(EditorUtility.NaturalCompare);

        // ---------------------------------------------------------------------
        // Entry point
        // ---------------------------------------------------------------------

        [MenuItem("Vectorier/Tools/Quick Actions/Auto-Sort Hierarchy", false, 58)]
        public static void Sort()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[HierarchySort] No valid scene is loaded.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);

            // 1. Find the existing structure: <SceneName>/{Objects, Images, Triggers, ...}
            //    Nothing is created yet; groups are only made once something needs them.
            Transform sceneRoot = FindSceneRoot(scene);
            var groupNames = SortGroups.ToDictionary(g => g.Tag, g => g.Group);

            // Structural transforms that already exist must never be sorted themselves.
            var structural = new HashSet<Transform>();
            if (sceneRoot != null)
            {
                structural.Add(sceneRoot);
                foreach (var (_, groupName) in SortGroups)
                {
                    Transform group = FindChild(sceneRoot, groupName);
                    if (group != null)
                        structural.Add(group);
                }

                Transform images = FindChild(sceneRoot, groupNames[ImageTag]);
                if (images != null)
                {
                    foreach (Transform layer in images)
                        if (ImageLayers.Any(l => l.Group == layer.name))
                            structural.Add(layer);
                }
            }

            Transform GetGroup(string tag)
            {
                if (sceneRoot == null)
                    sceneRoot = CreateSceneRoot(scene);
                return GetOrCreateChild(sceneRoot, groupNames[tag]);
            }

            // 2. Snapshot every transform in the scene before anything moves.
            var all = new List<Transform>();
            foreach (GameObject rootGo in scene.GetRootGameObjects())
                all.AddRange(rootGo.GetComponentsInChildren<Transform>(true));

            all.RemoveAll(t => t == null
                               || structural.Contains(t)
                               || (t.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0);

            // 3. Cameras -> scene root (also pulls them out of Objects).
            foreach (Transform t in all)
            {
                if (t.gameObject.tag == CameraTag)
                    HandleCamera(t);
            }

            // 4. Spawns -> scene root, unless they live inside an Object.
            foreach (Transform t in all)
            {
                GameObject go = t.gameObject;
                if (go.tag != SpawnTag || !SpawnNames.Contains(go.name))
                    continue;
                if (HasObjectAncestor(t))
                    continue;

                MoveTo(t, null);
            }

            // 5. Everything else that has a sortable tag.
            var objectRoots = new List<Transform>();

            foreach (Transform t in all)
            {
                string tag = t.gameObject.tag;
                if (!groupNames.ContainsKey(tag))
                    continue;

                // Descendants of an Object are never pulled out (except cameras, handled above).
                if (HasObjectAncestor(t))
                    continue;

                Transform group = GetGroup(tag);

                if (tag == ObjectTag)
                {
                    MoveTo(t, group);
                    objectRoots.Add(t);
                }
                else if (tag == ImageTag)
                {
                    string layerGroup = GetImageLayerGroup(t.gameObject);
                    Transform target = layerGroup != null
                        ? GetOrCreateChild(group, layerGroup)
                        : group;

                    MoveTo(t, target);
                }
                else
                {
                    MoveTo(t, group);
                }
            }

            // 6. Put the groups and image layer sub-groups in their defined order.
            if (sceneRoot != null)
            {
                ReorderNamedChildren(sceneRoot, SortGroups.Select(g => g.Group));

                Transform images = FindChild(sceneRoot, groupNames[ImageTag]);
                if (images != null)
                    ReorderNamedChildren(images, ImageLayers.Select(l => l.Group));
            }

            // 7. Sort the inside of every Object by tag priority, then name.
            foreach (Transform obj in objectRoots)
                SortChildrenRecursive(obj);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[HierarchySort] Sorted scene '{scene.name}'.");
        }

        // ---------------------------------------------------------------------
        // Rules
        // ---------------------------------------------------------------------

        private static void HandleCamera(Transform t)
        {
            GameObject go = t.gameObject;

            var selection = go.GetComponent<SelectionComponent>();
            if (selection != null
                && selection.Variant == SelectionComponent.SelectionVariant.HunterMode
                && go.name != HunterCameraName)
            {
                Undo.RecordObject(go, UndoName);
                go.name = HunterCameraName;
            }

            MoveTo(t, null);
        }

        // Returns the image sub-group name for an Image, or null if its layer is not one of the known values.
        // The layer is read from the GameObject's Unity layer: a named layer ("Backdrop"),
        // a plain number ("0.5"), or Default.
        private static string GetImageLayerGroup(GameObject go)
        {
            string layer = LayerMask.LayerToName(go.layer)?.Trim();

            if (string.IsNullOrEmpty(layer) || layer == DefaultLayerName)
                return DefaultLayerGroup;

            foreach (var (layerName, _, groupName) in ImageLayers)
            {
                if (string.Equals(layer, layerName, StringComparison.OrdinalIgnoreCase))
                    return groupName;
            }

            if (double.TryParse(layer, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                foreach (var (_, layerValue, groupName) in ImageLayers)
                {
                    if (Math.Abs(value - layerValue) < 1e-9)
                        return groupName;
                }
            }

            return null;
        }

        private static void SortChildrenRecursive(Transform parent)
        {
            int count = parent.childCount;
            if (count == 0)
                return;

            var children = new List<Transform>(count);
            for (int i = 0; i < count; i++)
                children.Add(parent.GetChild(i));

            var sorted = children
                .OrderBy(c => GetTagRank(c.gameObject.tag))
                .ThenBy(c => c.name, NaturalComparer)
                .ToList();

            if (children.All(CanRestructure))
            {
                for (int i = 0; i < sorted.Count; i++)
                {
                    if (sorted[i].GetSiblingIndex() != i)
                        Undo.SetSiblingIndex(sorted[i], i, UndoName);
                }
            }
            else
            {
                Debug.LogWarning($"[HierarchySort] Skipped reordering children of '{parent.name}' (part of a prefab instance).", parent);
            }

            foreach (Transform child in children)
                SortChildrenRecursive(child);
        }

        // Tag priority inside an Object. Unlisted tags (Spawn, Untagged, ...) go last.
        private static int GetTagRank(string tag)
        {
            for (int i = 0; i < SortGroups.Length; i++)
            {
                if (SortGroups[i].Tag == tag)
                    return i;
            }
            return int.MaxValue;
        }

        private static bool HasObjectAncestor(Transform t)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                if (p.gameObject.tag == ObjectTag)
                    return true;
            }
            return false;
        }

        // ---------------------------------------------------------------------
        // Hierarchy helpers
        // ---------------------------------------------------------------------

        private static string GetSceneRootName(Scene scene)
        {
            return string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name;
        }

        private static Transform FindSceneRoot(Scene scene)
        {
            string rootName = GetSceneRootName(scene);
            GameObject existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == rootName);
            return existing != null ? existing.transform : null;
        }

        private static Transform CreateSceneRoot(Scene scene)
        {
            var go = new GameObject(GetSceneRootName(scene));
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            return go.transform;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                    return child;
            }
            return null;
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform existing = FindChild(parent, name);
            if (existing != null)
                return existing;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            return go.transform;
        }

        // Moves the named children (if present) to the top of the parent, in the given order.
        private static void ReorderNamedChildren(Transform parent, IEnumerable<string> names)
        {
            int index = 0;
            foreach (string name in names)
            {
                Transform child = null;
                for (int i = 0; i < parent.childCount; i++)
                {
                    if (parent.GetChild(i).name == name)
                    {
                        child = parent.GetChild(i);
                        break;
                    }
                }

                if (child == null)
                    continue;

                if (child.GetSiblingIndex() != index)
                    Undo.SetSiblingIndex(child, index, UndoName);

                index++;
            }
        }

        // Re-parents a transform with undo support. A null parent means the scene root.
        private static void MoveTo(Transform t, Transform newParent)
        {
            if (t.parent == newParent)
                return;

            if (!CanRestructure(t))
            {
                Debug.LogWarning($"[HierarchySort] Cannot move '{t.name}': it is inside a prefab instance.", t);
                return;
            }

            Undo.SetTransformParent(t, newParent, true, UndoName);
        }

        // Children of a prefab instance can't be moved or reordered without unpacking.
        private static bool CanRestructure(Transform t)
        {
            GameObject go = t.gameObject;
            return !PrefabUtility.IsPartOfPrefabInstance(go)
                   || PrefabUtility.IsOutermostPrefabInstanceRoot(go);
        }
    }
}