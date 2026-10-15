using UnityEditor;
using UnityEngine;
using Vectorier.Component;

namespace Vectorier.EditorScript.Tools
{
    // For platforms that already exist (dragged in from the Project window, old scenes, etc.).
    // Adds a PlatformComponent to each selected platform and builds it. The generation itself lives in PlatformSurfaceBuilder.
    public static class QuickSurface
    {
        private const string MENU_ROOT = "Vectorier/Tools/Quick Actions/Build Surface/";

        // ================= MENU =================

        [MenuItem(MENU_ROOT + "Rough", false, 52)]
        private static void BuildRough() => Apply(PlatformComponent.SurfaceType.Rough);

        [MenuItem(MENU_ROOT + "Rough", true)]
        private static bool BuildRough_Validate() => HasValidSelection();

        [MenuItem(MENU_ROOT + "Smooth", false, 53)]
        private static void BuildSmooth() => Apply(PlatformComponent.SurfaceType.Smooth);

        [MenuItem(MENU_ROOT + "Smooth", true)]
        private static bool BuildSmooth_Validate() => HasValidSelection();

        [MenuItem(MENU_ROOT + "Block", false, 54)]
        private static void BuildBlock() => Apply(PlatformComponent.SurfaceType.Block);

        [MenuItem(MENU_ROOT + "Block", true)]
        private static bool BuildBlock_Validate() => HasValidSelection();

        // ================= CORE =================

        private static void Apply(PlatformComponent.SurfaceType type)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Build {type} Surface");

            int built = 0;

            // Each platform gets its own component + bounds, no merging them like the old version did
            foreach (GameObject gameObject in Selection.gameObjects)
            {
                if (PlatformSurfaceBuilder.Setup(gameObject, type, recordUndo: true))
                    built++;
            }

            if (built == 0)
                Debug.LogWarning($"[QuickSurface] Nothing built. Select objects tagged \"{PlatformSurfaceBuilder.PLATFORM_TAG}\" that have a SpriteRenderer.");

            Undo.CollapseUndoOperations(group);
        }

        private static bool HasValidSelection()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return false;

            foreach (GameObject gameObject in Selection.gameObjects)
            {
                if (PlatformSurfaceBuilder.IsValidPlatform(gameObject))
                    return true;
            }

            return false;
        }
    }
}