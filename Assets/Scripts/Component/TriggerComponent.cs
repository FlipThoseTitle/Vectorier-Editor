using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vectorier.Component
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vectorier/Component/Trigger Component")]
    public class TriggerComponent : MonoBehaviour
    {
        [Tooltip("Trigger's Content.")]
        [TextArea(6, 20)]
        public string contentXml;

        // editor only: the type each variable has in the Trigger Editor. never exported, hidden from the inspector
        [Serializable]
        public class VariableType
        {
            public string name;
            public string type;
        }

        [SerializeField, HideInInspector]
        public List<VariableType> variableTypes = new List<VariableType>();
    }
}