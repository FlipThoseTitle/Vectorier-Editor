using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEngine;
using Vectorier.Component;
using Vectorier.Core;
using Vectorier.ProjectManager;
using Section = Vectorier.Trigger.TriggerTemplateLibrary.Section;
using LoopInfo = Vectorier.Trigger.TriggerTemplateLibrary.LoopInfo;
using UsingVar = Vectorier.Trigger.TriggerTemplateLibrary.UsingVar;

namespace Vectorier.Trigger
{
    public class TriggerEditor : EditorWindow
    {
        const string PresetFolder = "Assets/Editor/TriggerEditor/Presets";
        const float TopH = 22f;
        const float RowH = 20f;
        const float SplitW = 5f;
        const float XmlH = 170f;
        const float VarNameW = 78f;
        const float VarTypeW = 78f;

        // pick menus longer than this get split into A/B/C submenus
        const int MenuGroupThreshold = 30;

        TriggerComponent target;
        TriggerData data = TriggerData.CreateDefault();
        bool dirty;

        readonly List<TriggerPresetAsset> presets = new List<TriggerPresetAsset>();
        int presetIdx;                       // 0 = Default
        string presetName = "Default";

        float leftW = 330f, loopsH = 260f;
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
        Action pending;                      // deferred edit, see ApplyPending

        GUIStyle blockTitle, blockDesc, cardTitle, xmlStyle;
        GUIStyle rowText, rowTextDark, rowTextSel, rowIcon, rowIconDark, rowIconSel;
        GUIStyle summaryHead, summaryLine, missingStyle, toggleText;
        bool stylesProSkin;

        float paramLabelW = 78f;             // set per card by LabelW()

        const int NoSel = -2;                 // -1 already means nothing selected
        int pendingSel = NoSel;
        int hoverIdx = -1;
        bool multiSelected;
        string xmlCache = "";
        Vector2 xmlSize;
        readonly List<int> shownBuf = new List<int>();

        static readonly string[] TriggerNodes = { "COM", "DetectorH", "DetectorV" };
        static readonly Color RowHoverColor = Color.white;
        static readonly Color RowSelectedColor = new Color(0.24f, 0.49f, 0.91f, 1f);

        // condition slots arent in the catalog, so their defs live here
        static readonly ParamDef CheckDef = new ParamDef("Value1", "Check").Queries();
        static readonly ParamDef AgainstDef = new ParamDef("Value2", "Against").Queries();
        static readonly ParamDef SelObjectDef = new ParamDef("Object", "Put each into");
        static readonly ParamDef SelFromDef = new ParamDef("From", "List", VType.String, "Equipped");

        [MenuItem("Vectorier/Tools/⚑  Trigger Editor", false, 27)]
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
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            ActiveProject.Changed += OnActiveProjectChanged;
            multiSelected = Selection.gameObjects.Length > 1;
            TriggerTemplateLibrary.Reload();
            ValueSources.InvalidateProject();
            RefreshPresets();
            Bind(FindSelected(), true);
        }

