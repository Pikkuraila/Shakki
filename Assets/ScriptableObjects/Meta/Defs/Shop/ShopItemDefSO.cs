using UnityEngine;

[CreateAssetMenu(fileName = "ShopItem", menuName = "Shakki/Meta/Shop Item")]
public class ShopItemDefSO : ScriptableObject
{
    public PieceDefSO piece;
    [Min(0)] public int price = 1;
    [Header("UI")]
    public Sprite overrideIcon; // jos haluat erikoisikonin, muuten k‰ytet‰‰n piece.whiteSpritea
}
