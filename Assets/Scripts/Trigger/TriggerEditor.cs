using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Vectorier.Component;

namespace Vectorier.Trigger
{
    public class TriggerEditor : EditorWindow
    {
        const string PresetFolder = "Assets/Editor/TriggerEditor/Presets";
        const float TopH = 22f;
        const float RowH = 20f;
        const float SplitW = 5f;
        const float XmlH = 170f;

        TriggerComponent target;
        TriggerData data = TriggerData.CreateDefault();
        bool dirty;

        readonly List<TriggerPresetAsset> presets = new List<TriggerPresetAsset>();
        int presetIdx;                       // 0 is always Default
        string presetName = "Default";

        float leftW = 280f, loopsH = 260f;
        bool dragV, dragH;

        string search = "";
        int sel = -1;
        Vector2 loopScroll, varScroll, mainScroll, xmlScroll;

        int renaming = -1;
        bool renameFocused;
        bool aiCustomMode;

        int dragFrom = -1, dragTo = -1;
        bool rowDragging;
        Vector2 rowDragStart;

        bool showXml;
        Action pending;                      // list edits run after the layout pass

        GUIStyle blockTitle, blockDesc, cardTitle, rowText, rowTextSel, rowIcon, rowIconSel, xmlStyle;

        const int NoSel = -2;                 // "no selection change queued"
        int pendingSel = NoSel;               // loop selection is applied on the next Layout, never mid-event
        int hoverIdx = -1;
        bool multiSelected;
        string xmlCache = "";
        Vector2 xmlSize;
        readonly List<int> shownBuf = new List<int>();

        static readonly string[] TriggerNodes = { "COM", "DetectorH", "DetectorV" };
        static readonly Color RowHoverColor = Color.white;
        static readonly Color RowSelectedColor = new Color(0.24f, 0.49f, 0.91f, 1f);

        [MenuItem("Vectorier/Tools/Trigger Editor", false, 27)]
        public static void Open()
        {
            var w = GetWindow<TriggerEditor>("Trigger Editor");
            w.minSize = new Vector2(880f, 520f);
            w.Show();
        }

        public static void Open(TriggerComponent t)
        {
            var w = GetWindow<TriggerEditor>("Trigger Editor");
            w.minSize = new Vector2(880f, 520f);
            w.Bind(t, true);
            w.Show();
        }

        void OnEnable()
        {
            wantsMouseMove = true;
            Selection.selectionChanged += OnSelectionChanged;
            multiSelected = Selection.gameObjects.Length > 1;
            TriggerTemplateLibrary.Reload();
            RefreshPresets();
            Bind(FindSelected(), true);
        }

        void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        static TriggerComponent FindSelected()
        {
            var go = Selection.activeGameObject;
            return go ? go.GetComponent<TriggerComponent>() : null;
        }

        void OnSelectionChanged()
        {
            multiSelected = Selection.gameObjects.Length > 1;
            if (multiSelected) { ClearFocus(); return; }

            var t = FindSelected();
            if (t == target) { Repaint(); return; }
            if (!t) { Repaint(); return; }

            if (dirty && target &&
                !EditorUtility.DisplayDialog("Unsaved Changes",
                    $"'{target.gameObject.name}' has changes that were never applied.\nSwitch to '{t.gameObject.name}' and lose them?",
                    "Switch", "Stay"))
            {
                Selection.activeGameObject = target.gameObject;
                return;
            }

            Bind(t, true);
            Repaint();
        }

        void Bind(TriggerComponent t, bool load)
        {
            target = t;
            sel = -1;
            renaming = -1;
            dirty = false;
            presetIdx = 0;
            presetName = "Default";
            aiCustomMode = false;

            if (load)
            {
                data = t ? TriggerData.Parse(t.contentXml) : TriggerData.CreateDefault();
                if (data.loops.Count > 0) sel = 0;
            }
        }

        void Touch() { dirty = true; }

        void Styles()
        {
            if (blockTitle != null) return;

            blockTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            blockDesc = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            cardTitle = new GUIStyle(EditorStyles.boldLabel);

            rowText = MakeRowStyle(Color.black, FontStyle.Normal);
            rowTextSel = MakeRowStyle(Color.white, FontStyle.Normal);
            rowIcon = MakeRowStyle(Color.black, FontStyle.Bold);
            rowIconSel = MakeRowStyle(Color.white, FontStyle.Bold);

            xmlStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = false };
        }

        static GUIStyle MakeRowStyle(Color c, FontStyle fs)
        {
            var s = new GUIStyle(EditorStyles.label) { fontStyle = fs };
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = c;
            s.onNormal.textColor = s.onHover.textColor = s.onActive.textColor = s.onFocused.textColor = c;
            return s;
        }

        // ================= Layout =================

        void OnGUI()
        {
            Styles();
            var ev = Event.current;

            // queued edits are applied at the start of a Layout event, so Layout and the event after it
            // always see the same data (this is what fixes the GUILayout error)
            if (ev.type == EventType.Layout) ApplyPending();

            // several objects selected: only draw, ignore all input
            if (multiSelected && ev.type != EventType.Layout && ev.type != EventType.Repaint) return;

            if (ev.type == EventType.MouseLeaveWindow && hoverIdx != -1) { hoverIdx = -1; Repaint(); }

            using (new EditorGUI.DisabledScope(multiSelected))
                DrawWindow();

            if (multiSelected) { DrawMultiSelectOverlay(); return; }

            // a click nobody handled = click on empty space, release the text field focus
            if (ev.type == EventType.MouseDown && ev.button == 0) ClearFocus();

            // something got queued during an input event, ask for a fresh Layout pass to apply it
            if (ev.type != EventType.Layout && ev.type != EventType.Repaint &&
                (pending != null || pendingSel != NoSel)) Repaint();
        }

        void DrawWindow()
        {
            bool xml = showXml;      // read once, the XML toggle in the top bar can flip during this event

            DrawTopBar(new Rect(0f, 0f, position.width, TopH));

            float bodyY = TopH;
            float bodyH = position.height - bodyY - (xml ? XmlH : 0f);
            if (bodyH < 120f) bodyH = 120f;

            if (!target)
            {
                DrawNoTarget(new Rect(0f, bodyY, position.width, bodyH));
                return;
            }

            leftW = Mathf.Clamp(leftW, 200f, Mathf.Max(220f, position.width - 420f));
            loopsH = Mathf.Clamp(loopsH, 120f, Mathf.Max(140f, bodyH - 150f));

            var loopsR = new Rect(0f, bodyY, leftW, loopsH);
            var hSplit = new Rect(0f, bodyY + loopsH, leftW, SplitW);
            var varsR = new Rect(0f, hSplit.yMax, leftW, bodyH - loopsH - SplitW);
            var vSplit = new Rect(leftW, bodyY, SplitW, bodyH);
            var mainR = new Rect(vSplit.xMax, bodyY, position.width - vSplit.xMax, bodyH);

            DrawLoopsPanel(loopsR);
            DrawVarsPanel(varsR);
            DrawMainPanel(mainR);

            Splitter(hSplit, false, ref dragH, ref loopsH, bodyY);
            Splitter(vSplit, true, ref dragV, ref leftW, 0f);

            if (xml) DrawXmlStrip(new Rect(0f, bodyY + bodyH, position.width, XmlH));
        }

