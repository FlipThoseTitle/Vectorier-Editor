using UnityEngine;

namespace Vectorier.Component
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Vectorier/Component/Area Component")]
    public class AreaComponent : MonoBehaviour
    {
        public enum AreaType
        {
            Animation,
            Catch,
            Trick,
            Help
        }
        [Tooltip("Animation - Used to define move area, Default type even if component isn't added.\n" +
            "Catch - Use Distance, If hunter is X distance away while leading, will perform a catch upon entering area.\n" +
            "Trick - Use ItemName and Score, Ex. TRICK_FOLDFLIP\n" +
            "Help - Use Key and Description, area to display tutorial prompt.")]
        public AreaType Type = AreaType.Animation;

        [Tooltip("The minimum distance required for the hunter to catch.\nTriggerCatchFast has distance set to 0\nDefault: 300")]
        public int Distance = 300;

        [Tooltip("The trick that will be activated\nEx. TRICK_FOLDFLIP")]
        public string ItemName = "TRICK_";

        [Tooltip("Score earned from performing trick.\nDefault: 100")]
        public int Score = 100;

        [Tooltip("Key used for the tutorial prompt\nKey: Up, Down, Left, Right")]
        public string Key = "Up";

        [Tooltip("The Description")]
        public string Description;
    }
}