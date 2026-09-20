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
        bool pendingMerge;
        bool moveUndoPushed;
        int dragStartFrame;
        List<(int idx, int origF)> moveList = new();
        static string N(DynamicTransform x) => (x && !string.IsNullOrEmpty(x.transformationName)) ? x.transformationName : "NewTransform";
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

        static readonly string[] kNoDT = { "-" };
        GameObject pendingPickGo;
        DynamicTimelineData pendingPickData;
        string pendingPickName;

        [MenuItem("Vectorier/Tools/Dynamic Editor", false, 26)] static void Open() { GetWindow<DynamicEditor>("Dynamic Editor"); }

        void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedo;
            Unbind();
        }

        void OnDisable()
        {
            Unbind();
            Selection.selectionChanged -= OnSelectionChanged;
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            ClearGhostCache();
        }

        void OnUndoRedo()
        {
            selF.Clear();
            moving = scrubbing = false;
            pendingMerge = false;
            moveList.Clear();

            if (d)
            {
                d.Sort();
                f = Mathf.Clamp(f, 0, d.totalFrames);
                scrollF = Mathf.Clamp(scrollF, 0, Mathf.Max(0, d.totalFrames - 1));
                if (p && d.keys.Count > 0) p.ApplyFrame(f);
            }

            Repaint();
            SceneView.RepaintAll();
        }

        void OnSelectionChanged()
        {
            Unbind();
            Repaint();
        }

        void Unbind()
        {
            StopAllPlayback();
            RestoreOriginalMulti();
            RestoreOriginal();
            boundGos = null;
            d = null;
            p = null;
            selF.Clear();
            moving = scrubbing = false;
            pendingMerge = false;
            moveList.Clear();
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

        void Update()
        {
            ApplyPendingPick();          // deferred scene mutation from the multi list
            TickMultiScrub();
            if (p && p.IsPlaying) Repaint();
        }

        void StopAllPlayback()
        {
            if (p && p.IsPlaying) p.TogglePlay();

            var gos = boundGos;
            if (gos == null) return;

            for (int i = 0; i < gos.Length; i++)
            {
                var g = gos[i];
                if (!g) continue;

                var pp = g.GetComponent<DynamicPreview>();
                if (pp && pp.IsPlaying) pp.TogglePlay();
            }
        }

        void TickMultiScrub()
        {
            var gos = boundGos;                       // no per-tick Selection.gameObjects allocation
            if (gos == null || gos.Length <= 1) return;

            int cf = -1;
            bool anyPreview = false, anyPlaying = false;

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
            gos = FilterSceneObjects(gos);
            if (gos.Length == 0) { boundGos = null; Repaint(); return; }

            boundGos = gos;
            bool multi = gos.Length > 1;

            var go = Selection.activeGameObject;
            if (!go || EditorUtility.IsPersistent(go) || !go.scene.IsValid()) go = gos[0];

            d = GetOrAdd<DynamicTimelineData>(go);
            p = GetOrAdd<DynamicPreview>(go);
            p.data = d;
            p.useCustomEase = customEase;

            selF.Clear();
            moving = scrubbing = false;
            pendingMerge = false;

            if (!multi)
            {
                d.Sort();
                f = Mathf.Clamp(f, 0, d.totalFrames);
                scrollF = Mathf.Clamp(scrollF, 0, Mathf.Max(0, d.totalFrames - 1));
                CacheOriginal();

                RefreshDTList();              // this already resolves curDTName
                LoadSelectedDTIntoTimeline();
                Repaint();
                return;
            }

            CacheOriginalMulti(gos);
            boundGO = null;

            int maxEnd = 1;
            for (int i = 0; i < gos.Length; i++)
            {
                var g = gos[i];
                if (!g) continue;

                var dd = GetOrAdd<DynamicTimelineData>(g);
                var pp = GetOrAdd<DynamicPreview>(g);
                pp.data = dd;
                pp.useCustomEase = customEase;

                var names = GetDTNames(g);
                int key = g.GetInstanceID();
                int pick = Mathf.Clamp(SessionState.GetInt("DynEdPick_" + key, 0), 0, Mathf.Max(0, names.Length - 1));
                SessionState.SetInt("DynEdPick_" + key, pick);

                if (names.Length > 0) LoadGOTransformIntoTimeline(g, dd, names[pick]);
                maxEnd = Mathf.Max(maxEnd, dd.totalFrames);
            }

            f = Mathf.Clamp(f, 0, maxEnd);
            scrollF = Mathf.Clamp(scrollF, 0, Mathf.Max(0, maxEnd - 1));
            Repaint();
        }

        static GameObject[] FilterSceneObjects(GameObject[] gos)
        {
            if (gos == null) return System.Array.Empty<GameObject>();

            var list = new List<GameObject>(gos.Length);
            for (int i = 0; i < gos.Length; i++)
            {
                var g = gos[i];
                if (!g) continue;
                if (EditorUtility.IsPersistent(g)) continue;   // prefab / model asset picked in Project view
                if (!g.scene.IsValid()) continue;
                list.Add(g);
            }
            return list.ToArray();
        }

        static T GetOrAdd<T>(GameObject go) where T : UnityEngine.Component
        {
            T c = go.GetComponent<T>();
            return (UnityEngine.Object)c ? c : Undo.AddComponent<T>(go);
        }

        void LoadGOTransformIntoTimeline(GameObject go, DynamicTimelineData dd, string name)
        {
            if (!go || !dd) return;

            Undo.RegisterCompleteObjectUndo(dd, "Load Transform");

            var dts = go.GetComponents<DynamicTransform>();
            DynamicTransform src = null;
            for (int i = 0; i < dts.Length; i++)
                if (dts[i] && N(dts[i]) == name) { src = dts[i]; break; }

            if (!src)
            {
                dd.keys.Clear();
                dd.transformationName = name;
                var k0 = dd.Snapshot(0);
                dd.Upsert(k0.f, k0.lp, k0.ls, k0.z, k0.c, k0.support, true);
                dd.totalFrames = Mathf.Max(1, dd.totalFrames);
                EditorUtility.SetDirty(dd);
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

            bool newOnion = GUILayout.Toggle(onion, "Onion", GUILayout.Width(60));
            float newOnionA = GUILayout.HorizontalSlider(onionA, 0.05f, 0.6f, GUILayout.Width(90));
            if (newOnion != onion || !Mathf.Approximately(newOnionA, onionA))
            {
                onion = newOnion;
                onionA = newOnionA;
                SceneView.RepaintAll();
            }
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
                if (GUILayout.Button(any ? "Pause" : "Play", GUILayout.Width(60))) TogglePlayAll();
            }

            EditorGUILayout.EndHorizontal();

            var r = GUILayoutUtility.GetRect(position.width - 10, 140);

            if (moving && Event.current.rawType == EventType.MouseUp) pendingMerge = true;

            DrawTimeline(r, endFrames, multi);

            if (pendingMerge) ApplyPendingMerge();

            float pxPerFrame = FramesToPPF(zoom);
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

            var names = new List<string>(dts.Length);
            for (int i = 0; i < dts.Length; i++)
            {
                if (!dts[i]) continue;
                string n = N(dts[i]);
                if (!names.Contains(n)) names.Add(n);
            }

            if (names.Count == 0)
            {
                if (string.IsNullOrEmpty(curDTName))
                    curDTName = string.IsNullOrEmpty(d.transformationName) ? "NewTransform" : d.transformationName;

                dtNames = new[] { curDTName };
                dtPick = 0;
                return;
            }

            dtNames = names.ToArray();
            int idx = System.Array.IndexOf(dtNames, curDTName);
            dtPick = Mathf.Clamp(idx >= 0 ? idx : 0, 0, dtNames.Length - 1);
            curDTName = dtNames[dtPick];
        }

        void DrawTransformSelectorRow()
        {
            if (dtNames == null || dtNames.Length == 0) RefreshDTList();

            GUILayout.Label("Transform", GUILayout.Width(65));

            Rect totalRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(180));
            Rect textRect = new Rect(totalRect.x, totalRect.y, totalRect.width - 20, totalRect.height);
            Rect btnRect = new Rect(totalRect.x + totalRect.width - 20, totalRect.y, 20, totalRect.height);

            GUI.SetNextControlName("TransformNameInput");
            string renamed = EditorGUI.DelayedTextField(textRect, curDTName);

            if (EditorGUI.DropdownButton(btnRect, GUIContent.none, FocusType.Passive, EditorStyles.popup))
            {
                GUI.FocusControl(null); // Unfocus text box when clicking dropdown
                GenericMenu menu = new GenericMenu();
                for (int i = 0; i < dtNames.Length; i++)
                {
                    int index = i;
                    menu.AddItem(new GUIContent(dtNames[i]), dtPick == index, () =>
                    {
                        GUI.FocusControl(null); // Unfocus text box on item select
                        dtPick = index;
                        curDTName = dtNames[index];
                        LoadSelectedDTIntoTimeline();
                    });
                }
                menu.DropDown(totalRect);
            }

            if (renamed != curDTName && !string.IsNullOrEmpty(renamed))
            {
                GUI.FocusControl(null); // Unfocus after confirming text
                RenameCurrentTransform(renamed);
            }

            if (GUILayout.Button("+", GUILayout.Width(24)))
            {
                GUI.FocusControl(null);
                AddNewTransformComponent();
            }

            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                GUI.FocusControl(null);
                RemoveSelectedTransformComponent();
                RefreshDTList();
                LoadSelectedDTIntoTimeline();
            }

            if (GUILayout.Button("Save", GUILayout.Width(60)))
            {
                GUI.FocusControl(null);
                SaveSelectedTransform();
                RefreshDTList();
            }
        }

        void RenameCurrentTransform(string targetName)
        {
            if (!d) return;

            string uniqueName = GetUniqueTransformName(targetName, curDTName);

            var dts = d.gameObject.GetComponents<DynamicTransform>();
            for (int i = 0; i < dts.Length; i++)
            {
                if (dts[i] && N(dts[i]) == curDTName)
                {
                    Undo.RecordObject(dts[i], "Rename Transform");
                    dts[i].transformationName = uniqueName;
                    EditorUtility.SetDirty(dts[i]);
                    break;
                }
            }

            Undo.RecordObject(d, "Rename Transform Timeline");
            d.transformationName = uniqueName;
            EditorUtility.SetDirty(d);

            curDTName = uniqueName;
            RefreshDTList();
        }

        string GetUniqueTransformName(string requestedName, string excludeCurrentName)
        {
            if (!d || !d.gameObject) return requestedName;

            var existingNames = d.gameObject.GetComponents<DynamicTransform>()
                .Where(dt => dt && N(dt) != excludeCurrentName)
                .Select(dt => N(dt))
                .ToHashSet();

            if (!existingNames.Contains(requestedName))
                return requestedName;

            string candidate = $"{requestedName}_copy";
            if (!existingNames.Contains(candidate))
                return candidate;

            int counter = 1;
            while (existingNames.Contains($"{requestedName}_copy_{counter}"))
            {
                counter++;
            }
            return $"{requestedName}_copy_{counter}";
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
            GUI.FocusControl(null); // Clear keyboard focus so text field refreshes visually
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

            var dt = d.BakeToDynamicTransform(d.gameObject, name, clear: true, useCustomEase: customEase);

            if (dt)
            {
                EditorUtility.SetDirty(dt);
                if (PrefabUtility.IsPartOfPrefabInstance(dt))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(dt);
                if (dt.gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(dt.gameObject.scene);
            }
            else
            {
                Debug.LogWarning($"[Dynamic Editor] Failed to bake transformation '{name}'.", d);
            }

            EditorUtility.SetDirty(d);
            curDTName = name;
        }

        void AddNewTransformComponent()
        {
            if (!d) return;

            string uniqueName = GetUniqueTransformName("NewTransform", null);

            var newDT = Undo.AddComponent<DynamicTransform>(d.gameObject);
            newDT.transformationName = uniqueName;

            Undo.RecordObject(d, "New Transform Timeline");
            d.transformationName = uniqueName;
            d.keys.Clear();

            var k0 = d.Snapshot(0);
            d.Upsert(k0.f, k0.lp, k0.ls, k0.z, k0.c, k0.support, true);
            d.totalFrames = Mathf.Max(1, d.totalFrames);
            f = 0;
            selF.Clear();

            d.BakeToDynamicTransform(newDT, clear: true, useCustomEase: customEase);

            EditorUtility.SetDirty(newDT);
            EditorUtility.SetDirty(d);

            if (p) p.ApplyFrame(0);

            curDTName = uniqueName;
            RefreshDTList();
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

            var e = Event.current;
            if (p.useCustomEase != customEase) p.useCustomEase = customEase;

            // DrawMeshNow
            if (onion && e.type == EventType.Repaint && d.keys != null && d.keys.Count > 0)
            {
                d.Sort();
                int prev = PrevKey(f), next = NextKey(f);
                if (prev >= 0) DrawGhostAll(prev, onionA);
                if (next >= 0) DrawGhostAll(next, onionA);
            }

            if (customEase) DynamicHandle.DrawPrevNextBezierAndSupport(d, p, f, true);

            // only force redraws while something is actually moving
            if (p.IsPlaying || scrubbing || moving) sv.Repaint();
        }

        int PrevKey(int fr) { int best = -1; for (int i = 0; i < d.keys.Count; i++) { int kf = d.keys[i].f; if (kf < fr && kf > best) best = kf; } return best; }
        int NextKey(int fr) { int best = int.MaxValue; for (int i = 0; i < d.keys.Count; i++) { int kf = d.keys[i].f; if (kf > fr && kf < best) best = kf; } return best == int.MaxValue ? -1 : best; }

        void DrawGhostAll(int fr, float a)
        {
            if (!d || !p) return;

            var root = d.transform;
            p.Eval(fr, out var lp, out var ls, out var z, out var rootCol, assumeSorted: true);

            var parentW = root.parent ? root.parent.localToWorldMatrix : Matrix4x4.identity;
            var evalRootW = parentW * Matrix4x4.TRS(lp, Quaternion.Euler(0f, 0f, z), ls);
            var delta = evalRootW * root.localToWorldMatrix.inverse;

            if (!smat)
            {
                var sh = Shader.Find("Sprites/Default");
                if (!sh) return;
                smat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }

            var srs = d.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < srs.Length; i++)
            {
                var sr = srs[i];
                if (!sr || !sr.enabled || !sr.gameObject.activeInHierarchy) continue;

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

        static void ClearGhostCache()
        {
            foreach (var kv in smesh) if (kv.Value) DestroyImmediate(kv.Value);
            smesh.Clear();
            if (smat) { DestroyImmediate(smat); smat = null; }
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
            if (e == null || e.type != EventType.KeyDown) return;

            // never steal keys from a focused text field (transform rename, frame/end IntFields)
            if (EditorGUIUtility.editingTextField) return;

            bool ctrl = e.control || e.command;

            if (ctrl)
            {
                if (e.keyCode == KeyCode.C) { CopySelected(); e.Use(); Repaint(); }
                else if (e.keyCode == KeyCode.V) { PasteAtFrame(f); e.Use(); Repaint(); }
                return;   // let Ctrl+S / Ctrl+Z pass through untouched
            }

            if (e.keyCode == KeyCode.Delete) { DeleteSelectedOrCurrent(); e.Use(); Repaint(); return; }
            if (e.keyCode == KeyCode.Space) { TogglePlayAll(); e.Use(); Repaint(); }
        }

        void TogglePlayAll()
        {
            var gos = boundGos ?? Selection.gameObjects;
            bool multi = gos != null && gos.Length > 1;

            if (!multi) { if (p) p.TogglePlay(); return; }

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
                    pp.ApplyFrame(Mathf.Clamp(f, 0, dd ? dd.totalFrames : 1));
                }
                if (pp.IsPlaying != !any) pp.TogglePlay();
            }
        }

        void DeleteSelectedOrCurrent()
        {
            if (!d) return;

            Undo.RegisterCompleteObjectUndo(d, "Delete Keyframe(s)");

            if (selF.Count > 0)
            {
                for (int i = d.keys.Count - 1; i >= 0; i--)
                    if (selF.Contains(d.keys[i].f)) d.keys.RemoveAt(i);
                selF.Clear();
            }
            else d.DeleteAt(f);

            d.Sort();
            EditorUtility.SetDirty(d);
            if (p && d.keys.Count > 0) p.ApplyFrame(Mathf.Clamp(f, 0, d.totalFrames));   // was left on the deleted pose
        }

        void CopySelected()
        {
            if (!d) return;
            d.Sort();

            var frames = new List<int>();
            if (selF.Count > 0) { foreach (var fr in selF) if (d.Has(fr, out _)) frames.Add(fr); }
            else if (d.Has(f, out _)) frames.Add(f);

            if (frames.Count == 0) return;
            frames.Sort();

            int baseF = frames[0];
            clip.Clear();
            for (int i = 0; i < frames.Count; i++)
            {
                d.Has(frames[i], out var idx);
                var k = d.keys[idx];
                clip.Add(new CK { df = frames[i] - baseF, lp = k.lp, ls = k.ls, z = k.z, c = k.c, support = k.support });
            }
        }

        void PasteAtFrame(int dstF)
        {
            if (!d || clip.Count == 0) return;

            Undo.RegisterCompleteObjectUndo(d, "Paste Keyframe(s)");

            selF.Clear();
            for (int i = 0; i < clip.Count; i++)
            {
                var ck = clip[i];
                int fr = Mathf.Clamp(dstF + ck.df, 0, d.totalFrames);
                d.Upsert(fr, ck.lp, ck.ls, ck.z, ck.c, ck.support, true);
                selF.Add(fr);                    // was a second full pass over clip
            }

            d.Sort();
            EditorUtility.SetDirty(d);
            if (p) p.ApplyFrame(Mathf.Clamp(dstF, 0, d.totalFrames));
        }

        static float FramesToPPF(float zoom) => Mathf.Max(2f, 10f * zoom);
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
        int MouseToFrame(float mouseX, float innerX, float ppf, int maxFrame) => Mathf.Clamp(Mathf.RoundToInt(scrollF + (mouseX - innerX) / ppf), 0, Mathf.Max(0, maxFrame));

        void DrawTimeline(Rect r, int total, bool multi)
        {
            GUI.Box(r, GUIContent.none);

            var inner = new Rect(r.x + 6f, r.y + 18f, r.width - 12f, r.height - 24f);
            float ppf = FramesToPPF(zoom);
            float visibleFrames = Mathf.Max(1f, inner.width / ppf);
            scrollF = Mathf.Clamp(scrollF, 0f, Mathf.Max(0f, total - visibleFrames));

            bool repaint = Event.current.type == EventType.Repaint;

            if (repaint)
            {
                const int marks = 10;
                Handles.color = new Color(1f, 1f, 1f, 0.15f);
                for (int i = 0; i <= marks; i++)
                {
                    float t = i / (float)marks;
                    float x = inner.x + t * inner.width;
                    Handles.DrawLine(new Vector3(x, inner.y), new Vector3(x, inner.y + inner.height));
                    int fr = Mathf.RoundToInt(Mathf.Lerp(scrollF, scrollF + visibleFrames, t));
                    GUI.Label(new Rect(x - 12f, r.y + 2f, 60f, 16f), fr.ToString(), EditorStyles.miniLabel);
                }
            }

            if (!multi && d && d.keys != null)
            {
                float rowY = inner.y + inner.height * 0.5f;
                for (int i = 0; i < d.keys.Count; i++)
                {
                    var k = d.keys[i];
                    if (k.f < scrollF - 1f || k.f > scrollF + visibleFrames + 1f) continue;

                    float x = inner.x + (k.f - scrollF) * ppf;
                    var kr = new Rect(x - 5f, rowY - 8f, 10f, 16f);

                    bool sel = selF.Contains(k.f);
                    EditorGUI.DrawRect(kr, sel
                        ? new Color(1f, 0.85f, 0.25f, 1f)
                        : (k.f == f ? new Color(1f, 0.4f, 0.4f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f)));

                    HandleKFEvents(i, k.f, kr, inner, ppf, total);
                    if (pendingMerge) break;   // d.keys is about to be rebuilt — stop walking it
                }
            }

            if (repaint)
            {
                float sx = inner.x + (f - scrollF) * ppf;
                Handles.color = Color.red;
                Handles.DrawLine(new Vector3(sx, inner.y), new Vector3(sx, inner.y + inner.height));
            }

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && inner.Contains(e.mousePosition) && !moving)
            {
                StopAllPlayback();

                scrubbing = true;
                SetF(MouseToFrame(e.mousePosition.x, inner.x, ppf, total));
                if (!multi && !e.control && !e.command && !e.shift) selF.Clear();
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && scrubbing && !moving)
            {
                SetF(MouseToFrame(e.mousePosition.x, inner.x, ppf, total));
                e.Use();
            }
            else if (e.type == EventType.MouseUp && scrubbing)
            {
                scrubbing = false;
                e.Use();
            }
        }

        void DrawMultiPickList(GameObject[] gos)
        {
            for (int i = 0; i < gos.Length; i++)
            {
                var g = gos[i];
                if (!g) continue;

                // components are guaranteed by Bind(); never AddComponent during OnGUI
                var dd = g.GetComponent<DynamicTimelineData>();

                var names = GetDTNames(g);
                bool has = names.Length > 0 && dd;
                int key = g.GetInstanceID();
                int pick = Mathf.Clamp(SessionState.GetInt("DynEdPick_" + key, 0), 0, Mathf.Max(0, names.Length - 1));

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label((i + 1).ToString(), GUILayout.Width(18));
                GUILayout.Label(g.name, GUILayout.Width(170));

                // identical control count on Layout and Repaint, whatever the data does
                using (new EditorGUI.DisabledScope(!has))
                {
                    int np = EditorGUILayout.Popup(has ? pick : 0, has ? names : kNoDT, GUILayout.Width(200));
                    if (has && np != pick)
                    {
                        SessionState.SetInt("DynEdPick_" + key, np);
                        pendingPickGo = g;
                        pendingPickData = dd;
                        pendingPickName = names[np];
                    }
                }

                GUILayout.Label(has ? string.Empty : "(no DynamicTransform)", EditorStyles.miniLabel, GUILayout.Width(150));
                EditorGUILayout.EndHorizontal();
            }
        }

        void ApplyPendingPick()
        {
            if (!pendingPickGo || !pendingPickData)
            {
                pendingPickGo = null; pendingPickData = null; pendingPickName = null;
                return;
            }

            LoadGOTransformIntoTimeline(pendingPickGo, pendingPickData, pendingPickName);

            var pp = pendingPickGo.GetComponent<DynamicPreview>();
            if (pp) pp.ApplyFrame(Mathf.Clamp(f, 0, pendingPickData.totalFrames));

            pendingPickGo = null; pendingPickData = null; pendingPickName = null;
            Repaint();
        }

        void HandleKFEvents(int idx, int frame, Rect kr, Rect inner, float ppf, int maxFrame)
        {
            if (!d) return;
            var e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0 && kr.Contains(e.mousePosition))
            {
                StopAllPlayback();

                bool additive = e.control || e.command;
                if (additive) { if (!selF.Remove(frame)) selF.Add(frame); }
                else if (e.shift) selF.Add(frame);
                else if (!selF.Contains(frame)) { selF.Clear(); selF.Add(frame); }

                moving = true;
                scrubbing = false;
                moveUndoPushed = false;
                dragStartFrame = MouseToFrame(e.mousePosition.x, inner.x, ppf, maxFrame);
                moveList = SelectedIndexList();
                SetF(frame);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDrag && moving && moveList.Count > 0)
            {
                int cur = MouseToFrame(e.mousePosition.x, inner.x, ppf, maxFrame);
                int delta = cur - dragStartFrame;

                // don't spam the undo stack for a plain selection click
                if (delta == 0 && !moveUndoPushed) { e.Use(); return; }
                if (!moveUndoPushed) { Undo.RegisterCompleteObjectUndo(d, "Move Keyframes"); moveUndoPushed = true; }

                selF.Clear();
                for (int i = 0; i < moveList.Count; i++)
                {
                    var (id, of) = moveList[i];
                    if (id < 0 || id >= d.keys.Count) continue;      // guard against an external re-sort

                    var k = d.keys[id];
                    k.f = Mathf.Clamp(of + delta, 0, d.totalFrames);
                    d.keys[id] = k;
                    selF.Add(k.f);
                }

                f = Mathf.Clamp(cur, 0, d.totalFrames);
                if (p) p.PreviewNoSort(f);
                EditorUtility.SetDirty(d);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp && moving)
            {
                pendingMerge = true;    // applied after the draw loop, never during it
                e.Use();
            }
        }

        void ApplyPendingMerge()
        {
            pendingMerge = false;
            moving = false;
            moveUndoPushed = false;

            if (!d || moveList.Count == 0) { moveList.Clear(); return; }

            var movedIdx = new HashSet<int>();
            for (int i = 0; i < moveList.Count; i++) movedIdx.Add(moveList[i].idx);

            var moved = new List<DynamicTimelineData.KF>(moveList.Count);
            for (int i = 0; i < moveList.Count; i++)
            {
                int id = moveList[i].idx;
                if (id >= 0 && id < d.keys.Count) moved.Add(d.keys[id]);
            }

            // merge by frame; a moved key wins over a stationary one on the same frame
            var byFrame = new Dictionary<int, DynamicTimelineData.KF>(d.keys.Count);
            for (int i = 0; i < d.keys.Count; i++)
                if (!movedIdx.Contains(i)) byFrame[d.keys[i].f] = d.keys[i];
            for (int i = 0; i < moved.Count; i++)
                byFrame[moved[i].f] = moved[i];

            d.keys.Clear();
            foreach (var kv in byFrame.OrderBy(kv => kv.Key)) d.keys.Add(kv.Value);

            selF.Clear();
            for (int i = 0; i < moved.Count; i++) selF.Add(moved[i].f);

            moveList.Clear();
            d.Sort();
            EditorUtility.SetDirty(d);
            if (p && d.keys.Count > 0) p.ApplyFrame(Mathf.Clamp(f, 0, d.totalFrames));
            Repaint();
        }

        List<(int idx, int origF)> SelectedIndexList()
        {
            var list = new List<(int, int)>();
            for (int i = 0; i < d.keys.Count; i++) if (selF.Contains(d.keys[i].f)) list.Add((i, d.keys[i].f));
            return list;
        }
    }
}
