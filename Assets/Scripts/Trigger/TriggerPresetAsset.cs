using System.Collections.Generic;
using UnityEngine;
using Vectorier.Component;

namespace Vectorier.Trigger
{
    // Same xml as TriggerComponent.contentXml so a preset can be applied without converting anything.
    [CreateAssetMenu(menuName = "Vectorier/Trigger Preset", fileName = "TriggerPreset")]
    public class TriggerPresetAsset : ScriptableObject
    {
        [Tooltip("Short note about what this preset does.")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Everything inside <Content>, same as the Trigger Component.")]
        [TextArea(8, 40)]
        public string contentXml;

        // Editor only. Types aren't in the xml, so they're kept on the side like TriggerComponent.variableTypes.
        [SerializeField, HideInInspector]
        public List<TriggerComponent.VariableType> variableTypes = new List<TriggerComponent.VariableType>();
    }
}