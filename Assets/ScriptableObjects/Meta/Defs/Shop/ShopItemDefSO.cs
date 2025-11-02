using UnityEngine;

[CreateAssetMenu(fileName = "ShopItem", menuName = "Shakki/Meta/Shop Item")]
public class ShopItemDefSO : ScriptableObject
{
    [Header("Content")]
    public PieceDefSO piece;               // jos nappula
    public PowerupDefSO powerup;           // jos instant/passiivinen powerup
    public ItemDefSO item;                 // jos erillinen itemi inventaarioon

    [Header("Pricing")]
    [Min(0)] public int price = 1;

    [Header("UI")]
    public Sprite overrideIcon;            // jos haluat erikoisikonin, muuten k‰ytet‰‰n piece.whiteSpritea

    [Header("Tags")]
    public string[] tags;                  // vapaaehtoiset filtterit (esim. "offense", "defense", "rare")
}
