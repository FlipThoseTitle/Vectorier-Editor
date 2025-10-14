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
    }
}
