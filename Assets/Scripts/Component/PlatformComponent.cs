using UnityEngine;

namespace Vectorier.Component
{
    // ExecuteAlways is only here so OnDestroy fires in edit mode (used to clean up generated textures).
    // There's no Update, so it costs nothing.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Vectorier/Component/Platform Component")]
    public class PlatformComponent : MonoBehaviour
    {
        public enum SurfaceType
        {
            Rough = 0,
            Smooth = 1,
            Block = 2
        }

        [Tooltip("Surface style.\nRough: random floor/wall tiles (old QuickSurface look).\nSmooth: fixed top corners, v_black covers the top.\nBlock: just v_black, nothing else.\nDefault: Rough")]
        public SurfaceType Type = SurfaceType.Rough;

        [Tooltip("Generate floor tiles along the top edge.\nNot used by Smooth.\nDefault: true")]
        public bool Floor = true;

        [Tooltip("Generate the left wall.\nDefault: true")]
        public bool LeftWall = true;

        [Tooltip("Generate the right wall.\nDefault: true")]
        public bool RightWall = true;

        [Tooltip("Seed for picking texture variants.\nKeeps the layout stable while resizing instead of reshuffling every frame.")]
        public int Seed;

        // Hash of whatever the last build was based on (scale, size, settings).
        // Serialized so reopening a scene doesn't think every platform is out of date
        [SerializeField, HideInInspector] private int builtHash;

        public int BuiltHash
        {
            get => builtHash;
            set => builtHash = value;
        }

        // Runtime assembly can't see editor code, so the editor side hooks in through these
        public static System.Action<PlatformComponent> EditorRebuildRequested;
        public static System.Action<GameObject> EditorComponentRemoved;

        private void Reset()
        {
            // Called when the component gets added (or someone hits Reset in the inspector)
            Seed = Random.Range(1, int.MaxValue);
            EditorRebuildRequested?.Invoke(this);
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
                return;

            // Could be a removed component, could be the scene closing, the editor side figures out which
            EditorComponentRemoved?.Invoke(gameObject);
        }
    }
}