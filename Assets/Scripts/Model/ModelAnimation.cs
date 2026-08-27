using UnityEditor;
using UnityEngine;
using System;
using System.Globalization;
using System.IO;
using System.Collections.Generic;

namespace Vectorier.Model
{
    [Serializable]
    public sealed class ModelAnimation
    {
        public const float SourceFps = 20f;
        public const float ZOffset = -300f;
        public const float TargetFps = 60f; // The interpolated playback frame rate
        public float ContinuousFrame { get; set; } // Tracks the floating-point time between frames

        public static readonly Vector3 AnimZPushVector = new(0f, 0f, ZOffset);

        public sealed class PlaybackState
        {
            public readonly List<Vector3[]> AnimationFrames = new();
            public readonly List<Vector3> CenterOfMassPath = new();

            public Vector3[] AnimationNodes;
            public Vector3[] PreviewNodes;
            public Vector3[] PreviewPose;

            public int CurrentFrameIndex;
            public int CenterOfMassNodeIndex = -1;

            public Vector3 StartOffset;
            public bool IsOffsetInitialized;
            public bool IsPreviewActive;

            public double LastPlaybackTime;

            public void Reset()
            {
                AnimationFrames.Clear();
                CenterOfMassPath.Clear();

                AnimationNodes = null;
                PreviewNodes = null;
                PreviewPose = null;

                CurrentFrameIndex = 0;
                CenterOfMassNodeIndex = -1;

                StartOffset = default;
                IsOffsetInitialized = false;
                IsPreviewActive = false;

                LastPlaybackTime = 0d;
            }

            public void AllocateNodeBuffers(int nodeCount)
            {
                if (nodeCount <= 0)
                    return;

                AnimationNodes = new Vector3[nodeCount];
                PreviewNodes = new Vector3[nodeCount];
            }
        }

        public PlaybackState State { get; } = new();
        public ModelData Model { get; private set; }

        public bool IsPlaying { get; set; } = true;
        bool stayInPlace;
        public bool Reverse { get; set; }

        public int StartFrame { get; set; } = 0;
        public int EndFrame { get; set; } = int.MaxValue;
        int stayInPlacePivotNodeIndex = -1;

        public int CurrentFrameIndex
        {
            get => State.CurrentFrameIndex;
            set => State.CurrentFrameIndex = value;
        }

        public IReadOnlyList<Vector3[]> AnimationFrames => State.AnimationFrames;
        public IReadOnlyList<Vector3> CenterOfMassPath => State.CenterOfMassPath;
        public Vector3[] AnimationNodes => State.AnimationNodes;
        public Vector3[] PreviewNodes => State.PreviewNodes;
        public bool IsPreviewActive => State.IsPreviewActive;
        public bool IsOffsetInitialized => State.IsOffsetInitialized;
        Transform animationSpaceTransform;

        public void ResetAll()
        {
            Model = null;
            State.Reset();
            IsPlaying = true;

            stayInPlace = false;
            stayInPlacePivotNodeIndex = -1;
            animationSpaceTransform = null;
        }

        public void ResetRuntimeStateOnly()
        {
            State.Reset();

            stayInPlace = false;
            stayInPlacePivotNodeIndex = -1;
            animationSpaceTransform = null;
        }

        public void EnsureModelLoadedForPreview(string xmlPath)
        {
            if (string.IsNullOrWhiteSpace(xmlPath) || !File.Exists(xmlPath))
                return;

            if (Model == null || !string.Equals(Model.SourcePath, xmlPath, StringComparison.OrdinalIgnoreCase))
            {
                Model = ModelData.LoadOrThrow(xmlPath);
                State.AllocateNodeBuffers(Model.NodeCount);
                State.PreviewPose = null;
            }

            // Initialize or refresh the preview pose mirroring the X-axis if ReverseX is true
            if (State.PreviewPose == null || State.PreviewPose.Length != Model.PreviewPose.Length)
            {
                State.PreviewPose = new Vector3[Model.PreviewPose.Length];
                for (int i = 0; i < Model.PreviewPose.Length; i++)
                {
                    Vector3 p = Model.PreviewPose[i];
                    State.PreviewPose[i] = new Vector3(Reverse ? -p.x : p.x, p.y, p.z);
                }
            }
            else
            {
                for (int i = 0; i < Model.PreviewPose.Length; i++)
                {
                    float originalX = Model.PreviewPose[i].x;
                    State.PreviewPose[i].x = Reverse ? -originalX : originalX;
                }
            }
        }

        public void LoadModel(string xmlPath)
        {
            Model = ModelData.LoadOrThrow(xmlPath);
            State.AllocateNodeBuffers(Model.NodeCount);

            State.PreviewPose = new Vector3[Model.PreviewPose.Length];
            for (int i = 0; i < Model.PreviewPose.Length; i++)
            {
                Vector3 p = Model.PreviewPose[i];
                State.PreviewPose[i] = new Vector3(Reverse ? -p.x : p.x, p.y, p.z);
            }
        }

