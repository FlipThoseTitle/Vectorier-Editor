using UnityEngine;

namespace Vectorier.Component
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vectorier/Component/Particle Component")]
    public class ParticleComponent : MonoBehaviour
    {
        public enum ParticleType
        {
            [InspectorName("1")] Type1 = 1,
            [InspectorName("2")] Type2 = 2
        }

        public ParticleType Type = ParticleType.Type1;
        public int Frame;
    }
}


