using UnityEditor;
using UnityEngine;

namespace Vectorier.Dynamic
{
    public static class DynamicHandle
    {
        // Draw segment between previous and next keyframe around the current frame
        public static void DrawPrevNextBezierAndSupport(DynamicTimelineData d, DynamicPreview p, int curFrame, bool customEase)
        {
            if (!customEase) return;
            if (!d || !p) return;
            if (d.keys == null || d.keys.Count < 2) return;

            d.Sort();

            int targetF, targetIdx;
            if (d.Has(curFrame, out targetIdx)) targetF = curFrame;
            else
            {
                targetF = FindNextKeyFrame(d, curFrame);
                if (targetF < 0) return;
                if (!d.Has(targetF, out targetIdx)) return;
            }

            int prevF = FindPrevKeyFrame(d, targetF);
            if (prevF < 0) return;
            if (!d.Has(prevF, out int prevIdx)) return;

            var k0 = d.keys[prevIdx];
            var k1 = d.keys[targetIdx];

            Vector3 lp0 = k0.lp, lp1 = k1.lp;
            Vector3 deltaL = lp1 - lp0;
            if (Mathf.Abs(deltaL.x) < 0.0001f && Mathf.Abs(deltaL.y) < 0.0001f) return;

            var parentW = d.transform.parent ? d.transform.parent.localToWorldMatrix : Matrix4x4.identity;

            // offset (visual only, doesn't effect value)
            Vector3 vOffW;
            {
                var t = d.transform;
                if (!t) vOffW = Vector3.zero;
                else if (t.childCount == 0)
                {
                    var r = t.GetComponent<Renderer>();
                    vOffW = (r ? r.bounds.center : t.position) - t.position;
                }
                else
                {
                    var rs = t.GetComponentsInChildren<Renderer>(true);
                    if (rs != null && rs.Length > 0)
                    {
                        Bounds b = rs[0].bounds;
                        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                        vOffW = b.center - t.position;
                    }
                    else
                    {
                        Vector3 sum = t.position; int c = 1;
                        for (int i = 0; i < t.childCount; i++) { sum += t.GetChild(i).position; c++; }
                        vOffW = (sum / Mathf.Max(1, c)) - t.position;
                    }
                }
            }

            Vector3 p0W = parentW.MultiplyPoint3x4(lp0) + vOffW;
            Vector3 p1W = parentW.MultiplyPoint3x4(lp1) + vOffW;

            Vector3 ctrlL = lp0 + new Vector3(k1.support.x, k1.support.y, 0f);
            Vector3 ctrlW = parentW.MultiplyPoint3x4(ctrlL) + vOffW;

            Vector3 c1 = Vector3.Lerp(p0W, ctrlW, 2f / 3f);
            Vector3 c2 = Vector3.Lerp(p1W, ctrlW, 2f / 3f);

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            Handles.color = new Color(1f, 0f, 0f, 0.65f);
            Handles.DrawBezier(p0W, p1W, c1, c2, Handles.color, null, 2f);

            float size = HandleUtility.GetHandleSize(ctrlW) * 0.08f;

            EditorGUI.BeginChangeCheck();
            Vector3 newCtrlW = Handles.Slider2D(
                ctrlW,
                Vector3.forward,
                Vector3.right,
                Vector3.up,
                size,
                Handles.DotHandleCap,
                0f
            );

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(d, "Move Support Handle");

                // remove visual offset before storing
                Vector3 newCtrlL = parentW.inverse.MultiplyPoint3x4(newCtrlW - vOffW);

                Vector2 newOffset = new Vector2(newCtrlL.x - lp0.x, newCtrlL.y - lp0.y);

                if (Event.current != null && Event.current.shift)
                {
                    Vector2 d2 = new Vector2(deltaL.x, deltaL.y);
                    Vector2 offIn = Vector2.zero;
                    Vector2 offLin = d2 * 0.5f;
                    Vector2 offOut = d2;

                    float din = (newOffset - offIn).sqrMagnitude;
                    float dlin = (newOffset - offLin).sqrMagnitude;
                    float dout = (newOffset - offOut).sqrMagnitude;

                    if (din <= dlin && din <= dout) newOffset = offIn;
                    else if (dlin <= din && dlin <= dout) newOffset = offLin;
                    else newOffset = offOut;
                }

                k1.support = newOffset;
                d.keys[targetIdx] = k1;

                d.Sort();
                EditorUtility.SetDirty(d);
                p.PreviewNoSort(Mathf.Clamp(curFrame, 0, d.totalFrames));
            }
        }

        static int FindPrevKeyFrame(DynamicTimelineData d, int fr)
        {
            int best = -1;
            for (int i = 0; i < d.keys.Count; i++)
            {
                int kf = d.keys[i].f;
                if (kf < fr && kf > best) best = kf;
            }
            return best;
        }

        static int FindNextKeyFrame(DynamicTimelineData d, int fr)
        {
            int best = int.MaxValue;
            for (int i = 0; i < d.keys.Count; i++)
            {
                int kf = d.keys[i].f;
                if (kf > fr && kf < best) best = kf;
            }
            return best == int.MaxValue ? -1 : best;
        }

        static float SafeNorm(float num, float den)
        {
            if (Mathf.Abs(den) < 0.0001f) return 0.5f;
            return num / den;
        }
    }
}
