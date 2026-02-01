using System;
using System.Collections.Generic;
using System.Xml;
using UnityEditor;
using UnityEngine;
using Vectorier.Component;

namespace Vectorier.Trigger
{
    public class TriggerEditor : EditorWindow
    {
        [Serializable] class InitVar { public string n = "Variable", v = "Value"; }
        public enum Ev { Enter, Exit, Activate, KeyPressed, Timeout }
        public enum Op { None, And, Or }
        public enum Ck { Equal, Greater, Less }

        const string PresetFolder = "Assets/Editor/TriggerEditorPreset";
        List<TriggerPresetAsset> foundPresetAssets = new List<TriggerPresetAsset>();
        List<string> foundPresetNames = new List<string>();
        int presetIndex = 0;
        int lastPresetIndex = -1;
        string presetName = "Default";
        string templateName = "";

        [Serializable] class Cond { public Ck k = Ck.Equal; public string a = "_Attribute", b = "Value"; public bool nott; } // Equal: a,b  | Greater/Less: a=Value, b=Than
        [Serializable] class Attr { public string k = "Attribute", v = "Value"; }
        [Serializable] class Act { public string n = "Action"; public List<Attr> at = new List<Attr>(); }
        [Serializable]
        class Loop
        {
            public string name = "";
            public string loopTemplate = "";
            public List<Ev> ev = new List<Ev>();
            public string evTemplate = "";
            public Op op = Op.None;
            public string condTemplate = "";
            public List<Cond> c = new List<Cond>();
            public List<Act> a = new List<Act>();
            public string actTemplate = "";
            public bool fold = true;
        }

        TriggerComponent tc;
        List<InitVar> init = new List<InitVar>() { new InitVar { n = "$Active", v = "1" } };
        List<Loop> loops = new List<Loop>();
        Vector2 scroll;

        public static void Open(TriggerComponent t)
        {
            var w = GetWindow<TriggerEditor>("Trigger Editor");
            w.tc = t; w.LoadFromComponent();
            w.Show();
        }

        void OnEnable()
        {
            if (!tc)
            {
                var go = Selection.activeGameObject;
                if (go) tc = go.GetComponent<TriggerComponent>();
            }

            RefreshPresetList();
            LoadFromComponent();
        }

        void LoadFromComponent()
        {
            if (!tc) return;
            try { FromXml(tc.contentXml, out init, out templateName, out loops); }
            catch {}
            if (init == null || init.Count == 0) init = new List<InitVar>()
            {
                new InitVar { n = "$Active", v = "1" },
                new InitVar { n = "$AI", v = "-1" },
                new InitVar { n = "$Node", v = "COM" },
                new InitVar { n = "Flag1", v = "0" }
            };
            if (templateName == null) templateName = "";
            if (loops == null || loops.Count == 0) loops = new List<Loop>();
            Repaint();
        }

        void SaveToComponent()
        {
            if (!tc) return;
            Undo.RecordObject(tc, "Trigger Editor");
            tc.contentXml = ToXml(init, templateName, loops);
            EditorUtility.SetDirty(tc);
        }

