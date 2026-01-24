using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shakki.Presentation.Inspect
{
    public sealed class TagChipView : MonoBehaviour
    {
        [SerializeField] private Image bg;
        [SerializeField] private TMP_Text label;

        public void Bind(string text, Color bgColor)
        {
            if (label) label.text = text ?? "";

            if (bg)
            {
                bg.color = bgColor;
                bg.enabled = true;
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (bg == null) bg = GetComponent<Image>();
            if (label == null) label = GetComponentInChildren<TMP_Text>(true);
        }
#endif
    }
}
