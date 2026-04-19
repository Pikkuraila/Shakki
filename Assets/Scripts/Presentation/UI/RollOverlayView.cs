using UnityEngine;
using UnityEngine.UI;

namespace Shakki.Presentation
{
    public sealed class RollOverlayView : MonoBehaviour
    {
        [SerializeField] private Image resultImage;

        public void Show(Sprite sprite)
        {
            if (resultImage != null)
                resultImage.sprite = sprite;

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}