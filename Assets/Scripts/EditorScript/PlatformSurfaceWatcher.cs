using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vectorier.Component;

namespace Vectorier.EditorScript.Tools
{
    // Keeps platform textures in sync while you scale/edit platforms in the editor.
    // Anything that goes through the undo system (scene view handles, inspector edits) shows up in
    // postprocessModifications, so that's what drives the real-time rebuild. Undo/redo itself doesn't,
    // so that gets a separate sweep.
    [InitializeOnLoad]
    public static class PlatformSurfaceWatcher
    {
        private static readonly HashSet<PlatformComponent> pending = new();
        private static bool flushScheduled;

        static PlatformSurfaceWatcher()
        {
            Undo.postprocessModifications += OnModifications;
            Undo.undoRedoPerformed += OnUndoRedo;

            PlatformComponent.EditorRebuildRequested += Request;
            PlatformComponent.EditorComponentRemoved += OnComponentRemoved;
        }

        // Queues a rebuild for next editor tick. Multiple requests for the same platform collapse into one.
        public static void Request(PlatformComponent platform)
        {
            if (!platform || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            pending.Add(platform);

            if (flushScheduled)
                return;

            // Don't rebuild inside the callback itself, Unity isn't done applying the change yet
            flushScheduled = true;
            EditorApplication.delayCall += Flush;
        }

        private static void Flush()
        {
            flushScheduled = false;

            List<PlatformComponent> batch = new(pending);
            pending.Clear();

            foreach (PlatformComponent platform in batch)
            {
                // hash check makes moves / no-op edits basically free
                if (platform && PlatformSurfaceBuilder.NeedsRebuild(platform))
                    PlatformSurfaceBuilder.Build(platform, recordUndo: false);
            }
        }

        private static UndoPropertyModification[] OnModifications(UndoPropertyModification[] modifications)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return modifications;

            // one scale drag = a modification per axis, no point checking the same object 3 times
            HashSet<Object> seen = new();

            foreach (UndoPropertyModification modification in modifications)
            {
                Object target = modification.currentValue?.target;
                if (!target || !seen.Add(target))
                    continue;

                switch (target)
                {
                    // Transform: could be the platform, or a parent that's scaling a bunch of platforms at once
                    case Transform transform:
                        foreach (PlatformComponent platform in transform.GetComponentsInChildren<PlatformComponent>(true))
                            Request(platform);
                        break;

                    // SpriteRenderer, the PlatformComponent itself, etc.
                    case UnityEngine.Component component:
                        if (component.TryGetComponent(out PlatformComponent owner))
                            Request(owner);
                        break;
                }
            }

            return modifications;
        }

        private static void OnUndoRedo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Just queue everything, Flush skips the ones that are already up to date
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                    QueueAll(root);
            }

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.prefabContentsRoot)
                QueueAll(prefabStage.prefabContentsRoot);
        }

        private static void QueueAll(GameObject root)
        {
            foreach (PlatformComponent platform in root.GetComponentsInChildren<PlatformComponent>(true))
                Request(platform);
        }

        private static void OnComponentRemoved(GameObject gameObject)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Wait a tick then check what actually happened
            EditorApplication.delayCall += () =>
            {
                // whole object is gone (deleted, scene closed...), nothing to clean
                if (!gameObject || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                // component came back (undo/redo shuffle), leave it be
                if (gameObject.TryGetComponent<PlatformComponent>(out _))
                    return;

                // component was removed for real, don't leave orphaned textures behind
                PlatformSurfaceBuilder.ClearGenerated(gameObject.transform);
            };
        }
    }
}