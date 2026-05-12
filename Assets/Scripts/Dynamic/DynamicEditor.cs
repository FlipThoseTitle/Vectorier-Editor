using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vectorier.Dynamic
{
    public class DynamicEditor : EditorWindow
    {
        DynamicTimelineData d; DynamicPreview p;
        int f; float zoom = 1f;
        float scrollF;
        readonly HashSet<int> selF = new();
        bool scrubbing, moving;
        int dragStartFrame;
        List<(int idx, int origF)> moveList = new();
        static string N(DynamicTransform x) => x && string.IsNullOrEmpty(x.transformationName) ? "NewTransform" : x ? x.transformationName : "NewTransform";
        Vector2 _multiListScroll;

        bool onion; float onionA = 0.25f;
        bool customEase;

        string[] dtNames = new string[0];
        int dtPick = 0;
        string curDTName = "NewTransform";
        GameObject[] boundGos;

        string[] GetDTNames(GameObject go)
        {
            if (!go) return System.Array.Empty<string>();
            var dts = go.GetComponents<DynamicTransform>();
            if (dts == null || dts.Length == 0) return System.Array.Empty<string>();
            return dts.Select(x => string.IsNullOrEmpty(x.transformationName) ? "NewTransform" : x.transformationName).Distinct().ToArray();
        }

        // restore-on-close / selection change
        GameObject boundGO;
        Vector3 oLP, oLS; Quaternion oLR; Color oC; bool oHasSR;

        struct CK { public int df; public Vector3 lp, ls; public float z; public Color c; public Vector2 support; }
        struct MO { public Vector3 lp, ls; public Quaternion lr; public Color c; public bool hasSR; }
        Dictionary<int, MO> mo = new();
        static List<CK> clip = new();
        static int clipSpan = 0;

        [MenuItem("Vectorier/Tools/Dynamic Editor")] static void Open() { GetWindow<DynamicEditor>("Dynamic Editor"); }

        void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += EditorTick;
            SceneView.duringSceneGui += OnSceneGUI;
            Unbind();
        }

        void OnDisable()
        {
            Unbind();
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.update -= EditorTick;
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        void OnSelectionChanged()
        {
            Unbind();
            Repaint();
        }

        void Unbind()
        {
            RestoreOriginalMulti();
            RestoreOriginal();
            boundGos = null;
            d = null;
            p = null;
            selF.Clear();
            moving = scrubbing = false;
        }

        void CacheOriginalMulti(GameObject[] gos)
        {
            mo.Clear();
            if (gos == null) return;

            for (int i = 0; i < gos.Length; i++)
            {
                var g = gos[i];
                if (!g) continue;

                var t = g.transform;
                var o = new MO { lp = t.localPosition, ls = t.localScale, lr = t.localRotation };

                var sr = g.GetComponent<SpriteRenderer>();
                o.hasSR = sr;
                o.c = sr ? sr.color : Color.white;

                mo[g.GetInstanceID()] = o;
            }
        }

        void RestoreOriginalMulti()
        {
            if (mo == null || mo.Count == 0) return;

            foreach (var kv in mo)
            {
                var g = EditorUtility.EntityIdToObject(kv.Key) as GameObject;
                if (!g) continue;

                var o = kv.Value;
                var t = g.transform;
                t.localPosition = o.lp;
                t.localScale = o.ls;
                t.localRotation = o.lr;

                var sr = g.GetComponent<SpriteRenderer>();
                if (o.hasSR && sr) sr.color = o.c;
            }

            mo.Clear();
            SceneView.RepaintAll();
        }

        void Update() => TickMultiScrub();

        void TickMultiScrub()
        {
            var gos = Selection.gameObjects;
            if (gos == null || gos.Length <= 1) return;

            int cf = -1;
            bool anyPreview = false;
            bool anyPlaying = false;

            for (int i = 0; i < gos.Length; i++)
            {
                var g = gos[i];
                if (!g) continue;

                var pp = g.GetComponent<DynamicPreview>();
                if (!pp) continue;

                anyPreview = true;
                anyPlaying |= pp.IsPlaying;
                cf = Mathf.Max(cf, pp.CurrentFrame);
            }

            if (!anyPreview || !anyPlaying || cf < 0) return;

            if (cf != f)
            {
                f = cf;
                Repaint();
                SceneView.RepaintAll();
            }
        }

        void EditorTick() { if (p && p.IsPlaying) Repaint(); }

        void CacheOriginal()
        {
            if (!d) return;
            boundGO = d.gameObject;
            var t = d.transform;
            oLP = t.localPosition;
            oLS = t.localScale;
            oLR = t.localRotation;
            var sr = d.GetComponent<SpriteRenderer>();
            oHasSR = sr;
            oC = sr ? sr.color : Color.white;
        }

        void RestoreOriginal()
        {
            if (!boundGO) return;
            var t = boundGO.transform;
            t.localPosition = oLP;
            t.localScale = oLS;
            t.localRotation = oLR;
            var sr = boundGO.GetComponent<SpriteRenderer>();
            if (oHasSR && sr) sr.color = oC;
            boundGO = null;
        }

        void Bind(GameObject[] gos)
        {
            boundGos = gos;
            bool multi = gos != null && gos.Length > 1;

            var go = Selection.activeGameObject;
            d = go ? go.GetComponent<DynamicTimelineData>() : null;
            p = go ? go.GetComponent<DynamicPreview>() : null;

            if (go && !d) d = go.AddComponent<DynamicTimelineData>();
            if (go && !p) p = go.AddComponent<DynamicPreview>();

            if (p) p.data = d;
            if (p) p.useCustomEase = customEase;

            if (!multi)
            {
                if (d)
                {
                    d.Sort();
                    f = Mathf.Clamp(f, 0, d.totalFrames);
                    scrollF = Mathf.Clamp(scrollF, 0, Mathf.Max(0, d.totalFrames - 1));
                    CacheOriginal();

                    RefreshDTList();
                    curDTName = (dtNames != null && dtNames.Length > 0) ? dtNames[dtPick] : "NewTransform";
                    LoadSelectedDTIntoTimeline();
                }

                selF.Clear();
                moving = scrubbing = false;
                Repaint();
                return;
            }

            CacheOriginalMulti(gos);

            boundGO = null;
            selF.Clear();
            moving = scrubbing = false;

            int maxEnd = 1;
            for (int i = 0; i < gos.Length; i++)
            {
                var g = gos[i];
                if (!g) continue;

                var dd = g.GetComponent<DynamicTimelineData>(); if (!dd) dd = g.AddComponent<DynamicTimelineData>();
                var pp = g.GetComponent<DynamicPreview>(); if (!pp) pp = g.AddComponent<DynamicPreview>();
                pp.data = dd; pp.useCustomEase = customEase;

                var names = GetDTNames(g);
                int pick = Mathf.Clamp(SessionState.GetInt("DynEdPick_" + g.GetInstanceID(), 0), 0, Mathf.Max(0, names.Length - 1));
                SessionState.SetInt("DynEdPick_" + g.GetInstanceID(), pick);

                if (names.Length > 0) LoadGOTransformIntoTimeline(g, dd, names[pick]);
                maxEnd = Mathf.Max(maxEnd, dd ? dd.totalFrames : 1);
            }

            f = Mathf.Clamp(f, 0, maxEnd);
            scrollF = Mathf.Clamp(scrollF, 0, Mathf.Max(0, maxEnd - 1));
            Repaint();
        }

        void LoadGOTransformIntoTimeline(GameObject go, DynamicTimelineData dd, string name)
        {
            if (!go || !dd) return;

            var dts = go.GetComponents<DynamicTransform>();
            DynamicTransform src = null;
            for (int i = 0; i < dts.Length; i++)
                if (dts[i] && (string.IsNullOrEmpty(dts[i].transformationName) ? "NewTransform" : dts[i].transformationName) == name) { src = dts[i]; break; }

            if (!src)
            {
                dd.keys.Clear();
                dd.transformationName = name;
                var k0 = dd.Snapshot(0);
                dd.Upsert(k0.f, k0.lp, k0.ls, k0.z, k0.c, k0.support, true);
                dd.totalFrames = Mathf.Max(1, dd.totalFrames);
                return;
            }

            dd.LoadFromDynamicTransform(src, useCustomEase: customEase, clearExisting: true);
            EditorUtility.SetDirty(dd);
        }

        int GetMultiMaxEnd(GameObject[] gos)
        {
            int m = 1;
            for (int i = 0; i < gos.Length; i++)
            {
                var dd = gos[i] ? gos[i].GetComponent<DynamicTimelineData>() : null;
                if (dd) m = Mathf.Max(m, dd.totalFrames);
            }
            return m;
        }

        bool AnyMultiPlaying(GameObject[] gos)
        {
            for (int i = 0; i < gos.Length; i++)
            {
                var pp = gos[i] ? gos[i].GetComponent<DynamicPreview>() : null;
                if (pp && pp.IsPlaying) return true;
            }
            return false;
        }

        void OnGUI()
        {
            var gos = Selection.gameObjects;

            if (gos == null || gos.Length == 0)
            {
                EditorGUILayout.HelpBox("Select a GameObject.", MessageType.Info);
                return;
            }

            if (boundGos == null)
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox($"You have selected {gos.Length} GameObject(s).\nClick below to edit them in Dynamic Editor.", MessageType.Info);
                GUILayout.Space(5);
                if (GUILayout.Button("Edit Selected GameObject(s)", GUILayout.Height(30)))
                {
                    Bind(gos);
                }
                return;
            }

            if (!Selection.activeGameObject || !d)
            {
                EditorGUILayout.HelpBox("Select a GameObject.", MessageType.Info);
                return;
            }

            bool multi = gos.Length > 1;

            HandleHotkeys();

            if (!multi && p && p.IsPlaying) f = Mathf.Clamp(p.CurrentFrame, 0, d.totalFrames);

            EditorGUILayout.BeginHorizontal();

            if (!multi) DrawTransformSelectorRow();
            else
            {
                GUILayout.Label("Multi Preview", GUILayout.Width(90));
                GUILayout.Label($"({gos.Length} objects)", GUILayout.Width(90));
            }

            GUILayout.FlexibleSpace();

            bool newCustom = GUILayout.Toggle(customEase, "Custom Ease", GUILayout.Width(95));
            if (newCustom != customEase)
            {
                customEase = newCustom;
                if (p) p.useCustomEase = customEase;
                if (multi)
                    for (int i = 0; i < gos.Length; i++)
                    {
                        var pp = gos[i] ? gos[i].GetComponent<DynamicPreview>() : null;
                        if (pp) pp.useCustomEase = customEase;
                    }
                SceneView.RepaintAll();
            }

            onion = GUILayout.Toggle(onion, "Onion", GUILayout.Width(60));
            onionA = GUILayout.HorizontalSlider(onionA, 0.05f, 0.6f, GUILayout.Width(90));
            zoom = GUILayout.HorizontalSlider(zoom, 0.25f, 6f, GUILayout.Width(160));

            EditorGUILayout.EndHorizontal();

            // Frame row 
            int endFrames = multi ? GetMultiMaxEnd(gos) : d.totalFrames;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("<<", GUILayout.Width(36))) SetF(f - 1);
            if (GUILayout.Button("<", GUILayout.Width(28))) SetF(f - 5);
            GUILayout.Label("Frame", GUILayout.Width(40));
            int nf = EditorGUILayout.IntField(f, GUILayout.Width(70));
            if (nf != f) SetF(nf);
            GUILayout.Label("/" + endFrames, GUILayout.Width(70));
            if (GUILayout.Button(">", GUILayout.Width(28))) SetF(f + 1);
            if (GUILayout.Button(">>", GUILayout.Width(36))) SetF(f + 5);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Controls row
            EditorGUILayout.BeginHorizontal();

            if (!multi)
            {
                GUILayout.Label("End", GUILayout.Width(30));
                int end = EditorGUILayout.IntField(d.totalFrames, GUILayout.Width(80));
                if (end != d.totalFrames)
                {
                    Undo.RecordObject(d, "Change Timeline End");
                    d.totalFrames = Mathf.Max(1, end);
                    f = Mathf.Clamp(f, 0, d.totalFrames);
                    scrollF = Mathf.Clamp(scrollF, 0, Mathf.Max(0, d.totalFrames - 1));
                    EditorUtility.SetDirty(d);
                }
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    if (EditorUtility.DisplayDialog("Clear Timeline", "Clear all keyframes in this timeline?", "Yes", "No"))
                    {
                        Undo.RegisterCompleteObjectUndo(d, "Clear Timeline");
                        d.keys.Clear();
                        selF.Clear();
                        f = 0;
                        EditorUtility.SetDirty(d);
                        if (p) p.ApplyFrame(0);
                    }
                }

                if (GUILayout.Button("Add KF", GUILayout.Width(70)))
                {
                    Undo.RecordObject(d, "Add Keyframe");
                    var k = d.Snapshot(f);
                    d.Upsert(k.f, k.lp, k.ls, k.z, k.c, k.support, true);
                    EditorUtility.SetDirty(d);
                }

                if (GUILayout.Button("Del KF", GUILayout.Width(70))) DeleteSelectedOrCurrent();

                if (GUILayout.Button((p && p.IsPlaying) ? "Pause" : "Play", GUILayout.Width(60))) { if (p) p.TogglePlay(); }
            }
            else
            {
                GUILayout.Label("End", GUILayout.Width(30));
                EditorGUILayout.IntField(endFrames, GUILayout.Width(80));
                GUILayout.FlexibleSpace();

                bool any = AnyMultiPlaying(gos);
                if (GUILayout.Button(any ? "Pause" : "Play", GUILayout.Width(60)))
                {
                    for (int i = 0; i < gos.Length; i++)
                    {
                        var pp = gos[i] ? gos[i].GetComponent<DynamicPreview>() : null;
                        if (!pp) continue;
                        if (!any) pp.ApplyFrame(Mathf.Clamp(f, 0, pp.data ? pp.data.totalFrames : endFrames));
                        if (pp.IsPlaying != !any) pp.TogglePlay();
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            var r = GUILayoutUtility.GetRect(position.width - 10, 140);

            if (!multi) DrawTimeline(r);
            else DrawTimelineMulti(r, endFrames);

            float pxPerFrame = FramesToPPF(r, zoom);
            float visible = Mathf.Max(1, (r.width - 24) / pxPerFrame);
            float maxScroll = Mathf.Max(0, endFrames - visible);
            float newScroll = GUILayout.HorizontalScrollbar(scrollF, visible, 0, endFrames);
            if (!Mathf.Approximately(newScroll, scrollF)) scrollF = Mathf.Clamp(newScroll, 0, maxScroll);

            GUILayout.Space(6);

            if (!multi && d.Has(f, out var idx))
            {
                var k = d.keys[idx];

                if (!customEase)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Ease", GUILayout.Width(50));

                    var newEase = (EasePreset)EditorGUILayout.EnumPopup(k.ease, GUILayout.Width(120));
                    if (newEase != k.ease)
                    {
                        Undo.RecordObject(d, "Change Ease");
                        k.ease = newEase;
                        if (newEase != EasePreset.Custom) k.support = EasePresetUtil.ToSupport(newEase);
                        d.keys[idx] = k; d.Sort(); EditorUtility.SetDirty(d);
                        if (p) p.ApplyFrame(f);
                    }
                    EditorGUILayout.EndHorizontal();

                    if (k.ease == EasePreset.Custom)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label("Support", GUILayout.Width(50));
                        var ns = EditorGUILayout.Vector2Field("", k.support, GUILayout.Width(220));
                        if (ns != k.support)
                        {
                            Undo.RecordObject(d, "Edit Support");
                            k.support = ns;
                            d.keys[idx] = k; d.Sort(); EditorUtility.SetDirty(d);
                            if (p) p.ApplyFrame(f);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Support", GUILayout.Width(50));
                    EditorGUILayout.LabelField($"({k.support.x:0.00}, {k.support.y:0.00})");
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (multi)
            {
                float h = Mathf.Min(260f, position.height * 0.35f);

                _multiListScroll = EditorGUILayout.BeginScrollView(_multiListScroll, GUILayout.Height(h));
                DrawMultiPickList(gos);
                EditorGUILayout.EndScrollView();
            }
        }

        void RefreshDTList()
        {
            if (!d) { dtNames = new[] { "NewTransform" }; dtPick = 0; curDTName = "NewTransform"; return; }
            var dts = d.gameObject.GetComponents<DynamicTransform>();
            if (dts == null || dts.Length == 0) { dtNames = new[] { "NewTransform" }; dtPick = 0; curDTName = "NewTransform"; return; }
            dtNames = dts.Select(x => string.IsNullOrEmpty(x.transformationName) ? "NewTransform" : x.transformationName).Distinct().ToArray();
            if (dtNames == null || dtNames.Length == 0) { dtNames = new[] { "NewTransform" }; dtPick = 0; curDTName = "NewTransform"; return; }
            int idx = System.Array.IndexOf(dtNames, curDTName);
            dtPick = idx >= 0 ? idx : 0;
            curDTName = dtNames[dtPick];
        }

        void DrawTransformSelectorRow()
        {
            if (dtNames == null || dtNames.Length == 0) RefreshDTList();

            GUILayout.Label("Transform", GUILayout.Width(65));

            int np = EditorGUILayout.Popup(dtPick, dtNames, GUILayout.Width(180));
            if (np != dtPick)
            {
                dtPick = np;
                curDTName = (dtNames != null && dtNames.Length > 0) ? dtNames[dtPick] : "NewTransform";
                LoadSelectedDTIntoTimeline();
            }

            if (GUILayout.Button("+", GUILayout.Width(24)))
            {
                curDTName = "NewTransform";
                dtPick = 0;
                dtNames = new[] { "NewTransform" }.Concat(dtNames ?? System.Array.Empty<string>()).Distinct().ToArray();
                ClearTimelineForNewTransform();
            }

            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                RemoveSelectedTransformComponent();
                RefreshDTList();
                LoadSelectedDTIntoTimeline();
            }

            if (GUILayout.Button("Save", GUILayout.Width(60)))
            {
                SaveSelectedTransform();
                RefreshDTList();
            }
        }

        void ClearTimelineForNewTransform()
        {
            if (!d) return;

            Undo.RegisterCompleteObjectUndo(d, "New Transform Timeline");
            d.transformationName = "NewTransform";
            d.keys.Clear();

            var k0 = d.Snapshot(0);
            d.Upsert(k0.f, k0.lp, k0.ls, k0.z, k0.c, k0.support, true);

            d.totalFrames = Mathf.Max(1, d.totalFrames);
            f = 0;
            selF.Clear();
            EditorUtility.SetDirty(d);
            if (p) p.ApplyFrame(0);
        }

        void LoadSelectedDTIntoTimeline()
        {
            if (!d) return;

            var go = d.gameObject;
            var dts = go.GetComponents<DynamicTransform>();

            DynamicTransform src = null;
            for (int i = 0; i < dts.Length; i++)
                if (dts[i] && N(dts[i]) == curDTName) { src = dts[i]; break; }

            if (!src)
            {
                ClearTimelineForNewTransform();
                d.transformationName = curDTName;
                return;
            }

            Undo.RegisterCompleteObjectUndo(d, "Load Transform");
            d.LoadFromDynamicTransform(src, useCustomEase: customEase, clearExisting: true);

            f = Mathf.Clamp(f, 0, d.totalFrames);
            scrollF = Mathf.Clamp(scrollF, 0, Mathf.Max(0, d.totalFrames - 1));
            selF.Clear();
            EditorUtility.SetDirty(d);
            if (p) p.ApplyFrame(Mathf.Clamp(f, 0, d.totalFrames));
        }

        void SaveSelectedTransform()
        {
            if (!d) return;

            string name = string.IsNullOrEmpty(curDTName) ? "NewTransform" : curDTName;

            Undo.RecordObject(d, "Save Transform");
            d.transformationName = name;

            // bake into named DT
            d.BakeToDynamicTransform(d.gameObject, name, clear: true, useCustomEase: customEase);
            curDTName = name;
        }

        void RemoveSelectedTransformComponent()
        {
            if (!d) return;

            var go = d.gameObject;
            var dts = go.GetComponents<DynamicTransform>();
            DynamicTransform target = null;

            for (int i = 0; i < dts.Length; i++)
                if (dts[i] && N(dts[i]) == curDTName) { target = dts[i]; break; }

            if (!target) return;

            Undo.DestroyObjectImmediate(target);
            curDTName = "NewTransform";
        }

        // -- Onion Skin --

        static readonly Dictionary<Sprite, Mesh> smesh = new();
        static Material smat;

        void OnSceneGUI(SceneView sv)
        {
            if (!d || !p) return;
            p.useCustomEase = customEase;
            if (onion) { d.Sort(); int prev = PrevKey(f), next = NextKey(f); if (prev >= 0) DrawGhostAll(prev, onionA); if (next >= 0) DrawGhostAll(next, onionA); }
            if (customEase) DynamicHandle.DrawPrevNextBezierAndSupport(d, p, f, true);
            sv.Repaint();
        }

        int PrevKey(int fr) { int best = -1; for (int i = 0; i < d.keys.Count; i++) { int kf = d.keys[i].f; if (kf < fr && kf > best) best = kf; } return best; }
        int NextKey(int fr) { int best = int.MaxValue; for (int i = 0; i < d.keys.Count; i++) { int kf = d.keys[i].f; if (kf > fr && kf < best) best = kf; } return best == int.MaxValue ? -1 : best; }

        void DrawGhostAll(int fr, float a)
        {
            var root = d.transform;

            // eval root animation at frame
            p.Eval(fr, out var lp, out var ls, out var z, out var rootCol, assumeSorted: true);

            var parentW = root.parent ? root.parent.localToWorldMatrix : Matrix4x4.identity;
            var evalRootW = parentW * Matrix4x4.TRS(lp, Quaternion.Euler(0, 0, z), ls);
            var curRootW = root.localToWorldMatrix;
            var delta = evalRootW * curRootW.inverse;

            if (!smat) smat = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };

            var srs = d.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < srs.Length; i++)
            {
                var sr = srs[i];
                var sp = sr.sprite;
                if (!sp) continue;

                var m = delta * sr.transform.localToWorldMatrix;
                var c = (sr.transform == root) ? rootCol : sr.color;
                c.a *= a;

                smat.mainTexture = sp.texture;
                smat.color = c;
                smat.SetPass(0);

                Graphics.DrawMeshNow(SpriteMesh(sp), m);
            }
        }

        static Mesh SpriteMesh(Sprite sp)
        {
            if (smesh.TryGetValue(sp, out var m) && m) return m;

            m = new Mesh { name = "SM_" + sp.GetInstanceID(), hideFlags = HideFlags.HideAndDontSave };
            var v2 = sp.vertices;
            var uv = sp.uv;
            var tri = sp.triangles;

            var v3 = new Vector3[v2.Length];
            for (int i = 0; i < v2.Length; i++) v3[i] = v2[i];

            m.vertices = v3;
            m.uv = uv;
            var t = new int[tri.Length];
            for (int i = 0; i < tri.Length; i++) t[i] = tri[i];
            m.triangles = t;
            m.RecalculateBounds();

            smesh[sp] = m;
            return m;
        }

        void HandleHotkeys()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;

            if (e.control && e.keyCode == KeyCode.C) { CopySelected(); e.Use(); }
            if (e.control && e.keyCode == KeyCode.V) { PasteAtFrame(f); e.Use(); }
            if (e.keyCode == KeyCode.Delete) { DeleteSelectedOrCurrent(); e.Use(); }
            if (e.keyCode == KeyCode.Space)
            {
                var gos = Selection.gameObjects;
                bool multi = gos != null && gos.Length > 1;

                if (!multi)
                {
                    if (p) p.TogglePlay();
                }
                else
                {
                    bool any = AnyMultiPlaying(gos);

                    for (int i = 0; i < gos.Length; i++)
                    {
                        var g = gos[i];
                        if (!g) continue;

                        var pp = g.GetComponent<DynamicPreview>();
                        if (!pp) continue;

                        if (!any)
                        {
                            var dd = pp.data ? pp.data : g.GetComponent<DynamicTimelineData>();
                            int end = dd ? dd.totalFrames : 1;
                            pp.ApplyFrame(Mathf.Clamp(f, 0, end));
                        }

                        if (pp.IsPlaying != !any) pp.TogglePlay();
                    }
                }

                e.Use();
                Repaint();
                return;
            }
        }

        void DeleteSelectedOrCurrent()
        {
            Undo.RecordObject(d, "Delete Keyframe(s)");
            if (selF.Count > 0)
            {
                for (int i = d.keys.Count - 1; i >= 0; i--) if (selF.Contains(d.keys[i].f)) d.keys.RemoveAt(i);
                selF.Clear(); d.Sort();
            }
            else d.DeleteAt(f);
            EditorUtility.SetDirty(d);
        }

        void CopySelected()
        {
            d.Sort();
            var frames = (selF.Count > 0 ? selF : new HashSet<int> { f }).Where(fr => d.Has(fr, out _)).OrderBy(fr => fr).ToList();
            if (frames.Count == 0) return;
            int baseF = frames[0];
            clip.Clear(); clipSpan = 0;
            foreach (var fr in frames)
            {
                d.Has(fr, out var idx);
                var k = d.keys[idx];
                clip.Add(new CK { df = fr - baseF, lp = k.lp, ls = k.ls, z = k.z, c = k.c, support = k.support });
                clipSpan = Mathf.Max(clipSpan, fr - baseF);
            }
        }

        void PasteAtFrame(int dstF)
        {
            if (clip == null || clip.Count == 0) return;
            Undo.RecordObject(d, "Paste Keyframe(s)");
            foreach (var ck in clip)
            {
                int fr = Mathf.Clamp(dstF + ck.df, 0, d.totalFrames);
                d.Upsert(fr, ck.lp, ck.ls, ck.z, ck.c, ck.support, true);
            }
            selF.Clear();
            foreach (var ck in clip) selF.Add(Mathf.Clamp(dstF + ck.df, 0, d.totalFrames));
            d.Sort();
            EditorUtility.SetDirty(d);
            if (p) p.ApplyFrame(Mathf.Clamp(dstF, 0, d.totalFrames));
        }

        static float FramesToPPF(Rect r, float zoom) { return Mathf.Max(2f, 10f * zoom); }
        void SetF(int nf)
        {
            var gos = Selection.gameObjects;
            bool multi = gos != null && gos.Length > 1;

            int endFrames = multi ? GetMultiMaxEnd(gos) : (d ? d.totalFrames : 1);
            f = Mathf.Clamp(nf, 0, endFrames);

            if (!multi) { if (p) p.ApplyFrame(f); return; }

            for (int i = 0; i < gos.Length; i++)
            {
                var g = gos[i];
                if (!g) continue;
                var pp = g.GetComponent<DynamicPreview>();
                if (!pp || !pp.data) continue;
                pp.ApplyFrame(Mathf.Clamp(f, 0, pp.data.totalFrames));
            }
        }
        int MouseToFrame(float mouseX, float innerX, float ppf) { return Mathf.Clamp(Mathf.RoundToInt(scrollF + (mouseX - innerX) / ppf), 0, d.totalFrames); }

        void DrawTimeline(Rect r)
        {
            GUI.Box(r, GUIContent.none);
            var inner = new Rect(r.x + 6, r.y + 18, r.width - 12, r.height - 24);
            float ppf = FramesToPPF(r, zoom);
            float visibleFrames = Mathf.Max(1, inner.width / ppf);
            float maxScroll = Mathf.Max(0, d.totalFrames - visibleFrames);
            scrollF = Mathf.Clamp(scrollF, 0, maxScroll);

            int marks = 10;
            for (int i = 0; i <= marks; i++)
            {
                float t = i / (float)marks; float x = inner.x + t * inner.width;
                Handles.color = new Color(1, 1, 1, 0.15f);
                Handles.DrawLine(new Vector3(x, inner.y), new Vector3(x, inner.y + inner.height));
                int fr = Mathf.RoundToInt(Mathf.Lerp(scrollF, scrollF + visibleFrames, t));
                GUI.Label(new Rect(x - 12, r.y + 2, 60, 16), fr.ToString(), EditorStyles.miniLabel);
            }

            float rowY = inner.y + inner.height * 0.5f;
            for (int i = 0; i < d.keys.Count; i++)
            {
                var k = d.keys[i];
                if (k.f < scrollF - 1 || k.f > scrollF + visibleFrames + 1) continue;
                float x = inner.x + (k.f - scrollF) * ppf;
                var kr = new Rect(x - 5, rowY - 8, 10, 16);

                bool sel = selF.Contains(k.f);
                var col = sel ? new Color(1f, 0.85f, 0.25f, 1f) : (k.f == f ? new Color(1f, 0.4f, 0.4f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f));
                EditorGUI.DrawRect(kr, col);

                HandleKFEvents(i, k.f, kr, inner, ppf);
            }

            float sx = inner.x + (f - scrollF) * ppf;
            Handles.color = Color.red;
            Handles.DrawLine(new Vector3(sx, inner.y), new Vector3(sx, inner.y + inner.height));

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && inner.Contains(e.mousePosition) && !moving)
            {
                scrubbing = true;
                SetF(MouseToFrame(e.mousePosition.x, inner.x, ppf));
                if (!e.control && !e.shift) selF.Clear();
                e.Use();
            }
            if (e.type == EventType.MouseDrag && scrubbing && !moving)
            {
                SetF(MouseToFrame(e.mousePosition.x, inner.x, ppf));
                e.Use();
            }
            if (e.type == EventType.MouseUp && scrubbing) { scrubbing = false; e.Use(); }
        }

        void DrawTimelineMulti(Rect r, int total)
        {
            GUI.Box(r, GUIContent.none);
            var inner = new Rect(r.x + 6, r.y + 18, r.width - 12, r.height - 24);
            float ppf = FramesToPPF(r, zoom);
            float visibleFrames = Mathf.Max(1, inner.width / ppf);
            float maxScroll = Mathf.Max(0, total - visibleFrames);
            scrollF = Mathf.Clamp(scrollF, 0, maxScroll);

            int marks = 10;
            for (int i = 0; i <= marks; i++)
            {
                float t = i / (float)marks; float x = inner.x + t * inner.width;
                Handles.color = new Color(1, 1, 1, 0.15f);
                Handles.DrawLine(new Vector3(x, inner.y), new Vector3(x, inner.y + inner.height));
                int fr = Mathf.RoundToInt(Mathf.Lerp(scrollF, scrollF + visibleFrames, t));
                GUI.Label(new Rect(x - 12, r.y + 2, 60, 16), fr.ToString(), EditorStyles.miniLabel);
            }

            float sx = inner.x + (f - scrollF) * ppf;
            Handles.color = Color.red;
            Handles.DrawLine(new Vector3(sx, inner.y), new Vector3(sx, inner.y + inner.height));

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && inner.Contains(e.mousePosition) && !moving)
            {
                scrubbing = true;
                SetF(MouseToFrame(e.mousePosition.x, inner.x, ppf));
                e.Use();
            }
            if (e.type == EventType.MouseDrag && scrubbing && !moving)
            {
                SetF(MouseToFrame(e.mousePosition.x, inner.x, ppf));
                e.Use();
            }
            if (e.type == EventType.MouseUp && scrubbing) { scrubbing = false; e.Use(); }
        }

        void DrawMultiPickList(GameObject[] gos)
        {
            for (int i = 0; i < gos.Length; i++)
            {
                var g = gos[i];
                if (!g) continue;

                var dd = g.GetComponent<DynamicTimelineData>(); if (!dd) dd = g.AddComponent<DynamicTimelineData>();
                var pp = g.GetComponent<DynamicPreview>(); if (!pp) pp = g.AddComponent<DynamicPreview>();
                pp.data = dd; pp.useCustomEase = customEase;

                var names = GetDTNames(g);
                int key = g.GetInstanceID();
                int pick = Mathf.Clamp(SessionState.GetInt("DynEdPick_" + key, 0), 0, Mathf.Max(0, names.Length - 1));

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label((i + 1).ToString(), GUILayout.Width(18));
                GUILayout.Label(g.name, GUILayout.Width(170));

                EditorGUI.BeginDisabledGroup(names.Length == 0);
                int np = (names.Length == 0) ? 0 : EditorGUILayout.Popup(pick, names, GUILayout.Width(200));
                EditorGUI.EndDisabledGroup();

                if (names.Length == 0) GUILayout.Label("(no DynamicTransform)", EditorStyles.miniLabel);

                EditorGUILayout.EndHorizontal();

                if (names.Length == 0) continue;

                if (np != pick)
                {
                    SessionState.SetInt("DynEdPick_" + key, np);
                    LoadGOTransformIntoTimeline(g, dd, names[np]);
                    if (pp) pp.ApplyFrame(Mathf.Clamp(f, 0, dd.totalFrames));
                }
            }
        }

        void HandleKFEvents(int idx, int frame, Rect kr, Rect inner, float ppf)
        {
            var e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0 && kr.Contains(e.mousePosition))
            {
                if (e.control) { if (selF.Contains(frame)) selF.Remove(frame); else selF.Add(frame); }
                else if (e.shift) selF.Add(frame);
                else { if (!selF.Contains(frame)) { selF.Clear(); selF.Add(frame); } }

                moving = true; scrubbing = false;
                dragStartFrame = MouseToFrame(e.mousePosition.x, inner.x, ppf);
                moveList = SelectedIndexList();
                SetF(frame);

                Undo.RegisterCompleteObjectUndo(d, "Move Keyframes");
                e.Use();
            }

            if (e.type == EventType.MouseDrag && moving && moveList.Count > 0)
            {
                int cur = MouseToFrame(e.mousePosition.x, inner.x, ppf);
                int delta = cur - dragStartFrame;

                for (int i = 0; i < moveList.Count; i++)
                {
                    var (id, of) = moveList[i];
                    var k = d.keys[id];
                    k.f = Mathf.Clamp(of + delta, 0, d.totalFrames);
                    d.keys[id] = k;
                }

                selF.Clear();
                for (int i = 0; i < moveList.Count; i++) selF.Add(d.keys[moveList[i].idx].f);

                f = cur;
                if (p) p.PreviewNoSort(Mathf.Clamp(f, 0, d.totalFrames));
                EditorUtility.SetDirty(d);
                e.Use();
            }

            if (e.type == EventType.MouseUp && moving)
            {
                var movedIdxSet = moveList.Select(x => x.idx).ToHashSet();

                var movedKeys = new List<dynamic>(moveList.Count);
                for (int i = 0; i < moveList.Count; i++)
                {
                    int idx2 = moveList[i].idx;
                    if (idx2 >= 0 && idx2 < d.keys.Count) movedKeys.Add(d.keys[idx2]);
                }

                var stationaryKeys = new List<dynamic>(d.keys.Count - movedIdxSet.Count);
                for (int i = 0; i < d.keys.Count; i++)
                    if (!movedIdxSet.Contains(i))
                        stationaryKeys.Add(d.keys[i]);

                // Merge by frame
                // If multiple moved land on the same frame, the later one in moveList wins.
                var byFrame = new Dictionary<int, dynamic>();

                for (int i = 0; i < stationaryKeys.Count; i++)
                {
                    var k = stationaryKeys[i];
                    byFrame[k.f] = k;
                }

                for (int i = 0; i < movedKeys.Count; i++)
                {
                    var k = movedKeys[i];
                    byFrame[k.f] = k; // overwrite
                }

                // rebuild list
                d.keys.Clear();
                foreach (var kv in byFrame.OrderBy(kv => kv.Key))
                    d.keys.Add(kv.Value);

                // selection becomes the moved frames
                selF.Clear();
                for (int i = 0; i < movedKeys.Count; i++) selF.Add(movedKeys[i].f);

                d.Sort();
                EditorUtility.SetDirty(d);
                moving = false;
                e.Use();
            }
        }

        List<(int idx, int origF)> SelectedIndexList()
        {
            var list = new List<(int, int)>();
            for (int i = 0; i < d.keys.Count; i++) if (selF.Contains(d.keys[i].f)) list.Add((i, d.keys[i].f));
            return list;
        }
    }
}
