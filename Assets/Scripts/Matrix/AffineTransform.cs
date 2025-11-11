using System;
using System.Globalization;
using UnityEngine;

namespace Vectorier.Matrix
{
    public struct AffineMatrixData
    {
        public float A, B, C, D, Tx, Ty;
        public float TopLeftX, TopLeftY;
        public float BoundingWidth, BoundingHeight;
        public int NativeWidth, NativeHeight;
    }

    public static class AffineTransformation
    {
        /// Computes the affine matrix for a GameObject with SpriteRenderer.
        /// Returns false if no rotation or flipping is applied.
        public static bool Compute(GameObject obj, SpriteRenderer spriteRenderer, out AffineMatrixData matrixData)
        {
            matrixData = new AffineMatrixData();

            if (obj == null || spriteRenderer == null || spriteRenderer.sprite == null)
                return false;

            // Base image position
            float imagePosX = obj.transform.position.x * 100f;
            float imagePosY = obj.transform.position.y * -100f;

            // Get rotation (Z-axis)
            float rotationAngle = obj.transform.eulerAngles.z % 360f;
            if (rotationAngle < 0f)
                rotationAngle += 360f;

            bool flipX = spriteRenderer.flipX;
            bool flipY = spriteRenderer.flipY;

            // If exactly one of them is flipped, negate angle
            if (flipX ^ flipY)
                rotationAngle = -rotationAngle;

            // Only process if rotated or flipped
            if ((rotationAngle == 0f || rotationAngle % 360f == 0f) && !flipX && !flipY)
                return false;

            // Sprite and scaling info
            Texture2D texture = spriteRenderer.sprite.texture;
            if (texture == null)
                return false;

            int nativeWidth = texture.width;
            int nativeHeight = texture.height;
            Vector3 localScale = obj.transform.localScale;

            float imageWidth = nativeWidth * localScale.x;
            float imageHeight = nativeHeight * localScale.y;

            // Start with identity matrix (scaled by width/height)
            float A = imageWidth, B = 0f, C = 0f, D = imageHeight;

            // Normalize rotation
            float normAngle = rotationAngle % 360f;
            if (normAngle < 0f) normAngle += 360f;

            // Handle 90/180/270 explicitly
            if (Mathf.Approximately(normAngle, 90f))
            {
                A = 0f;
                B = -imageWidth;
                C = imageHeight;
                D = 0f;
            }
            else if (Mathf.Approximately(normAngle, 180f))
            {
                A = -imageWidth;
                B = 0f;
                C = 0f;
                D = -imageHeight;
            }
            else if (Mathf.Approximately(normAngle, 270f))
            {
                A = 0f;
                B = imageWidth;
                C = -imageHeight;
                D = 0f;
            }
            else
            {
                // Free rotation
                float radians = rotationAngle * Mathf.Deg2Rad;
                float cosTheta = Mathf.Cos(radians);
                float sinTheta = Mathf.Sin(radians);

                A = imageWidth * cosTheta;
                B = -imageWidth * sinTheta;
                C = imageHeight * sinTheta;
                D = imageHeight * cosTheta;
            }

            // Apply flips
            if (flipX)
            {
                A = -A;
                C = -C;
            }
            if (flipY)
            {
                B = -B;
                D = -D;
            }

            // Compute translation offsets
            float topLeftX = imagePosX + Mathf.Min(0f, A) + Mathf.Min(0f, C);
            float topLeftY = imagePosY + Mathf.Min(0f, B) + Mathf.Min(0f, D);

            float Tx = imagePosX - topLeftX;
            float Ty = imagePosY - topLeftY;

            // Bounding box in world units
            Bounds bounds = spriteRenderer.bounds;
            float worldWidth = bounds.size.x * 100f;
            float worldHeight = bounds.size.y * 100f;

            matrixData.A = A;
            matrixData.B = B;
            matrixData.C = C;
            matrixData.D = D;
            matrixData.Tx = Tx;
            matrixData.Ty = Ty;
            matrixData.TopLeftX = topLeftX;
            matrixData.TopLeftY = topLeftY;
            matrixData.BoundingWidth = worldWidth;
            matrixData.BoundingHeight = worldHeight;
            matrixData.NativeWidth = nativeWidth;
            matrixData.NativeHeight = nativeHeight;

            return true;
        }
    }
}
