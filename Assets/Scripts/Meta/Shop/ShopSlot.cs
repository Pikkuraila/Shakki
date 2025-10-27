using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class ShopSlot : MonoBehaviour
{
    public string pieceId;    // mit‰ myyd‰‰n t‰st‰ slotista
    public int pricePreview;  // vain UI:lle (voi lukea SO:sta runtime)
}