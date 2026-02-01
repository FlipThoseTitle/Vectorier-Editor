using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Vectorier.Dynamic
{
    public enum EasePreset { EaseIn, Linear, EaseOut, Custom }
    public static class EasePresetUtil
    {
        public static float ToValue(EasePreset e) => e == EasePreset.EaseIn ? 0f : e == EasePreset.EaseOut ? 1f : 0.5f;
        public static Vector2 ToSupport(EasePreset e) { var v = ToValue(e); return new Vector2(v, v); }
    }

    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("")]
    public class DynamicTimelineData : MonoBehaviour
    {
#if UNITY_EDITOR
        void Reset() => hideFlags = HideFlags.HideInInspector;
        private void OnValidate()
        {
            hideFlags = HideFlags.HideInInspector;
            if (keys == null) keys = new List<KF>();
            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                k.EnsureDefaults();
                keys[i] = k;
            }
        }
#endif
        public string transformationName = "Transform";
        public int fps = 60, totalFrames = 300;
        public List<KF> keys = new();

        [Serializable]
        public struct KF
        {
            public int f;
            public Vector3 lp, ls;
            public float z;
            public Color c;
            public Vector2 support;
            public EasePreset ease;

            [SerializeField] private bool _init;

            public void EnsureDefaults()
            {
                if (_init) return;

                if (c == default) c = Color.white;
                if (ease == default) ease = EasePreset.Linear;
                if (support == default) support = EasePresetUtil.ToSupport(EasePreset.Linear);

                _init = true;
            }
        }

        public void Sort() { keys = keys.OrderBy(k => k.f).ToList(); }
        public bool Has(int f, out int idx) { for (int i = 0; i < keys.Count; i++) if (keys[i].f == f) { idx = i; return true; } idx = -1; return false; }

        public void Upsert(int f, Vector3 lp, Vector3 ls, float z, Color c, Vector2 support, bool replace = true)
        {
            const float E = 0.0005f;
            bool in01 = support.x >= -E && support.x <= 1f + E && support.y >= -E && support.y <= 1f + E;
            EasePreset ease = EasePreset.Custom;
            if (in01)
            {
                bool eq = Mathf.Abs(support.x - support.y) <= 0.001f;
                if (eq && Mathf.Abs(support.x - 0f) <= 0.001f) ease = EasePreset.EaseIn;
                else if (eq && Mathf.Abs(support.x - 0.5f) <= 0.001f) ease = EasePreset.Linear;
                else if (eq && Mathf.Abs(support.x - 1f) <= 0.001f) ease = EasePreset.EaseOut;
                else ease = EasePreset.Custom;
                if (ease != EasePreset.Custom) support = EasePresetUtil.ToSupport(ease);
            }
            KF kf = new KF { f = f, lp = lp, ls = ls, z = z, c = (c == default ? Color.white : c), support = support, ease = ease };
            kf.EnsureDefaults();
            if (Has(f, out var i)) { if (replace) keys[i] = kf; return; }
            keys.Add(kf);
            Sort();
        }

        public void DeleteAt(int f) { for (int i = 0; i < keys.Count; i++) if (keys[i].f == f) { keys.RemoveAt(i); return; } }

        static string N(string s) => string.IsNullOrEmpty(s) ? "NewTransform" : s;
        static DynamicTransform FindOrCreateDynamicTransform(GameObject go, string name)
        {
            var list = go.GetComponents<DynamicTransform>(); var nn = N(name);
            for (int i = 0; i < list.Length; i++) if (list[i] && N(list[i].transformationName) == nn) return list[i];
            var created = go.AddComponent<DynamicTransform>(); created.transformationName = nn; return created;
        }

        public DynamicTransform BakeToDynamicTransform(GameObject go, string targetName, bool clear, bool useCustomEase)
        { if (!go) return null; var dt = FindOrCreateDynamicTransform(go, targetName); return BakeToDynamicTransform(dt, clear, useCustomEase); }

        public DynamicTransform BakeToDynamicTransform(DynamicTransform dt, bool clear, bool useCustomEase)
        {
            if (!dt) return null;
            dt.transformationName = transformationName;
            Sort();

            if (clear)
            {
                dt.moves.Clear();
                dt.sizes.Clear();
                dt.rotations.Clear();
                dt.colors.Clear();
            }

            if (keys.Count < 2) return dt;

            var go = dt.gameObject;
            var sr = go.GetComponent<SpriteRenderer>();

            const float EPS = 0.000001f;
            const float EPS2 = 0.0000001f;

            bool V2Eq(Vector2 a, Vector2 b) => (a - b).sqrMagnitude <= EPS2;
            bool V3Eq(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= EPS2;
            bool FEq(float a, float b) => Mathf.Abs(a - b) <= 0.0005f;
            bool CEq(Color a, Color b) =>
                Mathf.Abs(a.r - b.r) <= 0.0005f &&
                Mathf.Abs(a.g - b.g) <= 0.0005f &&
                Mathf.Abs(a.b - b.b) <= 0.0005f &&
                Mathf.Abs(a.a - b.a) <= 0.0005f;

            bool IsZeroMove(Vector2 v) => Mathf.Abs(v.x) <= EPS && Mathf.Abs(v.y) <= EPS;
            var k0 = keys[0];

            bool anyMove = false, anySize = false, anyRot = false, anyColor = false;

            for (int i = 1; i < keys.Count; i++)
            {
                var k = keys[i];

                if (!anyMove)
                {
                    var md = new Vector2(k.lp.x - k0.lp.x, k.lp.y - k0.lp.y);
                    if (!IsZeroMove(md)) anyMove = true;
                }

                if (!anySize && !V3Eq(k.ls, k0.ls)) anySize = true;

                if (!anyRot && !FEq(Mathf.DeltaAngle(k0.z, k.z), 0f)) anyRot = true;

                if (!anyColor && !CEq(k.c, k0.c)) anyColor = true;

                if (anyMove && anySize && anyRot && anyColor) break;
            }

            int pendingDelayFrames = 0;

            for (int i = 0; i < keys.Count - 1; i++)
            {
                var a = keys[i];
                var b = keys[i + 1];

                int df = Mathf.Max(0, b.f - a.f);
                if (df <= 0) continue;

                // ---- MOVE ----
                if (anyMove)
                {
                    Vector2 moveDelta = new(b.lp.x - a.lp.x, b.lp.y - a.lp.y);

                    if (IsZeroMove(moveDelta))
                    {
                        pendingDelayFrames += df;
                    }
                    else
                    {
                        Vector2 sup;
                        if (useCustomEase || b.ease == EasePreset.Custom)
                        {
                            // absolute support
                            sup = new Vector2(b.support.x, b.support.y);
                        }
                        else
                        {
                            // preset support scaled by move delta
                            var s = EasePresetUtil.ToSupport(b.ease);
                            sup = new Vector2(moveDelta.x * s.x, moveDelta.y * s.y);
                        }

                        dt.moves.Add(new DynamicTransform.MoveData
                        {
                            frames = df,
                            delay = pendingDelayFrames, // frames
                            move = moveDelta,
                            support = sup
                        });

                        pendingDelayFrames = 0;
                    }
                }

                // ---- SIZE ----
                if (anySize)
                {
                    Vector2 wh = sr ? SpriteWH(sr, b.ls) : BoundsWH(go, b.lp, b.ls, b.z);

                    int last = dt.sizes.Count - 1;
                    if (last >= 0 && FEq(dt.sizes[last].finalWidth, wh.x) && FEq(dt.sizes[last].finalHeight, wh.y))
                    {
                        var s = dt.sizes[last];
                        s.frames += df;
                        dt.sizes[last] = s;
                    }
                    else
                    {
                        dt.sizes.Add(new DynamicTransform.SizeData
                        {
                            frames = df,
                            finalWidth = wh.x,
                            finalHeight = wh.y
                        });
                    }
                }

                // ---- ROTATION ----
                if (anyRot)
                {
                    float rotDelta = Mathf.DeltaAngle(a.z, b.z);

                    int last = dt.rotations.Count - 1;
                    if (last >= 0 && FEq(dt.rotations[last].angle, rotDelta) && V2Eq(dt.rotations[last].anchor, Vector2.zero))
                    {
                        var r = dt.rotations[last];
                        r.frames += df;
                        dt.rotations[last] = r;
                    }
                    else
                    {
                        dt.rotations.Add(new DynamicTransform.RotateData
                        {
                            angle = rotDelta,
                            anchor = Vector2.zero,
                            frames = df
                        });
                    }
                }

                // ---- COLOR ----
                if (anyColor)
                {
                    var cs = a.c;
                    var cf = b.c;

                    int last = dt.colors.Count - 1;
                    if (last >= 0 && CEq(dt.colors[last].colorStart, cs) && CEq(dt.colors[last].colorFinish, cf))
                    {
                        var c = dt.colors[last];
                        c.frames += df;
                        dt.colors[last] = c;
                    }
                    else
                    {
                        dt.colors.Add(new DynamicTransform.ColorData
                        {
                            colorStart = cs,
                            colorFinish = cf,
                            frames = df
                        });
                    }
                }
            }

            return dt;
        }

        public void LoadFromDynamicTransform(DynamicTransform src, bool useCustomEase, bool clearExisting = true)
        {
            if (!src) return;
            if (clearExisting) keys.Clear();
            transformationName = string.IsNullOrEmpty(src.transformationName) ? "NewTransform" : src.transformationName;

            var go = src.gameObject;
            var t = go.transform;
            Vector3 curLP = t.localPosition, curLS = t.localScale;
            float curZ = t.localEulerAngles.z;
            var sr = go.GetComponent<SpriteRenderer>();
            Color curC = sr ? sr.color : Color.white;

            int mc = src.moves?.Count ?? 0, sc = src.sizes?.Count ?? 0, rc = src.rotations?.Count ?? 0, cc = src.colors?.Count ?? 0;
            int n = Mathf.Max(Mathf.Max(mc, sc), Mathf.Max(rc, cc));

            keys.Add(new KF
            {
                f = 0,
                lp = curLP,
                ls = curLS,
                z = curZ,
                c = curC,
                support = EasePresetUtil.ToSupport(EasePreset.Linear),
                ease = EasePreset.Linear
            });

            if (n <= 0) { Sort(); totalFrames = Mathf.Max(1, totalFrames); return; }

            const float EPS = 0.0005f;
            bool MatchPreset(Vector2 mv, Vector2 sp, float k)
            {
                bool okx = Mathf.Abs(mv.x) <= EPS ? Mathf.Abs(sp.x) <= EPS : Mathf.Abs(sp.x - mv.x * k) <= EPS;
                bool oky = Mathf.Abs(mv.y) <= EPS ? Mathf.Abs(sp.y) <= EPS : Mathf.Abs(sp.y - mv.y * k) <= EPS;
                return okx && oky;
            }

            int frame = 0;

            for (int i = 0; i < n; i++)
            {
                var m = (i < mc && src.moves != null) ? src.moves[i] : null;
                var s = (i < sc && src.sizes != null) ? src.sizes[i] : null;
                var r = (i < rc && src.rotations != null) ? src.rotations[i] : null;
                var col = (i < cc && src.colors != null) ? src.colors[i] : null;

                // ----- materialize MOVE delay as a dummy KF (no position delta) -----
                if (m != null)
                {
                    int dly = Mathf.Max(0, Mathf.RoundToInt(m.delay)); // delay stored as frames in float
                    if (dly > 0)
                    {
                        frame += dly;

                        // Dummy keyframe: same state, (prev -> dummy) has move 0,0 and will be skipped when baking back.
                        keys.Add(new KF
                        {
                            f = frame,
                            lp = curLP,
                            ls = curLS,
                            z = curZ,
                            c = curC,
                            support = EasePresetUtil.ToSupport(EasePreset.Linear),
                            ease = EasePreset.Linear
                        });
                    }
                }

                int df = 0;
                if (m != null) df = Mathf.Max(df, m.frames);
                if (s != null) df = Mathf.Max(df, s.frames);
                if (r != null) df = Mathf.Max(df, r.frames);
                if (col != null) df = Mathf.Max(df, col.frames);

                Vector2 mv = m != null ? new Vector2(m.move.x, m.move.y) : Vector2.zero;
                Vector2 sp = m != null ? new Vector2(m.support.x, m.support.y) : Vector2.zero;

                if (m != null) curLP += new Vector3(mv.x, mv.y, 0f);
                if (r != null) curZ = Mathf.Repeat(curZ + r.angle, 360f);
                if (col != null) curC = col.colorFinish;
                if (s != null) curLS = ScaleFromFinalWH(go, s.finalWidth, s.finalHeight, curLS);

                Vector2 supportToStore;
                EasePreset easeToStore;

                if (useCustomEase)
                {
                    supportToStore = sp;
                    easeToStore = EasePreset.Custom;
                }
                else
                {
                    if (MatchPreset(mv, sp, 0f)) { easeToStore = EasePreset.EaseIn; supportToStore = EasePresetUtil.ToSupport(easeToStore); }
                    else if (MatchPreset(mv, sp, 0.5f)) { easeToStore = EasePreset.Linear; supportToStore = EasePresetUtil.ToSupport(easeToStore); }
                    else if (MatchPreset(mv, sp, 1f)) { easeToStore = EasePreset.EaseOut; supportToStore = EasePresetUtil.ToSupport(easeToStore); }
                    else { easeToStore = EasePreset.Custom; supportToStore = sp; }
                }

                frame += df;
                keys.Add(new KF
                {
                    f = frame,
                    lp = curLP,
                    ls = curLS,
                    z = curZ,
                    c = curC,
                    support = supportToStore,
                    ease = easeToStore
                });
            }

            totalFrames = Mathf.Max(1, frame + 1);
            Sort();
        }

        static float SafeRatio(float num, float den) { if (Mathf.Abs(den) < 0.000001f) return 0.5f; return num / den; }

        static Vector3 ScaleFromFinalWH(GameObject go, float finalW, float finalH, Vector3 fallback)
        {
            if (!go) return fallback;
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr && sr.sprite)
            {
                var sp = sr.sprite; Vector2 px = sp.rect.size; float ppu = sp.pixelsPerUnit <= 0f ? 100f : sp.pixelsPerUnit;
                float baseW = px.x / ppu, baseH = px.y / ppu;
                if (baseW <= 0.000001f || baseH <= 0.000001f) return fallback;
                float sx = Mathf.Abs(finalW / baseW), sy = Mathf.Abs(finalH / baseH);
                return new Vector3(sx, sy, fallback.z);
            }
            var t = go.transform; Vector3 oldS = t.localScale; t.localScale = Vector3.one;
            bool has = TryGetRendererBounds(go, out var b); t.localScale = oldS;
            if (!has) return fallback;
            float baseBW = Mathf.Abs(b.size.x), baseBH = Mathf.Abs(b.size.y);
            if (baseBW <= 0.000001f || baseBH <= 0.000001f) return fallback;
            float sx2 = Mathf.Abs(finalW / baseBW), sy2 = Mathf.Abs(finalH / baseBH);
            return new Vector3(sx2, sy2, fallback.z);
        }

        public KF Snapshot(int f)
        {
            var sr = GetComponent<SpriteRenderer>();
            var col = sr ? sr.color : Color.white;
            var k = new KF { f = f, lp = transform.localPosition, ls = transform.localScale, z = transform.localEulerAngles.z, c = col, support = EasePresetUtil.ToSupport(EasePreset.Linear), ease = EasePreset.Linear };
            k.EnsureDefaults();
            return k;
        }

        static Vector2 SpriteWH(SpriteRenderer sr, Vector3 ls)
        {
            if (!sr || !sr.sprite) return new Vector2(ls.x, ls.y);
            var sp = sr.sprite; Vector2 px = sp.rect.size; float ppu = sp.pixelsPerUnit <= 0f ? 100f : sp.pixelsPerUnit;
            float w = (px.x / ppu) * ls.x, h = (px.y / ppu) * ls.y; if (w < 0f) w = -w; if (h < 0f) h = -h; return new Vector2(w, h);
        }

        static Vector2 BoundsWH(GameObject go, Vector3 lp, Vector3 ls, float z)
        {
            if (!go) return new Vector2(ls.x, ls.y);
            var t = go.transform; Vector3 oldP = t.localPosition, oldS = t.localScale; Quaternion oldR = t.localRotation;
            t.localPosition = lp; t.localScale = ls; t.localRotation = Quaternion.Euler(0f, 0f, z);
            bool has = TryGetRendererBounds(go, out var b);
            t.localPosition = oldP; t.localScale = oldS; t.localRotation = oldR;
            if (!has) return new Vector2(ls.x, ls.y);
            float w = b.size.x; if (w < 0f) w = -w; float h = b.size.y; if (h < 0f) h = -h; return new Vector2(w, h);
        }

        static bool TryGetRendererBounds(GameObject go, out Bounds b)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true); bool any = false; b = default;
            for (int i = 0; i < rs.Length; i++)
            {
                var r = rs[i]; if (!r || !r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
