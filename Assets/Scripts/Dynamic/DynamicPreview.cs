using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Vectorier.Dynamic
{
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("")]
    public class DynamicPreview : MonoBehaviour
    {
#if UNITY_EDITOR
        void Reset() => hideFlags = HideFlags.HideInInspector;
        void OnValidate() => hideFlags = HideFlags.HideInInspector;
#endif

        public DynamicTimelineData data;
        public bool previewing;
        int lastFrame = -1;
        public bool useCustomEase;

        public bool playing;
        public int fps = 60;

        double lastT;
        double carry; // fractional frame accumulator

        void OnEnable()
        {
#if UNITY_EDITOR
            hideFlags = HideFlags.HideInInspector;
#endif
            if (!data) data = GetComponent<DynamicTimelineData>();
            lastT = EditorApplication.timeSinceStartup;
            carry = 0;
            EditorApplication.update += EditorTick;
        }

        void OnDisable()
        {
            EditorApplication.update -= EditorTick;
            playing = false;
        }

        void EditorTick()
        {
            if (!playing || !data) { lastT = EditorApplication.timeSinceStartup; return; }

            double now = EditorApplication.timeSinceStartup;
            double dt = now - lastT;
            lastT = now;
            if (dt <= 0) return;

            int fpsUse = data && data.fps > 0 ? data.fps : fps;

            carry += dt * fpsUse;
            int adv = (int)carry;
            if (adv <= 0) return;
            carry -= adv;

            int cur = (lastFrame < 0) ? 0 : lastFrame;
            int nf = cur + adv;
            int last = Mathf.Max(0, data.totalFrames);

            // allow nf == last (play last frame), stop only once we go past it
            if (nf > last)
            {
                ApplyFrame(last);
                playing = false;
                return;
            }

            ApplyFrame(nf);
        }

        public void TogglePlay()
        {
            if (!data) return;
            playing = !playing;
            lastT = EditorApplication.timeSinceStartup;
            carry = 0;

            if (playing)
            {
                int last = Mathf.Max(0, data.totalFrames);

                // if starting from end (or invalid), restart from beginning
                if (lastFrame < 0 || lastFrame > last || lastFrame == last) lastFrame = 0;

                ApplyFrame(lastFrame);
            }
        }

        public bool IsPlaying => playing;
        public int CurrentFrame => lastFrame;

        public void ApplyFrame(int f)
        {
            if (!data) return;
            data.Sort();
            if (data.keys.Count == 0) return;

            f = Mathf.Clamp(f, 0, data.totalFrames);
            Eval(f, out var lp, out var ls, out var z, out var c, assumeSorted: true);
            Set(lp, ls, z, c);

            lastFrame = f;
            previewing = true;
        }

        void Set(Vector3 lp, Vector3 ls, float z, Color c)
        {
            transform.localPosition = lp;
            transform.localScale = ls;
            var e = transform.localEulerAngles; e.z = z; transform.localEulerAngles = e;
            var sr = GetComponent<SpriteRenderer>(); if (sr) sr.color = c;
        }

        static float Bez1(float t, float p0, float p1, float p2)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        public void PreviewNoSort(int f)
        {
            if (!data) return;
            if (data.keys.Count == 0) return;

            f = Mathf.Clamp(f, 0, data.totalFrames);
            Eval(f, out var lp, out var ls, out var z, out var c, assumeSorted: false);
            Set(lp, ls, z, c);

            lastFrame = f;
            previewing = true;
        }

        public void Eval(int f, out Vector3 lp, out Vector3 ls, out float z, out Color c, bool assumeSorted)
        {
            var src = data.keys;
            var k = assumeSorted ? src : src.OrderBy(x => x.f).ToList();

            if (k.Count == 1) { var a = k[0]; lp = a.lp; ls = a.ls; z = a.z; c = a.c; return; }
            if (f <= k[0].f) { var a = k[0]; lp = a.lp; ls = a.ls; z = a.z; c = a.c; return; }
            if (f >= k[^1].f) { var a = k[^1]; lp = a.lp; ls = a.ls; z = a.z; c = a.c; return; }

            int i = 0;
            for (; i < k.Count - 1; i++) if (f >= k[i].f && f <= k[i + 1].f) break;

            var a0 = k[i];
            var a1 = k[i + 1];

            float den = Mathf.Max(1, a1.f - a0.f);
            float t = (f - a0.f) / den;

            Vector3 ctrl;
            if (useCustomEase || a1.ease == EasePreset.Custom) ctrl = a0.lp + new Vector3(a1.support.x, a1.support.y, 0f);
            else { var dlt = a1.lp - a0.lp; Vector2 s = EasePresetUtil.ToSupport(a1.ease); ctrl = a0.lp + new Vector3(dlt.x * s.x, dlt.y * s.y, 0f); }

            lp = new Vector3(
                Bez1(t, a0.lp.x, ctrl.x, a1.lp.x),
                Bez1(t, a0.lp.y, ctrl.y, a1.lp.y),
                Mathf.Lerp(a0.lp.z, a1.lp.z, t)
            );

            ls = Vector3.Lerp(a0.ls, a1.ls, t);
            z = Mathf.LerpAngle(a0.z, a1.z, t);
            c = Color.Lerp(a0.c, a1.c, t);
        }
    }
}
