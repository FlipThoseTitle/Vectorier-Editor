using UnityEngine;

namespace Vectorier.Component
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vectorier/Component/Item Component")]
    public class ItemComponent : MonoBehaviour
    {
        public enum ItemType
        {
            Bonus = 0,
            Coin = 1
        }

        [Tooltip("Item type (Bonus or Coin).")]
        public ItemType Type = ItemType.Bonus;

        [Tooltip("The score value when this item is collected.\nDefault: 10")]
        public int Score = 10;

        [Tooltip("The area radius of the item.\nDefault: 80")]
        public float Radius = 80f;

        [Tooltip("Group ID (Only used for coins item.)\nDefault: 1")]
        public int GroupId = 1;
    }
}