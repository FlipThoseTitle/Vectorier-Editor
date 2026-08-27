using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;
using System.IO.Compression;

namespace Vectorier.EditorScript.Tools
{
    public static class SpriteRender
    {
        private const string CameraName = "~ExportCam";
        private const string DefaultFileName = "LevelRender.png";
        private const float PixelsPerUnit = 1f;

        // Tile size used when output exceeds rendertexture limits
        private static int TileSize => Mathf.Min(4096, SystemInfo.maxTextureSize);

        [MenuItem("Vectorier/Tools/Export Selected Sprites as PNG", false, 29)]
        public static void ExportSelected()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                Debug.LogWarning("Select one or more GameObjects that contain SpriteRenderers.");
                return;
            }

            if (!TryComputeSpriteBounds(selected, out var bounds))
            {
                Debug.LogWarning("No SpriteRenderers with sprites found in selection.");
                return;
            }

            int fullWidth = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x * PixelsPerUnit));
            int fullHeight = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y * PixelsPerUnit));

            string outPath = EditorUtility.SaveFilePanel("Save level render", "", DefaultFileName, "png");
            if (string.IsNullOrEmpty(outPath))
                return;

            try
            {
                if (FitsSingleRenderTexture(fullWidth, fullHeight))
                {
                    ExportSingle(bounds, fullWidth, fullHeight, outPath);
                }
                else
                {
                    ExportTiledStreamedPng(bounds, fullWidth, fullHeight, outPath);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ================= BOUNDS ================= //
        private static bool TryComputeSpriteBounds(GameObject[] roots, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            foreach (var root in roots)
            {
                var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var spriteRenderer in renderers)
                {
                    if (spriteRenderer.sprite == null) continue;

                    if (!hasBounds)
                    {
                        bounds = spriteRenderer.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(spriteRenderer.bounds);
                    }
                }
            }

            return hasBounds;
        }

        private static bool FitsSingleRenderTexture(int width, int height)
        {
            int max = SystemInfo.maxTextureSize;
            return width <= max && height <= max;
        }

        // ================= SINGLE-PASS EXPORT ================= //
        private static void ExportSingle(Bounds bounds, int width, int height, string outPath)
        {
            var cameraObject = CreateExportCamera(out var cam);
            try
            {
                FrameCameraToBounds(cam, bounds, width, height);

                using (var rtWrap = CreateRenderTexture(width, height).Wrap())
                {
                    RenderTexture renderTexture = rtWrap;

                    if (!renderTexture.Create())
                    {
                        Debug.LogError($"RenderTexture.Create failed for {width}x{height}");
                        return;
                    }

                    cam.targetTexture = renderTexture;

                    var previous = RenderTexture.active;
                    RenderTexture.active = renderTexture;

                    cam.Render();

                    var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Point
                    };
                    texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    texture.Apply();

                    RenderTexture.active = previous;

                    File.WriteAllBytes(outPath, texture.EncodeToPNG());
                    Debug.Log($"Saved: {outPath} ({width}x{height})");

                    cam.targetTexture = null;
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        // ================= Tiled export + streamed PNG ================= //
        private static void ExportTiledStreamedPng(Bounds bounds, int fullWidth, int fullHeight, string outPngPath)
        {
            int tileSize = TileSize;
            int tilesX = Mathf.CeilToInt((float)fullWidth / tileSize);
            int tilesY = Mathf.CeilToInt((float)fullHeight / tileSize);

            string tempDirectory = CreateTempTileDir();

            var cameraObject = CreateExportCamera(out var cam);

            var tiles = new TileInfo[tilesY, tilesX];

            try
            {
                RenderTilesToRawFiles(cam, bounds, fullWidth, fullHeight, tileSize, tiles, tempDirectory);

                EditorUtility.DisplayProgressBar("Export Level Render", "Stitching PNG...", 0.55f);
                PngStreamStitcher.WritePngFromRawTiles(outPngPath, fullWidth, fullHeight, tiles, tileSize);

                Debug.Log($"Saved (tiled+streamed PNG): {outPngPath} ({fullWidth}x{fullHeight})");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
                TryDeleteDirectory(tempDirectory);
            }
        }

        private static void RenderTilesToRawFiles(Camera cam, Bounds bounds, int fullWidth, int fullHeight, int tileSize, TileInfo[,] tiles, string tempDirectory)
        {
            int tilesY = tiles.GetLength(0);
            int tilesX = tiles.GetLength(1);
            int total = tilesX * tilesY;
            int done = 0;

            float worldMinX = bounds.min.x;
            float worldMinY = bounds.min.y;

            for (int ty = 0; ty < tilesY; ty++)
            {
                for (int tx = 0; tx < tilesX; tx++)
                {
                    done++;
                    float progress = Mathf.Clamp01(done / (float)total) * 0.5f;
                    EditorUtility.DisplayProgressBar("Export Level Render", $"Rendering tiles... ({done}/{total})", progress);

                    int thisW = Mathf.Min(tileSize, fullWidth - tx * tileSize);
                    int thisH = Mathf.Min(tileSize, fullHeight - ty * tileSize);

                    PositionCameraForTile(cam, worldMinX, worldMinY, tx, ty, tileSize, thisW, thisH);

                    using (var rtWrap = CreateRenderTexture(thisW, thisH).Wrap())
                    {
                        RenderTexture renderTexture = rtWrap;

                        if (!renderTexture.Create())
                            throw new Exception($"RenderTexture.Create failed for tile {thisW}x{thisH}");

                        cam.targetTexture = renderTexture;

                        var previous = RenderTexture.active;
                        RenderTexture.active = renderTexture;

                        cam.Render();

                        var tileTexture = new Texture2D(thisW, thisH, TextureFormat.RGBA32, false)
                        {
                            filterMode = FilterMode.Point
                        };
                        tileTexture.ReadPixels(new Rect(0, 0, thisW, thisH), 0, 0);
                        tileTexture.Apply();

                        RenderTexture.active = previous;

                        string rawPath = Path.Combine(tempDirectory, $"tile_x{tx}_y{ty}.rgba");
                        WriteRawRgba(tileTexture, rawPath);

                        tiles[ty, tx] = new TileInfo(rawPath, thisW, thisH);

                        cam.targetTexture = null;
                        UnityEngine.Object.DestroyImmediate(tileTexture);
                    }
                }
            }
        }

        private static void PositionCameraForTile(Camera cam, float worldMinX, float worldMinY, int tx, int ty, int tileSize, int tileW, int tileH)
        {
            // ppu is 1 for vector, so /PixelsPerUnit is effectively no-op.
            float tileWorldMinX = worldMinX + (tx * tileSize) / PixelsPerUnit;
            float tileWorldMinY = worldMinY + (ty * tileSize) / PixelsPerUnit;

            float tileWorldW = tileW / PixelsPerUnit;
            float tileWorldH = tileH / PixelsPerUnit;

            cam.transform.position = new Vector3(tileWorldMinX + tileWorldW * 0.5f, tileWorldMinY + tileWorldH * 0.5f, -10f);
            cam.orthographicSize = tileWorldH * 0.5f;
        }

        // ================= RENDER ================= //
        private static GameObject CreateExportCamera(out Camera cam)
        {
            var gameObject = new GameObject(CameraName);
            cam = gameObject.AddComponent<Camera>();
            ConfigureCamera(cam);
            return gameObject;
        }

        private static void ConfigureCamera(Camera cam)
        {
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = ~0;
            cam.allowHDR = false;
            cam.allowMSAA = false;
        }

        private static void FrameCameraToBounds(Camera cam, Bounds bounds, int width, int height)
        {
            cam.transform.position = new Vector3(bounds.center.x, bounds.center.y, -10f);

            float aspect = (float)width / height;
            float sizeY = bounds.size.y * 0.5f;
            float sizeX = (bounds.size.x * 0.5f) / aspect;

            cam.orthographicSize = Mathf.Max(sizeY, sizeX);
        }

        private static RenderTexture CreateRenderTexture(int width, int height)
        {
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point,
                antiAliasing = 1
            };
            return renderTexture;
        }

        private static void WriteRawRgba(Texture2D texture, string path)
        {
            var raw = texture.GetRawTextureData(); // NativeArray<byte>
            byte[] bytes = raw.ToArray();      // copies tile bytes (bounded by tile size)

            using (var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                fileStream.Write(bytes, 0, bytes.Length);
        }

        private static string CreateTempTileDir()
        {
            string directory = Path.Combine(Path.GetTempPath(), "VectorierSpriteRender_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void TryDeleteDirectory(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch
            {
                // ignore cleanup failures
            }
        }

        // ================= TILE METADATA ================= //
        private readonly struct TileInfo
        {
            public readonly string Path;
            public readonly int Width;
            public readonly int Height;
            public int StrideBytes => Width * 4;

            public TileInfo(string path, int width, int height)
            {
                Path = path;
                Width = width;
                Height = height;
            }
        }

        // ================= PNG STREAM ================= //
        private static class PngStreamStitcher
        {
            private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };

            public static void WritePngFromRawTiles(string outPath, int fullWidth, int fullHeight, TileInfo[,] tiles, int tileSize)
            {
                using (var fileStream = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fileStream.Write(PngSignature, 0, PngSignature.Length);

                    WriteIHDR(fileStream, fullWidth, fullHeight);
                    WriteIDAT(fileStream, fullWidth, fullHeight, tiles, tileSize);
                    WriteChunk(fileStream, "IEND", Array.Empty<byte>(), 0);
                }
            }

            private static void WriteIHDR(Stream stream, int width, int height)
            {
                // IHDR data; width, height, bit depth=8, color type=RGBA(6), compression=0, filter=0, interlace=0
                byte[] ihdr = new byte[13];
                WriteU32ToArrayBE(ihdr, 0, (uint)width);
                WriteU32ToArrayBE(ihdr, 4, (uint)height);
                ihdr[8] = 8;
                ihdr[9] = 6;
                ihdr[10] = 0;
                ihdr[11] = 0;
                ihdr[12] = 0;

                WriteChunk(stream, "IHDR", ihdr, ihdr.Length);
            }

            private static void WriteIDAT(Stream pngStream, int fullWidth, int fullHeight, TileInfo[,] tiles, int tileSize)
            {
                int tilesY = tiles.GetLength(0);
                int tilesX = tiles.GetLength(1);

                // scanline: 1 filter byte + RGBA bytes
                byte[] scanline = new byte[1 + fullWidth * 4];
                scanline[0] = 0; // filter=0 (None)

                FileStream[,] tileStreams = new FileStream[tilesY, tilesX];

                try
                {
                    for (int ty = 0; ty < tilesY; ty++)
                        for (int tx = 0; tx < tilesX; tx++)
                            tileStreams[ty, tx] = new FileStream(tiles[ty, tx].Path, FileMode.Open, FileAccess.Read, FileShare.Read);

                    using var idatSink = new IdatChunkStream(pngStream);
                    using var deflater = new DeflateStream(idatSink, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true);

                    // raw tiles are stored bottom-up; PNG expected top-down
                    for (int globalY = fullHeight - 1; globalY >= 0; globalY--)
                    {
                        float p = 0.55f + 0.45f * (1f - (globalY / (float)Mathf.Max(1, fullHeight - 1)));
                        EditorUtility.DisplayProgressBar("Export Level Render", "Stitching PNG scanlines...", p);

                        int ty = globalY / tileSize;
                        int localY = globalY - ty * tileSize;

                        int dst = 1; // after filter byte

                        for (int tx = 0; tx < tilesX; tx++)
                        {
                            var tile = tiles[ty, tx];
                            var ts = tileStreams[ty, tx];

                            long rowOffset = (long)localY * tile.StrideBytes;
                            ts.Seek(rowOffset, SeekOrigin.Begin);

                            int bytesToRead = tile.Width * 4;
                            ReadExact(ts, scanline, dst, bytesToRead);
                            dst += bytesToRead;
                        }

                        deflater.Write(scanline, 0, scanline.Length);
                    }

                    // making sure all compressed bytes are emitted
                    deflater.Flush();
                    idatSink.Finish();
                }
                finally
                {
                    for (int ty = 0; ty < tileStreams.GetLength(0); ty++)
                        for (int tx = 0; tx < tileStreams.GetLength(1); tx++)
                            tileStreams[ty, tx]?.Dispose();
                }
            }

            private static void ReadExact(Stream stream, byte[] buffer, int offset, int count)
            {
                int read = 0;
                while (read < count)
                {
                    int n = stream.Read(buffer, offset + read, count - read);
                    if (n <= 0) throw new EndOfStreamException("Unexpected EOF while reading tile row.");
                    read += n;
                }
            }

            // --- PNG chunk writing + CRC32 ---

            private static void WriteChunk(Stream stream, string type, byte[] data, int dataLen)
            {
                byte[] type4 = { (byte)type[0], (byte)type[1], (byte)type[2], (byte)type[3] };

                WriteU32BE(stream, (uint)dataLen);
                stream.Write(type4, 0, 4);
                if (dataLen > 0) stream.Write(data, 0, dataLen);

                uint crc = Crc32.Compute(type4, data, dataLen);
                WriteU32BE(stream, crc);
            }

            private static void WriteU32BE(Stream stream, uint v)
            {
                stream.WriteByte((byte)(v >> 24));
                stream.WriteByte((byte)(v >> 16));
                stream.WriteByte((byte)(v >> 8));
                stream.WriteByte((byte)(v));
            }

            private static void WriteU32ToArrayBE(byte[] arr, int offset, uint v)
            {
                arr[offset + 0] = (byte)(v >> 24);
                arr[offset + 1] = (byte)(v >> 16);
                arr[offset + 2] = (byte)(v >> 8);
                arr[offset + 3] = (byte)(v);
            }

            private static class Crc32
            {
                private static readonly uint[] Table = InitTable();

                private static uint[] InitTable()
                {
                    uint[] table = new uint[256];
                    for (uint i = 0; i < 256; i++)
                    {
                        uint c = i;
                        for (int k = 0; k < 8; k++)
                            c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : (c >> 1);
                        table[i] = c;
                    }
                    return table;
                }

                public static uint Compute(byte[] type4, byte[] data, int dataLen)
                {
                    uint c = 0xFFFFFFFFu;

                    for (int i = 0; i < 4; i++)
                        c = Table[(c ^ type4[i]) & 0xFF] ^ (c >> 8);

                    for (int i = 0; i < dataLen; i++)
                        c = Table[(c ^ data[i]) & 0xFF] ^ (c >> 8);

                    return c ^ 0xFFFFFFFFu;
                }
            }

            // Buffers compressed bytes and emits them as IDAT chunks
            private sealed class IdatChunkStream : Stream
            {
                private readonly Stream _png;
                private readonly byte[] _buf;
                private int _pos;

                public IdatChunkStream(Stream pngStream, int chunkSizeBytes = 1 << 20) // ~1MB
                {
                    _png = pngStream;
                    int size = Mathf.Max(64 * 1024, chunkSizeBytes);
                    _buf = new byte[size];
                    _pos = 0;
                }

                public void Finish()
                {
                    FlushIdat();
                }

                public override void Write(byte[] buffer, int offset, int count)
                {
                    while (count > 0)
                    {
                        int n = Math.Min(count, _buf.Length - _pos);
                        Buffer.BlockCopy(buffer, offset, _buf, _pos, n);
                        _pos += n;
                        offset += n;
                        count -= n;

                        if (_pos == _buf.Length)
                            FlushIdat();
                    }
                }

                private void FlushIdat()
                {
                    if (_pos <= 0) return;
                    WriteChunk(_png, "IDAT", _buf, _pos);
                    _pos = 0;
                }

                public override void Flush() { /* no-op; call Finish */ }
                public override bool CanRead => false;
                public override bool CanSeek => false;
                public override bool CanWrite => true;
                public override long Length => throw new NotSupportedException();
                public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
                public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
                public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
                public override void SetLength(long value) => throw new NotSupportedException();
            }
        }
    }

    // Small helper to allow the "using (var rt = ..)" even though renderTxeture is UnityEngine.Object
    internal static class RenderTextureDisposableExtensions
    {
        public static RenderTextureDisposable Wrap(this RenderTexture renderTexture) => new RenderTextureDisposable(renderTexture);

        internal readonly struct RenderTextureDisposable : IDisposable
        {
            private readonly RenderTexture _renderTexture;
            public RenderTextureDisposable(RenderTexture renderTexture) => _renderTexture = renderTexture;
            public void Dispose()
            {
                if (_renderTexture != null)
                    UnityEngine.Object.DestroyImmediate(_renderTexture);
            }

            public static implicit operator RenderTexture(RenderTextureDisposable d) => d._renderTexture;
        }
    }
}