        void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
            ActiveProject.Changed -= OnActiveProjectChanged;
        }

        void OnHierarchyChanged() { ValueSources.InvalidateAll(); Repaint(); }

        // ./Projects is outside Assets, so projectChanged won't see edits there. re-read on focus instead
        void OnFocus() { ValueSources.InvalidateModels(); ValueSources.InvalidateProject(); }

        void OnProjectChanged() { ValueSources.InvalidateModels(); Repaint(); }
        void OnActiveProjectChanged(string _) { ValueSources.InvalidateProject(); Repaint(); }

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
                if (t) ReadTypes(data, t.variableTypes);
                if (data.loops.Count > 0) sel = 0;
            }
        }

        void Touch() { dirty = true; }

        void Styles()
        {
            // rebuild on skin change or row text keeps the old skin's colour
            if (blockTitle != null && stylesProSkin == EditorGUIUtility.isProSkin) return;
            stylesProSkin = EditorGUIUtility.isProSkin;

            blockTitle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            blockDesc = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            cardTitle = new GUIStyle(EditorStyles.boldLabel);

            Color neutral = EditorStyles.label.normal.textColor;

            rowText = MakeRowStyle(neutral, FontStyle.Normal);
            rowIcon = MakeRowStyle(neutral, FontStyle.Bold);
            rowTextDark = MakeRowStyle(Color.black, FontStyle.Normal);   // readable on the hover/selection colours
            rowIconDark = MakeRowStyle(Color.black, FontStyle.Bold);
            rowTextSel = MakeRowStyle(Color.white, FontStyle.Normal);
            rowIconSel = MakeRowStyle(Color.white, FontStyle.Bold);

            // head is derived from line, otherwise the baselines don't match
            summaryLine = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            summaryHead = new GUIStyle(summaryLine) { fontStyle = FontStyle.Bold, wordWrap = false };

            // miniBoldLabel sits a pixel off, so build the bold one from miniLabel
            missingStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };

            // lines up with the checkbox next to it
            toggleText = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fixedHeight = EditorGUIUtility.singleLineHeight,
                margin = new RectOffset(EditorStyles.miniLabel.margin.left, EditorStyles.miniLabel.margin.right, 2, 2)
            };

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

            bool unsaved = dirty && target;
            if (hasUnsavedChanges != unsaved)
            {
                hasUnsavedChanges = unsaved;
                saveChangesMessage = unsaved
                    ? $"'{target.gameObject.name}' has changes that were never applied.\nApply them before closing?"
                    : "";
            }

            // only touch data on Layout. changing it between Layout and the next event
            // is what caused the GUILayout mismatch errors
            if (ev.type == EventType.Layout) ApplyPending();

            // multi-select: draw greyed out, swallow input
            if (multiSelected && ev.type != EventType.Layout && ev.type != EventType.Repaint) return;

            if (ev.type == EventType.MouseLeaveWindow && hoverIdx != -1) { hoverIdx = -1; Repaint(); }

            using (new EditorGUI.DisabledScope(multiSelected))
                DrawWindow();

            if (multiSelected) { DrawMultiSelectOverlay(); return; }

            if (ev.type == EventType.MouseDown && ev.button == 0) ClearFocus();

            if (ev.type != EventType.Layout && ev.type != EventType.Repaint &&
                (pending != null || pendingSel != NoSel)) Repaint();
        }

        void DrawWindow()
        {
            bool xml = showXml;      // cached, the toggle can flip mid-event

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

            dirty = true;    // before p(), Apply/LoadPreset clear it themselves
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

            if (GUILayout.Button(new GUIContent("Reload Templates",
                        "Re-read TriggerTemplates.xml, the model list, the scene's spawn points, " +
                        "and the active project's animations and sounds"),
                    EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                TriggerTemplateLibrary.Reload();
                ValueSources.InvalidateAll();
            }

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
            target.variableTypes = WriteTypes(data);
            EditorUtility.SetDirty(target);
            dirty = false;
        }

        // types arent in the xml, they're kept in variableTypes on the component/preset
        static void ReadTypes(TriggerData d, List<TriggerComponent.VariableType> list)
        {
            if (list == null) return;
            foreach (var v in d.vars)
            {
                if (v.locked) continue;
                var e = list.Find(x => x != null && x.name == v.name);
                if (e != null) v.type = VTypes.FromName(e.type);
            }
        }

        static List<TriggerComponent.VariableType> WriteTypes(TriggerData d)
        {
            var list = new List<TriggerComponent.VariableType>();
            foreach (var v in d.vars)
                if (!v.locked && !string.IsNullOrEmpty(v.name) && v.type != VType.String)
                    list.Add(new TriggerComponent.VariableType { name = v.name, type = v.type.ToString() });
            return list;
        }

        // called from Unity's close prompt (hasUnsavedChanges)
        public override void SaveChanges()
        {
            Apply();
            base.SaveChanges();
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
                if (target) ReadTypes(data, target.variableTypes);
            }
            else
            {
                var a = presets[idx - 1];
                presetName = a.name;
                data = TriggerData.Parse(a.contentXml);
                ReadTypes(data, a.variableTypes);
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
            a.variableTypes = WriteTypes(data);

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
            a.variableTypes = WriteTypes(data);
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
            bool mouseInList = GUI.enabled && listR.Contains(e.mousePosition);   // has to be read before BeginScrollView, coords change inside it
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

                if (e.type == EventType.Repaint)
                {
                    if (selected) EditorGUI.DrawRect(row, RowSelectedColor);
                    else if (hovered) EditorGUI.DrawRect(row, RowHoverColor);
                }

                // text has to contrast with whatever background the row got
                GUIStyle txt, ico;
                if (selected && hovered) { txt = rowTextSel; ico = rowIconSel; }
                else if (selected || hovered) { txt = rowTextDark; ico = rowIconDark; }
                else { txt = rowText; ico = rowIcon; }

                var iconR = new Rect(row.x + 4f, row.y + 1f, 16f, RowH - 2f);
                if (L.kind != LoopKind.Custom)
                    GUI.Label(iconR, new GUIContent("T", "Built from a template"), ico);

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
                    GUI.Label(nameR, L.Display, txt);

                    if (e.type == EventType.MouseDown && e.button == 0 && overRow)
                    {
                        pendingSel = i;          // deferred, the main panel cant switch loops mid event
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

            // empty space in the list deselects
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

            // MouseMove fires a lot, only repaint when the hovered row changes
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
                    GUILayout.Label(new GUIContent(v.name, LockedHint(v.name)), EditorStyles.boldLabel, GUILayout.Width(VarNameW));
                    DrawLockedValue(v);
                    GUILayout.Space(VarTypeW + 26f);      // keeps columns aligned with the editable rows
                }
                else
                {
                    string nn = EditorGUILayout.DelayedTextField(v.name, GUILayout.Width(VarNameW), GUILayout.MinWidth(50));
                    if (Event.current.type == EventType.Repaint)
                        GUI.Label(GUILayoutUtility.GetLastRect(), new GUIContent(string.Empty, v.name));
                    if (nn != v.name) { RenameVariable(v.name, nn); Touch(); }

                    VType t = v.type;      // cached, the popup below can change it
                    string nv = DrawLiteral(t, v.value, null, ref v.uiCustom, picked => v.value = picked);
                    if (nv != v.value) { v.value = nv; Touch(); }

                    int ct = Array.IndexOf(VTypes.VariableTypes, t);
                    int nt = EditorGUILayout.Popup(ct, VTypes.VariableTypeLabels, GUILayout.Width(VarTypeW));
                    if (nt != ct && nt >= 0)
                    {
                        v.type = VTypes.VariableTypes[nt];
                        v.value = VTypes.Coerce(v.type, v.value, ValueSources.Models);
                        v.uiCustom = false;
                        Touch();
                    }

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
                    data.vars.Add(new TVar(n, "", VType.String));
                };
        }

        static string LockedHint(string n)
        {
            switch (n)
            {
                case "$Active": return "Is the trigger switched on? Ticked = 1, unticked = 0.";
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
                bool on = v.value != "0";
                bool nb = EditorGUILayout.Toggle(on, GUILayout.Width(18));
                GUILayout.Label(nb ? "Active" : "Inactive", toggleText);
                GUILayout.FlexibleSpace();
                if (nb != on) { v.value = nb ? "1" : "0"; Touch(); }
                return;
            }

            if (v.name == "$AI")
            {
                string[] vals = VTypes.AIValues;
                string[] labels = VTypes.AILabels;

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

                var dr = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18));
                if (ArrowButton(dr))
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

        // also renames every reference to it
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
            if (v.mode == ValueMode.Literal && v.text == o) v.text = n;   // Name on Set/Add Variable. also hits any literal that happens to match, careful
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
            var kind = L.kind;      // cached, "Build From" can change it mid-event

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

        // ---------------- Templates ----------------

        void DrawFullTemplate(TLoop L)
        {
            EditorGUILayout.HelpBox("The whole template runs as it is. Its loops are listed below.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Template", GUILayout.Width(74));
            string nn = EditorGUILayout.TextField(L.template);
            if (nn != L.template) { L.template = nn; Touch(); }

            var dr = GUILayoutUtility.GetRect(20f, 18f, GUILayout.Width(20));
            if (ArrowButton(dr))
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
            if (info == null) { NotFound(L.template); return; }

            GUILayout.Space(8);
            GUILayout.Label("What this template does", EditorStyles.boldLabel);
            VariableLegend();
            foreach (var l in info.loops) DrawLoopCard(l, true);

            DrawRequired(TriggerTemplateLibrary.CollectUses(info.loops, ""));
        }

        void DrawTemplateLoop(TLoop L)
        {
            EditorGUILayout.HelpBox("This loop runs a ready-made loop from TriggerTemplates.xml.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Template Loop", GUILayout.Width(90));
            string nn = EditorGUILayout.TextField(L.template);
            if (nn != L.template) { L.template = nn; Touch(); }

            var dr = GUILayoutUtility.GetRect(20f, 18f, GUILayout.Width(20));
            if (ArrowButton(dr))
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

            var info = TriggerTemplateLibrary.Find(L.template);
            if (info == null) { NotFound(L.template); return; }

            GUILayout.Space(8);
            GUILayout.Label("What this loop does", EditorStyles.boldLabel);
            VariableLegend();
            DrawLoopCard(info, false);

            DrawRequired(TriggerTemplateLibrary.CollectUses(new[] { info }, ""));
        }

        // read-only summary of a template loop
        void DrawLoopCard(LoopInfo l, bool showName)
        {
            EditorGUILayout.BeginVertical("box");
            if (showName) GUILayout.Label(string.IsNullOrEmpty(l.name) ? "(unnamed loop)" : l.name, cardTitle);

            // separator only between sections that actually have lines
            bool any = false;
            Section[] order = { Section.When, Section.OnlyIf, Section.Do };
            string[] heads = { "When", "Only If", "Do" };
            for (int i = 0; i < order.Length; i++)
            {
                var lines = l.Lines(order[i]);
                if (lines == null || lines.Count == 0) continue;
                if (any) Separator();
                DrawSummary(heads[i], lines);
                any = true;
            }
            if (!any) GUILayout.Label("This loop is empty.", summaryLine);

            EditorGUILayout.EndVertical();
        }

        bool DrawSummary(string head, List<string> lines)
        {
            if (lines == null || lines.Count == 0) return false;

            // header only on the first row so it lines up with the first line
            for (int i = 0; i < lines.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(i == 0 ? head : "", summaryHead, GUILayout.Width(52));
                GUILayout.Label(lines[i], summaryLine);
                EditorGUILayout.EndHorizontal();
            }
            return true;
        }

        void VariableLegend()
        {
            GUILayout.Label("Names in { } are variables.", blockDesc);
            Separator();
        }

        static void Separator()
        {
            GUILayout.Space(3);
            var r = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(r, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.15f) : new Color(0f, 0f, 0f, 0.2f));
            GUILayout.Space(3);
        }

        void NotFound(string template)
        {
            if (string.IsNullOrEmpty(template)) return;
            GUILayout.Label(TriggerTemplateLibrary.LoadFailed
                    ? "TriggerTemplates.xml couldn't be read."
                    : "'" + template + "' isn't in TriggerTemplates.xml.", blockDesc);
        }

        // the template's <Using> vars, plus a button to add the missing ones
        void DrawRequired(List<UsingVar> uses)
        {
            if (uses == null || uses.Count == 0) return;

            GUILayout.Space(8);
            GUILayout.Label("Required Variables", EditorStyles.boldLabel);
            GUILayout.Label("The template reads these from the Variables panel. Add the missing ones, then set their values there.", blockDesc);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Name", EditorStyles.miniBoldLabel, GUILayout.Width(140));
            GUILayout.Label("Type", EditorStyles.miniBoldLabel, GUILayout.Width(80));
            GUILayout.Label("Default", EditorStyles.miniBoldLabel, GUILayout.Width(90));
            GUILayout.Label("Status", EditorStyles.miniBoldLabel);
            EditorGUILayout.EndHorizontal();

            int missing = 0;
            foreach (var u in uses)
            {
                var have = data.vars.Find(x => x.name == u.name);
                string status = have == null ? "Missing"
                              : have.type != u.vtype && !string.IsNullOrEmpty(u.type) ? "Added (as " + have.type + ")"
                              : "Added";
                if (have == null) missing++;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent(u.name, u.name), EditorStyles.miniLabel, GUILayout.Width(140));
                GUILayout.Label(u.vtype.ToString(), EditorStyles.miniLabel, GUILayout.Width(80));
                GUILayout.Label(string.IsNullOrEmpty(u.def) ? "(empty)" : u.def, EditorStyles.miniLabel, GUILayout.Width(90));
                GUILayout.Label(status, have == null ? missingStyle : EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            if (missing > 0) GUILayout.Space(6);
            if (missing > 0 && GUILayout.Button("Add Missing Variables (" + missing + ")", GUILayout.Height(20)))
            {
                var list = uses;
                pending = () =>
                {
                    foreach (var u in list)
                        if (!data.vars.Exists(x => x.name == u.name))
                            data.vars.Add(new TVar(u.name, VTypes.Coerce(u.vtype, u.def, ValueSources.Models), u.vtype));
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
                        paramLabelW = LabelW(def.ps);
                        for (int p = 0; p < def.ps.Length; p++) DrawParam(def.ps[p], E.Get(def.ps[p].attr));
                    }
                }
                else if (E.kind == EvKind.Block) DrawBlockRef(ref E.template, ref E.prefix, Section.When);
                else DrawRaw(E);

                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Add Event", GUILayout.Height(22))) ShowEventMenu(L);

            EndBlock();
        }

        // no templates here, EventBlock is only read from old files
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

                            paramLabelW = LabelW(SelObjectDef, SelFromDef);
                            DrawParam(SelObjectDef, C.selObject);
                            DrawParam(SelFromDef, C.selFrom);

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
                            DrawBlockRef(ref C.template, ref C.prefix, Section.OnlyIf);
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
            paramLabelW = LabelW(CheckDef, AgainstDef);
            DrawParam(CheckDef, C.a);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("", GUILayout.Width(paramLabelW));

            int cur = ((int)C.cmp) * 2 + (C.not ? 1 : 0);
            int nv = EditorGUILayout.Popup(cur, CompareLabels, GUILayout.Width(150));
            if (nv != cur) { C.cmp = (Cmp)(nv / 2); C.not = (nv % 2) == 1; Touch(); }

            GUILayout.FlexibleSpace();
            DrawMoveButtons(list, idx);
            EditorGUILayout.EndHorizontal();

            DrawParam(AgainstDef, C.b);
        }

        void ShowCondMenu(List<TCond> list)
        {
            var menu = new GenericMenu();

            menu.AddItem(new GUIContent("Compare two values"), false,
                () => { pending = () => list.Add(new TCond { kind = CondKind.Compare }); Repaint(); });

            // no "/" in these labels, GenericMenu turns it into a submenu
            menu.AddItem(new GUIContent("Group"), false,
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

                    string order = A.order;     // cached, the popup below changes it
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

                        // Set/Add Variable: the Value editor matches the picked variable's type.
                        // resolved up front so editing Name mid-event can't change the layout
                        VType follow = VType.String;
                        for (int p = 0; p < def.ps.Length; p++)
                            if (def.ps[p].varName) { follow = data.TypeOf(A.Get(def.ps[p].attr).text); break; }

                        paramLabelW = LabelW(def.ps);
                        for (int p = 0; p < def.ps.Length; p++)
                        {
                            var pd = def.ps[p];
                            DrawParam(pd, A.Get(pd.attr), pd.typedByVariable ? follow : pd.type);
                        }
                    }
                }
                else if (A.kind == ActKind.Choose)
                {
                    DrawActList(A.children);
                    if (GUILayout.Button("+ Add To Group", GUILayout.Height(20))) ShowActMenu(A.children);
                }
                else if (A.kind == ActKind.Block) DrawBlockRef(ref A.template, ref A.prefix, Section.Do);
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

            // not in the catalog since it has children, added under Flow by hand
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

        void AddTemplateItems(GenericMenu menu, string prefix, Func<LoopInfo, bool> filter, Action<string> pick)
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

        // ConditionBlock / ActionBlock, or an EventBlock from an older file
        void DrawBlockRef(ref string template, ref string prefix, Section section)
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

            var info = TriggerTemplateLibrary.Find(template);
            if (info == null) { NotFound(template); return; }

            var lines = info.Lines(section);
            if (lines.Count > 0)
            {
                EditorGUILayout.BeginVertical("box");
                DrawSummary(section == Section.When ? "When" : section == Section.OnlyIf ? "Only If" : "Do", lines);
                EditorGUILayout.EndVertical();
            }

            DrawRequired(TriggerTemplateLibrary.CollectUses(new[] { info }, prefix));
        }

        void DrawRaw(TEvent E)
        {
            string nr = EditorGUILayout.TextArea(E.raw, GUILayout.MinHeight(38));
            if (nr != E.raw) { E.raw = nr; Touch(); }
        }

        // ---------------- Value editing ----------------

        // widen to the longest label (capped), At spawn point was getting cut
        static float LabelW(params ParamDef[] ps)
        {
            float w = 78f;
            if (ps != null)
                foreach (var p in ps)
                    w = Mathf.Max(w, EditorStyles.label.CalcSize(new GUIContent(p.label)).x + 6f);
            return Mathf.Min(w, 180f);
        }

        void DrawParam(ParamDef pd, TValue v) { DrawParam(pd, v, pd.type); }

        void DrawParam(ParamDef pd, TValue v, VType lit)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(pd.label, string.IsNullOrEmpty(pd.hint) ? pd.label : pd.hint), GUILayout.Width(paramLabelW));
            DrawValue(pd, v, lit);
            EditorGUILayout.EndHorizontal();
        }

        static string ModeLabel(ValueMode m, VType lit)
        {
            switch (m)
            {
                case ValueMode.Variable: return "Variable";
                case ValueMode.ModelProp: return "Model";
                case ValueMode.NodePos: return "Node";
                default: return VTypes.SlotLabel(lit);
            }
        }

        // mode popup + the editor for whichever mode is picked
        void DrawValue(ParamDef pd, TValue v, VType lit)
        {
            // name slots are always a plain name, no mode popup
            if (pd.varName)
            {
                string s = EditorGUILayout.TextField(v.text);
                if (s != v.text) { v.mode = ValueMode.Literal; v.text = s; Touch(); }
                VarPicker(pd, lit, picked => { v.mode = ValueMode.Literal; v.text = picked; });
                return;
            }

            var modes = new List<ValueMode> { ValueMode.Literal, ValueMode.Variable };
            if (pd.query) { modes.Add(ValueMode.ModelProp); modes.Add(ValueMode.NodePos); }

            var mode = v.mode;      // cached, the popup below changes it
            if (!modes.Contains(mode)) modes.Add(mode);      // file may have a query where we dont offer one, dont lose it

            var labels = new string[modes.Count];
            for (int i = 0; i < modes.Count; i++) labels[i] = ModeLabel(modes[i], lit);

            int cur = modes.IndexOf(mode);
            int nm = EditorGUILayout.Popup(cur, labels, GUILayout.Width(74));
            if (nm != cur && nm >= 0)
            {
                v.mode = modes[nm];
                if (v.mode == ValueMode.Literal) v.text = VTypes.Coerce(lit, v.text, ValueSources.Models);
                v.uiCustom = false;
                Touch();
            }

            switch (mode)
            {
                case ValueMode.Variable:
                    {
                        string s = EditorGUILayout.TextField(v.varName);
                        if (s != v.varName) { v.varName = s; Touch(); }
                        VarPicker(pd, lit, picked => v.varName = picked);
                        break;
                    }

                case ValueMode.ModelProp:
                    {
                        DrawRef(v, false, 110f);
                        int c = Array.IndexOf(TriggerCatalog.ModelProps, v.prop);
                        int nv = EditorGUILayout.Popup(c < 0 ? 0 : c, TriggerCatalog.ModelProps);
                        if (nv != c) { v.prop = TriggerCatalog.ModelProps[nv]; Touch(); }
                        break;
                    }

                case ValueMode.NodePos:
                    {
                        DrawRef(v, false, 86f);
                        DrawRef(v, true, 76f);
                        int c = Array.IndexOf(TriggerCatalog.NodeCoords, v.coord);
                        int nv = EditorGUILayout.Popup(c < 0 ? 0 : c, TriggerCatalog.NodeCoords, GUILayout.Width(106));
                        if (nv != c) { v.coord = TriggerCatalog.NodeCoords[nv]; Touch(); }
                        break;
                    }

                default:
                    {
                        string nv = DrawLiteral(lit, v.text, pd, ref v.uiCustom, picked => v.text = picked);
                        if (nv != v.text) { v.text = nv; Touch(); }
                        break;
                    }
            }
        }

        // shared by slots and the Variables panel.
        // menu picks come in on a later event, so those go through setLater, not the return value
        string DrawLiteral(VType t, string val, ParamDef pd, ref bool custom, Action<string> setLater)
        {
            val = val ?? "";

            switch (t)
            {
                case VType.Bool:
                    {
                        string on = pd != null ? pd.onValue : "1";
                        string off = pd != null ? pd.offValue : "0";
                        bool cur = string.Equals(val, on, StringComparison.OrdinalIgnoreCase) ||
                                   (on == "1" && string.Equals(val, "true", StringComparison.OrdinalIgnoreCase));

                        bool nb = EditorGUILayout.Toggle(cur, GUILayout.Width(18));
                        GUILayout.Label(on == "1" ? (nb ? "True" : "False") : (nb ? on : off), toggleText);
                        GUILayout.FlexibleSpace();
                        return nb != cur ? (nb ? on : off) : val;
                    }

                case VType.Int:
                case VType.UInt:
                    {
                        int n;
                        VTypes.TryInt(val, out n);
                        if (t == VType.UInt) n = Mathf.Max(0, n);

                        int nn = EditorGUILayout.IntField(n);
                        if (t == VType.UInt) nn = Mathf.Max(0, nn);
                        return nn != n ? nn.ToString(VTypes.Inv) : val;
                    }

                case VType.Number:
                    {
                        float f;
                        VTypes.TryFloat(val, out f);
                        float nf = EditorGUILayout.FloatField(f);
                        return nf != f ? VTypes.Num(nf) : val;
                    }

                case VType.AI:
                    {
                        int matched = Array.IndexOf(VTypes.AIValues, val);
                        bool showCustom = custom || matched < 0;        // cached, the popup can flip custom
                        int shown = showCustom ? VTypes.AILabels.Length - 1 : matched;

                        string res = val;
                        int ni = EditorGUILayout.Popup(shown, VTypes.AILabels,
                            showCustom ? GUILayout.Width(86) : GUILayout.ExpandWidth(true));
                        if (ni != shown)
                        {
                            if (ni == VTypes.AILabels.Length - 1) custom = true;
                            else { custom = false; res = VTypes.AIValues[ni]; }
                        }

                        if (showCustom)
                        {
                            int n;
                            if (!VTypes.TryInt(res, out n)) n = -1;
                            n = Mathf.Max(-1, n);
                            int nn = Mathf.Max(-1, EditorGUILayout.IntField(n));    // -1 = any
                            if (nn != n) res = nn.ToString(VTypes.Inv);
                        }
                        return res;
                    }

                case VType.Zoom:
                    {
                        int matched = -1;
                        float zf;
                        if (VTypes.TryFloat(val, out zf))
                            for (int i = 0; i < VTypes.ZoomValues.Length; i++)
                            {
                                float pz;
                                VTypes.TryFloat(VTypes.ZoomValues[i], out pz);
                                if (Mathf.Abs(pz - zf) < 0.0001f) { matched = i; break; }
                            }

                        bool showCustom = custom || matched < 0;
                        int shown = showCustom ? VTypes.ZoomLabels.Length - 1 : matched;

                        string res = val;
                        int ni = EditorGUILayout.Popup(shown, VTypes.ZoomLabels,
                            showCustom ? GUILayout.Width(86) : GUILayout.ExpandWidth(true));
                        if (ni != shown)
                        {
                            if (ni == VTypes.ZoomLabels.Length - 1) custom = true;
                            else { custom = false; res = VTypes.ZoomValues[ni]; }
                        }

                        if (showCustom)
                        {
                            float f;
                            if (!VTypes.TryFloat(res, out f) || f <= 0f) f = 1f;
                            float nf = Mathf.Max(0.01f, EditorGUILayout.FloatField(f));
                            if (nf != f) res = VTypes.Num(nf);
                        }
                        return res;
                    }

                case VType.Key:
                    return StrictPopup(val, VTypes.Keys);

                case VType.Orientation:
                    return StrictPopup(val, pd != null && pd.choices != null ? pd.choices : VTypes.Orientations);

                case VType.Model:
                    return StrictPopup(val, ValueSources.ModelChoices);

                case VType.Spawn:
                    {
                        var spawns = ValueSources.Spawns;
                        if (spawns.Count > 0) return StrictPopup(val, spawns);

                        // no Spawntagged objects in the scene, just let them type it
                        return EditorGUILayout.TextField(val);
                    }

                case VType.Color:
                    {
                        Color c;
                        string hex = VTypes.NormalizeHex(val);
                        if (hex == null || !ColorUtility.TryParseHtmlString(hex, out c)) c = Color.white;

                        Color nc = EditorGUILayout.ColorField(GUIContent.none, c, true, true, false);
                        return nc != c ? "#" + ColorUtility.ToHtmlStringRGBA(nc) : val;
                    }

                // free text so unlisted names still work, with a picker filled from the active project
                case VType.Animation:
                case VType.Sound:
                    {
                        string s = EditorGUILayout.TextField(val);
                        if (setLater != null)
                        {
                            bool anim = t == VType.Animation;
                            ProjectPickMenu(
                                anim ? ValueSources.Animations : ValueSources.Sounds,
                                s, setLater,
                                anim ? $"No moves found in {ActiveProject.Name}'s moves.xml"
                                     : $"No sounds found in {ActiveProject.Name}'s sounds folder");
                        }
                        return s;
                    }

                default:   // String
                    {
                        string s = EditorGUILayout.TextField(val);
                        if (pd != null && pd.choices != null && pd.choices.Length > 0 && setLater != null)
                            PickMenu(pd.choices, s, setLater);
                        return s;
                    }
            }
        }

        // popup limited to options. an unknown value from a file shows as not found
        // and is left alone until something else is picked
        static string StrictPopup(string val, IList<string> options)
        {
            int idx = VTypes.IndexOf(options, val, false);
            bool extra = idx < 0 && !string.IsNullOrEmpty(val);

            var shown = new string[options.Count + (extra ? 1 : 0)];
            for (int i = 0; i < options.Count; i++) shown[i] = options[i];
            if (extra)
            {
                shown[shown.Length - 1] = val + "  (not found)";
                idx = shown.Length - 1;
            }

            int ni = EditorGUILayout.Popup(idx, shown);
            return ni != idx && ni >= 0 && ni < options.Count ? options[ni] : val;
        }

        // model/node part of a ?getModel query, leading _ means variable
        void DrawRef(TValue v, bool nodeSlot, float width)
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

            if (!PickButton(out var dr)) return;

            IList<string> literals = nodeSlot ? (IList<string>)TriggerCatalog.CommonNodes
                : ValueSources.Models.Count > 0 ? ValueSources.Models : (IList<string>)TriggerCatalog.CommonModels;

            var menu = new GenericMenu();
            foreach (var lit in literals)
            {
                string name = lit;
                menu.AddItem(new GUIContent("Name/" + name), false, () =>
                {
                    if (nodeSlot) { v.nodeIsVar = false; v.node = name; }
                    else { v.modelIsVar = false; v.model = name; }
                    Touch(); ClearFocus();
                });
            }
            foreach (var vn in data.VarNames())
            {
                string name = vn;
                menu.AddItem(new GUIContent("Variable/" + name), false, () =>
                {
                    if (nodeSlot) { v.nodeIsVar = true; v.node = name; }
                    else { v.modelIsVar = true; v.model = name; }
                    Touch(); ClearFocus();
                });
            }
            menu.DropDown(dr);
        }

        // matching types first, the rest under Other Types
        void VarPicker(ParamDef pd, VType lit, Action<string> apply)
        {
            if (!PickButton(out var dr)) return;

            bool all = pd == null || pd.query || pd.varName;
            var fam = VTypes.FamilyOf(lit);
            var names = data.VarNames();

            var menu = new GenericMenu();
            for (int pass = 0; pass < 2; pass++)
                foreach (var n in names)
                {
                    var t = data.TypeOf(n);
                    bool fits = all || VTypes.FamilyOf(t) == fam;
                    if (fits != (pass == 0)) continue;

                    string name = n;
                    string label = (pass == 0 ? "" : "Other Types/") + name + "   (" + t + ")";
                    menu.AddItem(new GUIContent(label), false, () => { apply(name); Touch(); ClearFocus(); });
                }
            menu.DropDown(dr);
        }

        // apply runs later, from the menu callback
        void PickMenu(IList<string> items, string current, Action<string> apply)
        {
            if (!PickButton(out var dr)) return;

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

        // like PickMenu, but long lists are split into A, B, C... submenus,
        // and an empty list shows why instead of an empty menu
        void ProjectPickMenu(IList<string> items, string current, Action<string> apply, string emptyText)
        {
            if (!PickButton(out var dr)) return;

            var menu = new GenericMenu();

            if (items.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent(emptyText));
                menu.DropDown(dr);
                return;
            }

            bool group = items.Count > MenuGroupThreshold;

            for (int i = 0; i < items.Count; i++)
            {
                string it = items[i];

                // "/" in a name would create a submenu, swap it for a lookalike
                string label = it.Replace('/', '∕');
                if (group)
                {
                    char first = char.ToUpperInvariant(it[0]);
                    label = (char.IsLetter(first) ? first.ToString() : "#") + "/" + label;
                }

                menu.AddItem(new GUIContent(label), current != null && current == it,
                    () =>
                    {
                        apply(it);
                        Touch();
                        ClearFocus();
                    });
            }
            menu.DropDown(dr);
        }

        static bool PickButton(out Rect dr)
        {
            dr = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18));
            return ArrowButton(dr);
        }

        // miniPullDown draws the arrow offcentre at this width, so draw it ourselves
        static Texture arrowTex;

        static bool ArrowButton(Rect r)
        {
            bool clicked = GUI.Button(r, GUIContent.none, EditorStyles.miniButton);

            if (Event.current.type == EventType.Repaint)
            {
                if (!arrowTex) arrowTex = EditorGUIUtility.IconContent("icon dropdown").image;
                if (arrowTex)
                {
                    float w = Mathf.Min(arrowTex.width, r.width - 4f), h = Mathf.Min(arrowTex.height, r.height - 4f);
                    var ir = new Rect(Mathf.Round(r.center.x - w * 0.5f), Mathf.Round(r.center.y - h * 0.5f), w, h);
                    GUI.DrawTexture(ir, arrowTex, ScaleMode.ScaleToFit);
                }
            }
            return clicked;
        }

        // ================= XML strip =================

        void DrawXmlStrip(Rect r)
        {
            GUI.Box(r, GUIContent.none, EditorStyles.helpBox);
            var pad = new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 8f);

            // only rebuild on Layout so Layout and Repaint agree on the size
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

    // ================= Scene data for the dropdowns =================

    static class ValueSources
    {
        // relative to ./Projects/{ActiveProject.Name}/
        const string MovesXmlRelativePath = "animations/moves.xml";
        const string SoundsRelativeFolder = "sounds";

        static readonly string[] NoModels = { VTypes.NoModel };
        static List<string> spawns, models, animations, sounds;

        public static void InvalidateSpawns() { spawns = null; }
        public static void InvalidateModels() { models = null; }
        public static void InvalidateProject() { animations = null; sounds = null; }
        public static void InvalidateAll() { spawns = null; models = null; animations = null; sounds = null; }

        // FindObjectsOfTypeAll also returns prefabs and hidden objects, hence the filtering
        public static List<string> Spawns
        {
            get
            {
                if (spawns != null) return spawns;

                var set = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (!go || EditorUtility.IsPersistent(go) || !go.scene.IsValid()) continue;
                    if ((go.hideFlags & (HideFlags.HideInHierarchy | HideFlags.DontSave)) != 0) continue;
                    if (go.tag == "Spawn" && !string.IsNullOrEmpty(go.name)) set.Add(go.name);
                }
                spawns = new List<string>(set);
                return spawns;
            }
        }

        // from every ExportConfig in the open scene(s), duplicates skipped
        public static List<string> Models
        {
            get
            {
                if (models != null) return models;

                models = new List<string>();
                var seen = new HashSet<string>();
                foreach (var cfg in Resources.FindObjectsOfTypeAll<ExportConfig>())
                {
                    if (!cfg || EditorUtility.IsPersistent(cfg) || !cfg.gameObject.scene.IsValid()) continue;
                    AddModels(cfg.commonModeModelDefinitions, seen);
                    AddModels(cfg.hunterModeModelDefinitions, seen);
                }
                return models;
            }
        }

        static void AddModels(List<ExportConfig.ModelDefinition> defs, HashSet<string> seen)
        {
            if (defs == null) return;
            foreach (var d in defs)
                if (d != null && !string.IsNullOrEmpty(d.name) && seen.Add(d.name)) models.Add(d.name);
        }

        // popup cant be empty, show None when there are no models
        public static IList<string> ModelChoices
        {
            get { return Models.Count > 0 ? (IList<string>)Models : NoModels; }
        }

        // tag name of every element under <root><Moves> in the active project's moves.xml
        // (falls back to the Vector project if the active one has no moves.xml)
        public static List<string> Animations
        {
            get
            {
                if (animations != null) return animations;

                animations = new List<string>();
                string path = ActiveProject.ResolveFile(MovesXmlRelativePath);
                if (path == null) return animations;

                try
                {
                    var doc = new XmlDocument();
                    doc.Load(path);

                    var moves = doc.DocumentElement?.SelectSingleNode("Moves") as XmlElement;
                    if (moves != null)
                    {
                        var seen = new HashSet<string>(StringComparer.Ordinal);
                        foreach (XmlNode n in moves.ChildNodes)
                        {
                            var e = n as XmlElement;
                            if (e != null && seen.Add(e.LocalName)) animations.Add(e.LocalName);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Trigger Editor] Couldn't read moves from '{path}': {e.Message}");
                }

                animations.Sort(StringComparer.OrdinalIgnoreCase);
                return animations;
            }
        }

        // file names (no extension) in the active project's sounds folder, subfolders included
        // (falls back to the Vector project if the active one has no sounds folder)
        public static List<string> Sounds
        {
            get
            {
                if (sounds != null) return sounds;

                sounds = new List<string>();
                string dir = ActiveProject.ResolveDirectory(SoundsRelativeFolder);
                if (dir == null) return sounds;

                try
                {
                    var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        string fileName = Path.GetFileName(file);

                        // skip .gitkeep, .DS_Store and Unity .meta files
                        if (fileName.StartsWith(".") ||
                            fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;

                        string name = Path.GetFileNameWithoutExtension(file);
                        if (!string.IsNullOrEmpty(name)) set.Add(name);
                    }
                    sounds.AddRange(set);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Trigger Editor] Couldn't read sounds from '{dir}': {e.Message}");
                }

                return sounds;
            }
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