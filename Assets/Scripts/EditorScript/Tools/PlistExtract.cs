using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace Vectorier.EditorScript.Tools
{
    public static class PlistExtract
    {
        private const string MenuPath = "Vectorier/Tools/Extract Animated Sprite";

        [MenuItem(MenuPath, false, 30)]
        public static void ExtractAnimatedSprite()
        {
            // Select .plist
            string plistPath = EditorUtility.OpenFilePanel("Select .plist", Application.dataPath, "plist");
            if (string.IsNullOrEmpty(plistPath))
                return;

            // Select atlas .png
            string pngPath = EditorUtility.OpenFilePanel("Select Atlas PNG", Path.GetDirectoryName(plistPath) ?? Application.dataPath, "png");
            if (string.IsNullOrEmpty(pngPath))
                return;

            // Select output folder
            string outputFolder = EditorUtility.OpenFolderPanel("Select Output Folder", Path.GetDirectoryName(plistPath) ?? Application.dataPath, "");
            if (string.IsNullOrEmpty(outputFolder))
                return;

            try
            {
                var frames = ParseTexturePackerPlist(plistPath);
                if (frames.Count == 0)
                {
                    EditorUtility.DisplayDialog("PlistExtract", "No frames found in the selected .plist.", "OK");
                    return;
                }

                var atlas = LoadPngAsTexture(pngPath);
                if (atlas == null)
                {
                    EditorUtility.DisplayDialog("PlistExtract", "Failed to load the selected PNG.", "OK");
                    return;
                }

                int exported = 0;
                for (int i = 0; i < frames.Count; i++)
                {
                    var f = frames[i];

                    EditorUtility.DisplayProgressBar("Extracting Frames", $"Exporting {f.Name} ({i + 1}/{frames.Count})", (float)(i + 1) / frames.Count);

                    var extracted = ExtractFrame(atlas, f);
                    if (extracted == null)
                        continue;

                    string safeName = SanitizeFileName(f.Name);
                    string outPath = Path.Combine(outputFolder, safeName);

                    if (!outPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                        outPath += ".png";

                    File.WriteAllBytes(outPath, extracted.EncodeToPNG());
                    exported++;
                }

                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("PlistExtract", $"Done.\nExported {exported} frame(s) to:\n{outputFolder}", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("PlistExtract", $"Failed:\n{ex.Message}", "OK");
            }
        }

        public static string ExtractMiddleFrame(string pngPath, string plistPath, string outputFolder, string originalFileName)
        {
            try
            {
                var frames = ParseTexturePackerPlist(plistPath);
                if (frames.Count == 0) return null;

                var atlas = LoadPngAsTexture(pngPath);
                if (atlas == null) return null;

                // Getting the exact middle frame (e.g., 25 / 2 = 12)
                int middleIndex = frames.Count / 2;
                var middleFrame = frames[middleIndex];

                var extracted = ExtractFrame(atlas, middleFrame);
                if (extracted == null) return null;

                if (!Directory.Exists(outputFolder))
                    Directory.CreateDirectory(outputFolder);

                string outPath = Path.Combine(outputFolder, originalFileName).Replace("\\", "/");
                File.WriteAllBytes(outPath, extracted.EncodeToPNG());
                
                return outPath;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return null;
            }
        }

        // ================= PARSING ================= //

        private sealed class FrameInfo
        {
            public string Name;
            public RectInt FrameRect;
            public bool Rotated;
            public RectInt SourceColorRect;
            public Vector2Int SourceSize;
        }

        private static List<FrameInfo> ParseTexturePackerPlist(string plistPath)
        {
            var doc = XDocument.Load(plistPath);
            var plist = doc.Root;
            if (plist == null) return new List<FrameInfo>();

            var topDict = plist.Elements().FirstOrDefault(e => e.Name.LocalName == "dict");
            if (topDict == null) return new List<FrameInfo>();

            var framesDict = GetDictForKey(topDict, "frames");
            if (framesDict == null) return new List<FrameInfo>();

            var result = new List<FrameInfo>();

            var children = framesDict.Elements().ToList();
            for (int i = 0; i < children.Count - 1; i++)
            {
                if (children[i].Name.LocalName != "key")
                    continue;

                string frameName = children[i].Value?.Trim();
                if (string.IsNullOrEmpty(frameName))
                    continue;

                var frameEntryDict = children[i + 1];
                if (frameEntryDict == null || frameEntryDict.Name.LocalName != "dict")
                    continue;

                string frameStr = GetStringForKey(frameEntryDict, "frame");
                bool rotated = GetBoolForKey(frameEntryDict, "rotated");

                string sourceColorRectStr = GetStringForKey(frameEntryDict, "sourceColorRect");
                string sourceSizeStr = GetStringForKey(frameEntryDict, "sourceSize");

                if (!TryParseFrameRect(frameStr, out RectInt atlasRect))
                    continue;

                if (!TryParseFrameRect(sourceColorRectStr, out RectInt srcColorRect))
                    continue;

                if (!TryParsePoint(sourceSizeStr, out Vector2Int srcSize))
                    continue;

                result.Add(new FrameInfo
                {
                    Name = frameName,
                    FrameRect = atlasRect,
                    Rotated = rotated,
                    SourceColorRect = srcColorRect,
                    SourceSize = srcSize
                });
            }

            return result.OrderBy(f => NaturalKey(f.Name)).ToList();
        }

        private static XElement GetDictForKey(XElement dictElement, string keyName)
        {
            var nodes = dictElement.Elements().ToList();
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                if (nodes[i].Name.LocalName == "key" && nodes[i].Value == keyName)
                {
                    var next = nodes[i + 1];
                    if (next != null && next.Name.LocalName == "dict")
                        return next;
                }
            }
            return null;
        }

        private static string GetStringForKey(XElement dictElement, string keyName)
        {
            var nodes = dictElement.Elements().ToList();
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                if (nodes[i].Name.LocalName == "key" && nodes[i].Value == keyName)
                {
                    var next = nodes[i + 1];
                    if (next != null && next.Name.LocalName == "string")
                        return next.Value?.Trim();
                }
            }
            return null;
        }

        private static bool GetBoolForKey(XElement dictElement, string keyName)
        {
            var nodes = dictElement.Elements().ToList();
            for (int i = 0; i < nodes.Count - 1; i++)
            {
                if (nodes[i].Name.LocalName == "key" && nodes[i].Value == keyName)
                {
                    var next = nodes[i + 1];
                    if (next == null) return false;
                    if (next.Name.LocalName == "true") return true;
                    if (next.Name.LocalName == "false") return false;
                }
            }
            return false;
        }

        private static bool TryParseFrameRect(string frameStr, out RectInt rect)
        {
            rect = default;
            if (string.IsNullOrEmpty(frameStr))
                return false;

            var nums = ExtractInts(frameStr);
            if (nums.Count < 4)
                return false;

            rect = new RectInt(nums[0], nums[1], nums[2], nums[3]);
            return true;
        }

        private static bool TryParsePoint(string pointStr, out Vector2Int pt)
        {
            pt = default;
            if (string.IsNullOrEmpty(pointStr))
                return false;

            var nums = ExtractInts(pointStr);
            if (nums.Count < 2)
                return false;

            pt = new Vector2Int(nums[0], nums[1]);
            return true;
        }

        private static List<int> ExtractInts(string s)
        {
            var list = new List<int>(4);
            int sign = 1;
            int value = 0;
            bool inNumber = false;

            foreach (char c in s)
            {
                if (c == '-') sign = -1;
                else if (char.IsDigit(c))
                {
                    inNumber = true;
                    value = (value * 10) + (c - '0');
                }
                else
                {
                    if (inNumber)
                    {
                        list.Add(sign * value);
                        sign = 1;
                        value = 0;
                        inNumber = false;
                    }
                }
            }

            if (inNumber)
                list.Add(sign * value);

            return list;
        }

        private static (int prefix, string rest) NaturalKey(string name)
        {
            int n = -1;
            var digits = new string(name.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrEmpty(digits))
                int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out n);

            return (n, name);
        }

        // ================= EXTRACT ================= //

        private static Texture2D LoadPngAsTexture(string pngPath)
        {
            byte[] bytes = File.ReadAllBytes(pngPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, bytes))
                return null;

            tex.Apply(false, false);
            return tex;
        }

        private static Texture2D ExtractFrame(Texture2D atlas, FrameInfo frame)
        {
            int atlasH = atlas.height;
            int packX = frame.FrameRect.x;
            int packYTop = frame.FrameRect.y;
            int packW = frame.FrameRect.width;
            int packH = frame.FrameRect.height;
            int packYUnity = atlasH - packYTop - packH;

            if (packX < 0 || packYUnity < 0 || packX + packW > atlas.width || packYUnity + packH > atlas.height)
                return null;

            Color[] packed = atlas.GetPixels(packX, packYUnity, packW, packH);

            int trimmedW = frame.SourceColorRect.width;
            int trimmedH = frame.SourceColorRect.height;

            Color[] trimmedPixels;

            if (!frame.Rotated)
            {
                if (packW != trimmedW || packH != trimmedH) {}
                trimmedPixels = packed;
            }
            else
            {
                trimmedPixels = new Color[trimmedW * trimmedH];

                for (int srcY = 0; srcY < packH; srcY++)
                {
                    for (int srcX = 0; srcX < packW; srcX++)
                    {
                        Color c = packed[srcY * packW + srcX];

                        int dstX = srcY;
                        int dstY = (packW - 1) - srcX;

                        if (dstX >= 0 && dstX < trimmedW && dstY >= 0 && dstY < trimmedH)
                            trimmedPixels[dstY * trimmedW + dstX] = c;
                    }
                }
            }

            int canvasW = frame.SourceSize.x;
            int canvasH = frame.SourceSize.y;

            if (canvasW <= 0 || canvasH <= 0)
                return null;

            var canvas = new Texture2D(canvasW, canvasH, TextureFormat.RGBA32, false);

            var clear = new Color[canvasW * canvasH];
            canvas.SetPixels(clear);

            int placeX = frame.SourceColorRect.x;
            int placeYTop = frame.SourceColorRect.y;
            int placeYBottom = canvasH - placeYTop - trimmedH;

            if (placeX < 0 || placeYBottom < 0 || placeX + trimmedW > canvasW || placeYBottom + trimmedH > canvasH)
                return null;

            canvas.SetPixels(placeX, placeYBottom, trimmedW, trimmedH, trimmedPixels);
            canvas.Apply(false, false);
            return canvas;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }
}