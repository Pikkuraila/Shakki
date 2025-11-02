using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class PieceIcon : MonoBehaviour
{
    [Header("Refs (optional)")]
    [SerializeField] private Image image;           // pääkuva
    [SerializeField] private TMP_Text priceText;    // hinta
    [SerializeField] private GameObject priceTag;   // esim. tausta tai label-ryhmä

    private void Awake()
    {
        // Varmista että pääkuva löytyy myös ilman manuaalista linkitystä
        if (image == null)
            image = GetComponent<Image>();
    }


    /// <summary>
    /// Bindaa uuden spriten ja hinnan ikoniin.
    /// </summary>
    public void Bind(Sprite sprite, int price, bool showPrice)
    {

        Debug.Log($"[PieceIcon] Bind sprite={(sprite ? sprite.name : "null")} price={price} show={showPrice}", this);



        // --- Kuva ---
        if (image == null)
            image = GetComponent<Image>();

        if (image != null)
        {
            image.sprite = sprite;
            image.enabled = (sprite != null);

            // Näytä tyhjä ikoni läpinäkyvänä eikä valkoisena laatikkona
            image.color = (sprite == null)
                ? new Color(1f, 1f, 1f, 0f)
                : Color.white;
        }

        // --- Hinta ---
        if (priceText != null)
            priceText.text = price.ToString();

        // --- Hintalapun näkyvyys ---
        if (priceTag != null)
            priceTag.SetActive(showPrice);
        else if (priceText != null)
            priceText.gameObject.SetActive(showPrice);
    }
}