        void OnGUI()
        {
            if (!tc)
            {
                EditorGUILayout.HelpBox("Select a GameObject with TriggerComponent.", MessageType.Info);
                if (GUILayout.Button("Try Use Selection")) { var go = Selection.activeGameObject; if (go) tc = go.GetComponent<TriggerComponent>(); LoadFromComponent(); }
                return;
            }

            if (foundPresetNames == null || foundPresetNames.Count == 0)
                RefreshPresetList();

            float oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUIUtility.labelWidth = 70f;

            {
                Rect r = EditorGUILayout.GetControlRect();
                presetIndex = EditorGUI.Popup(r, "Template", presetIndex, foundPresetNames.ToArray());
            }

            // auto load on selection change
            if (presetIndex != lastPresetIndex)
            {
                lastPresetIndex = presetIndex;

                if (presetIndex == 0)
                {
                    presetName = "Default";
                    ApplyDefaultPreset();
                }
                else
                {
                    var asset = foundPresetAssets[presetIndex];
                    presetName = foundPresetNames[presetIndex];
                    if (asset != null) ApplyPresetAsset(asset);
                }

                GUI.FocusControl(null);
                Repaint();
            }

            {
                Rect r = EditorGUILayout.GetControlRect();
                presetName = EditorGUI.TextField(r, "Name", presetName);
            }
            EditorGUIUtility.labelWidth = oldLabelWidth;
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            {
                using (new EditorGUI.DisabledScope(presetIndex == 0)) // can't delete Default
                {
                    if (GUILayout.Button("Delete Preset", GUILayout.Width(120))) DeleteSelectedPreset();
                }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Save Preset", GUILayout.Width(120))) SaveOrUpdatePreset(presetName);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            // INIT
            EditorGUILayout.LabelField("Init", EditorStyles.boldLabel);
            for (int i = 0; i < init.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                init[i].n = EditorGUILayout.TextField(init[i].n);
                init[i].v = EditorGUILayout.TextField(init[i].v);
                if (GUILayout.Button("-", GUILayout.Width(22))) { init.RemoveAt(i); i--; }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ Variable")) init.Add(new InitVar());

            EditorGUILayout.Space(10);

            // TEMPLATE
            EditorGUILayout.LabelField("Template", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Name", GUILayout.Width(50));
            templateName = EditorGUILayout.TextField(templateName ?? "");
            if (GUILayout.Button("X", GUILayout.Width(22))) templateName = "";
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // LOOPS
            EditorGUILayout.LabelField("Loops", EditorStyles.boldLabel);

            for (int li = 0; li < loops.Count; li++)
            {
                var L = loops[li];

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();

                L.fold = EditorGUILayout.Foldout(
                    L.fold,
                    string.IsNullOrEmpty(L.name) ? $"Loop {li}" : $"Loop: {L.name}",
                    true
                );

                if (GUILayout.Button("-", GUILayout.Width(22)))
                {
                    loops.RemoveAt(li);
                    li--;
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }

                EditorGUILayout.EndHorizontal();

                if (L.fold)
                {
                    L.name = EditorGUILayout.TextField("Name (optional)", L.name);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Loop Template", GUILayout.Width(90));
                    L.loopTemplate = EditorGUILayout.TextField(L.loopTemplate ?? "");
                    if (GUILayout.Button("X", GUILayout.Width(22))) L.loopTemplate = "";
                    EditorGUILayout.EndHorizontal();

                    var loopLocked = !string.IsNullOrEmpty(L.loopTemplate);
                    if (loopLocked)
                    {
                        if (!string.IsNullOrEmpty(L.condTemplate) ||
                            !string.IsNullOrEmpty(L.evTemplate) ||
                            !string.IsNullOrEmpty(L.actTemplate) ||
                            L.op != Op.None ||
                            L.ev.Count > 0 ||
                            L.c.Count > 0 ||
                            L.a.Count > 0)
                        {
                            L.evTemplate = "";
                            L.actTemplate = "";
                            L.condTemplate = "";
                            L.op = Op.None;
                            L.ev.Clear();
                            L.c.Clear();
                            L.a.Clear();
                        }
                    }

                    using (new EditorGUI.DisabledScope(loopLocked))
                    {
                        // EVENTS
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Template", GUILayout.Width(60));
                        L.evTemplate = EditorGUILayout.TextField(L.evTemplate ?? "");
                        if (GUILayout.Button("X", GUILayout.Width(22))) L.evTemplate = "";
                        EditorGUILayout.EndHorizontal();

                        var evLocked = !string.IsNullOrEmpty(L.evTemplate);
                        using (new EditorGUI.DisabledScope(evLocked))
                        {
                            for (int ei = 0; ei < L.ev.Count; ei++)
                            {
                                EditorGUILayout.BeginHorizontal();
                                L.ev[ei] = (Ev)EditorGUILayout.EnumPopup(L.ev[ei]);
                                if (GUILayout.Button("-", GUILayout.Width(22))) { L.ev.RemoveAt(ei); ei--; }
                                EditorGUILayout.EndHorizontal();
                            }
                            if (GUILayout.Button("+ Event")) L.ev.Add(Ev.Enter);
                        }
                        if (evLocked && L.ev.Count > 0) L.ev.Clear();

                        // CONDITIONS
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Template", GUILayout.Width(60));
                        L.condTemplate = EditorGUILayout.TextField(L.condTemplate ?? "");
                        if (GUILayout.Button("X", GUILayout.Width(22))) L.condTemplate = "";
                        EditorGUILayout.EndHorizontal();

                        var condLocked = !string.IsNullOrEmpty(L.condTemplate);
                        using (new EditorGUI.DisabledScope(condLocked))
                        {
                            L.op = (Op)EditorGUILayout.EnumPopup("Operator", L.op);
                            for (int ci = 0; ci < L.c.Count; ci++)
                            {
                                var C = L.c[ci];
                                EditorGUILayout.BeginHorizontal();
                                C.k = (Ck)EditorGUILayout.EnumPopup(C.k, GUILayout.Width(85));
                                C.nott = GUILayout.Toggle(C.nott, "Not", GUILayout.Width(45));
                                C.a = EditorGUILayout.TextField(C.a);
                                C.b = EditorGUILayout.TextField(C.b);
                                if (GUILayout.Button("-", GUILayout.Width(22))) { L.c.RemoveAt(ci); ci--; }
                                EditorGUILayout.EndHorizontal();
                            }
                            if (GUILayout.Button("+ Condition")) L.c.Add(new Cond());
                        }
                        if (condLocked && (L.op != Op.None || L.c.Count > 0)) { L.op = Op.None; L.c.Clear(); }

                        // ACTIONS
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Template", GUILayout.Width(60));
                        L.actTemplate = EditorGUILayout.TextField(L.actTemplate ?? "");
                        if (GUILayout.Button("X", GUILayout.Width(22))) L.actTemplate = "";
                        EditorGUILayout.EndHorizontal();

                        var actLocked = !string.IsNullOrEmpty(L.actTemplate);
                        using (new EditorGUI.DisabledScope(actLocked))
                        {
                            for (int ai = 0; ai < L.a.Count; ai++)
                            {
                                var A = L.a[ai];

                                EditorGUILayout.BeginVertical("helpbox");
                                EditorGUILayout.BeginHorizontal();
                                A.n = EditorGUILayout.TextField("Tag", A.n);
                                if (GUILayout.Button("-", GUILayout.Width(22)))
                                {
                                    L.a.RemoveAt(ai);
                                    ai--;
                                    EditorGUILayout.EndHorizontal();
                                    EditorGUILayout.EndVertical();
                                    continue;
                                }
                                EditorGUILayout.EndHorizontal();

                                for (int ati = 0; ati < A.at.Count; ati++)
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    A.at[ati].k = EditorGUILayout.TextField(A.at[ati].k);
                                    A.at[ati].v = EditorGUILayout.TextField(A.at[ati].v);
                                    if (GUILayout.Button("-", GUILayout.Width(22))) { A.at.RemoveAt(ati); ati--; }
                                    EditorGUILayout.EndHorizontal();
                                }

                                EditorGUILayout.BeginHorizontal();
                                if (GUILayout.Button("+ Attr")) A.at.Add(new Attr());
                                if (GUILayout.Button("Clear Attrs")) A.at.Clear();
                                EditorGUILayout.EndHorizontal();

                                EditorGUILayout.EndVertical();
                            }

                            if (GUILayout.Button("+ Action")) L.a.Add(new Act { n = "Action" });
                        }
                        if (actLocked && L.a.Count > 0) L.a.Clear();
                    }
                }
                EditorGUILayout.EndVertical();
            }

            if (GUILayout.Button("+ Loop")) loops.Add(new Loop());

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load XML")) LoadFromComponent();
            if (GUILayout.Button("Save XML")) SaveToComponent();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("XML Preview", EditorStyles.miniBoldLabel);
            EditorGUILayout.TextArea(ToXml(init, templateName, loops), GUILayout.MinHeight(120));
        }

        // ===== PRESET =====

        void RefreshPresetList()
        {
            foundPresetAssets.Clear();
            foundPresetNames.Clear();
            foundPresetNames.Add("Default");
            foundPresetAssets.Add(null);

            if (!AssetDatabase.IsValidFolder(PresetFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Editor"))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/Editor"))
                        AssetDatabase.CreateFolder("Assets", "Editor");
                }
                if (!AssetDatabase.IsValidFolder(PresetFolder))
                    AssetDatabase.CreateFolder("Assets/Editor", "TriggerEditorPreset");
            }

            var guids = AssetDatabase.FindAssets("t:TriggerPresetAsset", new[] { PresetFolder });
            var temp = new List<(string name, TriggerPresetAsset asset)>();

            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var a = AssetDatabase.LoadAssetAtPath<TriggerPresetAsset>(path);
                if (a != null) temp.Add((a.name, a));
            }

