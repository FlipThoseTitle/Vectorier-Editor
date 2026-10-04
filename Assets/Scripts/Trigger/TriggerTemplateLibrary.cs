using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

namespace Vectorier.Trigger
{
    // read only view of the game's TriggerTemplates.xml, we never write to it
    public static class TriggerTemplateLibrary
    {
        public const string Path = "Assets/Editor/TriggerEditor/TriggerTemplates.xml";

        public class UsingVar
        {
            public string name = "";
            public string def = "";
            public string type = "";
        }

        public class LoopInfo
        {
            public string template = "";
            public string name = "";
            public string Full => template + "." + name;

            public bool hasEvents, hasConditions, hasActions;
            public List<UsingVar> uses = new List<UsingVar>();
            public string summary = "";
        }

        public class TemplateInfo
        {
            public string name = "";
            public bool library;
            public List<LoopInfo> loops = new List<LoopInfo>();
            public List<KeyValuePair<string, string>> init = new List<KeyValuePair<string, string>>();
        }

        static List<TemplateInfo> templates;
        static bool loadFailed;

        public static bool Available => templates != null && templates.Count > 0;
        public static bool LoadFailed => loadFailed;

        public static List<TemplateInfo> Templates
        {
            get { if (templates == null) Reload(); return templates; }
        }

        public static void Reload()
        {
            templates = new List<TemplateInfo>();
            loadFailed = false;

            if (!File.Exists(Path)) { loadFailed = true; return; }

            var doc = new XmlDocument();
            try { doc.Load(Path); }
            catch (System.Exception e) { loadFailed = true; Debug.LogWarning("[Trigger Editor] Couldn't read TriggerTemplates.xml: " + e.Message); return; }

            var root = doc.DocumentElement;
            if (root == null) { loadFailed = true; return; }

            foreach (XmlNode tn in root.ChildNodes)
            {
                var te = tn as XmlElement;
                if (te == null || te.Name != "Template") continue;

                var t = new TemplateInfo
                {
                    name = te.GetAttribute("Name"),
                    library = te.GetAttribute("Library") == "1"
                };

                var initEl = te.SelectSingleNode("Init") as XmlElement;
                if (initEl != null)
                    foreach (XmlNode vn in initEl.ChildNodes)
                    {
                        var ve = vn as XmlElement;
                        if (ve == null || ve.Name != "SetVariable") continue;
                        t.init.Add(new KeyValuePair<string, string>(ve.GetAttribute("Name"), ve.GetAttribute("Value")));
                    }

                foreach (XmlNode ln in te.ChildNodes)
                {
                    var le = ln as XmlElement;
                    if (le == null || le.Name != "Loop") continue;

                    var l = new LoopInfo { template = t.name, name = le.GetAttribute("Name") };

                    var ev = le.SelectSingleNode("Events") as XmlElement;
                    var cd = le.SelectSingleNode("Conditions") as XmlElement;
                    var ac = le.SelectSingleNode("Actions") as XmlElement;

                    l.hasEvents = ev != null;
                    l.hasConditions = cd != null;
                    l.hasActions = ac != null;

                    var us = le.SelectSingleNode("Using") as XmlElement;
                    if (us != null)
                        foreach (XmlNode vn in us.ChildNodes)
                        {
                            var ve = vn as XmlElement;
                            if (ve == null || ve.Name != "Variable") continue;
                            l.uses.Add(new UsingVar
                            {
                                name = ve.GetAttribute("Name"),
                                def = ve.GetAttribute("DefaultValue"),
                                type = ve.GetAttribute("Type")
                            });
                        }

                    l.summary = Summarize(ev) + Summarize(cd) + Summarize(ac);
                    t.loops.Add(l);
                }

                templates.Add(t);
            }
        }

        static string Summarize(XmlElement block)
        {
            if (block == null) return "";

            var names = new List<string>();
            foreach (XmlNode n in block.ChildNodes)
            {
                var e = n as XmlElement;
                if (e == null) continue;
                names.Add(e.Name == "EventBlock" || e.Name == "ConditionBlock" || e.Name == "ActionBlock"
                    ? e.GetAttribute("Template")
                    : e.Name);
            }

            string t = block.GetAttribute("Template");
            if (!string.IsNullOrEmpty(t)) names.Add(t);
            if (names.Count == 0) return "";

            return block.Name + ": " + string.Join(", ", names.ToArray()) + "   ";
        }

        public static LoopInfo Find(string full)
        {
            if (string.IsNullOrEmpty(full)) return null;
            int dot = full.IndexOf('.');
            if (dot < 0) return null;

            string tn = full.Substring(0, dot), ln = full.Substring(dot + 1);
            var list = Templates;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].name != tn) continue;
                for (int j = 0; j < list[i].loops.Count; j++)
                    if (list[i].loops[j].name == ln) return list[i].loops[j];
            }
            return null;
        }

        public static TemplateInfo FindTemplate(string name)
        {
            var list = Templates;
            for (int i = 0; i < list.Count; i++) if (list[i].name == name) return list[i];
            return null;
        }
    }
}