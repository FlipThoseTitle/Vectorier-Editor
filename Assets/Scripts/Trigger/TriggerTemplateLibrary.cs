using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

namespace Vectorier.Trigger
{
    // Read only, we never write back to TriggerTemplates.xml.
    public static class TriggerTemplateLibrary
    {
        public const string Path = "Assets/Editor/TriggerEditor/TriggerTemplates.xml";

        public enum Section { When, OnlyIf, Do }

        const int MaxDepth = 4;             // stops circular template refs from looping forever
        const string Indent = "      ";

        // <Using> is only for the editor, the game ignores it
        public class UsingVar
        {
            public string name = "";
            public string def = "";
            public string type = "";
            public VType vtype = VType.String;
        }

        // Template="X.Y" found anywhere inside a loop
        public class BlockRef
        {
            public string full = "";
            public string prefix = "";
        }

        public class LoopInfo
        {
            public string template = "";
            public string name = "";
            public string Full => template + "." + name;

            public bool hasEvents, hasConditions, hasActions;
            public List<UsingVar> uses = new List<UsingVar>();
            public List<BlockRef> refs = new List<BlockRef>();

            internal XmlElement el;
            List<string> when, onlyIf, doLines;

            // built lazily, nested templates make these slow to describe
            public List<string> Lines(Section s)
            {
                switch (s)
                {
                    case Section.When: return when ?? (when = DescribeWhen(el, 0));
                    case Section.OnlyIf: return onlyIf ?? (onlyIf = DescribeOnlyIf(el, 0));
                    default: return doLines ?? (doLines = DescribeDo(el, 0));
                }
            }
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
                    foreach (var ve in Kids(initEl))
                        if (ve.Name == "SetVariable")
                            t.init.Add(new KeyValuePair<string, string>(ve.GetAttribute("Name"), ve.GetAttribute("Value")));

                foreach (var le in Kids(te))
                {
                    if (le.Name != "Loop") continue;

                    var l = new LoopInfo { template = t.name, name = le.GetAttribute("Name"), el = le };

                    l.hasEvents = Child(le, "Events") != null;
                    l.hasConditions = Child(le, "Conditions") != null;
                    l.hasActions = Child(le, "Actions") != null;

                    var us = Child(le, "Using");
                    if (us != null)
                        foreach (var ve in Kids(us))
                        {
                            if (ve.Name != "Variable") continue;

                            var u = new UsingVar
                            {
                                name = ve.GetAttribute("Name"),
                                def = ve.GetAttribute("DefaultValue"),
                                type = ve.GetAttribute("Type")
                            };
                            u.vtype = VTypes.FromName(u.type);

                            // no DefaultValue, use what the template's Init sets
                            if (!ve.HasAttribute("DefaultValue"))
                                foreach (var kv in t.init)
                                    if (kv.Key == u.name) { u.def = kv.Value; break; }

                            l.uses.Add(u);
                        }

                    var withTemplate = le.SelectNodes(".//*[@Template]");
                    if (withTemplate != null)
                        foreach (XmlNode n in withTemplate)
                        {
                            var e = n as XmlElement;
                            if (e == null) continue;
                            l.refs.Add(new BlockRef { full = e.GetAttribute("Template"), prefix = e.GetAttribute("Prefix") });
                        }

                    t.loops.Add(l);
                }

                templates.Add(t);
            }
        }

        // ---------------- Lookup ----------------

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

        // Follows template refs too. Prefixes stack, so Prefix="1" turns Sound into 1Sound.
        public static List<UsingVar> CollectUses(IEnumerable<LoopInfo> roots, string prefix)
        {
            var o = new List<UsingVar>();
            var names = new HashSet<string>();
            var seen = new HashSet<string>();
            if (roots != null)
                foreach (var l in roots) Collect(l, prefix ?? "", o, names, seen, 0);
            return o;
        }

        static void Collect(LoopInfo l, string prefix, List<UsingVar> o, HashSet<string> names, HashSet<string> seen, int depth)
        {
            if (l == null || depth > MaxDepth || !seen.Add(prefix + "|" + l.Full)) return;

            foreach (var u in l.uses)
            {
                string n = prefix + u.name;
                if (string.IsNullOrEmpty(u.name) || !names.Add(n)) continue;
                o.Add(new UsingVar { name = n, def = u.def, type = u.type, vtype = u.vtype });
            }

            foreach (var r in l.refs) Collect(Find(r.full), prefix + r.prefix, o, names, seen, depth + 1);
        }

        // ---------------- Describe ----------------

        static List<string> DescribeWhen(XmlElement le, int depth)
        {
            var o = new List<string>();
            var ev = Child(le, "Events");
            if (ev == null) return o;

            Expand(o, ev.GetAttribute("Template"), Section.When, depth, "");
            foreach (var e in Kids(ev))
            {
                if (e.Name == "EventBlock") Expand(o, e.GetAttribute("Template"), Section.When, depth, "");
                else o.Add(EntryLine(TriggerCatalog.FindEvent(e.Name), e));
            }
            return o;
        }

        static List<string> DescribeOnlyIf(XmlElement le, int depth)
        {
            var o = new List<string>();
            var cd = Child(le, "Conditions");
            if (cd == null) return o;

            Expand(o, cd.GetAttribute("Template"), Section.OnlyIf, depth, "");

            var kids = new List<XmlElement>(Kids(cd));
            if (kids.Count == 1 && kids[0].Name == "Operator")
            {
                if (kids[0].GetAttribute("Type") == "Or")
                {
                    o.Add("Any of these:");
                    CondLines(kids[0], o, Indent, depth);
                }
                else CondLines(kids[0], o, "", depth);
            }
            else CondLines(cd, o, "", depth);

            return o;
        }

