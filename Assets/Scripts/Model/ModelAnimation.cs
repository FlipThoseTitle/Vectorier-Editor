using UnityEditor;
using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

namespace Vectorier.Model
{
    [Serializable]
    public sealed class ModelAnimation
    {
        public const float SourceFps = 20f;
        public const float ZOffset = -300f;

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
                State.PreviewPose = Model.PreviewPose;
            }

            if (State.PreviewPose == null || State.PreviewPose.Length == 0)
                State.PreviewPose = Model.PreviewPose;
        }

        public void LoadModel(string xmlPath)
        {
            Model = ModelData.LoadOrThrow(xmlPath);
            State.AllocateNodeBuffers(Model.NodeCount);
            State.PreviewPose = Model.PreviewPose;
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
            if (!State.IsOffsetInitialized || State.AnimationFrames.Count == 0 || !IsPlaying)
                return false;

            double now = EditorApplication.timeSinceStartup;
            double frameInterval = 1.0 / SourceFps;

            if (now - State.LastPlaybackTime < frameInterval)
                return false;

            State.LastPlaybackTime = now;

            State.CurrentFrameIndex++;

            if (State.CurrentFrameIndex > EndFrame)
                State.CurrentFrameIndex = StartFrame;

            ApplyFrame(State.CurrentFrameIndex);

            return true;
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

        public static string ResolveBinFullPathOrThrow( bool isCustomMode, string customBinPath, string binFolderPath, string binFileName)
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
                    throw new Exception(
                        $"Animation node count mismatch. Model expects {expectedNodeCount}, but frame {frameIndex} contains {nodeCount} nodes.");
                }

                var frame = new Vector3[nodeCount];

                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();
                    frame[nodeIndex] = new Vector3(x, y, -z);
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