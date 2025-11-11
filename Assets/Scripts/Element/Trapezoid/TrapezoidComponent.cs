using UnityEngine;

namespace Vectorier.Component
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vectorier/Component/Trapezoid Component")]
    public class TrapezoidComponent : MonoBehaviour
    {
        public enum TrapezoidType
        {
            [InspectorName("1")] Type1 = 1,
            [InspectorName("2")] Type2 = 2
        }

        [Tooltip("The type of trapezoid. Type 2 is the mirrored version.\nDefault: 1")]
        public TrapezoidType Type = TrapezoidType.Type1;
    }
}