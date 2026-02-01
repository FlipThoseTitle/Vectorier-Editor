using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vectorier.Trigger
{
    [CreateAssetMenu(menuName = "Vectorier/Trigger Preset", fileName = "TriggerPreset")]
    public class TriggerPresetAsset : ScriptableObject
    {
        [Serializable] public class InitVar { public string n; public string v; }

        [Serializable]
        public class Cond
        {
            public TriggerEditor.Ck k;
            public string a;
            public string b;
            public bool nott;
        }

        [Serializable] public class Attr { public string k; public string v; }

        [Serializable]
        public class Act
        {
            public string n;
            public List<Attr> at = new List<Attr>();
        }

        public string templateName;

        [Serializable]
        public class LoopPreset
        {
            public string name;
            public string loopTemplate;

            public List<TriggerEditor.Ev> ev = new List<TriggerEditor.Ev>();
            public string evTemplate;

            public TriggerEditor.Op op;
            public string condTemplate;
            public List<Cond> c = new List<Cond>();

            public List<Act> a = new List<Act>();
            public string actTemplate;
        }

        public List<InitVar> init = new List<InitVar>();
        public List<LoopPreset> loops = new List<LoopPreset>();
    }
}
