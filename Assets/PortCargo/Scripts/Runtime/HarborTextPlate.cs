using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace PortCargo.Runtime
{
    /// <summary>Authored readability plate follows whether a transient label has content.</summary>
    public sealed class HarborTextPlate : MonoBehaviour
    {
        public TMP_Text label;
        public Image plate;
        private void LateUpdate(){if(plate&&label){var visible=!string.IsNullOrEmpty(label.text);if(plate.enabled!=visible)plate.enabled=visible;}}
    }
}
