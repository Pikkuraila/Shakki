using UnityEngine;

public sealed class ShopService : MonoBehaviour
{
    public bool BuyPiece(string pieceId, int price)
    {
        if (!PlayerService.Instance.SpendCoins(price)) return false;
        PlayerService.Instance.GrantPiece(pieceId);
        return true;
    }

    public bool BuyUpgrade(string upgradeId) => PlayerService.Instance.BuyUpgrade(upgradeId);
}
