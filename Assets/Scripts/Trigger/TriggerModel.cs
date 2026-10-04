using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Vectorier.Trigger
{
    // ================= Values =================

    public enum ValueMode { Literal, Variable, ModelProp, NodePos }

    // one slot in a condition or an action, covers plain text, _Variable and the ?getModel queries
    public class TValue
    {
        public ValueMode mode = ValueMode.Literal;
        public string text = "";

        public string varName = "$Model";

        public bool modelIsVar = true;
        public string model = "$Model";
        public string prop = "animationName";

        public bool nodeIsVar = true;
        public string node = "$Node";
        public string coord = "worldPositionX";

        public static TValue Literal(string s) => new TValue { mode = ValueMode.Literal, text = s ?? "" };

        public TValue Clone() => Parse(ToXml());

        public string ModelRef => modelIsVar ? "_" + model : model;
        public string NodeRef => nodeIsVar ? "_" + node : node;

        public string ToXml()
        {
            switch (mode)
            {
                case ValueMode.Variable: return "_" + (varName ?? "");
                case ValueMode.ModelProp: return "?getModel[" + ModelRef + "]." + prop;
                case ValueMode.NodePos: return "?getModel[" + ModelRef + "].getNode[" + NodeRef + "]." + coord;
                default: return text ?? "";
            }
        }

        static readonly Regex NodeRx = new Regex(@"^\?getModel\[(.+?)\]\.getNode\[(.+?)\]\.(\w+)$");
        static readonly Regex PropRx = new Regex(@"^\?getModel\[(.+?)\]\.(\w+)$");

        public static TValue Parse(string s)
        {
            var v = new TValue();
            s = s ?? "";

            var m = NodeRx.Match(s);
            if (m.Success)
            {
                v.mode = ValueMode.NodePos;
                SetRef(m.Groups[1].Value, out v.modelIsVar, out v.model);
                SetRef(m.Groups[2].Value, out v.nodeIsVar, out v.node);
                v.coord = m.Groups[3].Value;
                return v;
            }

            m = PropRx.Match(s);
            if (m.Success)
            {
                v.mode = ValueMode.ModelProp;
                SetRef(m.Groups[1].Value, out v.modelIsVar, out v.model);
                v.prop = m.Groups[2].Value;
                return v;
            }

            if (s.Length > 1 && s[0] == '_')
            {
                v.mode = ValueMode.Variable;
                v.varName = s.Substring(1);
                return v;
            }

            v.mode = ValueMode.Literal;
            v.text = s;
            return v;
        }

        static void SetRef(string raw, out bool isVar, out string name)
        {
            if (!string.IsNullOrEmpty(raw) && raw[0] == '_') { isVar = true; name = raw.Substring(1); }
            else { isVar = false; name = raw; }
        }
    }

    public class TParam
    {
        public string attr;
        public TValue v = new TValue();
        public TParam(string attr, TValue v) { this.attr = attr; this.v = v; }
    }

    // ================= Events =================

    public enum EvKind { Event, Block, Raw }

    public class TEvent
    {
        public EvKind kind = EvKind.Event;
        public string id = "Enter";
        public List<TParam> ps = new List<TParam>();
        public string template = "";
        public string prefix = "";
        public string raw = "";

        public TValue Get(string attr)
        {
            for (int i = 0; i < ps.Count; i++) if (ps[i].attr == attr) return ps[i].v;
            var nv = new TValue();
            ps.Add(new TParam(attr, nv));
            return nv;
        }
    }

    // ================= Conditions =================

    public enum CondKind { Compare, Group, Select, Block, Raw }
    public enum Cmp { Equal, Greater, Less }

    public class TCond
    {
        public CondKind kind = CondKind.Compare;

        public Cmp cmp = Cmp.Equal;
        public bool not;
        public TValue a = new TValue();
        public TValue b = new TValue();
        public string type = "";        // rare Type="Bool" hint, preserved as-is
        public bool altPair;            // Greater/Less written with Value1/Value2 instead of Value/Than

        public string op = "And";       // group
        public List<TCond> children = new List<TCond>();

        public TValue selObject = new TValue();   // <Select Object From>
        public TValue selFrom = new TValue();

        public string template = "";
        public string prefix = "";
        public string raw = "";
    }

    // ================= Actions =================

    public enum ActKind { Action, Choose, Block, Raw }

    public class TAct
    {
        public ActKind kind = ActKind.Action;

        public string id = "SetVariable";
        public List<TParam> ps = new List<TParam>();

        public string order = "Straight";
        public int set = 1;
        public List<TAct> children = new List<TAct>();

        public string template = "";
        public string prefix = "";
        public string raw = "";

        public TValue Get(string attr)
        {
            for (int i = 0; i < ps.Count; i++) if (ps[i].attr == attr) return ps[i].v;
            var nv = new TValue();
            ps.Add(new TParam(attr, nv));
            return nv;
        }
    }

    // ================= Loops =================

    public enum LoopKind { Custom, TemplateLoop, FullTemplate, Raw }

    public class TLoop
    {
        public const string NoName = "Loop";

        public string name = NoName;
        public LoopKind kind = LoopKind.Custom;
        public string template = "";

        public List<TEvent> events = new List<TEvent>();
        public string rootOp = "And";
        public List<TCond> conds = new List<TCond>();
        public List<TAct> acts = new List<TAct>();

        // kept so a file that was written a certain way comes back out the same way
        public bool rootExplicit;                          // <Conditions> wrapped everything in <Operator Type="And">
        public bool evTagForm, condTagForm, actTagForm;    // template sat on <Events>/<Conditions>/<Actions> itself

        public string raw = "";

        public bool HasName => !string.IsNullOrEmpty(name) && name != NoName;

        public string Display
        {
            get
            {
                if (kind == LoopKind.FullTemplate) return string.IsNullOrEmpty(template) ? "Template" : template;
                if (kind == LoopKind.Raw) return "Unparsed";
                return HasName ? name : NoName;
            }
        }
    }

    // ================= Variables =================

    public class TVar
    {
        public string name = "NewVariable";
        public string value = "0";
        public bool locked;

        public TVar() { }
        public TVar(string n, string v, bool locked = false) { name = n; value = v; this.locked = locked; }
    }

    // ================= Whole trigger =================

    public class TriggerData
    {
        public List<TVar> vars = new List<TVar>();
        public List<TLoop> loops = new List<TLoop>();

        public static readonly string[] LockedNames = { "$Active", "$AI", "$Node", "Flag1" };

        public static TriggerData CreateDefault()
        {
            var d = new TriggerData();
            d.vars.Add(new TVar("$Active", "1", true));
            d.vars.Add(new TVar("$AI", "-1", true));
            d.vars.Add(new TVar("$Node", "COM", true));
            d.vars.Add(new TVar("Flag1", "0", true));
            return d;
        }

        // the four standard variables always exist and keep their order at the top
        public void EnsureLockedVars()
        {
            for (int i = LockedNames.Length - 1; i >= 0; i--)
            {
                string n = LockedNames[i];
                int at = vars.FindIndex(v => v.name == n);
                if (at < 0)
                {
                    string def = n == "$Active" ? "1" : n == "$AI" ? "-1" : n == "$Node" ? "COM" : "0";
                    vars.Insert(0, new TVar(n, def, true));
                }
                else
                {
                    vars[at].locked = true;
                    var v = vars[at];
                    vars.RemoveAt(at);
                    vars.Insert(0, v);
                }
            }
        }

        public List<string> VarNames()
        {
            var list = new List<string>();
            for (int i = 0; i < vars.Count; i++)
                if (!string.IsNullOrEmpty(vars[i].name)) list.Add(vars[i].name);
            for (int i = 0; i < TriggerCatalog.BuiltInVars.Length; i++)
                if (!list.Contains(TriggerCatalog.BuiltInVars[i])) list.Add(TriggerCatalog.BuiltInVars[i]);
            return list;
        }

        public TriggerData Clone() => Parse(ToXml());

        // ---------------- Write ----------------

        public string ToXml()
        {
            var doc = new XmlDocument();
            var root = doc.CreateElement("Content");
            doc.AppendChild(root);

            var init = doc.CreateElement("Init");
            root.AppendChild(init);
            for (int i = 0; i < vars.Count; i++)
            {
                if (string.IsNullOrEmpty(vars[i].name)) continue;
                var sv = doc.CreateElement("SetVariable");
                sv.SetAttribute("Name", vars[i].name);
                sv.SetAttribute("Value", vars[i].value ?? "");
                init.AppendChild(sv);
            }

            for (int i = 0; i < loops.Count; i++) WriteLoop(doc, root, loops[i]);

            return Pretty(root);
        }

        static void WriteLoop(XmlDocument doc, XmlElement root, TLoop L)
        {
            if (L.kind == LoopKind.Raw)
            {
                AppendRaw(doc, root, L.raw);
                return;
            }

            if (L.kind == LoopKind.FullTemplate)
            {
                if (string.IsNullOrEmpty(L.template)) return;
                var te = doc.CreateElement("Template");
                te.SetAttribute("Name", L.template);
                root.AppendChild(te);
                return;
            }

            var le = doc.CreateElement("Loop");
            root.AppendChild(le);

            if (L.kind == LoopKind.TemplateLoop)
            {
                le.SetAttribute("Template", L.template ?? "");
                if (L.HasName) le.SetAttribute("Name", L.name);
                return;
            }

            if (L.HasName) le.SetAttribute("Name", L.name);

            if (L.events.Count > 0)
            {
                var ee = doc.CreateElement("Events");
                le.AppendChild(ee);

                if (L.evTagForm && L.events.Count == 1 && L.events[0].kind == EvKind.Block && string.IsNullOrEmpty(L.events[0].prefix))
                    ee.SetAttribute("Template", L.events[0].template ?? "");
                else
                    for (int i = 0; i < L.events.Count; i++) WriteEvent(doc, ee, L.events[i]);
            }

            if (L.conds.Count > 0)
            {
                var ce = doc.CreateElement("Conditions");
                le.AppendChild(ce);

                if (L.condTagForm && L.conds.Count == 1 && L.conds[0].kind == CondKind.Block && string.IsNullOrEmpty(L.conds[0].prefix))
                {
                    ce.SetAttribute("Template", L.conds[0].template ?? "");
                }
                else
                {
                    XmlElement parent = ce;
                    bool wrap = L.rootOp == "Or" || L.rootExplicit;

                    if (wrap)
                    {
                        var op = doc.CreateElement("Operator");
                        op.SetAttribute("Type", L.rootOp == "Or" ? "Or" : "And");
                        ce.AppendChild(op);
                        parent = op;
                    }
                    for (int i = 0; i < L.conds.Count; i++) WriteCond(doc, parent, L.conds[i]);
                }
            }

            if (L.acts.Count > 0)
            {
                var ae = doc.CreateElement("Actions");
                le.AppendChild(ae);

                if (L.actTagForm && L.acts.Count == 1 && L.acts[0].kind == ActKind.Block && string.IsNullOrEmpty(L.acts[0].prefix))
                    ae.SetAttribute("Template", L.acts[0].template ?? "");
                else
                    for (int i = 0; i < L.acts.Count; i++) WriteAct(doc, ae, L.acts[i]);
            }
        }

        static void WriteEvent(XmlDocument doc, XmlElement parent, TEvent E)
        {
            if (E.kind == EvKind.Raw) { AppendRaw(doc, parent, E.raw); return; }

            if (E.kind == EvKind.Block)
            {
                var b = doc.CreateElement("EventBlock");
                b.SetAttribute("Template", E.template ?? "");
                if (!string.IsNullOrEmpty(E.prefix)) b.SetAttribute("Prefix", E.prefix);
                parent.AppendChild(b);
                return;
            }

            var def = TriggerCatalog.FindEvent(E.id);
            var el = doc.CreateElement(def != null ? def.tag : E.id);
            WriteParams(el, def, E.ps);
            parent.AppendChild(el);
        }

        static void WriteCond(XmlDocument doc, XmlElement parent, TCond C)
        {
            switch (C.kind)
            {
                case CondKind.Raw:
                    AppendRaw(doc, parent, C.raw);
                    return;

                case CondKind.Block:
                    {
                        var b = doc.CreateElement("ConditionBlock");
                        b.SetAttribute("Template", C.template ?? "");
                        if (!string.IsNullOrEmpty(C.prefix)) b.SetAttribute("Prefix", C.prefix);
                        parent.AppendChild(b);
                        return;
                    }

                case CondKind.Group:
                    {
                        var g = doc.CreateElement("Operator");
                        g.SetAttribute("Type", C.op == "Or" ? "Or" : "And");
                        parent.AppendChild(g);
                        for (int i = 0; i < C.children.Count; i++) WriteCond(doc, g, C.children[i]);
                        return;
                    }

                case CondKind.Select:
                    {
                        var s = doc.CreateElement("Select");
                        s.SetAttribute("Object", C.selObject.ToXml());
                        s.SetAttribute("From", C.selFrom.ToXml());
                        if (C.not) s.SetAttribute("Not", "1");
                        parent.AppendChild(s);
                        for (int i = 0; i < C.children.Count; i++) WriteCond(doc, s, C.children[i]);
                        return;
                    }

                default:
                    {
                        var e = doc.CreateElement(C.cmp.ToString());
                        if (C.cmp == Cmp.Equal || C.altPair)
                        {
                            e.SetAttribute("Value1", C.a.ToXml());
                            e.SetAttribute("Value2", C.b.ToXml());
                        }
                        else
                        {
                            e.SetAttribute("Value", C.a.ToXml());
                            e.SetAttribute("Than", C.b.ToXml());
                        }
                        if (!string.IsNullOrEmpty(C.type)) e.SetAttribute("Type", C.type);
                        if (C.not) e.SetAttribute("Not", "1");
                        parent.AppendChild(e);
                        return;
                    }
            }
        }

        static void WriteAct(XmlDocument doc, XmlElement parent, TAct A)
        {
            switch (A.kind)
            {
                case ActKind.Raw:
                    AppendRaw(doc, parent, A.raw);
                    return;

                case ActKind.Block:
                    {
                        var b = doc.CreateElement("ActionBlock");
                        b.SetAttribute("Template", A.template ?? "");
                        if (!string.IsNullOrEmpty(A.prefix)) b.SetAttribute("Prefix", A.prefix);
                        parent.AppendChild(b);
                        return;
                    }

                case ActKind.Choose:
                    {
                        var c = doc.CreateElement("Choose");
                        c.SetAttribute("Order", A.order);
                        if (A.order == "Random") c.SetAttribute("Set", Math.Max(1, A.set).ToString());
                        parent.AppendChild(c);
                        for (int i = 0; i < A.children.Count; i++) WriteAct(doc, c, A.children[i]);
                        return;
                    }

                default:
                    {
                        var def = TriggerCatalog.FindAction(A.id);
                        var el = doc.CreateElement(def != null ? def.tag : A.id);
                        WriteParams(el, def, A.ps);
                        parent.AppendChild(el);
                        return;
                    }
            }
        }

        static void WriteParams(XmlElement el, EntryDef def, List<TParam> ps)
        {
            if (def == null)
            {
                for (int i = 0; i < ps.Count; i++) el.SetAttribute(ps[i].attr, ps[i].v.ToXml());
                return;
            }

            // written in the order the catalog declares, so the xml reads the same every time
            for (int i = 0; i < def.ps.Length; i++)
            {
                var pd = def.ps[i];
                string v = null;
                for (int j = 0; j < ps.Count; j++) if (ps[j].attr == pd.attr) { v = ps[j].v.ToXml(); break; }
                if (v == null) v = pd.def;
                if (pd.optional && string.IsNullOrEmpty(v)) continue;
                el.SetAttribute(pd.attr, v ?? "");
            }

            // anything the catalog doesn't know about but the file had, kept anyway
            for (int i = 0; i < ps.Count; i++)
            {
                bool known = false;
                for (int j = 0; j < def.ps.Length; j++) if (def.ps[j].attr == ps[i].attr) { known = true; break; }
                if (!known) el.SetAttribute(ps[i].attr, ps[i].v.ToXml());
            }
        }

        static void AppendRaw(XmlDocument doc, XmlElement parent, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            try
            {
                var frag = doc.CreateDocumentFragment();
                frag.InnerXml = raw;
                parent.AppendChild(frag);
            }
            catch { }
        }

        static string Pretty(XmlElement root)
        {
            var sb = new StringBuilder();
            var set = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n",
                OmitXmlDeclaration = true,
                ConformanceLevel = ConformanceLevel.Fragment
            };
            using (var w = XmlWriter.Create(sb, set)) root.WriteContentTo(w);
            return sb.ToString().Trim();
        }

        // ---------------- Read ----------------

        public static TriggerData Parse(string contentInnerXml)
        {
            var d = new TriggerData();

            var doc = new XmlDocument();
            try { doc.LoadXml("<Content>" + (contentInnerXml ?? "") + "</Content>"); }
            catch { d.EnsureLockedVars(); return d; }

            var root = doc.DocumentElement;
            if (root == null) { d.EnsureLockedVars(); return d; }

            foreach (XmlNode n in root.ChildNodes)
            {
                var e = n as XmlElement;
                if (e == null) continue;

                if (e.Name == "Init")
                {
                    foreach (XmlNode vn in e.ChildNodes)
                    {
                        var ve = vn as XmlElement;
                        if (ve == null || ve.Name != "SetVariable") continue;
                        d.vars.Add(new TVar(ve.GetAttribute("Name"), ve.GetAttribute("Value")));
                    }
                }
                else if (e.Name == "Template")
                {
                    d.loops.Add(new TLoop { kind = LoopKind.FullTemplate, template = e.GetAttribute("Name") });
                }
                else if (e.Name == "Loop")
                {
                    d.loops.Add(ReadLoop(e));
                }
                else
                {
                    d.loops.Add(new TLoop { kind = LoopKind.Raw, raw = e.OuterXml });
                }
            }

            d.EnsureLockedVars();
            return d;
        }

        static TLoop ReadLoop(XmlElement le)
        {
            var L = new TLoop();
            L.name = le.GetAttribute("Name");
            if (string.IsNullOrEmpty(L.name)) L.name = TLoop.NoName;

            string lt = le.GetAttribute("Template");
            if (!string.IsNullOrEmpty(lt))
            {
                L.kind = LoopKind.TemplateLoop;
                L.template = lt;
                return L;
            }

            var ev = le.SelectSingleNode("Events") as XmlElement;
            if (ev != null)
            {
                string t = ev.GetAttribute("Template");
                if (!string.IsNullOrEmpty(t))
                {
                    L.evTagForm = true;
                    L.events.Add(new TEvent { kind = EvKind.Block, template = t, prefix = ev.GetAttribute("Prefix") });
                }

                foreach (XmlNode n in ev.ChildNodes)
                {
                    var e = n as XmlElement;
                    if (e == null) continue;
                    L.events.Add(ReadEvent(e));
                }
            }

            var cond = le.SelectSingleNode("Conditions") as XmlElement;
            if (cond != null)
            {
                string t = cond.GetAttribute("Template");
                if (!string.IsNullOrEmpty(t))
                {
                    L.condTagForm = true;
                    L.conds.Add(new TCond { kind = CondKind.Block, template = t, prefix = cond.GetAttribute("Prefix") });
                }

                var kids = Elements(cond);

                // a single top level Operator becomes the root And/Or instead of a nested group
                if (L.conds.Count == 0 && kids.Count == 1 && kids[0].Name == "Operator")
                {
                    L.rootOp = kids[0].GetAttribute("Type") == "Or" ? "Or" : "And";
                    L.rootExplicit = true;
                    foreach (var c in Elements(kids[0])) L.conds.Add(ReadCond(c));
                }
                else
                {
                    for (int i = 0; i < kids.Count; i++) L.conds.Add(ReadCond(kids[i]));
                }
            }

            var act = le.SelectSingleNode("Actions") as XmlElement;
            if (act != null)
            {
                string t = act.GetAttribute("Template");
                if (!string.IsNullOrEmpty(t))
                {
                    L.actTagForm = true;
                    L.acts.Add(new TAct { kind = ActKind.Block, template = t, prefix = act.GetAttribute("Prefix") });
                }

                foreach (var a in Elements(act)) L.acts.Add(ReadAct(a));
            }

            // loose children like <Sound Name="_Sound"/> sitting straight under <Loop>
            foreach (XmlNode n in le.ChildNodes)
            {
                var e = n as XmlElement;
                if (e == null) continue;
                if (e.Name == "Events" || e.Name == "Conditions" || e.Name == "Actions" || e.Name == "Using") continue;
                L.acts.Add(ReadAct(e));
            }

            return L;
        }

        static List<XmlElement> Elements(XmlElement parent)
        {
            var list = new List<XmlElement>();
            foreach (XmlNode n in parent.ChildNodes)
            {
                var e = n as XmlElement;
                if (e != null) list.Add(e);
            }
            return list;
        }

        static TEvent ReadEvent(XmlElement e)
        {
            if (e.Name == "EventBlock")
                return new TEvent { kind = EvKind.Block, template = e.GetAttribute("Template"), prefix = e.GetAttribute("Prefix") };

            var def = TriggerCatalog.FindEvent(e.Name);
            if (def == null) return new TEvent { kind = EvKind.Raw, raw = e.OuterXml };

            var E = new TEvent { kind = EvKind.Event, id = def.id };
            ReadParams(e, E.ps);
            return E;
        }

        static TCond ReadCond(XmlElement e)
        {
            if (e.Name == "ConditionBlock")
                return new TCond { kind = CondKind.Block, template = e.GetAttribute("Template"), prefix = e.GetAttribute("Prefix") };

            if (e.Name == "Operator")
            {
                var g = new TCond { kind = CondKind.Group, op = e.GetAttribute("Type") == "Or" ? "Or" : "And" };
                foreach (var c in Elements(e)) g.children.Add(ReadCond(c));
                return g;
            }

            if (e.Name == "Select")
            {
                var s = new TCond
                {
                    kind = CondKind.Select,
                    selObject = TValue.Parse(e.GetAttribute("Object")),
                    selFrom = TValue.Parse(e.GetAttribute("From")),
                    not = e.GetAttribute("Not") == "1"
                };
                foreach (var c in Elements(e)) s.children.Add(ReadCond(c));
                return s;
            }

            if (e.Name == "Equal" || e.Name == "Greater" || e.Name == "Less")
            {
                var C = new TCond { kind = CondKind.Compare };
                C.cmp = e.Name == "Greater" ? Cmp.Greater : e.Name == "Less" ? Cmp.Less : Cmp.Equal;
                C.not = e.GetAttribute("Not") == "1";
                C.type = e.GetAttribute("Type");

                if (e.HasAttribute("Value1") || e.HasAttribute("Value2"))
                {
                    C.altPair = C.cmp != Cmp.Equal;
                    C.a = TValue.Parse(e.GetAttribute("Value1"));
                    C.b = TValue.Parse(e.GetAttribute("Value2"));
                }
                else
                {
                    C.a = TValue.Parse(e.GetAttribute("Value"));
                    C.b = TValue.Parse(e.GetAttribute("Than"));
                }
                return C;
            }

            return new TCond { kind = CondKind.Raw, raw = e.OuterXml };
        }

        static TAct ReadAct(XmlElement e)
        {
            if (e.Name == "ActionBlock")
                return new TAct { kind = ActKind.Block, template = e.GetAttribute("Template"), prefix = e.GetAttribute("Prefix") };

            if (e.Name == "Choose")
            {
                var c = new TAct { kind = ActKind.Choose, order = e.GetAttribute("Order") };
                if (string.IsNullOrEmpty(c.order)) c.order = "Straight";
                int.TryParse(e.GetAttribute("Set"), out c.set);
                if (c.set < 1) c.set = 1;
                foreach (var k in Elements(e)) c.children.Add(ReadAct(k));
                return c;
            }

            var names = new List<string>();
            if (e.HasAttributes) foreach (XmlAttribute a in e.Attributes) names.Add(a.Name);

            var def = TriggerCatalog.MatchAction(e.Name, names);
            if (def == null) return new TAct { kind = ActKind.Raw, raw = e.OuterXml };

            var A = new TAct { kind = ActKind.Action, id = def.id };
            ReadParams(e, A.ps);
            return A;
        }

        static void ReadParams(XmlElement e, List<TParam> ps)
        {
            if (!e.HasAttributes) return;
            foreach (XmlAttribute a in e.Attributes)
                ps.Add(new TParam(a.Name, TValue.Parse(a.Value)));
        }
    }
}