            temp.Sort((x, y) => string.CompareOrdinal(x.name, y.name));

            foreach (var t in temp)
            {
                foundPresetNames.Add(t.name);
                foundPresetAssets.Add(t.asset);
            }

            presetIndex = Mathf.Clamp(presetIndex, 0, foundPresetNames.Count - 1);
            presetName = foundPresetNames[presetIndex];
        }

        void ApplyDefaultPreset()
        {
            init = new List<InitVar>()
            {
                new InitVar { n = "$Active", v = "1" },
                new InitVar { n = "$AI", v = "-1" },
                new InitVar { n = "$Node", v = "COM" },
                new InitVar { n = "Flag1", v = "0" }
            };
            templateName = "";
            loops = new List<Loop>();
        }

        void ApplyPresetAsset(TriggerPresetAsset asset)
        {
            // Reset
            init = new List<InitVar>();
            loops = new List<Loop>();

            templateName = asset != null ? (asset.templateName ?? "") : "";

            if (asset == null)
            {
                Repaint();
                return;
            }

            // Copy init
            foreach (var iv in asset.init)
                init.Add(new InitVar { n = iv.n ?? "", v = iv.v ?? "" });

            // Copy loops
            foreach (var lp in asset.loops)
            {
                var L = new Loop();
                L.name = lp.name ?? "";
                L.loopTemplate = lp.loopTemplate ?? "";

                L.evTemplate = lp.evTemplate ?? "";
                L.ev = new List<Ev>();
                if (lp.ev != null) L.ev.AddRange(lp.ev);

                L.condTemplate = lp.condTemplate ?? "";
                L.op = lp.op;
                L.c = new List<Cond>();
                if (lp.c != null)
                    foreach (var c in lp.c)
                        L.c.Add(new Cond { k = c.k, a = c.a ?? "", b = c.b ?? "", nott = c.nott });

                L.actTemplate = lp.actTemplate ?? "";
                L.a = new List<Act>();
                if (lp.a != null)
                {
                    foreach (var a in lp.a)
                    {
                        var A = new Act { n = a.n ?? "", at = new List<Attr>() };
                        if (a.at != null)
                            foreach (var at in a.at)
                                A.at.Add(new Attr { k = at.k ?? "", v = at.v ?? "" });
                        L.a.Add(A);
                    }
                }

                if (!string.IsNullOrEmpty(L.loopTemplate))
                {
                    L.evTemplate = ""; L.condTemplate = ""; L.actTemplate = "";
                    L.op = Op.None;
                    L.ev.Clear(); L.c.Clear(); L.a.Clear();
                }
                if (!string.IsNullOrEmpty(L.evTemplate)) L.ev.Clear();
                if (!string.IsNullOrEmpty(L.condTemplate)) { L.op = Op.None; L.c.Clear(); }
                if (!string.IsNullOrEmpty(L.actTemplate)) L.a.Clear();

                loops.Add(L);
            }

            Repaint();
        }

