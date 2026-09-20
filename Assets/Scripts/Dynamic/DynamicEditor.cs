using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vectorier.Dynamic
{
    public class DynamicEditor : EditorWindow
    {
        class Track
        {
            public GameObject go;
            public DynamicTimelineData d;
            public DynamicPreview p;

            public readonly HashSet<int> selF = new();

            public bool scrubbing, moving, pendingMerge, moveUndoPushed;
            public int dragStartFrame;
            public readonly List<(int idx, int origF)> moveList = new();

            public string[] dtNames = Array.Empty<string>();
            public int dtPick;
            public string curDTName = "NewTransform";
            public bool expanded = true;

            // pose before we touched anything, put back on unbind
            public Vector3 oLP, oLS;
            public Quaternion oLR;
            public Color oC;
            public bool oHasSR;
        }

        readonly List<Track> tracks = new();
        int active;

        // shared across every track
        int f;                  // master playhead, tracks clamp to their own end
        float scrollF;
        bool onion;
        float onionA = 0.25f;
        bool customEase;
        float zoom = 1f;
        Vector2 scroll;
        float lastTimelineW = 400f;

        // master clock, drives every track from one place so they can't drift apart
        bool playing;
        double lastT, carry;

        bool pendingBind;

        struct CK { public int df; public Vector3 lp, ls; public float z; public Color c; public Vector2 support; }
        static readonly List<CK> clip = new();

        const float kTimelineH = 120f;
        static string N(DynamicTransform x) => (x && !string.IsNullOrEmpty(x.transformationName)) ? x.transformationName : "NewTransform";

        [MenuItem("Vectorier/Tools/Dynamic Editor", false, 26)]
        static void Open() { GetWindow<DynamicEditor>("Dynamic Editor"); }

        void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedo;
            tracks.Clear();
            active = 0;
            f = 0;
            playing = false;
        }

        void OnDisable()
        {
            UnbindAll();
            Selection.selectionChanged -= OnSelectionChanged;
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            ClearGhostCache();
        }

        void Update()
        {
            if (pendingBind) { pendingBind = false; BindSelection(); Repaint(); }

            PruneDeadTracks();
            TickPlayback();
        }

        void OnUndoRedo()
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                var t = tracks[i];
                if (!t.d) continue;

                t.selF.Clear();
                t.moving = t.scrubbing = false;
                t.pendingMerge = false;
                t.moveList.Clear();
                t.d.Sort();
            }

            SetFrame(f);
            Repaint();
            SceneView.RepaintAll();
        }

        void OnSelectionChanged()
        {
            var go = Selection.activeGameObject;
            if (go)
            {
                int i = IndexOf(go);
                if (i >= 0) active = i;
            }
            Repaint();
        }

        // ================= Binding ================= //

        void BindSelection()
        {
            var gos = FilterSceneObjects(Selection.gameObjects);
            if (gos.Length == 0) { UnbindAll(); return; }

            StopAll();

            var old = new Dictionary<int, Track>();
            for (int i = 0; i < tracks.Count; i++)
                if (tracks[i].go) old[tracks[i].go.GetInstanceID()] = tracks[i];

            var rebuilt = new List<Track>(gos.Length);
            for (int i = 0; i < gos.Length; i++)
            {
                int id = gos[i].GetInstanceID();
                if (old.TryGetValue(id, out var keep)) { old.Remove(id); rebuilt.Add(keep); continue; }
                rebuilt.Add(MakeTrack(gos[i]));
            }

            // anything dropped from the selection goes back to its original pose
            foreach (var kv in old) RestoreTrack(kv.Value);

            tracks.Clear();
            tracks.AddRange(rebuilt);

            var act = Selection.activeGameObject;
            active = act ? Mathf.Max(0, IndexOf(act)) : 0;
            active = Mathf.Clamp(active, 0, Mathf.Max(0, tracks.Count - 1));

            SetFrame(0);
        }

        Track MakeTrack(GameObject go)
        {
            var t = new Track { go = go };

            t.d = GetOrAdd<DynamicTimelineData>(go);
            t.p = GetOrAdd<DynamicPreview>(go);
            t.p.data = t.d;
            t.p.useCustomEase = customEase;

            // the window owns playback never let a preview run its own clock
            if (t.p.IsPlaying) t.p.TogglePlay();

            CacheOriginal(t);

            t.d.Sort();
            RefreshDTList(t);
            LoadSelectedDT(t);
            return t;
        }

        void UnbindAll()
        {
            StopAll();
            for (int i = 0; i < tracks.Count; i++) RestoreTrack(tracks[i]);
            tracks.Clear();
            active = 0;
            SceneView.RepaintAll();
        }

        void PruneDeadTracks()
        {
            bool changed = false;
            for (int i = tracks.Count - 1; i >= 0; i--)
                if (!tracks[i].go) { tracks.RemoveAt(i); changed = true; }

            if (!changed) return;
            active = Mathf.Clamp(active, 0, Mathf.Max(0, tracks.Count - 1));
            if (tracks.Count == 0) playing = false;
            Repaint();
        }

        void CacheOriginal(Track t)
        {
            if (!t.go) return;

            var tr = t.go.transform;
            t.oLP = tr.localPosition;
            t.oLS = tr.localScale;
            t.oLR = tr.localRotation;

            var sr = t.go.GetComponent<SpriteRenderer>();
            t.oHasSR = sr;
            t.oC = sr ? sr.color : Color.white;
        }

        void RestoreTrack(Track t)
        {
            if (t == null || !t.go) return;
            if (t.p && t.p.IsPlaying) t.p.TogglePlay();

            var tr = t.go.transform;
            tr.localPosition = t.oLP;
            tr.localScale = t.oLS;
            tr.localRotation = t.oLR;

            var sr = t.go.GetComponent<SpriteRenderer>();
            if (t.oHasSR && sr) sr.color = t.oC;
        }

        int IndexOf(GameObject go)
        {
            for (int i = 0; i < tracks.Count; i++) if (tracks[i].go == go) return i;
            return -1;
        }

        Track ActiveTrack() => (active >= 0 && active < tracks.Count) ? tracks[active] : null;

        static GameObject[] FilterSceneObjects(GameObject[] gos)
        {
            if (gos == null) return Array.Empty<GameObject>();

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

        bool SelectionMatchesTracks()
        {
            var sel = FilterSceneObjects(Selection.gameObjects);
            if (sel.Length != tracks.Count) return false;
            for (int i = 0; i < sel.Length; i++) if (IndexOf(sel[i]) < 0) return false;
            return true;
        }

        // ================= Frame & playback ================= //

        // longest track decides how far the shared playhead can travel
        int MaxEnd()
        {
            int m = 1;
            for (int i = 0; i < tracks.Count; i++)
                if (tracks[i].d) m = Mathf.Max(m, tracks[i].d.totalFrames);
            return m;
        }

        // a track that ends early just sits on its last frame
        int TrackFrame(Track t) => t.d ? Mathf.Min(f, t.d.totalFrames) : 0;

        int MasterFps()
        {
            int fps = 0;
            for (int i = 0; i < tracks.Count; i++)
                if (tracks[i].d && tracks[i].d.fps > 0) fps = Mathf.Max(fps, tracks[i].d.fps);
            return fps > 0 ? fps : 60;
        }

        void SetFrame(int nf)
        {
            f = Mathf.Clamp(nf, 0, MaxEnd());
            ApplyFrameToAll(null);
            SceneView.RepaintAll();
        }

        void ApplyFrameToAll(Track noSortFor)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                var t = tracks[i];
                if (!t.p || !t.d || t.d.keys.Count == 0) continue;

                if (t == noSortFor) t.p.PreviewNoSort(TrackFrame(t));
                else t.p.ApplyFrame(TrackFrame(t));
            }
        }

        void TogglePlay()
        {
            if (tracks.Count == 0) return;

            playing = !playing;
            lastT = EditorApplication.timeSinceStartup;
            carry = 0;

            if (playing && f >= MaxEnd()) SetFrame(0);
            Repaint();
        }

        void StopAll()
        {
            playing = false;
            for (int i = 0; i < tracks.Count; i++)
            {
                var t = tracks[i];
                if (t.p && t.p.IsPlaying) t.p.TogglePlay();   // safety, in case a preview was left running
            }
        }

        void TickPlayback()
        {
            if (!playing) { lastT = EditorApplication.timeSinceStartup; return; }
            if (tracks.Count == 0) { playing = false; return; }

            double now = EditorApplication.timeSinceStartup;
            double dt = now - lastT;
            lastT = now;
            if (dt <= 0) return;

            carry += dt * MasterFps();
            int adv = (int)carry;
            if (adv <= 0) return;
            carry -= adv;

            int last = MaxEnd();
            int nf = f + adv;

            if (nf >= last) { f = last; playing = false; }
            else f = nf;

            ApplyFrameToAll(null);
            Repaint();
            SceneView.RepaintAll();
        }

        void Rewind()
        {
            StopAll();
            SetFrame(0);
        }

        // ================= Window GUI ================= //

        void OnGUI()
        {
            DrawTopBar();

            if (tracks.Count == 0) { DrawBindPrompt(); return; }

            DrawTransportBar();
            HandleHotkeys();

            if (!SelectionMatchesTracks()) DrawRebindStrip();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < tracks.Count; i++) DrawTrack(tracks[i], i);
            GUILayout.Space(6);
            EditorGUILayout.EndScrollView();

            DrawSharedScrollbar();
        }

        void DrawTopBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            bool newCustom = GUILayout.Toggle(customEase, "Custom Ease", EditorStyles.toolbarButton, GUILayout.Width(90));
            if (newCustom != customEase)
            {
                customEase = newCustom;
                for (int i = 0; i < tracks.Count; i++) if (tracks[i].p) tracks[i].p.useCustomEase = customEase;
                SceneView.RepaintAll();
            }

            bool newOnion = GUILayout.Toggle(onion, "Onion", EditorStyles.toolbarButton, GUILayout.Width(52));
            GUILayout.Space(4);
            float newOnionA = GUILayout.HorizontalSlider(onionA, 0.05f, 0.6f, GUILayout.Width(80));
            if (newOnion != onion || !Mathf.Approximately(newOnionA, onionA))
            {
                onion = newOnion;
                onionA = newOnionA;
                SceneView.RepaintAll();
            }

            GUILayout.Space(10);
            GUILayout.Label("Zoom", EditorStyles.miniLabel, GUILayout.Width(36));
            zoom = GUILayout.HorizontalSlider(zoom, 0.25f, 6f, GUILayout.Width(120));

            GUILayout.FlexibleSpace();

            if (tracks.Count > 0)
            {
                GUILayout.Label($"{tracks.Count} object", EditorStyles.miniLabel, GUILayout.Width(80));
                if (GUILayout.Button("Exit", EditorStyles.toolbarButton, GUILayout.Width(56))) UnbindAll();
            }

            EditorGUILayout.EndHorizontal();
        }

        // shared playhead lives here, every track reads from it
        void DrawTransportBar()
        {
            int maxEnd = MaxEnd();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("<<", EditorStyles.toolbarButton, GUILayout.Width(30))) { StopAll(); SetFrame(f - 5); }
            if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(26))) { StopAll(); SetFrame(f - 1); }

            GUILayout.Label("Frame", EditorStyles.miniLabel, GUILayout.Width(40));
            int nf = EditorGUILayout.DelayedIntField(f, EditorStyles.toolbarTextField, GUILayout.Width(56));
            if (nf != f) { StopAll(); SetFrame(nf); }
            GUILayout.Label("/ " + maxEnd, EditorStyles.miniLabel, GUILayout.Width(56));

            if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(26))) { StopAll(); SetFrame(f + 1); }
            if (GUILayout.Button(">>", EditorStyles.toolbarButton, GUILayout.Width(30))) { StopAll(); SetFrame(f + 5); }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(playing ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(52))) TogglePlay();
            if (GUILayout.Button("Restart", EditorStyles.toolbarButton, GUILayout.Width(56))) Rewind();

            EditorGUILayout.EndHorizontal();
        }

        void DrawSharedScrollbar()
        {
            int total = MaxEnd();
            float ppf = FramesToPPF(zoom);
            float visible = Mathf.Max(1f, (lastTimelineW - 24f) / ppf);
            float maxScroll = Mathf.Max(0f, total - visible);

            float ns = GUILayout.HorizontalScrollbar(scrollF, visible, 0f, total);
            if (!Mathf.Approximately(ns, scrollF)) { scrollF = Mathf.Clamp(ns, 0f, maxScroll); Repaint(); }
        }

        void DrawBindPrompt()
        {
            var sel = FilterSceneObjects(Selection.gameObjects);

            GUILayout.Space(10);
            if (sel.Length == 0)
            {
                EditorGUILayout.HelpBox("Select one or more GameObjects in the scene.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox($"You have selected {sel.Length} GameObject.\nPress Edit Selected GameObject to start creating Dynamic.", MessageType.Info);
            GUILayout.Space(5);
            if (GUILayout.Button("Edit Selected GameObject", GUILayout.Height(30))) pendingBind = true;
        }

        void DrawRebindStrip()
        {
            var sel = FilterSceneObjects(Selection.gameObjects);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label($"Scene selection ({sel.Length}) doesn't match the bound tracks.", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(sel.Length == 0))
                if (GUILayout.Button("Add Selected", GUILayout.Width(110))) pendingBind = true;
            EditorGUILayout.EndHorizontal();
        }

        void DrawTrack(Track t, int i)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawTrackHeader(t, i);

            if (!t.expanded || !t.d || !t.p)
            {
                if (t.expanded && (!t.d || !t.p))
                    EditorGUILayout.HelpBox("Timeline components are missing on this object. Rebind the selection.", MessageType.Warning);

                EditorGUILayout.EndVertical();
                GUILayout.Space(2);
                return;
            }

            DrawTransformRow(t, i);
            DrawTrackControlRow(t, i);

            var r = GUILayoutUtility.GetRect(1f, kTimelineH, GUILayout.ExpandWidth(true));
            if (r.width > 1f) lastTimelineW = r.width;

            if (t.moving && Event.current.rawType == EventType.MouseUp) t.pendingMerge = true;

            DrawTimeline(t, i, r);

            if (t.pendingMerge) ApplyPendingMerge(t);

            DrawEaseRow(t);

            EditorGUILayout.EndVertical();
            GUILayout.Space(3);
        }

        void DrawTrackHeader(Track t, int i)
        {
            bool isActive = i == active;

            var hr = EditorGUILayout.GetControlRect(false, 20f);
            EditorGUI.DrawRect(hr, isActive ? new Color(0.25f, 0.47f, 0.78f, 0.35f) : new Color(0f, 0f, 0f, 0.16f));

            // clicking anywhere on the bar makes this the track the scene handles follow
            if (Event.current.type == EventType.MouseDown && hr.Contains(Event.current.mousePosition)) { active = i; Repaint(); }

            var foldR = new Rect(hr.x + 3f, hr.y + 2f, 14f, hr.height - 4f);
            t.expanded = EditorGUI.Foldout(foldR, t.expanded, GUIContent.none);

            var iconR = new Rect(hr.x + 20f, hr.y + 2f, 16f, 16f);
            GUI.Label(iconR, EditorGUIUtility.IconContent("GameObject Icon"));

            var nameR = new Rect(hr.x + 38f, hr.y + 1f, Mathf.Max(60f, hr.width - 150f), hr.height - 2f);
            GUI.Label(nameR, $"{i + 1}.  {t.go.name}", EditorStyles.boldLabel);

            var pingR = new Rect(hr.xMax - 106f, hr.y + 2f, 48f, hr.height - 4f);
            if (GUI.Button(pingR, "Ping", EditorStyles.miniButton)) EditorGUIUtility.PingObject(t.go);

            var selR = new Rect(hr.xMax - 54f, hr.y + 2f, 52f, hr.height - 4f);
            if (GUI.Button(selR, "Select", EditorStyles.miniButton)) { active = i; Selection.activeGameObject = t.go; }
        }

        void DrawTrackControlRow(Track t, int i)
        {
            int tf = TrackFrame(t);
            bool clamped = f > t.d.totalFrames;

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("Frame", GUILayout.Width(42));
            int nf = EditorGUILayout.DelayedIntField(tf, GUILayout.Width(56));
            if (nf != tf) { active = i; StopAll(); SetFrame(nf); }

            GUILayout.Space(8);
            GUILayout.Label("End", GUILayout.Width(28));
            int end = EditorGUILayout.IntField(t.d.totalFrames, GUILayout.Width(60));
            if (end != t.d.totalFrames)
            {
                active = i;
                Undo.RecordObject(t.d, "Change Timeline End");
                t.d.totalFrames = Mathf.Max(1, end);
                EditorUtility.SetDirty(t.d);
                SetFrame(f);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Clear", GUILayout.Width(56)))
            {
                active = i;
                if (EditorUtility.DisplayDialog("Clear Timeline", $"Clear all keyframes on '{t.go.name}'?", "Yes", "No"))
                {
                    Undo.RegisterCompleteObjectUndo(t.d, "Clear Timeline");
                    t.d.keys.Clear();
                    t.selF.Clear();
                    EditorUtility.SetDirty(t.d);
                    SetFrame(f);
                }
            }

            if (GUILayout.Button("Add KF", GUILayout.Width(62)))
            {
                active = i;
                Undo.RecordObject(t.d, "Add Keyframe");
                var k = t.d.Snapshot(TrackFrame(t));
                t.d.Upsert(k.f, k.lp, k.ls, k.z, k.c, k.support, true);
                EditorUtility.SetDirty(t.d);
            }

            if (GUILayout.Button("Del KF", GUILayout.Width(62))) { active = i; DeleteSelectedOrCurrent(t); }

            EditorGUILayout.EndHorizontal();
        }

        void DrawEaseRow(Track t)
        {
            if (!t.d.Has(TrackFrame(t), out var idx)) return;

            var k = t.d.keys[idx];

            if (!customEase)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Ease", GUILayout.Width(50));

                var newEase = (EasePreset)EditorGUILayout.EnumPopup(k.ease, GUILayout.Width(120));
                if (newEase != k.ease)
                {
                    Undo.RecordObject(t.d, "Change Ease");
                    k.ease = newEase;
                    if (newEase != EasePreset.Custom) k.support = EasePresetUtil.ToSupport(newEase);
                    t.d.keys[idx] = k; t.d.Sort(); EditorUtility.SetDirty(t.d);
                    if (t.p) t.p.ApplyFrame(TrackFrame(t));
                }
                EditorGUILayout.EndHorizontal();

                if (k.ease == EasePreset.Custom)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Support", GUILayout.Width(50));
                    var ns = EditorGUILayout.Vector2Field("", k.support, GUILayout.Width(220));
                    if (ns != k.support)
                    {
                        Undo.RecordObject(t.d, "Edit Support");
                        k.support = ns;
                        t.d.keys[idx] = k; t.d.Sort(); EditorUtility.SetDirty(t.d);
                        if (t.p) t.p.ApplyFrame(TrackFrame(t));
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

        // ================= Transform selector ================= //

        string[] GetDTNames(GameObject go)
        {
            if (!go) return Array.Empty<string>();
            var dts = go.GetComponents<DynamicTransform>();
            if (dts == null || dts.Length == 0) return Array.Empty<string>();
            return dts.Select(x => string.IsNullOrEmpty(x.transformationName) ? "NewTransform" : x.transformationName).Distinct().ToArray();
        }

        void RefreshDTList(Track t)
        {
            if (!t.d) { t.dtNames = new[] { "NewTransform" }; t.dtPick = 0; t.curDTName = "NewTransform"; return; }

            var names = GetDTNames(t.d.gameObject).ToList();

            if (names.Count == 0)
            {
                if (string.IsNullOrEmpty(t.curDTName))
                    t.curDTName = string.IsNullOrEmpty(t.d.transformationName) ? "NewTransform" : t.d.transformationName;

                t.dtNames = new[] { t.curDTName };
                t.dtPick = 0;
                return;
            }

            t.dtNames = names.ToArray();
            int idx = Array.IndexOf(t.dtNames, t.curDTName);
            t.dtPick = Mathf.Clamp(idx >= 0 ? idx : 0, 0, t.dtNames.Length - 1);
            t.curDTName = t.dtNames[t.dtPick];
        }

        void DrawTransformRow(Track t, int trackIndex)
        {
            if (t.dtNames == null || t.dtNames.Length == 0) RefreshDTList(t);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Transform", GUILayout.Width(65));

            Rect totalRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.Width(180));
            Rect textRect = new Rect(totalRect.x, totalRect.y, totalRect.width - 20, totalRect.height);
            Rect btnRect = new Rect(totalRect.x + totalRect.width - 20, totalRect.y, 20, totalRect.height);

            string renamed = EditorGUI.DelayedTextField(textRect, t.curDTName);

            if (EditorGUI.DropdownButton(btnRect, GUIContent.none, FocusType.Passive, EditorStyles.popup))
            {
                GUI.FocusControl(null);
                active = trackIndex;

                var menu = new GenericMenu();
                for (int i = 0; i < t.dtNames.Length; i++)
                {
                    int index = i;
                    menu.AddItem(new GUIContent(t.dtNames[i]), t.dtPick == index, () =>
                    {
                        GUI.FocusControl(null);
                        t.dtPick = index;
                        t.curDTName = t.dtNames[index];
                        LoadSelectedDT(t);
                        Repaint();
                    });
                }
                menu.DropDown(totalRect);
            }

            if (renamed != t.curDTName && !string.IsNullOrEmpty(renamed))
            {
                GUI.FocusControl(null);
                active = trackIndex;
                RenameCurrentTransform(t, renamed);
            }

            if (GUILayout.Button("+", GUILayout.Width(24))) { GUI.FocusControl(null); active = trackIndex; AddNewTransformComponent(t); }

            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                GUI.FocusControl(null);
                active = trackIndex;
                RemoveSelectedTransformComponent(t);
                RefreshDTList(t);
                LoadSelectedDT(t);
            }

            if (GUILayout.Button("Save", GUILayout.Width(56))) { GUI.FocusControl(null); active = trackIndex; SaveSelectedTransform(t); RefreshDTList(t); }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void RenameCurrentTransform(Track t, string targetName)
        {
            if (!t.d) return;

            string uniqueName = GetUniqueTransformName(t, targetName, t.curDTName);

            var dts = t.d.gameObject.GetComponents<DynamicTransform>();
            for (int i = 0; i < dts.Length; i++)
            {
                if (dts[i] && N(dts[i]) == t.curDTName)
                {
                    Undo.RecordObject(dts[i], "Rename Transform");
                    dts[i].transformationName = uniqueName;
                    EditorUtility.SetDirty(dts[i]);
                    break;
                }
            }

            Undo.RecordObject(t.d, "Rename Transform Timeline");
            t.d.transformationName = uniqueName;
            EditorUtility.SetDirty(t.d);

            t.curDTName = uniqueName;
            RefreshDTList(t);
        }

        string GetUniqueTransformName(Track t, string requestedName, string excludeCurrentName)
        {
            if (!t.d || !t.d.gameObject) return requestedName;

            var existingNames = t.d.gameObject.GetComponents<DynamicTransform>()
                .Where(dt => dt && N(dt) != excludeCurrentName)
                .Select(dt => N(dt))
                .ToHashSet();

            if (!existingNames.Contains(requestedName)) return requestedName;

            string candidate = $"{requestedName}_copy";
            if (!existingNames.Contains(candidate)) return candidate;

            int counter = 1;
            while (existingNames.Contains($"{requestedName}_copy_{counter}")) counter++;
            return $"{requestedName}_copy_{counter}";
        }

        void ClearTimelineForNewTransform(Track t)
        {
            if (!t.d) return;

            Undo.RegisterCompleteObjectUndo(t.d, "New Transform Timeline");
            t.d.transformationName = "NewTransform";
            t.d.keys.Clear();

            var k0 = t.d.Snapshot(0);
            t.d.Upsert(k0.f, k0.lp, k0.ls, k0.z, k0.c, k0.support, true);

            t.d.totalFrames = Mathf.Max(1, t.d.totalFrames);
            t.selF.Clear();
            EditorUtility.SetDirty(t.d);
            if (t.p) t.p.ApplyFrame(TrackFrame(t));
        }

        void LoadSelectedDT(Track t)
        {
            GUI.FocusControl(null);
            if (!t.d) return;

            var dts = t.d.gameObject.GetComponents<DynamicTransform>();

            DynamicTransform src = null;
            for (int i = 0; i < dts.Length; i++)
                if (dts[i] && N(dts[i]) == t.curDTName) { src = dts[i]; break; }

            if (!src)
            {
                ClearTimelineForNewTransform(t);
                t.d.transformationName = t.curDTName;
                return;
            }

            Undo.RegisterCompleteObjectUndo(t.d, "Load Transform");
            t.d.LoadFromDynamicTransform(src, useCustomEase: customEase, clearExisting: true);

            t.selF.Clear();
            EditorUtility.SetDirty(t.d);
            if (t.p) t.p.ApplyFrame(TrackFrame(t));
        }

        void SaveSelectedTransform(Track t)
        {
            if (!t.d) return;

            string name = string.IsNullOrEmpty(t.curDTName) ? "NewTransform" : t.curDTName;

            Undo.RecordObject(t.d, "Save Transform");
            t.d.transformationName = name;

            var dt = t.d.BakeToDynamicTransform(t.d.gameObject, name, clear: true, useCustomEase: customEase);

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
                Debug.LogWarning($"[Dynamic Editor] Failed to bake transformation '{name}'.", t.d);
            }

            EditorUtility.SetDirty(t.d);
            t.curDTName = name;
        }

        void AddNewTransformComponent(Track t)
        {
            if (!t.d) return;

            string uniqueName = GetUniqueTransformName(t, "NewTransform", null);

            var newDT = Undo.AddComponent<DynamicTransform>(t.d.gameObject);
            newDT.transformationName = uniqueName;

            Undo.RecordObject(t.d, "New Transform Timeline");
            t.d.transformationName = uniqueName;
            t.d.keys.Clear();

            var k0 = t.d.Snapshot(0);
            t.d.Upsert(k0.f, k0.lp, k0.ls, k0.z, k0.c, k0.support, true);
            t.d.totalFrames = Mathf.Max(1, t.d.totalFrames);
            t.selF.Clear();

            t.d.BakeToDynamicTransform(newDT, clear: true, useCustomEase: customEase);

            EditorUtility.SetDirty(newDT);
            EditorUtility.SetDirty(t.d);

            if (t.p) t.p.ApplyFrame(TrackFrame(t));

            t.curDTName = uniqueName;
            RefreshDTList(t);
        }

        void RemoveSelectedTransformComponent(Track t)
        {
            if (!t.d) return;

            var dts = t.d.gameObject.GetComponents<DynamicTransform>();
            DynamicTransform target = null;

            for (int i = 0; i < dts.Length; i++)
                if (dts[i] && N(dts[i]) == t.curDTName) { target = dts[i]; break; }

            if (!target) return;

            Undo.DestroyObjectImmediate(target);
            t.curDTName = "NewTransform";
        }

        // ================= Timeline ================= //

        static float FramesToPPF(float zoom) => Mathf.Max(2f, 10f * zoom);

        // every track shares scroll, zoom and range so their playheads line up vertically
        int MouseToFrame(float mouseX, float innerX, float ppf) => Mathf.Clamp(Mathf.RoundToInt(scrollF + (mouseX - innerX) / ppf), 0, MaxEnd());

        void DrawTimeline(Track t, int trackIndex, Rect r)
        {
            int total = MaxEnd();
            int trackEnd = Mathf.Max(1, t.d.totalFrames);

            GUI.Box(r, GUIContent.none);

            var inner = new Rect(r.x + 6f, r.y + 18f, r.width - 12f, r.height - 24f);
            float ppf = FramesToPPF(zoom);
            float visibleFrames = Mathf.Max(1f, inner.width / ppf);
            scrollF = Mathf.Clamp(scrollF, 0f, Mathf.Max(0f, total - visibleFrames));

            bool repaint = Event.current.type == EventType.Repaint;

            // anything past this track's own end is dead space
            if (repaint && trackEnd < scrollF + visibleFrames)
            {
                float dx = inner.x + (trackEnd - scrollF) * ppf;
                if (dx < inner.xMax)
                {
                    float x0 = Mathf.Max(dx, inner.x);
                    EditorGUI.DrawRect(new Rect(x0, inner.y, inner.xMax - x0, inner.height), new Color(0f, 0f, 0f, 0.35f));
                }
            }

            if (repaint)
            {
                const int marks = 10;
                Handles.color = new Color(1f, 1f, 1f, 0.15f);
                for (int i = 0; i <= marks; i++)
                {
                    float tt = i / (float)marks;
                    float x = inner.x + tt * inner.width;
                    Handles.DrawLine(new Vector3(x, inner.y), new Vector3(x, inner.y + inner.height));
                    int fr = Mathf.RoundToInt(Mathf.Lerp(scrollF, scrollF + visibleFrames, tt));
                    GUI.Label(new Rect(x - 12f, r.y + 2f, 60f, 16f), fr.ToString(), EditorStyles.miniLabel);
                }
            }

            int tf = TrackFrame(t);

            if (t.d.keys != null)
            {
                float rowY = inner.y + inner.height * 0.5f;
                for (int i = 0; i < t.d.keys.Count; i++)
                {
                    var k = t.d.keys[i];
                    if (k.f < scrollF - 1f || k.f > scrollF + visibleFrames + 1f) continue;

                    float x = inner.x + (k.f - scrollF) * ppf;
                    var kr = new Rect(x - 5f, rowY - 8f, 10f, 16f);

                    bool sel = t.selF.Contains(k.f);
                    EditorGUI.DrawRect(kr, sel
                        ? new Color(1f, 0.85f, 0.25f, 1f)
                        : (k.f == tf ? new Color(1f, 0.4f, 0.4f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f)));

                    HandleKFEvents(t, trackIndex, k.f, kr, inner, ppf);
                    if (t.pendingMerge) break;   // keys list is about to be rebuilt, stop walking it
                }
            }

            if (repaint)
            {
                // clamped to this track's end, so a short track parks its line on the last frame
                float sx = inner.x + (tf - scrollF) * ppf;
                Handles.color = (f > trackEnd) ? new Color(1f, 0.35f, 0.35f, 0.45f) : Color.red;
                Handles.DrawLine(new Vector3(sx, inner.y), new Vector3(sx, inner.y + inner.height));
            }

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && inner.Contains(e.mousePosition) && !t.moving)
            {
                active = trackIndex;
                StopAll();

                t.scrubbing = true;
                SetFrame(MouseToFrame(e.mousePosition.x, inner.x, ppf));
                if (!e.control && !e.command && !e.shift) t.selF.Clear();
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && t.scrubbing && !t.moving)
            {
                SetFrame(MouseToFrame(e.mousePosition.x, inner.x, ppf));
                e.Use();
            }
            else if (e.type == EventType.MouseUp && t.scrubbing)
            {
                t.scrubbing = false;
                e.Use();
            }
        }

        void HandleKFEvents(Track t, int trackIndex, int frame, Rect kr, Rect inner, float ppf)
        {
            if (!t.d) return;
            var e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0 && kr.Contains(e.mousePosition))
            {
                active = trackIndex;
                StopAll();

                bool additive = e.control || e.command;
                if (additive) { if (!t.selF.Remove(frame)) t.selF.Add(frame); }
                else if (e.shift) t.selF.Add(frame);
                else if (!t.selF.Contains(frame)) { t.selF.Clear(); t.selF.Add(frame); }

                t.moving = true;
                t.scrubbing = false;
                t.moveUndoPushed = false;
                t.dragStartFrame = MouseToFrame(e.mousePosition.x, inner.x, ppf);
                t.moveList.Clear();
                t.moveList.AddRange(SelectedIndexList(t));
                SetFrame(frame);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDrag && t.moving && t.moveList.Count > 0)
            {
                int cur = MouseToFrame(e.mousePosition.x, inner.x, ppf);
                int delta = cur - t.dragStartFrame;

                // don't spam the undo stack for a plain selection click
                if (delta == 0 && !t.moveUndoPushed) { e.Use(); return; }
                if (!t.moveUndoPushed) { Undo.RegisterCompleteObjectUndo(t.d, "Move Keyframes"); t.moveUndoPushed = true; }

                t.selF.Clear();
                for (int i = 0; i < t.moveList.Count; i++)
                {
                    var (id, of) = t.moveList[i];
                    if (id < 0 || id >= t.d.keys.Count) continue;      // guard against an external re-sort

                    var k = t.d.keys[id];
                    k.f = Mathf.Clamp(of + delta, 0, t.d.totalFrames);   // keys never leave their own track's range
                    t.d.keys[id] = k;
                    t.selF.Add(k.f);
                }

                f = Mathf.Clamp(cur, 0, MaxEnd());
                ApplyFrameToAll(t);
                EditorUtility.SetDirty(t.d);
                e.Use();
                return;
            }

            if (e.type == EventType.MouseUp && t.moving)
            {
                t.pendingMerge = true;    // applied after the draw loop, never during it
                e.Use();
            }
        }

        void ApplyPendingMerge(Track t)
        {
            t.pendingMerge = false;
            t.moving = false;
            t.moveUndoPushed = false;

            if (!t.d || t.moveList.Count == 0) { t.moveList.Clear(); return; }

            var movedIdx = new HashSet<int>();
            for (int i = 0; i < t.moveList.Count; i++) movedIdx.Add(t.moveList[i].idx);

            var moved = new List<DynamicTimelineData.KF>(t.moveList.Count);
            for (int i = 0; i < t.moveList.Count; i++)
            {
                int id = t.moveList[i].idx;
                if (id >= 0 && id < t.d.keys.Count) moved.Add(t.d.keys[id]);
            }

            // merge by frame, a moved key wins over a stationary one on the same frame
            var byFrame = new Dictionary<int, DynamicTimelineData.KF>(t.d.keys.Count);
            for (int i = 0; i < t.d.keys.Count; i++)
                if (!movedIdx.Contains(i)) byFrame[t.d.keys[i].f] = t.d.keys[i];
            for (int i = 0; i < moved.Count; i++)
                byFrame[moved[i].f] = moved[i];

            t.d.keys.Clear();
            foreach (var kv in byFrame.OrderBy(kv => kv.Key)) t.d.keys.Add(kv.Value);

            t.selF.Clear();
            for (int i = 0; i < moved.Count; i++) t.selF.Add(moved[i].f);

            t.moveList.Clear();
            t.d.Sort();
            EditorUtility.SetDirty(t.d);
            ApplyFrameToAll(null);
            Repaint();
        }

        List<(int idx, int origF)> SelectedIndexList(Track t)
        {
            var list = new List<(int, int)>();
            for (int i = 0; i < t.d.keys.Count; i++) if (t.selF.Contains(t.d.keys[i].f)) list.Add((i, t.d.keys[i].f));
            return list;
        }

        // ================= Keyframe ops ================= //

        void DeleteSelectedOrCurrent(Track t)
        {
            if (!t.d) return;

            Undo.RegisterCompleteObjectUndo(t.d, "Delete Keyframe(s)");

            if (t.selF.Count > 0)
            {
                for (int i = t.d.keys.Count - 1; i >= 0; i--)
                    if (t.selF.Contains(t.d.keys[i].f)) t.d.keys.RemoveAt(i);
                t.selF.Clear();
            }
            else t.d.DeleteAt(TrackFrame(t));

            t.d.Sort();
            EditorUtility.SetDirty(t.d);
            if (t.p && t.d.keys.Count > 0) t.p.ApplyFrame(TrackFrame(t));   // was left on the deleted pose
        }

        void CopySelected(Track t)
        {
            if (!t.d) return;
            t.d.Sort();

            int tf = TrackFrame(t);
            var frames = new List<int>();
            if (t.selF.Count > 0) { foreach (var fr in t.selF) if (t.d.Has(fr, out _)) frames.Add(fr); }
            else if (t.d.Has(tf, out _)) frames.Add(tf);

            if (frames.Count == 0) return;
            frames.Sort();

            int baseF = frames[0];
            clip.Clear();
            for (int i = 0; i < frames.Count; i++)
            {
                t.d.Has(frames[i], out var idx);
                var k = t.d.keys[idx];
                clip.Add(new CK { df = frames[i] - baseF, lp = k.lp, ls = k.ls, z = k.z, c = k.c, support = k.support });
            }
        }

        void PasteAtFrame(Track t, int dstF)
        {
            if (!t.d || clip.Count == 0) return;

            Undo.RegisterCompleteObjectUndo(t.d, "Paste Keyframe(s)");

            t.selF.Clear();
            for (int i = 0; i < clip.Count; i++)
            {
                var ck = clip[i];
                int fr = Mathf.Clamp(dstF + ck.df, 0, t.d.totalFrames);
                t.d.Upsert(fr, ck.lp, ck.ls, ck.z, ck.c, ck.support, true);
                t.selF.Add(fr);
            }

            t.d.Sort();
            EditorUtility.SetDirty(t.d);
            if (t.p) t.p.ApplyFrame(TrackFrame(t));
        }

        void HandleHotkeys()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;

            // never steal keys from a focused text field (rename, frame/end IntFields)
            if (EditorGUIUtility.editingTextField) return;

            var t = ActiveTrack();
            bool ctrl = e.control || e.command;

            if (ctrl)
            {
                if (t != null && e.keyCode == KeyCode.C) { CopySelected(t); e.Use(); Repaint(); }
                else if (t != null && e.keyCode == KeyCode.V) { PasteAtFrame(t, TrackFrame(t)); e.Use(); Repaint(); }
                return;   // let Ctrl+S / Ctrl+Z pass through untouched
            }

            if (e.keyCode == KeyCode.Delete) { if (t != null) DeleteSelectedOrCurrent(t); e.Use(); Repaint(); return; }
            if (e.keyCode == KeyCode.Space) { TogglePlay(); e.Use(); Repaint(); }
        }

        // ================= Onion Skin ================= //

        static readonly Dictionary<Sprite, Mesh> smesh = new();
        static Material smat;

        void OnSceneGUI(SceneView sv)
        {
            if (tracks.Count == 0) return;

            var e = Event.current;
            bool anyMotion = playing;

            for (int i = 0; i < tracks.Count; i++)
            {
                var t = tracks[i];
                if (!t.d || !t.p) continue;

                if (t.p.useCustomEase != customEase) t.p.useCustomEase = customEase;
                anyMotion |= t.scrubbing || t.moving;

                // ghosts are drawn for every bound object, not just the active one
                if (onion && e.type == EventType.Repaint && t.d.keys != null && t.d.keys.Count > 0)
                {
                    t.d.Sort();
                    int tf = TrackFrame(t);
                    int prev = PrevKey(t.d, tf), next = NextKey(t.d, tf);
                    if (prev >= 0) DrawGhostAll(t, prev, onionA);
                    if (next >= 0) DrawGhostAll(t, next, onionA);
                }
            }

            // only the active track gets a support handle, otherwise they'd overlap
            if (customEase)
            {
                var at = ActiveTrack();
                if (at != null && at.d && at.p) DynamicHandle.DrawPrevNextBezierAndSupport(at.d, at.p, TrackFrame(at), true);
            }

            if (anyMotion) sv.Repaint();
        }

        static int PrevKey(DynamicTimelineData d, int fr) { int best = -1; for (int i = 0; i < d.keys.Count; i++) { int kf = d.keys[i].f; if (kf < fr && kf > best) best = kf; } return best; }
        static int NextKey(DynamicTimelineData d, int fr) { int best = int.MaxValue; for (int i = 0; i < d.keys.Count; i++) { int kf = d.keys[i].f; if (kf > fr && kf < best) best = kf; } return best == int.MaxValue ? -1 : best; }

        void DrawGhostAll(Track t, int fr, float a)
        {
            if (!t.d || !t.p) return;

            var root = t.d.transform;
            t.p.Eval(fr, out var lp, out var ls, out var z, out var rootCol, assumeSorted: true);

            var parentW = root.parent ? root.parent.localToWorldMatrix : Matrix4x4.identity;
            var evalRootW = parentW * Matrix4x4.TRS(lp, Quaternion.Euler(0f, 0f, z), ls);
            var delta = evalRootW * root.localToWorldMatrix.inverse;

            if (!smat)
            {
                var sh = Shader.Find("Sprites/Default");
                if (!sh) return;
                smat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }

            var srs = t.d.GetComponentsInChildren<SpriteRenderer>(true);
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
    }
}