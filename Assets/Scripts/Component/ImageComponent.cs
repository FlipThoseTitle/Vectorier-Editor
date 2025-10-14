using UnityEngine;
namespace Vectorier.Component
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vectorier/Component/Image Component")]
    public class ImageComponent : MonoBehaviour
    {
        public enum ImageType
        {
            None,
            Static,
            Vanishing,
            Dynamic
        }

        [Tooltip("None - Default\nStatic - Static\nVanishing - Image will disappear if under trigger with NoneType event.\nDynamic - Plays the image's animation if it has .plist")]
        public ImageType Type = ImageType.None;
    }
}