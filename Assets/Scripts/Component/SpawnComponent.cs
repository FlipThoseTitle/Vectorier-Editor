using UnityEngine;

namespace Vectorier.Component
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vectorier/Component/Spawn Component")]
    public class SpawnComponent : MonoBehaviour
    {
        [Tooltip("First Parameter - Moves name\nSecond Parameter - Starting Frame\nEx. JumpOff|18")]
        public string Animation = "JumpOff|18";
    }
}