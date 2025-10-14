using UnityEngine;

namespace Vectorier.Component
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vectorier/Component/Selection Component")]
    public class SelectionComponent : MonoBehaviour
    {
        public enum SelectionVariant
        {
            CommonMode,
            HunterMode
        }

        [Tooltip("CommonMode - Only display this object in Normal Mode.\nHunterMode - Only display this object in Hunter Mode.")]
        public SelectionVariant Variant = SelectionVariant.CommonMode;
    }
}