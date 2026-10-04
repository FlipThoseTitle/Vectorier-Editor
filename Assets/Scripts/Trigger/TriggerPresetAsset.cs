using UnityEngine;

namespace Vectorier.Trigger
{
    // a preset is just a saved trigger body, stored as the same xml a TriggerComponent holds
    [CreateAssetMenu(menuName = "Vectorier/Trigger Preset", fileName = "TriggerPreset")]
    public class TriggerPresetAsset : ScriptableObject
    {
        [Tooltip("Short note about what this preset does.")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("Everything inside <Content>, same as the Trigger Component.")]
        [TextArea(8, 40)]
        public string contentXml;
    }
}