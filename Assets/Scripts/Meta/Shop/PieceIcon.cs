using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PieceIcon : MonoBehaviour
{
    public Image icon;
    public TMP_Text priceTag; // shopissa näkyy, loadoutissa piilotetaan

    public void Bind(Sprite sprite, int price = 0, bool showPrice = false)
    {
        icon.sprite = sprite;
        if (priceTag != null)
        {
            priceTag.gameObject.SetActive(showPrice);
            priceTag.text = showPrice ? price.ToString() : "";
        }
    }
}
