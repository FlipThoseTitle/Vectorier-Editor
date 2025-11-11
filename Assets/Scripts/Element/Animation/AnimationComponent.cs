using UnityEngine;

namespace Vectorier.Component
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vectorier/Component/Animation Component")]
    public class AnimationComponent : MonoBehaviour
    {
        public enum AnimationType
        {
            [InspectorName("0")] Type0 = 0,
            [InspectorName("1")] Type1 = 1
        }

        [Tooltip("Type 0 - Animated only, mostly used for papers effect.\nType 1 - Animated with Direction and Acceleration, mostly used for birds flying.")]
        public AnimationType Type = AnimationType.Type1;

        [Tooltip("Direction of the animated.\nEx. 2|-3 (Top Right Direction)")]
        public string Direction = "0|0";

        [Tooltip("Acceleration of the animated.\nEx. 0.08|-0.1 (Move 0.08 unit horizontally every frame | Move 0.1 unit vertically every frame)")]
        public string Acceleration = "0|0";

        [Tooltip("Time in second before the animated disappear.")]
        public float Time = 0f;
    }
}


