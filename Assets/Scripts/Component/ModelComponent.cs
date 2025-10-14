using UnityEngine;

namespace Vectorier.Component
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vectorier/Component/Model Component")]
    public class ModelComponent : MonoBehaviour
    {
        public enum ModelType
        {
            Physics = 0,
            Static = 1
        }

        [Tooltip("Physics - Activate physics upon touching, Ex. Stool, Ladder\nStatic - Static model that can only be animated, Ex. glider, lift")]
        public ModelType Type = ModelType.Physics;

        [Tooltip("The life time of physics model. (Only used for type Physics)\nDefault: 10")]
        public int LifeTime = 10;
    }
}