        TriggerPresetAsset BuildAssetFromCurrent()
        {
            var asset = ScriptableObject.CreateInstance<TriggerPresetAsset>();
            asset.init = new List<TriggerPresetAsset.InitVar>();
            asset.templateName = templateName ?? "";
            asset.loops = new List<TriggerPresetAsset.LoopPreset>();

            // init
            foreach (var iv in init) asset.init.Add(new TriggerPresetAsset.InitVar { n = iv.n, v = iv.v });

            // loops
            foreach (var L in loops)
            {
                var lp = new TriggerPresetAsset.LoopPreset();
                lp.name = L.name;
                lp.loopTemplate = L.loopTemplate;

                lp.evTemplate = L.evTemplate;
                lp.ev = new List<Ev>();
                if (L.ev != null) lp.ev.AddRange(L.ev);

                lp.op = L.op;
                lp.condTemplate = L.condTemplate;
                lp.c = new List<TriggerPresetAsset.Cond>();
                if (L.c != null)
                {
                    foreach (var c in L.c)
                    {
                        lp.c.Add(new TriggerPresetAsset.Cond
                        {
                            k = c.k,
                            a = c.a,
                            b = c.b,
                            nott = c.nott
                        });
                    }
                }

                lp.actTemplate = L.actTemplate;
                lp.a = new List<TriggerPresetAsset.Act>();
                if (L.a != null)
                {
                    foreach (var a in L.a)
                    {
                        var act = new TriggerPresetAsset.Act();
                        act.n = a.n;
                        act.at = new List<TriggerPresetAsset.Attr>();
                        if (a.at != null)
                        {
                            foreach (var at in a.at)
                                act.at.Add(new TriggerPresetAsset.Attr { k = at.k, v = at.v });
                        }
                        lp.a.Add(act);
                    }
                }

                asset.loops.Add(lp);
            }

            return asset;
        }