        static void CondLines(XmlElement parent, List<string> o, string ind, int depth)
        {
            foreach (var e in Kids(parent))
            {
                switch (e.Name)
                {
                    case "ConditionBlock":
                        Expand(o, e.GetAttribute("Template"), Section.OnlyIf, depth, ind);
                        break;

                    case "Operator":
                        o.Add(ind + (e.GetAttribute("Type") == "Or" ? "Any of these:" : "All of these:"));
                        CondLines(e, o, ind + Indent, depth);
                        break;

                    case "Select":
                        o.Add(ind + "For each " + Pretty(e.GetAttribute("Object")) + " in " + Pretty(e.GetAttribute("From")) +
                              (e.GetAttribute("Not") == "1" ? ", none of them may match:" : ":"));
                        CondLines(e, o, ind + Indent, depth);
                        break;

                    case "Equal":
                    case "Greater":
                    case "Less":
                        o.Add(ind + CompareLine(e));
                        break;

                    default:
                        o.Add(ind + e.Name);
                        break;
                }
            }
        }

        static string CompareLine(XmlElement e)
        {
            string a, b;
            if (e.HasAttribute("Value1") || e.HasAttribute("Value2")) { a = e.GetAttribute("Value1"); b = e.GetAttribute("Value2"); }
            else { a = e.GetAttribute("Value"); b = e.GetAttribute("Than"); }

            string verb = e.Name == "Greater" ? "greater than" : e.Name == "Less" ? "less than" : "equal to";
            return Pretty(a) + (e.GetAttribute("Not") == "1" ? " is not " : " is ") + verb + " " + Pretty(b);
        }

        static List<string> DescribeDo(XmlElement le, int depth)
        {
            var o = new List<string>();

            var ac = Child(le, "Actions");
            if (ac != null)
            {
                Expand(o, ac.GetAttribute("Template"), Section.Do, depth, "");
                foreach (var e in Kids(ac)) ActLine(e, o, "", depth);
            }

            // same loose-action case as TriggerData.ReadLoop
            foreach (var e in Kids(le))
            {
                if (e.Name == "Events" || e.Name == "Conditions" || e.Name == "Actions" || e.Name == "Using") continue;
                ActLine(e, o, "", depth);
            }
            return o;
        }

        static void ActLine(XmlElement e, List<string> o, string ind, int depth)
        {
            if (e.Name == "ActionBlock")
            {
                Expand(o, e.GetAttribute("Template"), Section.Do, depth, ind);
                return;
            }

            if (e.Name == "Choose")
            {
                string order = e.GetAttribute("Order");
                string set = e.GetAttribute("Set");
                string head = order == "Sync" ? "All at the same time:"
                            : order == "Random" ? "Pick " + (string.IsNullOrEmpty(set) ? "1" : set) + " at random:"
                            : "One after another:";
                o.Add(ind + head);
                foreach (var k in Kids(e)) ActLine(k, o, ind + Indent, depth);
                return;
            }

            var names = new List<string>();
            foreach (XmlAttribute a in e.Attributes) names.Add(a.Name);
            o.Add(ind + EntryLine(TriggerCatalog.MatchAction(e.Name, names), e));
        }

        static void Expand(List<string> o, string full, Section s, int depth, string ind)
        {
            if (string.IsNullOrEmpty(full)) return;

            var l = Find(full);
            if (l == null) { o.Add(ind + "Template " + full + " (not found)"); return; }
            if (depth >= MaxDepth) { o.Add(ind + "Template " + full); return; }

            List<string> lines = s == Section.When ? DescribeWhen(l.el, depth + 1)
                               : s == Section.OnlyIf ? DescribeOnlyIf(l.el, depth + 1)
                               : DescribeDo(l.el, depth + 1);
            foreach (var x in lines) o.Add(ind + x);
        }

        // e.g. Force Animation  (Animation: RunForward, On model: {$Model})
        static string EntryLine(EntryDef def, XmlElement e)
        {
            string title = def != null ? def.title : e.Name;
            var parts = new List<string>();
            var done = new HashSet<string>();

            if (def != null)
                foreach (var pd in def.ps)
                    if (e.HasAttribute(pd.attr))
                    {
                        parts.Add(pd.label + ": " + Pretty(e.GetAttribute(pd.attr)));
                        done.Add(pd.attr);
                    }

            foreach (XmlAttribute a in e.Attributes)
                if (!done.Contains(a.Name)) parts.Add(a.Name + ": " + Pretty(a.Value));

            return parts.Count == 0 ? title : title + "  (" + string.Join(", ", parts.ToArray()) + ")";
        }

        // {Var} for variables, "{$Model}'s animationName" for queries
        public static string Pretty(string s)
        {
            var v = TValue.Parse(s);
            switch (v.mode)
            {
                case ValueMode.Variable: return "{" + v.varName + "}";
                case ValueMode.ModelProp: return Ref(v.modelIsVar, v.model) + "'s " + v.prop;
                case ValueMode.NodePos: return Ref(v.modelIsVar, v.model) + "'s " + Ref(v.nodeIsVar, v.node) + " " + v.coord;
                default: return string.IsNullOrEmpty(v.text) ? "(empty)" : v.text;
            }
        }

        static string Ref(bool isVar, string n) { return isVar ? "{" + n + "}" : n; }

        // ---------------- Xml helpers ----------------

        static XmlElement Child(XmlElement p, string name)
        {
            return p == null ? null : p.SelectSingleNode(name) as XmlElement;
        }

        static IEnumerable<XmlElement> Kids(XmlElement p)
        {
            if (p == null) yield break;
            foreach (XmlNode n in p.ChildNodes)
            {
                var e = n as XmlElement;
                if (e != null) yield return e;
            }
        }
    }
}