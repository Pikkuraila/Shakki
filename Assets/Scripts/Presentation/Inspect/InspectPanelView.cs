using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Shakki.Presentation.Inspect
{
    public sealed class InspectPanelView : MonoBehaviour
    {
        [Header("Portrait (Frame + Inner)")]
        [SerializeField] private Image portraitFrameImage;   // sprite = Portrait-border
        [SerializeField] private Image portraitImage;        // vaihtuva sprite

        [Header("Text")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text loreText;

        [Header("Tags")]
        [SerializeField] private Transform tagsRoot;
        [SerializeField] private TagChipView tagChipPrefab;
        [SerializeField] private TagStyleRegistrySO tagStyles;
        [SerializeField] private Color unknownTagBg = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        [Header("Info lines (optional)")]
        [SerializeField] private Transform infoRoot;
        [SerializeField] private TMP_Text infoRowPrefab;

        [Header("Fallback")]
        [SerializeField] private Sprite fallbackPortrait;

        public void Render(InspectData data)
        {
            if (data == null)
            {
                if (nameText) nameText.text = "";
                if (loreText) loreText.text = "";
                if (portraitImage)
                {
                    portraitImage.sprite = fallbackPortrait;
                    portraitImage.enabled = fallbackPortrait != null;
                    portraitImage.preserveAspect = true;
                }
                ClearChildren(tagsRoot);
                ClearChildren(infoRoot);
                return;
            }

            if (nameText) nameText.text = data.title ?? "";
            if (loreText) loreText.text = data.lore ?? "";

            if (portraitImage)
            {
                portraitImage.sprite = data.portrait != null ? data.portrait : fallbackPortrait;
                portraitImage.enabled = portraitImage.sprite != null;
                portraitImage.preserveAspect = true;
            }

            if (portraitFrameImage)
            {
                portraitFrameImage.enabled = portraitFrameImage.sprite != null;
                portraitFrameImage.preserveAspect = true;
            }

            // TAG CHIPS
            ClearChildren(tagsRoot);
            if (tagsRoot != null && tagChipPrefab != null && data.tags != null)
            {
                for (int i = 0; i < data.tags.Length; i++)
                {
                    var raw = data.tags[i];
                    if (string.IsNullOrWhiteSpace(raw)) continue;

                    var style = tagStyles != null
                        ? tagStyles.GetOrDefault(raw, unknownTagBg)
                        : default;

                    var label = (tagStyles != null && !string.IsNullOrWhiteSpace(style.label))
                        ? style.label
                        : raw;

                    var bg = (tagStyles != null) ? style.background : unknownTagBg;

                    var chip = Instantiate(tagChipPrefab, tagsRoot);
                    chip.Bind(label, bg);
                }
            }

            // INFO LINES (optional)
            ClearChildren(infoRoot);
            if (infoRoot != null && infoRowPrefab != null && data.infoLines != null)
            {
                foreach (var line in data.infoLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var row = Instantiate(infoRowPrefab, infoRoot);
                    row.text = line;
                }
            }
        }

        static void ClearChildren(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }
    }
}
