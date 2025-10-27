using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/Meta/ShopPricing", fileName = "ShopPricing")]
public sealed class ShopPricingSO : ScriptableObject
{
    [System.Serializable] public struct Entry { public string pieceId; public int price; }
    public List<Entry> prices;

    public int GetPrice(string id)
    {
        foreach (var e in prices)
            if (!string.IsNullOrEmpty(e.pieceId) && e.pieceId == id)
                return Mathf.Max(0, e.price);
        return 0;
    }
}