        void OverwriteAssetFromCurrent(TriggerPresetAsset dst)
        {
            var src = BuildAssetFromCurrent();

            // overwrite lists
            dst.init = src.init;
            dst.templateName = src.templateName;
            dst.loops = src.loops;

            DestroyImmediate(src);
        }

        void SaveOrUpdatePreset(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            name = name.Trim();
            var path = $"{PresetFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TriggerPresetAsset>(path);

            if (existing == null)
            {
                var a = BuildAssetFromCurrent();
                a.name = name;
                AssetDatabase.CreateAsset(a, path);
                EditorUtility.SetDirty(a);
            }
            else
            {
                OverwriteAssetFromCurrent(existing);
                EditorUtility.SetDirty(existing);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RefreshPresetList();
            presetIndex = Mathf.Max(0, foundPresetNames.IndexOf(name));
            lastPresetIndex = presetIndex;
        }

        void DeleteSelectedPreset()
        {
            if (presetIndex <= 0) return; // can't delete Default

            var asset = foundPresetAssets[presetIndex];
            if (asset == null) return;

            var path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // fallback to default after delete
            presetIndex = 0;
            lastPresetIndex = -1;
            RefreshPresetList();
            ApplyDefaultPreset();
            Repaint();
        }

        // ===== XML CONVERT =====

        static string ToXml(List<InitVar> init, string templateName, List<Loop> loops)
        {
            var doc = new XmlDocument();
            var root = doc.CreateElement("Content"); doc.AppendChild(root);

            var initEl = doc.CreateElement("Init"); root.AppendChild(initEl);
            for (int i = 0; i < (init?.Count ?? 0); i++)
            {
                var s = doc.CreateElement("SetVariable");
                s.SetAttribute("Name", init[i].n ?? "");
                s.SetAttribute("Value", init[i].v ?? "");
                initEl.AppendChild(s);
            }

            if (!string.IsNullOrEmpty(templateName))
            {
                var tEl = doc.CreateElement("Template");
                tEl.SetAttribute("Name", templateName);
                root.AppendChild(tEl);
            }

            for (int li = 0; li < (loops?.Count ?? 0); li++)
            {
                var L = loops[li]; if (L == null) continue;
                var loopEl = doc.CreateElement("Loop");

                if (!string.IsNullOrEmpty(L.name)) loopEl.SetAttribute("Name", L.name);

                // Loop Template
                if (!string.IsNullOrEmpty(L.loopTemplate))
                {
                    loopEl.SetAttribute("Template", L.loopTemplate);
                    root.AppendChild(loopEl);
                    continue;
                }

                root.AppendChild(loopEl);

                // Events
                var hasEvT = !string.IsNullOrEmpty(L.evTemplate);
                var hasEvC = (L.ev?.Count ?? 0) > 0;
                if (hasEvT || hasEvC)
                {
                    var evEl = doc.CreateElement("Events"); loopEl.AppendChild(evEl);
                    if (hasEvT) evEl.SetAttribute("Template", L.evTemplate);
                    else for (int ei = 0; ei < L.ev.Count; ei++) evEl.AppendChild(doc.CreateElement(L.ev[ei].ToString()));
                }

                // Conditions
                var hasCoT = !string.IsNullOrEmpty(L.condTemplate);
                var hasCoC = (L.c?.Count ?? 0) > 0;
                var hasCoO = L.op != Op.None;

                if (hasCoT || hasCoC || hasCoO)
                {
                    var condEl = doc.CreateElement("Conditions"); loopEl.AppendChild(condEl);

                    if (hasCoT) condEl.SetAttribute("Template", L.condTemplate);
                    else
                    {
                        XmlElement condParent = condEl;
                        if (hasCoO)
                        {
                            var opEl = doc.CreateElement("Operator");
                            opEl.SetAttribute("Type", L.op.ToString());
                            condEl.AppendChild(opEl);
                            condParent = opEl;
                        }

                        for (int ci = 0; ci < (L.c?.Count ?? 0); ci++)
                        {
                            var C = L.c[ci]; if (C == null) continue;
                            var ce = doc.CreateElement(C.k.ToString());
                            if (C.nott) ce.SetAttribute("Not", "1");

                            if (C.k == Ck.Equal) { ce.SetAttribute("Value1", C.a ?? ""); ce.SetAttribute("Value2", C.b ?? ""); }
                            else { ce.SetAttribute("Value", C.a ?? ""); ce.SetAttribute("Than", C.b ?? ""); }

                            condParent.AppendChild(ce);
                        }
                    }
                }

                // Actions
                var hasAcT = !string.IsNullOrEmpty(L.actTemplate);
                var hasAcC = (L.a?.Count ?? 0) > 0;
                if (hasAcT || hasAcC)
                {
                    var actEl = doc.CreateElement("Actions"); loopEl.AppendChild(actEl);
                    if (hasAcT) actEl.SetAttribute("Template", L.actTemplate);
                    else
                        for (int ai = 0; ai < L.a.Count; ai++)
                        {
                            var A = L.a[ai]; if (A == null) continue;
                            var ae = doc.CreateElement(string.IsNullOrEmpty(A.n) ? "Action" : A.n);

                            for (int ati = 0; ati < (A.at?.Count ?? 0); ati++)
                                if (!string.IsNullOrEmpty(A.at[ati].k))
                                    ae.SetAttribute(A.at[ati].k, A.at[ati].v ?? "");

                            actEl.AppendChild(ae);
                        }
                }
            }

            var sb = new System.Text.StringBuilder();
            var xws = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n",
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment
            };
            using (var xw = XmlWriter.Create(sb, xws)) root.WriteContentTo(xw);
            return sb.ToString().Trim();
        }

