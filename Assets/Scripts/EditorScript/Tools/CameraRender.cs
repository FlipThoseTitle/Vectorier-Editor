using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;

namespace Vectorier.EditorScript.Tools
{
    public static class CameraRender
    {
        [MenuItem("Vectorier/Tools/Render Camera as PNG/1920x1080", false, 56)]
        public static void Render1080p() => ProcessCameraExport(1920, 1080);

        [MenuItem("Vectorier/Tools/Render Camera as PNG/512x340", false, 57)]
        public static void RenderSmall() => ProcessCameraExport(512, 340);

        private static void ProcessCameraExport(int width, int height)
        {
            // Find all GameObjects tagged "Camera"
            var cameraObjects = GameObject.FindGameObjectsWithTag("Camera");

            // Filter out objects that don't have a Camera component
            var validCameras = cameraObjects
                .Select(go => go.GetComponent<Camera>())
                .Where(cam => cam != null)
                .ToList();

            // No valid camera found
            if (validCameras.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Camera Not Found",
                    "No GameObject tagged 'Camera' with a Camera component was found in the scene.",
                    "OK"
                );
                return;
            }

            // More than one valid camera found
            if (validCameras.Count > 1)
            {
                EditorUtility.DisplayDialog(
                    "Multiple Cameras Found",
                    $"Found {validCameras.Count} GameObjects tagged 'Camera' with Camera components.\n\n" +
                    "Please ensure only one GameObject is tagged 'Camera' before rendering.",
                    "OK"
                );
                return;
            }

            // Exactly one valid camera
            Camera targetCamera = validCameras[0];

            // Prompt user for save location
            string defaultName = $"CameraRender_{width}x{height}.png";
            string outPath = EditorUtility.SaveFilePanel(
                "Save camera render",
                "",
                defaultName,
                "png"
            );

            if (string.IsNullOrEmpty(outPath))
                return;

            // Render and export
            ExportSingle(targetCamera, width, height, outPath);
        }

        private static void ExportSingle(Camera cam, int width, int height, string outPath)
        {
            RenderTexture renderTexture = null;
            Texture2D outputTexture = null;

            // Cache previous camera states so we can restore them later
            RenderTexture previousTarget = cam.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            float previousAspect = cam.aspect;

            try
            {
                EditorUtility.DisplayProgressBar("Exporting Camera", "Rendering image...", 0.5f);

                // Force camera aspect ratio to match our output resolution temporarily
                cam.aspect = (float)width / height;

                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Point,
                    antiAliasing = 1
                };

                if (!renderTexture.Create())
                {
                    Debug.LogError($"RenderTexture.Create failed for {width}x{height}");
                    return;
                }

                // Render camera to texture
                cam.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                cam.Render();

                // Read pixels into Texture2D
                outputTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point
                };
                outputTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                outputTexture.Apply();

                // Encode and write to disk
                File.WriteAllBytes(outPath, outputTexture.EncodeToPNG());
                Debug.Log($"Saved Camera Render: {outPath} ({width}x{height})");
            }
            finally
            {
                // Restore camera and system states
                cam.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                cam.aspect = previousAspect;

                // Cleanup memory safely
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }

                if (outputTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(outputTexture);
                }

                EditorUtility.ClearProgressBar();
            }
        }
    }
}