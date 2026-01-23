using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        [SerializeField] private Transform tagsRoot;
        [SerializeField] private TMP_Text tagChipPrefab;

        [SerializeField] private Transform infoRoot;
        [SerializeField] private TMP_Text infoRowPrefab;

        [Header("Optional tags UI (voit jättää tyhjäksi nyt)")]
        // TODO: tagsRoot + tagPrefab jos haluat chipit
        // [SerializeField] private Transform tagsRoot;

        [Header("Fallback")]
        [SerializeField] private Sprite fallbackPortrait;

        public void Render(InspectData data)
        {
            Debug.Log($"[InspectPanelView] Render called on '{name}' active={gameObject.activeInHierarchy} dataTitle='{data?.title}' dataPortrait={(data?.portrait ? data.portrait.name : "NULL")}");
            Debug.Log($"[InspectPanelView] refs: nameText={(nameText ? "OK" : "NULL")} portraitImage={(portraitImage ? "OK" : "NULL")} frame={(portraitFrameImage ? "OK" : "NULL")}");

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

            // Frame on aina päällä jos siihen on asetettu sprite inspectorissa.
            if (portraitFrameImage)
            {
                portraitFrameImage.enabled = portraitFrameImage.sprite != null;
                portraitFrameImage.preserveAspect = true;
            }

            // TODO tags: instantiate chipit data.tags perusteella

            // TAGS
            ClearChildren(tagsRoot);
            if (data.tags != null && data.tags.Length > 0 && tagsRoot != null && tagChipPrefab != null)
            {
                foreach (var t in data.tags)
                {
                    if (string.IsNullOrWhiteSpace(t)) continue;
                    var chip = Instantiate(tagChipPrefab, tagsRoot);
                    chip.text = t;
                }
            }

            // INFO LINES
            ClearChildren(infoRoot);
            if (data.infoLines != null && data.infoLines.Length > 0 && infoRoot != null && infoRowPrefab != null)
            {
                foreach (var line in data.infoLines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var row = Instantiate(infoRowPrefab, infoRoot);
                    row.text = line;
                }
            }

        }

        void ClearChildren(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }
    }
}