        public void UpdatePreview(Vector3 cursorWorldPosition, Transform parentTransform, string pivotNodeName)
        {
            if (Model == null || State.PreviewPose == null || State.PreviewNodes == null)
                return;

            int pivotIndex = ResolvePivotNodeIndex(pivotNodeName);

            Vector3 pivotLocal = State.PreviewPose[pivotIndex] + AnimZPushVector;
            Vector3 targetLocal = parentTransform != null
                ? parentTransform.InverseTransformPoint(cursorWorldPosition)
                : cursorWorldPosition;

            Vector3 offset = targetLocal - pivotLocal;

            for (int i = 0; i < State.PreviewPose.Length; i++)
                State.PreviewNodes[i] = State.PreviewPose[i] + offset + AnimZPushVector;

            State.IsPreviewActive = true;
        }

        public void ClearPreview()
        {
            State.IsPreviewActive = false;
        }

        public void PlaceAt(Vector3 worldPosition, Transform parentTransform, string xmlPath, string binFullPath, string pivotNodeName, bool stayInPlace)
        {
            ResetRuntimeStateOnly();
            LoadModel(xmlPath);

            animationSpaceTransform = parentTransform;

            LoadBinaryAnimation(binFullPath, Model.NodeCount);

            int maxFrame = State.AnimationFrames.Count - 1;

            StartFrame = Mathf.Clamp(StartFrame, 0, maxFrame);
            EndFrame = maxFrame;

            if (EndFrame < StartFrame)
                EndFrame = StartFrame;

            State.CenterOfMassNodeIndex = Model.TryGetNodeIndex("COM", out int comIdx) ? comIdx : -1;

            int pivotIndex = ResolvePivotNodeIndex(pivotNodeName);
            SetStayInPlace(stayInPlace, pivotNodeName);

            Vector3 pivotFrameStartLocal = State.AnimationFrames[StartFrame][pivotIndex] + AnimZPushVector;
            Vector3 targetLocal = parentTransform != null
                ? parentTransform.InverseTransformPoint(worldPosition)
                : worldPosition;

            State.StartOffset = targetLocal - pivotFrameStartLocal;
            State.IsOffsetInitialized = true;

            PrecomputeCenterOfMassPath();

            State.CurrentFrameIndex = StartFrame;
            IsPlaying = true;
            ApplyFrame(StartFrame);

            State.LastPlaybackTime = EditorApplication.timeSinceStartup;
        }

        public void ApplyFrame(int frameIndex)
        {
            if (State.AnimationFrames.Count == 0 || State.AnimationNodes == null)
                return;

            frameIndex = Mathf.Clamp(frameIndex, StartFrame, EndFrame);

            // Sync continuous frame when scrubbing
            ContinuousFrame = frameIndex;

            Vector3[] frame = State.AnimationFrames[frameIndex];
            Vector3 frameOffset = State.StartOffset;

            if (stayInPlace &&
                stayInPlacePivotNodeIndex >= 0 &&
                stayInPlacePivotNodeIndex < frame.Length)
            {
                Vector3 startPivot = State.AnimationFrames[StartFrame][stayInPlacePivotNodeIndex];
                Vector3 currentPivot = frame[stayInPlacePivotNodeIndex];

                frameOffset += startPivot - currentPivot;
            }

            for (int i = 0; i < frame.Length; i++)
                State.AnimationNodes[i] = frame[i] + frameOffset + AnimZPushVector;

            State.CurrentFrameIndex = frameIndex;
        }

        public bool TryAdvancePlayback()
        {
            if (!State.IsOffsetInitialized || State.AnimationFrames.Count == 0)
                return false;

            double now = EditorApplication.timeSinceStartup;

            if (!IsPlaying)
            {
                State.LastPlaybackTime = now;
                return false;
            }

            double updateInterval = 1.0 / TargetFps;

            // Calculate time passed since last playback update
            double dt = now - State.LastPlaybackTime;

            if (dt < updateInterval)
                return false;

            State.LastPlaybackTime = now;

            // Advance the continuous frame based on the original 20 FPS speed
            ContinuousFrame += (float)(dt * SourceFps);

            if (ContinuousFrame > EndFrame)
            {
                // Seamlessly wrap around to start
                ContinuousFrame = StartFrame + (ContinuousFrame - EndFrame);
            }

            // Keep the main CurrentFrameIndex in sync for the scrubber
            State.CurrentFrameIndex = Mathf.FloorToInt(ContinuousFrame);

            // Apply the vertex interpolation
            ApplyInterpolatedFrame(ContinuousFrame);

            return true;
        }