        void DrawMultiSelectOverlay()
        {
            if (Event.current.type != EventType.Repaint) return;

            var full = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(full, new Color(0.35f, 0.35f, 0.35f, 0.55f));

            var box = new Rect(0f, 0f, Mathf.Min(380f, position.width - 40f), 48f);
            box.center = full.center;
            EditorGUI.HelpBox(box, "You are selecting multiple GameObjects.\nPlease select only one.", MessageType.Warning);
        }

        void ApplyPending()
        {
            if (pendingSel != NoSel)
            {
                sel = Mathf.Clamp(pendingSel, -1, data.loops.Count - 1);
                pendingSel = NoSel;
            }

            if (pending == null) return;

            var p = pending;
            pending = null;

            dirty = true;    // set first so preset loading and Apply can clear it again
            p();
            Repaint();
        }

        void ClearFocus()
        {
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
            Repaint();
        }

        // edits queued during the layout pass run here, once everything is drawn
        void RunPending()
        {
            if (pending == null) return;

            var p = pending;
            pending = null;

            dirty = true;    // set first so preset loading and Apply can clear it again
            p();
            Repaint();
        }

        void Splitter(Rect r, bool vertical, ref bool state, ref float value, float origin)
        {
            EditorGUI.DrawRect(r, new Color(0f, 0f, 0f, 0.25f));
            EditorGUIUtility.AddCursorRect(r, vertical ? MouseCursor.ResizeHorizontal : MouseCursor.ResizeVertical);

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition)) { state = true; e.Use(); }
            else if (e.type == EventType.MouseDrag && state)
            {
                value = vertical ? e.mousePosition.x : e.mousePosition.y - origin;
                e.Use();
                Repaint();
            }
            else if (e.rawType == EventType.MouseUp && state) { state = false; Repaint(); }
        }

        void DrawNoTarget(Rect r)
        {
            GUILayout.BeginArea(r);
            GUILayout.Space(20);

            var go = Selection.activeGameObject;
            if (!go)
                EditorGUILayout.HelpBox("Select a Trigger in the scene to edit it.", MessageType.Info);
            else
            {
                EditorGUILayout.HelpBox($"'{go.name}' has no Trigger Component.", MessageType.Info);
                GUILayout.Space(6);
                if (GUILayout.Button($"Add Trigger Component to '{go.name}'", GUILayout.Height(26)))
                {
                    var added = Undo.AddComponent<TriggerComponent>(go);
                    pending = () => Bind(added, true);
                }
            }
            GUILayout.EndArea();
        }

        // ================= Top bar =================

        void DrawTopBar(Rect r)
        {
            GUI.Box(r, GUIContent.none, EditorStyles.toolbar);
            GUILayout.BeginArea(r);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            string title = target ? target.gameObject.name : "No trigger selected";
            GUILayout.Label(new GUIContent(title, "The GameObject this trigger belongs to"),
                EditorStyles.boldLabel, GUILayout.Width(190));

            using (new EditorGUI.DisabledScope(!target))
            {
                GUILayout.Label("Preset", EditorStyles.miniLabel, GUILayout.Width(44));

                var nameRect = GUILayoutUtility.GetRect(150f, 18f, GUILayout.Width(150));
                var textRect = new Rect(nameRect.x, nameRect.y, nameRect.width - 18f, nameRect.height);
                var dropRect = new Rect(textRect.xMax, nameRect.y, 18f, nameRect.height);

                using (new EditorGUI.DisabledScope(presetIdx == 0))
                {
                    string nn = EditorGUI.DelayedTextField(textRect, presetName, EditorStyles.toolbarTextField);
                    if (nn != presetName && !string.IsNullOrWhiteSpace(nn)) pending = () => RenamePreset(nn.Trim());
                }

                if (EditorGUI.DropdownButton(dropRect, GUIContent.none, FocusType.Passive, EditorStyles.toolbarDropDown))
                    ShowPresetMenu(nameRect);

                if (GUILayout.Button(new GUIContent("+", "Save the current trigger as a new preset"),
                        EditorStyles.toolbarButton, GUILayout.Width(24))) pending = NewPreset;

                using (new EditorGUI.DisabledScope(presetIdx == 0))
                {
                    if (GUILayout.Button(new GUIContent("-", "Delete this preset"),
                            EditorStyles.toolbarButton, GUILayout.Width(24))) pending = DeletePreset;

                    if (GUILayout.Button(new GUIContent("Save Preset", "Overwrite this preset with what's on screen"),
                            EditorStyles.toolbarButton, GUILayout.Width(88))) pending = SavePreset;
                }
            }

            GUILayout.FlexibleSpace();

            showXml = GUILayout.Toggle(showXml, new GUIContent("XML", "Preview the XML this will produce"),
                EditorStyles.toolbarButton, GUILayout.Width(40));

            if (GUILayout.Button(new GUIContent("Reload Templates", "Re-read TriggerTemplates.xml"),
                    EditorStyles.toolbarButton, GUILayout.Width(110))) TriggerTemplateLibrary.Reload();

            GUILayout.Space(8);
            if (dirty) GUILayout.Label("Unapplied changes", EditorStyles.miniLabel, GUILayout.Width(110));

            using (new EditorGUI.DisabledScope(!target))
                if (GUILayout.Button(new GUIContent("Apply", "Write this trigger to the Trigger Component"),
                        EditorStyles.toolbarButton, GUILayout.Width(60))) pending = Apply;

            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void Apply()
        {
            if (!target) return;
            Undo.RecordObject(target, "Apply Trigger");
            target.contentXml = data.ToXml();
            EditorUtility.SetDirty(target);
            dirty = false;
        }

        // ================= Presets =================

        void RefreshPresets()
        {
            presets.Clear();
            if (!AssetDatabase.IsValidFolder(PresetFolder)) return;

            var guids = AssetDatabase.FindAssets("t:TriggerPresetAsset", new[] { PresetFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                var a = AssetDatabase.LoadAssetAtPath<TriggerPresetAsset>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (a) presets.Add(a);
            }
            presets.Sort((x, y) => string.Compare(x.name, y.name, StringComparison.OrdinalIgnoreCase));
        }

        void ShowPresetMenu(Rect at)
        {
            RefreshPresets();

            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Default"), presetIdx == 0, () => { pending = () => LoadPreset(0); Repaint(); });
            if (presets.Count > 0) menu.AddSeparator("");

            for (int i = 0; i < presets.Count; i++)
            {
                int idx = i + 1;
                menu.AddItem(new GUIContent(presets[i].name), presetIdx == idx, () => { pending = () => LoadPreset(idx); Repaint(); });
            }
            menu.DropDown(at);
        }

        void LoadPreset(int idx)
        {
            if (dirty && !EditorUtility.DisplayDialog("Unapplied changes",
                    "Loading a preset replaces everything on screen. Continue?", "Load", "Cancel")) return;

            presetIdx = idx;

            if (idx == 0)
            {
                presetName = "Default";
                data = target ? TriggerData.Parse(target.contentXml) : TriggerData.CreateDefault();
            }
            else
            {
                var a = presets[idx - 1];
                presetName = a.name;
                data = TriggerData.Parse(a.contentXml);
            }

            sel = data.loops.Count > 0 ? 0 : -1;
            renaming = -1;
            aiCustomMode = false;
            dirty = idx != 0;
            GUI.FocusControl(null);
        }

        void EnsurePresetFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Editor")) AssetDatabase.CreateFolder("Assets", "Editor");
            if (!AssetDatabase.IsValidFolder("Assets/Editor/TriggerEditor")) AssetDatabase.CreateFolder("Assets/Editor", "TriggerEditor");
            if (!AssetDatabase.IsValidFolder(PresetFolder)) AssetDatabase.CreateFolder("Assets/Editor/TriggerEditor", "Presets");
        }

        void NewPreset()
        {
            EnsurePresetFolder();

            var a = CreateInstance<TriggerPresetAsset>();
            a.contentXml = data.ToXml();

            string path = AssetDatabase.GenerateUniqueAssetPath(PresetFolder + "/NewPreset.asset");
            AssetDatabase.CreateAsset(a, path);
            AssetDatabase.SaveAssets();

            RefreshPresets();
            presetIdx = IndexOfPreset(a) + 1;
            presetName = presetIdx > 0 ? presets[presetIdx - 1].name : "Default";
            dirty = false;
        }

        int IndexOfPreset(TriggerPresetAsset a)
        {
            if (!a) return -1;
            int i = presets.IndexOf(a);
            if (i >= 0) return i;
            return presets.FindIndex(x => x && x.name == a.name);
        }

        void SavePreset()
        {
            if (presetIdx <= 0 || presetIdx > presets.Count) return;

            var a = presets[presetIdx - 1];
            Undo.RecordObject(a, "Save Trigger Preset");
            a.contentXml = data.ToXml();
            EditorUtility.SetDirty(a);
            AssetDatabase.SaveAssets();
            dirty = false;
        }

        void DeletePreset()
        {
            if (presetIdx <= 0 || presetIdx > presets.Count) return;

            var a = presets[presetIdx - 1];
            if (!EditorUtility.DisplayDialog("Delete preset",
                    $"Delete the preset '{a.name}'?\nThis removes the asset from the project and can't be undone.",
                    "Delete", "Cancel")) return;

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(a));
            AssetDatabase.SaveAssets();

            RefreshPresets();
            LoadPreset(0);
        }

        void RenamePreset(string newName)
        {
            if (presetIdx <= 0 || presetIdx > presets.Count) return;
            if (string.Equals(newName, "Default", StringComparison.OrdinalIgnoreCase)) return;

            var a = presets[presetIdx - 1];
            string err = AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(a), newName);
            if (!string.IsNullOrEmpty(err)) { Debug.LogWarning("[Trigger Editor] " + err); return; }

            AssetDatabase.SaveAssets();
            RefreshPresets();
            presetIdx = IndexOfPreset(a) + 1;
            presetName = presetIdx > 0 ? presets[presetIdx - 1].name : "Default";
        }

        // ================= Loops panel =================

        void DrawLoopsPanel(Rect r)
        {
            GUI.Box(r, GUIContent.none, EditorStyles.helpBox);
            var pad = new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f);

            float y = pad.y;
            GUI.Label(new Rect(pad.x, y, pad.width, 16f), "Loops", EditorStyles.boldLabel);
            y += 18f;

            search = EditorGUI.TextField(new Rect(pad.x, y, pad.width, 18f), search, EditorStyles.toolbarSearchField);
            y += 22f;

            float footH = 22f;
            var listR = new Rect(pad.x, y, pad.width, Mathf.Max(24f, pad.yMax - y - footH - 4f));
            var footR = new Rect(pad.x, pad.yMax - footH, pad.width, footH);

            var shown = shownBuf;
            shown.Clear();
            for (int i = 0; i < data.loops.Count; i++)
                if (string.IsNullOrEmpty(search) ||
                    data.loops[i].Display.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    shown.Add(i);

            bool canReorder = string.IsNullOrEmpty(search);

            var e = Event.current;
            bool mouseInList = GUI.enabled && listR.Contains(e.mousePosition);   // outer coordinates, read before the scroll view
            bool clickedRow = false;
            int hoverNow = -1;

            var content = new Rect(0f, 0f, listR.width - 16f, shown.Count * RowH + 2f);
            loopScroll = GUI.BeginScrollView(listR, loopScroll, content);

            dragTo = -1;

            for (int k = 0; k < shown.Count; k++)
            {
                int i = shown[k];
                var L = data.loops[i];
                var row = new Rect(0f, k * RowH, content.width, RowH);

                bool overRow = mouseInList && row.Contains(e.mousePosition);
                if (overRow && e.type == EventType.MouseDown) clickedRow = true;

                bool hovered = overRow && renaming != i;
                bool selected = i == sel;
                if (hovered) hoverNow = i;

                // selected = blue, hover = white, otherwise nothing
                if (e.type == EventType.Repaint)
                {
                    if (selected) EditorGUI.DrawRect(row, RowSelectedColor);
                    else if (hovered) EditorGUI.DrawRect(row, RowHoverColor);
                }

                // black text, white only for a selected row that is also hovered
                bool whiteText = selected && hovered;

                var iconR = new Rect(row.x + 4f, row.y + 1f, 16f, RowH - 2f);
                if (L.kind != LoopKind.Custom)
                    GUI.Label(iconR, new GUIContent("T", "Built from a template"), whiteText ? rowIconSel : rowIcon);

                var nameR = new Rect(row.x + 22f, row.y + 1f, row.width - 26f, RowH - 2f);

                if (renaming == i)
                {
                    GUI.SetNextControlName("loopRename");
                    string nn = GUI.TextField(nameR, L.name);
                    if (nn != L.name) { L.name = nn; Touch(); }

                    if (!renameFocused) { EditorGUI.FocusTextInControl("loopRename"); renameFocused = true; }

                    if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Escape))
                    {
                        if (string.IsNullOrWhiteSpace(L.name)) L.name = TLoop.NoName;
                        renaming = -1;
                        GUI.FocusControl(null);
                        e.Use();
                    }
                    else if (e.type == EventType.MouseDown && !nameR.Contains(e.mousePosition))
                    {
                        if (string.IsNullOrWhiteSpace(L.name)) L.name = TLoop.NoName;
                        renaming = -1;
                    }
                }
                else
                {
                    GUI.Label(nameR, L.Display, whiteText ? rowTextSel : rowText);

                    if (e.type == EventType.MouseDown && e.button == 0 && overRow)
                    {
                        pendingSel = i;          // applied on the next Layout, the main panel must not change mid-event
                        GUI.FocusControl(null);

                        if (e.clickCount == 2 && L.kind != LoopKind.FullTemplate)
                        {
                            renaming = i;
                            renameFocused = false;
                        }
                        else if (canReorder)
                        {
                            dragFrom = k;
                            rowDragStart = e.mousePosition;
                        }
                        e.Use();
                        Repaint();
                    }
                }
            }

            // click on empty space inside the list = deselect
            if (e.type == EventType.MouseDown && e.button == 0 && mouseInList && !clickedRow &&
                new Rect(0f, 0f, content.width, Mathf.Max(listR.height, content.height)).Contains(e.mousePosition))
            {
                pendingSel = -1;
                renaming = -1;
                dragFrom = -1;
                GUI.FocusControl(null);
                e.Use();
                Repaint();
            }

            if (canReorder && dragFrom >= 0)
            {
                if (e.type == EventType.MouseDrag && !rowDragging &&
                    Mathf.Abs(e.mousePosition.y - rowDragStart.y) > 4f) rowDragging = true;

                if (rowDragging)
                {
                    dragTo = Mathf.Clamp(Mathf.RoundToInt(e.mousePosition.y / RowH), 0, shown.Count);
                    if (e.type == EventType.Repaint)
                        EditorGUI.DrawRect(new Rect(0f, dragTo * RowH - 1f, content.width, 2f), new Color(1f, 0.8f, 0.2f, 0.9f));
                    Repaint();
                }

                if (e.rawType == EventType.MouseUp)
                {
                    if (rowDragging && dragTo >= 0 && dragTo != dragFrom && dragTo != dragFrom + 1)
                    {
                        int from = dragFrom, to = dragTo;
                        pending = () =>
                        {
                            var item = data.loops[from];
                            data.loops.RemoveAt(from);
                            data.loops.Insert(to > from ? to - 1 : to, item);
                            sel = to > from ? to - 1 : to;
                        };
                    }
                    dragFrom = -1;
                    rowDragging = false;
                }
            }

            GUI.EndScrollView();

            // repaint only when the hovered row actually changes, not on every mouse move
            if (e.type == EventType.MouseMove && hoverNow != hoverIdx) { hoverIdx = hoverNow; Repaint(); }
            else if (e.type == EventType.Repaint) hoverIdx = hoverNow;

            if (shown.Count == 0)
                GUI.Label(new Rect(listR.x + 6f, listR.y + 6f, listR.width - 12f, 32f),
                    string.IsNullOrEmpty(search) ? "No Loops Yet." : "No Loop Matches That Search.", EditorStyles.miniLabel);

            var addR = new Rect(footR.x, footR.y, footR.width - 26f, footR.height);
            var delR = new Rect(footR.xMax - 24f, footR.y, 24f, footR.height);

            if (GUI.Button(addR, "+ New Loop")) pending = AddLoop;

            using (new EditorGUI.DisabledScope(sel < 0 || sel >= data.loops.Count))
                if (GUI.Button(delR, "-")) pending = DeleteLoop;
        }

        void AddLoop()
        {
            data.loops.Add(new TLoop());
            sel = data.loops.Count - 1;
            renaming = sel;
            renameFocused = false;
        }

        void DeleteLoop()
        {
            if (sel < 0 || sel >= data.loops.Count) return;

            var L = data.loops[sel];
            if (!EditorUtility.DisplayDialog("Delete loop", $"Delete '{L.Display}'?", "Delete", "Cancel")) return;

            data.loops.RemoveAt(sel);
            sel = Mathf.Clamp(sel, -1, data.loops.Count - 1);
            renaming = -1;
        }

        // ================= Variables panel =================

        void DrawVarsPanel(Rect r)
        {
            GUI.Box(r, GUIContent.none, EditorStyles.helpBox);
            var pad = new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f);

            float y = pad.y;
            GUI.Label(new Rect(pad.x, y, pad.width, 16f), "Variables", EditorStyles.boldLabel);
            y += 18f;

            float footH = 22f;
            var listR = new Rect(pad.x, y, pad.width, Mathf.Max(24f, pad.yMax - y - footH - 4f));
            var footR = new Rect(pad.x, pad.yMax - footH, pad.width, footH);

            GUILayout.BeginArea(listR);
            varScroll = EditorGUILayout.BeginScrollView(varScroll);

            for (int i = 0; i < data.vars.Count; i++)
            {
                var v = data.vars[i];
                EditorGUILayout.BeginHorizontal();

                if (v.locked)
                {
                    GUILayout.Label(new GUIContent(v.name, LockedHint(v.name)), EditorStyles.boldLabel, GUILayout.Width(78));
                    DrawLockedValue(v);
                    GUILayout.Space(22);
                }
                else
                {
                    string nn = EditorGUILayout.DelayedTextField(v.name, GUILayout.Width(78), GUILayout.MinWidth(50));
                    if (Event.current.type == EventType.Repaint)
                        GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent(string.Empty, v.name));
                    if (nn != v.name) { RenameVariable(v.name, nn); Touch(); }

                    string nv = EditorGUILayout.TextField(v.value);
                    if (nv != v.value) { v.value = nv; Touch(); }

                    int idx = i;
                    if (GUILayout.Button("-", GUILayout.Width(20))) pending = () => data.vars.RemoveAt(idx);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();

            if (GUI.Button(footR, "+ New Variable"))
                pending = () =>
                {
                    string n = "NewVariable";
                    int k = 1;
                    while (data.vars.Exists(x => x.name == n)) n = "NewVariable" + (++k);
                    data.vars.Add(new TVar(n, "0"));
                };
        }

        static string LockedHint(string n)
        {
            switch (n)
            {
                case "$Active": return "Is the trigger switched on? 1 yes, 0 no.";
                case "$AI": return "Which AI this trigger reacts to. -1 any, 0 player, 1 hunter, 2 helper.";
                case "$Node": return "The model's node used to test entry, usually COM.";
                case "Flag1": return "General purpose flag used by several templates.";
            }
            return "";
        }

        void DrawLockedValue(TVar v)
        {
            if (v.name == "$Active")
            {
                int cur = v.value == "0" ? 1 : 0;
                int nv = EditorGUILayout.Popup(cur, new[] { "Active (1)", "Inactive (0)" });
                if (nv != cur) { v.value = nv == 0 ? "1" : "0"; Touch(); }
                return;
            }

            if (v.name == "$AI")
            {
                string[] labels = { "Any (-1)", "Player (0)", "Hunter (1)", "Custom" };
                string[] vals = { "-1", "0", "1" };

                int matched = Array.IndexOf(vals, v.value);
                bool custom = aiCustomMode || matched < 0;
                int shown = custom ? labels.Length - 1 : matched;

                int nv = EditorGUILayout.Popup(shown, labels, GUILayout.Width(96));
                if (nv != shown)
                {
                    if (nv == labels.Length - 1) aiCustomMode = true;
                    else { aiCustomMode = false; v.value = vals[nv]; Touch(); }
                }

                if (custom)
                {
                    string s = EditorGUILayout.TextField(v.value);
                    if (s != v.value) { v.value = s; Touch(); }
                }
                return;
            }

            if (v.name == "$Node")
            {
                EditorGUILayout.BeginHorizontal();
                string s = EditorGUILayout.TextField(v.value);
                if (s != v.value) { v.value = s; Touch(); }

                var dr = GUILayoutUtility.GetRect(16f, 18f, GUILayout.Width(16));
                if (EditorGUI.DropdownButton(dr, GUIContent.none, FocusType.Passive, EditorStyles.miniPullDown))
                {
                    var m = new GenericMenu();
                    foreach (var n in TriggerNodes)
                    {
                        string nn = n;
                        m.AddItem(new GUIContent(nn), v.value == nn, () => { v.value = nn; Touch(); ClearFocus(); });
                    }
                    m.DropDown(dr);
                }
                EditorGUILayout.EndHorizontal();
                return;
            }

            string plain = EditorGUILayout.TextField(v.value);
            if (plain != v.value) { v.value = plain; Touch(); }
        }

        // keeps every _Reference pointing at the renamed variable
        void RenameVariable(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName) || oldName == newName) return;
            if (data.vars.Exists(x => x.name == newName)) return;

            var v = data.vars.Find(x => x.name == oldName);
            if (v == null) return;
            v.name = newName;

            foreach (var L in data.loops)
            {
                foreach (var e in L.events) foreach (var p in e.ps) RenameIn(p.v, oldName, newName);
                foreach (var c in L.conds) RenameInCond(c, oldName, newName);
                foreach (var a in L.acts) RenameInAct(a, oldName, newName);
            }
        }

        static void RenameIn(TValue v, string o, string n)
        {
            if (v == null) return;
            if (v.mode == ValueMode.Variable && v.varName == o) v.varName = n;
            if (v.modelIsVar && v.model == o) v.model = n;
            if (v.nodeIsVar && v.node == o) v.node = n;
            if (v.mode == ValueMode.Literal && v.text == o) v.text = n;   // SetVariable Name="..."
        }

        static void RenameInCond(TCond c, string o, string n)
        {
            RenameIn(c.a, o, n); RenameIn(c.b, o, n);
            RenameIn(c.selObject, o, n); RenameIn(c.selFrom, o, n);
            foreach (var k in c.children) RenameInCond(k, o, n);
        }

        static void RenameInAct(TAct a, string o, string n)
        {
            foreach (var p in a.ps) RenameIn(p.v, o, n);
            foreach (var k in a.children) RenameInAct(k, o, n);
        }

        // ================= Main panel =================

        void DrawMainPanel(Rect r)
        {
            GUI.Box(r, GUIContent.none, EditorStyles.helpBox);
            var pad = new Rect(r.x + 6f, r.y + 6f, r.width - 12f, r.height - 12f);

            GUILayout.BeginArea(pad);

            if (sel < 0 || sel >= data.loops.Count)
            {
                GUILayout.Space(30);
                var c = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                GUILayout.Label("Pick a Loop on the left.\n" + "Or press '+ New Loop' to start editing Trigger.", c);
                GUILayout.EndArea();
                return;
            }

            var L = data.loops[sel];
            var kind = L.kind;      // read before the header, the "Build From" popup can change it mid-event

            DrawLoopHeader(L);
            GUILayout.Space(4);

            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);

            switch (kind)
            {
                case LoopKind.FullTemplate: DrawFullTemplate(L); break;
                case LoopKind.TemplateLoop: DrawTemplateLoop(L); break;
                case LoopKind.Raw: DrawRawLoop(L); break;
                default:
                    DrawWhen(L);
                    GUILayout.Space(6);
                    DrawOnlyIf(L);
                    GUILayout.Space(6);
                    DrawDo(L);
                    break;
            }

            GUILayout.Space(10);
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawLoopHeader(TLoop L)
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("Loop Name", GUILayout.Width(74));
            using (new EditorGUI.DisabledScope(L.kind == LoopKind.FullTemplate))
            {
                string nn = EditorGUILayout.TextField(L.name, GUILayout.Width(180));
                if (nn != L.name) { L.name = nn; Touch(); }
            }

            if (!L.HasName)
                GUILayout.Label(new GUIContent("(unnamed)", "A loop called 'Loop' is written without a Name attribute."),
                    EditorStyles.miniLabel, GUILayout.Width(66));
            else GUILayout.Space(66);

            GUILayout.Space(10);
            GUILayout.Label("Build From", GUILayout.Width(70));

            string[] kinds = { "Scratch", "Template Loop", "Whole Template" };
            int cur = L.kind == LoopKind.TemplateLoop ? 1 : L.kind == LoopKind.FullTemplate ? 2 : 0;
            int nk = EditorGUILayout.Popup(cur, kinds, GUILayout.Width(120));
            if (nk != cur)
            {
                L.kind = nk == 1 ? LoopKind.TemplateLoop : nk == 2 ? LoopKind.FullTemplate : LoopKind.Custom;
                Touch();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void DrawRawLoop(TLoop L)
        {
            EditorGUILayout.HelpBox("This part of the trigger isn't something the editor recognises, " +
                                    "so it's kept exactly as it was written.", MessageType.Info);
            string nn = EditorGUILayout.TextArea(L.raw, GUILayout.MinHeight(80));
            if (nn != L.raw) { L.raw = nn; Touch(); }
        }

        void DrawFullTemplate(TLoop L)
        {
            EditorGUILayout.HelpBox("The template runs as is. Set the required variables in the Variables panel.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Template", GUILayout.Width(74));
            string nn = EditorGUILayout.TextField(L.template);
            if (nn != L.template) { L.template = nn; Touch(); }

            var dr = GUILayoutUtility.GetRect(20f, 18f, GUILayout.Width(20));
            if (EditorGUI.DropdownButton(dr, GUIContent.none, FocusType.Passive, EditorStyles.miniPullDown))
            {
                var m = new GenericMenu();
                foreach (var t in TriggerTemplateLibrary.Templates)
                {
                    if (t.library) continue;
                    string tn = t.name;
                    m.AddItem(new GUIContent(tn), L.template == tn, () => { L.template = tn; Touch(); GUI.FocusControl(null); Repaint(); });
                }
                m.DropDown(dr);
            }
            EditorGUILayout.EndHorizontal();

            var info = TriggerTemplateLibrary.FindTemplate(L.template);
            if (info == null) return;

            GUILayout.Space(6);
            GUILayout.Label("This template contains", EditorStyles.boldLabel);
            foreach (var l in info.loops)
                GUILayout.Label("    " + l.name + (string.IsNullOrEmpty(l.summary) ? "" : "   -   " + l.summary), EditorStyles.miniLabel);

            if (info.init.Count > 0)
            {
                GUILayout.Space(6);
                GUILayout.Label("Variables it starts with", EditorStyles.boldLabel);
                foreach (var kv in info.init) GUILayout.Label("    " + kv.Key + " = " + kv.Value, EditorStyles.miniLabel);

                if (GUILayout.Button("Copy those variables into this trigger", GUILayout.Height(20)))
                    pending = () =>
                    {
                        foreach (var kv in info.init)
                            if (!data.vars.Exists(x => x.name == kv.Key)) data.vars.Add(new TVar(kv.Key, kv.Value));
                        data.EnsureLockedVars();
                    };
            }
        }

        void DrawTemplateLoop(TLoop L)
        {
            EditorGUILayout.HelpBox("This loop uses a pre-made loop from TriggerTemplates.xml.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Template Loop", GUILayout.Width(90));
            string nn = EditorGUILayout.TextField(L.template);
            if (nn != L.template) { L.template = nn; Touch(); }

            var dr = GUILayoutUtility.GetRect(20f, 18f, GUILayout.Width(20));
            if (EditorGUI.DropdownButton(dr, GUIContent.none, FocusType.Passive, EditorStyles.miniPullDown))
            {
                var m = new GenericMenu();
                foreach (var t in TriggerTemplateLibrary.Templates)
                    foreach (var l in t.loops)
                    {
                        string full = l.Full;
                        m.AddItem(new GUIContent(t.name + "/" + l.name), L.template == full,
                            () => { L.template = full; Touch(); GUI.FocusControl(null); Repaint(); });
                    }
                m.DropDown(dr);
            }
            EditorGUILayout.EndHorizontal();

            DrawTemplateUses(TriggerTemplateLibrary.Find(L.template));
        }

        void DrawTemplateUses(TriggerTemplateLibrary.LoopInfo info)
        {
            if (info == null) return;

            if (!string.IsNullOrEmpty(info.summary))
            {
                GUILayout.Space(4);
                GUILayout.Label(info.summary, EditorStyles.miniLabel);
            }

            if (info.uses.Count == 0) return;

            GUILayout.Space(6);
            GUILayout.Label("Variables this expects", EditorStyles.boldLabel);

            bool missing = false;
            foreach (var u in info.uses)
            {
                bool has = data.vars.Exists(x => x.name == u.name);
                missing |= !has;
                GUILayout.Label((has ? "    " : "    missing:  ") + u.name +
                                (string.IsNullOrEmpty(u.def) ? "" : "   default " + u.def) +
                                (string.IsNullOrEmpty(u.type) ? "" : "   (" + u.type + ")"), EditorStyles.miniLabel);
            }

            if (missing && GUILayout.Button("Add the missing variables", GUILayout.Height(20)))
            {
                var uses = info.uses;
                pending = () =>
                {
                    foreach (var u in uses)
                        if (!data.vars.Exists(x => x.name == u.name)) data.vars.Add(new TVar(u.name, u.def));
                };
            }
        }

        // ---------------- When ----------------

        void DrawWhen(TLoop L)
        {
            BeginBlock("When", "When does this loop run?");

            if (L.events.Count == 0)
                EditorGUILayout.HelpBox("None. Without an event this loop never runs.", MessageType.None);

            for (int i = 0; i < L.events.Count; i++)
            {
                var E = L.events[i];
                int idx = i;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();

                if (E.kind == EvKind.Block) GUILayout.Label("Template   " + E.template, cardTitle);
                else if (E.kind == EvKind.Raw) GUILayout.Label("Kept as written", cardTitle);
                else
                {
                    var def = TriggerCatalog.FindEvent(E.id);
                    GUILayout.Label(def != null ? def.title : E.id, cardTitle);
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(20)))
                    pending = () => L.events.RemoveAt(idx);
                EditorGUILayout.EndHorizontal();

                if (E.kind == EvKind.Event)
                {
                    var def = TriggerCatalog.FindEvent(E.id);
                    if (def != null)
                    {
                        if (!string.IsNullOrEmpty(def.hint)) GUILayout.Label(def.hint, blockDesc);
                        for (int p = 0; p < def.ps.Length; p++) DrawParam(def.ps[p], E.Get(def.ps[p].attr));
                    }
                }
                else if (E.kind == EvKind.Block) DrawBlockRef(ref E.template, ref E.prefix, true);
                else DrawRaw(E);

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Event", GUILayout.Height(22))) ShowEventMenu(L);

            EndBlock();
        }

        void ShowEventMenu(TLoop L)
        {
            var menu = new GenericMenu();

            for (int i = 0; i < TriggerCatalog.Events.Length; i++)
            {
                var def = TriggerCatalog.Events[i];
                bool used = L.events.Exists(x => x.kind == EvKind.Event && x.id == def.id);

                if (used) menu.AddDisabledItem(new GUIContent(def.menu));
                else menu.AddItem(new GUIContent(def.menu), false, () =>
                {
                    pending = () =>
                    {
                        var E = new TEvent { id = def.id };
                        for (int p = 0; p < def.ps.Length; p++) E.ps.Add(new TParam(def.ps[p].attr, TValue.Parse(def.ps[p].def)));
                        L.events.Add(E);
                    };
                    Repaint();
                });
            }

            menu.AddSeparator("");
            AddTemplateItems(menu, "From Template/", l => l.hasEvents, full =>
            {
                pending = () => L.events.Add(new TEvent { kind = EvKind.Block, template = full });
                Repaint();
            });

            menu.ShowAsContext();
        }

        // ---------------- Only If ----------------

        void DrawOnlyIf(TLoop L)
        {
            BeginBlock("Only If", "What must be true?");

            if (L.conds.Count == 0)
                EditorGUILayout.HelpBox("Nothing. The loop runs every time the event happens.", MessageType.None);

            if (L.conds.Count > 1)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Match", GUILayout.Width(44));
                int cur = L.rootOp == "Or" ? 1 : 0;
                int nv = EditorGUILayout.Popup(cur, new[] { "All of these", "Any of these" }, GUILayout.Width(120));
                if (nv != cur) { L.rootOp = nv == 1 ? "Or" : "And"; Touch(); }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            DrawCondList(L.conds);

            if (GUILayout.Button("+ Add Condition", GUILayout.Height(22))) ShowCondMenu(L.conds);

            EndBlock();
        }

        void DrawCondList(List<TCond> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var C = list[i];
                int idx = i;

                EditorGUILayout.BeginVertical("box");

                switch (C.kind)
                {
                    case CondKind.Compare: DrawCompare(C, list, idx); break;

                    case CondKind.Group:
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label("Group", cardTitle, GUILayout.Width(50));
                            int cur = C.op == "Or" ? 1 : 0;
                            int nv = EditorGUILayout.Popup(cur, new[] { "All of these", "Any of these" }, GUILayout.Width(120));
                            if (nv != cur) { C.op = nv == 1 ? "Or" : "And"; Touch(); }
                            GUILayout.FlexibleSpace();
                            DrawMoveButtons(list, idx);
                            EditorGUILayout.EndHorizontal();

                            DrawCondList(C.children);
                            if (GUILayout.Button("+ Add To Group", GUILayout.Height(20))) ShowCondMenu(C.children);
                            break;
                        }

                    case CondKind.Select:
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label("Look through a list", cardTitle);
                            GUILayout.FlexibleSpace();
                            DrawMoveButtons(list, idx);
                            EditorGUILayout.EndHorizontal();

                            DrawParam(new ParamDef("Object", "Put each into"), C.selObject);
                            DrawParam(new ParamDef("From", "List", ParamKind.Text, "Equipped"), C.selFrom);

                            bool nn = EditorGUILayout.ToggleLeft("None of them may match", C.not);
                            if (nn != C.not) { C.not = nn; Touch(); }

                            DrawCondList(C.children);
                            if (GUILayout.Button("+ Add To List Test", GUILayout.Height(20))) ShowCondMenu(C.children);
                            break;
                        }

                    case CondKind.Block:
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label("Template   " + C.template, cardTitle);
                            GUILayout.FlexibleSpace();
                            DrawMoveButtons(list, idx);
                            EditorGUILayout.EndHorizontal();
                            DrawBlockRef(ref C.template, ref C.prefix, false);
                            break;
                        }

                    default:
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Label("Kept as written", cardTitle);
                            GUILayout.FlexibleSpace();
                            DrawMoveButtons(list, idx);
                            EditorGUILayout.EndHorizontal();
                            string nr = EditorGUILayout.TextArea(C.raw, GUILayout.MinHeight(38));
                            if (nr != C.raw) { C.raw = nr; Touch(); }
                            break;
                        }
                }

                EditorGUILayout.EndVertical();
            }
        }

        static readonly string[] CompareLabels =
        {
            "is equal to", "is not equal to",
            "is greater than", "is not greater than",
            "is less than", "is not less than"
        };

        void DrawCompare(TCond C, List<TCond> list, int idx)
        {
            DrawParam(new ParamDef("Value1", "Check"), C.a);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(78));

            int cur = ((int)C.cmp) * 2 + (C.not ? 1 : 0);
            int nv = EditorGUILayout.Popup(cur, CompareLabels, GUILayout.Width(150));
            if (nv != cur) { C.cmp = (Cmp)(nv / 2); C.not = (nv % 2) == 1; Touch(); }

            GUILayout.FlexibleSpace();
            DrawMoveButtons(list, idx);
            EditorGUILayout.EndHorizontal();

            DrawParam(new ParamDef("Value2", "Against"), C.b);
        }

        void ShowCondMenu(List<TCond> list)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Compare two values"), false,
                () => { pending = () => list.Add(new TCond { kind = CondKind.Compare }); Repaint(); });

            menu.AddItem(new GUIContent("Group (all of / any of)"), false,
                () => { pending = () => list.Add(new TCond { kind = CondKind.Group }); Repaint(); });

            menu.AddItem(new GUIContent("Look through a list"), false,
                () =>
                {
                    pending = () => list.Add(new TCond { kind = CondKind.Select, selFrom = TValue.Literal("Equipped") });
                    Repaint();
                });

            menu.AddSeparator("");

            AddQuickCond(menu, list, "Quick/Triggered by the Player", "?getModel[_$Model].AI", "0", false);
            AddQuickCond(menu, list, "Quick/Triggered by a Bot", "?getModel[_$Model].AI", "0", true);
            AddQuickCond(menu, list, "Quick/This Trigger is Active", "_$Active", "1", false);
            AddQuickCond(menu, list, "Quick/Key Pressed was Up", "_$Key", "Up", false);
            AddQuickCond(menu, list, "Quick/Signal Matches a Variable", "_$ActionID", "_Activator", false);
            AddQuickCond(menu, list, "Quick/Player is Standing Still", "?getModel[Player].animationName", "Stand", false);
            AddQuickCond(menu, list, "Quick/Model Faces Right", "?getModel[_$Model].direction", "1", false);

            menu.AddSeparator("");
            AddTemplateItems(menu, "From Template/", l => l.hasConditions, full =>
            {
                pending = () => list.Add(new TCond { kind = CondKind.Block, template = full });
                Repaint();
            });

            menu.ShowAsContext();
        }

        void AddQuickCond(GenericMenu menu, List<TCond> list, string path, string a, string b, bool not, Cmp cmp = Cmp.Equal)
        {
            menu.AddItem(new GUIContent(path), false, () =>
            {
                pending = () => list.Add(new TCond { kind = CondKind.Compare, cmp = cmp, not = not, a = TValue.Parse(a), b = TValue.Parse(b) });
                Repaint();
            });
        }

        // ---------------- Do ----------------

        void DrawDo(TLoop L)
        {
            BeginBlock("Do", "What happens next?");

            if (L.acts.Count == 0)
                EditorGUILayout.HelpBox("Nothing yet.", MessageType.None);

            DrawActList(L.acts);

            if (GUILayout.Button("+ Add Action", GUILayout.Height(22))) ShowActMenu(L.acts);

            EndBlock();
        }

        void DrawActList(List<TAct> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var A = list[i];
                int idx = i;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();

                if (A.kind == ActKind.Choose) GUILayout.Label("Run Order", cardTitle, GUILayout.Width(70));
                else if (A.kind == ActKind.Block) GUILayout.Label("Template   " + A.template, cardTitle);
                else if (A.kind == ActKind.Raw) GUILayout.Label("Kept as written", cardTitle);
                else
                {
                    var def = TriggerCatalog.FindAction(A.id);
                    GUILayout.Label(def != null ? def.title : A.id, cardTitle);
                }

                if (A.kind == ActKind.Choose)
                {
                    string[] labels = { "One after another", "All at the same time", "Pick at random" };
                    string[] vals = { "Straight", "Sync", "Random" };

                    string order = A.order;     // read once, the popup below changes A.order mid-event
                    int cur = Mathf.Max(0, Array.IndexOf(vals, order));
                    int nv = EditorGUILayout.Popup(cur, labels, GUILayout.Width(150));
                    if (nv != cur) { A.order = vals[nv]; Touch(); }

                    if (order == "Random")
                    {
                        GUILayout.Label("How many", GUILayout.Width(62));
                        int ns = Mathf.Max(1, EditorGUILayout.IntField(A.set, GUILayout.Width(40)));
                        if (ns != A.set) { A.set = ns; Touch(); }
                    }
                }

                GUILayout.FlexibleSpace();
                DrawMoveButtonsAct(list, idx);
                EditorGUILayout.EndHorizontal();

                if (A.kind == ActKind.Action)
                {
                    var def = TriggerCatalog.FindAction(A.id);
                    if (def != null)
                    {
                        if (!string.IsNullOrEmpty(def.hint)) GUILayout.Label(def.hint, blockDesc);
                        for (int p = 0; p < def.ps.Length; p++) DrawParam(def.ps[p], A.Get(def.ps[p].attr));
                    }
                }
                else if (A.kind == ActKind.Choose)
                {
                    DrawActList(A.children);
                    if (GUILayout.Button("+ Add To Group", GUILayout.Height(20))) ShowActMenu(A.children);
                }
                else if (A.kind == ActKind.Block) DrawBlockRef(ref A.template, ref A.prefix, false);
                else
                {
                    string nr = EditorGUILayout.TextArea(A.raw, GUILayout.MinHeight(38));
                    if (nr != A.raw) { A.raw = nr; Touch(); }
                }

                EditorGUILayout.EndVertical();
            }
        }

        void ShowActMenu(List<TAct> list)
        {
            var menu = new GenericMenu();

            for (int i = 0; i < TriggerCatalog.Actions.Length; i++)
            {
                var def = TriggerCatalog.Actions[i];
                menu.AddItem(new GUIContent(def.menu), false, () =>
                {
                    pending = () =>
                    {
                        var A = new TAct { kind = ActKind.Action, id = def.id };
                        for (int p = 0; p < def.ps.Length; p++) A.ps.Add(new TParam(def.ps[p].attr, TValue.Parse(def.ps[p].def)));
                        list.Add(A);
                    };
                    Repaint();
                });
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Flow/Run Order Group"), false,
                () => { pending = () => list.Add(new TAct { kind = ActKind.Choose, order = "Straight" }); Repaint(); });

            menu.AddSeparator("");
            AddTemplateItems(menu, "From Template/", l => l.hasActions, full =>
            {
                pending = () => list.Add(new TAct { kind = ActKind.Block, template = full });
                Repaint();
            });

            menu.ShowAsContext();
        }

        // ---------------- Shared bits ----------------

        void AddTemplateItems(GenericMenu menu, string prefix, Func<TriggerTemplateLibrary.LoopInfo, bool> filter, Action<string> pick)
        {
            if (!TriggerTemplateLibrary.Available)
            {
                menu.AddDisabledItem(new GUIContent(prefix + "TriggerTemplates.xml not found"));
                return;
            }

            foreach (var t in TriggerTemplateLibrary.Templates)
                foreach (var l in t.loops)
                {
                    if (!filter(l)) continue;
                    string full = l.Full;
                    menu.AddItem(new GUIContent(prefix + t.name + "/" + l.name), false, () => pick(full));
                }
        }

        void BeginBlock(string title, string desc)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(title, blockTitle);
            GUILayout.Label(desc, blockDesc);
            GUILayout.Space(2);
        }

        void EndBlock()
        {
            EditorGUILayout.EndVertical();
        }

        void DrawMoveButtons(List<TCond> list, int idx)
        {
            using (new EditorGUI.DisabledScope(idx == 0))
                if (GUILayout.Button("^", EditorStyles.miniButtonLeft, GUILayout.Width(20)))
                    pending = () => { var t = list[idx]; list.RemoveAt(idx); list.Insert(idx - 1, t); };

            using (new EditorGUI.DisabledScope(idx >= list.Count - 1))
                if (GUILayout.Button("v", EditorStyles.miniButtonMid, GUILayout.Width(20)))
                    pending = () => { var t = list[idx]; list.RemoveAt(idx); list.Insert(idx + 1, t); };

            if (GUILayout.Button("x", EditorStyles.miniButtonRight, GUILayout.Width(20)))
                pending = () => list.RemoveAt(idx);
        }

        void DrawMoveButtonsAct(List<TAct> list, int idx)
        {
            using (new EditorGUI.DisabledScope(idx == 0))
                if (GUILayout.Button("^", EditorStyles.miniButtonLeft, GUILayout.Width(20)))
                    pending = () => { var t = list[idx]; list.RemoveAt(idx); list.Insert(idx - 1, t); };

            using (new EditorGUI.DisabledScope(idx >= list.Count - 1))
                if (GUILayout.Button("v", EditorStyles.miniButtonMid, GUILayout.Width(20)))
                    pending = () => { var t = list[idx]; list.RemoveAt(idx); list.Insert(idx + 1, t); };

            if (GUILayout.Button("x", EditorStyles.miniButtonRight, GUILayout.Width(20)))
                pending = () => list.RemoveAt(idx);
        }

        void DrawBlockRef(ref string template, ref string prefix, bool isEvent)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Template", GUILayout.Width(78));

            string nt = EditorGUILayout.TextField(template);
            if (nt != template) { template = nt; Touch(); }

            GUILayout.Label(new GUIContent("Prefix", "Makes the template read 1Sound instead of Sound, for example."),
                GUILayout.Width(44));
            string np = EditorGUILayout.TextField(prefix ?? "", GUILayout.Width(50));
            if (np != prefix) { prefix = np; Touch(); }

            EditorGUILayout.EndHorizontal();

            DrawTemplateUses(TriggerTemplateLibrary.Find(template));
        }

        void DrawRaw(TEvent E)
        {
            string nr = EditorGUILayout.TextArea(E.raw, GUILayout.MinHeight(38));
            if (nr != E.raw) { E.raw = nr; Touch(); }
        }

        // ---------------- Value editing ----------------

        void DrawParam(ParamDef pd, TValue v)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(pd.label, pd.hint), GUILayout.Width(78));
            DrawValue(pd, v);
            EditorGUILayout.EndHorizontal();
        }

        static readonly string[] ValueModes = { "Text", "Variable", "Model", "Node" };

        void DrawValue(ParamDef pd, TValue v)
        {
            // a variable name slot is just a plain name, no underscore and no query
            if (pd != null && pd.kind == ParamKind.VarName)
            {
                string s = EditorGUILayout.TextField(v.text);
                if (s != v.text) { v.mode = ValueMode.Literal; v.text = s; Touch(); }
                PickMenu(data.VarNames(), null, picked => { v.mode = ValueMode.Literal; v.text = picked; });
                return;
            }

            var mode = v.mode;      // read once, the popup below changes v.mode mid-event
            int m = (int)mode;
            int nm = EditorGUILayout.Popup(m, ValueModes, GUILayout.Width(70));
            if (nm != m) { v.mode = (ValueMode)nm; Touch(); }

            switch (mode)
            {
                case ValueMode.Variable:
                    {
                        string s = EditorGUILayout.TextField(v.varName);
                        if (s != v.varName) { v.varName = s; Touch(); }
                        PickMenu(data.VarNames(), null, picked => v.varName = picked);
                        break;
                    }

                case ValueMode.ModelProp:
                    {
                        DrawRef(v, false, TriggerCatalog.CommonModels, 110f);
                        int cur = Array.IndexOf(TriggerCatalog.ModelProps, v.prop);
                        int nv = EditorGUILayout.Popup(cur < 0 ? 0 : cur, TriggerCatalog.ModelProps);
                        if (nv != cur) { v.prop = TriggerCatalog.ModelProps[nv]; Touch(); }
                        break;
                    }

                case ValueMode.NodePos:
                    {
                        DrawRef(v, false, TriggerCatalog.CommonModels, 86f);
                        DrawRef(v, true, TriggerCatalog.CommonNodes, 76f);
                        int cur = Array.IndexOf(TriggerCatalog.NodeCoords, v.coord);
                        int nv = EditorGUILayout.Popup(cur < 0 ? 0 : cur, TriggerCatalog.NodeCoords, GUILayout.Width(106));
                        if (nv != cur) { v.coord = TriggerCatalog.NodeCoords[nv]; Touch(); }
                        break;
                    }

                default:
                    {
                        string s = EditorGUILayout.TextField(v.text);
                        if (s != v.text) { v.text = s; Touch(); }

                        if (pd != null && pd.choices != null && pd.choices.Length > 0)
                            PickMenu(pd.choices, v.text, picked => v.text = picked);
                        break;
                    }
            }
        }

        // model or node slot, either a plain name or a variable
        void DrawRef(TValue v, bool nodeSlot, string[] literals, float width)
        {
            string shown = nodeSlot ? v.NodeRef : v.ModelRef;

            string ns = EditorGUILayout.TextField(shown, GUILayout.Width(width));
            if (ns != shown)
            {
                bool isVar = ns.Length > 1 && ns[0] == '_';
                string name = isVar ? ns.Substring(1) : ns;
                if (nodeSlot) { v.nodeIsVar = isVar; v.node = name; }
                else { v.modelIsVar = isVar; v.model = name; }
                Touch();
            }

            var items = new List<string>();
            for (int i = 0; i < literals.Length; i++) items.Add("Name/" + literals[i]);
            foreach (var vn in data.VarNames()) items.Add("Variable/" + vn);

            PickMenu(items, null, picked =>
            {
                bool isVar = picked.StartsWith("Variable/");
                string name = picked.Substring(picked.IndexOf('/') + 1);
                if (nodeSlot) { v.nodeIsVar = isVar; v.node = name; }
                else { v.modelIsVar = isVar; v.model = name; }
            });
        }

        // little dropdown arrow next to a text field, the callback fires on a later event
        void PickMenu(IList<string> items, string current, Action<string> apply)
        {
            var dr = GUILayoutUtility.GetRect(16f, 18f, GUILayout.Width(16));
            if (!EditorGUI.DropdownButton(dr, GUIContent.none, FocusType.Passive, EditorStyles.miniPullDown)) return;

            var menu = new GenericMenu();
            for (int i = 0; i < items.Count; i++)
            {
                string it = items[i];
                menu.AddItem(new GUIContent(it), current != null && current == it,
                    () =>
                    {
                        apply(it);
                        Touch();
                        ClearFocus();
                    });
            }
            menu.DropDown(dr);
        }

        // ================= XML strip =================

        void DrawXmlStrip(Rect r)
        {
            GUI.Box(r, GUIContent.none, EditorStyles.helpBox);
            var pad = new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f);

            // serialise once per layout pass; Layout and the event after it must agree on text and size
            if (Event.current.type == EventType.Layout)
            {
                xmlCache = data.ToXml();
                xmlSize = xmlStyle.CalcSize(new GUIContent(xmlCache));
            }

            GUILayout.BeginArea(pad);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("XML Preview", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy", EditorStyles.miniButton, GUILayout.Width(60)))
                EditorGUIUtility.systemCopyBuffer = xmlCache;
            EditorGUILayout.EndHorizontal();

            xmlScroll = EditorGUILayout.BeginScrollView(xmlScroll);
            EditorGUILayout.SelectableLabel(xmlCache, xmlStyle,
                GUILayout.MinWidth(xmlSize.x + 12f), GUILayout.MinHeight(xmlSize.y + 4f),
                GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }

    [CustomEditor(typeof(TriggerComponent))]
    public class TriggerComponentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            GUILayout.Space(6);
            if (GUILayout.Button("Open Trigger Editor", GUILayout.Height(32)))
                TriggerEditor.Open((TriggerComponent)target);
        }
    }
}