        static void FromXml(string contentInnerXml, out List<InitVar> init, out string templateName, out List<Loop> loops)
        {
            init = new List<InitVar>();
            loops = new List<Loop>();
            templateName = "";

            var doc = new XmlDocument();
            doc.LoadXml("<Content>" + (contentInnerXml ?? "") + "</Content>");
            var root = doc.DocumentElement;
            if (root == null) return;

            var initEl = root.SelectSingleNode("Init") as XmlElement;
            if (initEl != null)
                foreach (XmlNode n in initEl.ChildNodes)
                {
                    var e = n as XmlElement; if (e == null || e.Name != "SetVariable") continue;
                    init.Add(new InitVar { n = e.GetAttribute("Name"), v = e.GetAttribute("Value") });
                }

            var tEl = root.SelectSingleNode("Template") as XmlElement;
            if (tEl != null) templateName = tEl.GetAttribute("Name");

            foreach (XmlNode ln in root.SelectNodes("Loop"))
            {
                var le = ln as XmlElement; if (le == null) continue;
                var L = new Loop();

                L.name = le.GetAttribute("Name");
                L.loopTemplate = le.GetAttribute("Template");
                L.evTemplate = ""; L.condTemplate = ""; L.actTemplate = "";
                L.op = Op.None;
                L.ev = new List<Ev>();
                L.c = new List<Cond>();
                L.a = new List<Act>();

                // Loop Template
                if (!string.IsNullOrEmpty(L.loopTemplate))
                {
                    loops.Add(L);
                    continue;
                }

                // Events
                var evEl = le.SelectSingleNode("Events") as XmlElement;
                if (evEl != null)
                {
                    var t = evEl.GetAttribute("Template");
                    if (!string.IsNullOrEmpty(t)) L.evTemplate = t;
                    else
                        foreach (XmlNode en in evEl.ChildNodes)
                        {
                            var ee = en as XmlElement; if (ee == null) continue;
                            if (Enum.TryParse(ee.Name, out Ev ev)) L.ev.Add(ev);
                        }
                }

                // Conditions
                var condEl = le.SelectSingleNode("Conditions") as XmlElement;
                if (condEl != null)
                {
                    var t = condEl.GetAttribute("Template");
                    if (!string.IsNullOrEmpty(t)) L.condTemplate = t;
                    else
                    {
                        XmlElement condParent = condEl;
                        var opEl = condEl.SelectSingleNode("Operator") as XmlElement;
                        if (opEl != null)
                        {
                            Enum.TryParse(opEl.GetAttribute("Type"), out L.op);
                            condParent = opEl;
                        }

                        foreach (XmlNode cn in condParent.ChildNodes)
                        {
                            var ce = cn as XmlElement; if (ce == null) continue;
                            if (!Enum.TryParse(ce.Name, out Ck ck)) continue;

                            var C = new Cond();
                            C.k = ck;
                            C.nott = ce.GetAttribute("Not") == "1";
                            if (ck == Ck.Equal) { C.a = ce.GetAttribute("Value1"); C.b = ce.GetAttribute("Value2"); }
                            else { C.a = ce.GetAttribute("Value"); C.b = ce.GetAttribute("Than"); }
                            L.c.Add(C);
                        }
                    }
                }

                // Actions
                var actEl = le.SelectSingleNode("Actions") as XmlElement;
                if (actEl != null)
                {
                    var t = actEl.GetAttribute("Template");
                    if (!string.IsNullOrEmpty(t)) L.actTemplate = t;
                    else
                        foreach (XmlNode an in actEl.ChildNodes)
                        {
                            var ae = an as XmlElement; if (ae == null) continue;
                            var A = new Act { n = ae.Name, at = new List<Attr>() };
                            if (ae.HasAttributes)
                                foreach (XmlAttribute at in ae.Attributes)
                                    A.at.Add(new Attr { k = at.Name, v = at.Value });
                            L.a.Add(A);
                        }
                }

                // clear
                if (!string.IsNullOrEmpty(L.evTemplate)) L.ev.Clear();
                if (!string.IsNullOrEmpty(L.condTemplate)) { L.op = Op.None; L.c.Clear(); }
                if (!string.IsNullOrEmpty(L.actTemplate)) L.a.Clear();

                loops.Add(L);
            }

            if (templateName == null) templateName = "";
        }
    }

    [CustomEditor(typeof(TriggerComponent))]
    public class TriggerComponentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Trigger Editor", GUILayout.Height(35)))
                TriggerEditor.Open((TriggerComponent)target);
        }
    }
}