        public void ApplyInterpolatedFrame(float frameTime)
        {
            if (State.AnimationFrames.Count == 0 || State.AnimationNodes == null)
                return;

            frameTime = Mathf.Clamp(frameTime, StartFrame, EndFrame);

            // Identify the two frames we are blending between
            int frameA = Mathf.FloorToInt(frameTime);
            int frameB = Mathf.Min(frameA + 1, EndFrame);
            float t = frameTime - frameA; // Yields a 0.0 to 1.0 value for the Lerp

            Vector3[] nodesA = State.AnimationFrames[frameA];
            Vector3[] nodesB = State.AnimationFrames[frameB];
            Vector3 frameOffset = State.StartOffset;

            if (stayInPlace &&
                stayInPlacePivotNodeIndex >= 0 &&
                stayInPlacePivotNodeIndex < nodesA.Length)
            {
                Vector3 startPivot = State.AnimationFrames[StartFrame][stayInPlacePivotNodeIndex];

                // Interpolate the pivot point to prevent snapping when staying in place
                Vector3 currentPivotA = nodesA[stayInPlacePivotNodeIndex];
                Vector3 currentPivotB = nodesB[stayInPlacePivotNodeIndex];
                Vector3 currentPivot = Vector3.Lerp(currentPivotA, currentPivotB, t);

                frameOffset += startPivot - currentPivot;
            }

            for (int i = 0; i < nodesA.Length; i++)
            {
                // Linearly interpolate each individual vertex position
                Vector3 lerpedNode = Vector3.Lerp(nodesA[i], nodesB[i], t);
                State.AnimationNodes[i] = lerpedNode + frameOffset + AnimZPushVector;
            }
        }

        public bool TryGetNodeIndex(string nodeName, out int index)
        {
            index = -1;
            return Model != null && Model.TryGetNodeIndex(nodeName, out index);
        }

        public Vector3? GetAnimationNodeWorldPosition(string nodeName)
        {
            if (State.AnimationNodes == null || Model == null)
                return null;

            if (!Model.TryGetNodeIndex(nodeName, out int idx))
                return null;

            if (idx < 0 || idx >= State.AnimationNodes.Length)
                return null;

            Vector3 nodePosition = State.AnimationNodes[idx];
            return animationSpaceTransform != null
                ? animationSpaceTransform.TransformPoint(nodePosition)
                : nodePosition;
        }

        public void SetAnimationSpaceTransform(Transform parentTransform)
        {
            animationSpaceTransform = parentTransform;
        }

        public int ResolvePivotNodeIndex(string requestedPivot)
        {
            if (Model == null)
                throw new InvalidOperationException("Model must be loaded before resolving pivot node.");

            if (!string.IsNullOrWhiteSpace(requestedPivot) && Model.TryGetNodeIndex(requestedPivot, out int idx))
                return idx;

            if (Model.TryGetNodeIndex("NPivot", out int pivotIdx))
                return pivotIdx;

            throw new Exception("Pivot node not found. Requested pivot missing, and 'NPivot' not found in XML <Nodes>.");
        }

        public static string ResolveFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (Path.IsPathRooted(path))
                return path;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        public static string ResolveBinFullPathOrThrow(bool isCustomMode, string customBinPath, string binFolderPath, string binFileName)
        {
            string fullPath;

            if (isCustomMode)
            {
                fullPath = customBinPath;
                if (string.IsNullOrWhiteSpace(fullPath))
                    throw new Exception("Custom Bin Path is empty.");
            }
            else
            {
                fullPath = Path.Combine(binFolderPath ?? string.Empty, binFileName ?? string.Empty);
            }

            fullPath = ResolveFullPath(fullPath);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Bin file not found: {fullPath}");

            return fullPath;
        }

        void LoadBinaryAnimation(string fullPath, int expectedNodeCount)
        {
            State.AnimationFrames.Clear();

            using var reader = new BinaryReader(File.OpenRead(fullPath));

            int frameCount = reader.ReadInt32();

            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                reader.ReadByte();

                int nodeCount = reader.ReadInt32();

                if (expectedNodeCount > 0 && nodeCount != expectedNodeCount)
                {
                    throw new Exception(string.Format(CultureInfo.InvariantCulture, "Animation node count mismatch. Model expects {0}, but frame {1} contains {2} nodes.", expectedNodeCount, frameIndex, nodeCount));
                }

                var frame = new Vector3[nodeCount];

                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();

                    // Negate X coordinate if reverse mode is enabled
                    frame[nodeIndex] = new Vector3(Reverse ? -x : x, y, -z);
                }

                State.AnimationFrames.Add(frame);
            }

            if (State.AnimationFrames.Count == 0)
                throw new Exception("Animation file contains zero frames.");
        }

        void PrecomputeCenterOfMassPath()
        {
            State.CenterOfMassPath.Clear();

            if (State.CenterOfMassNodeIndex < 0)
                return;

            foreach (var frame in State.AnimationFrames)
                State.CenterOfMassPath.Add(frame[State.CenterOfMassNodeIndex] + State.StartOffset + AnimZPushVector);
        }

        public void SetStayInPlace(bool enabled, string pivotNodeName)
        {
            stayInPlace = enabled;
            stayInPlacePivotNodeIndex = enabled ? ResolvePivotNodeIndex(pivotNodeName) : -1;
        }
    }
}