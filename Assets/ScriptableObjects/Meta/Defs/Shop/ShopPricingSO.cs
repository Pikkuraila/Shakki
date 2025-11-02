using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/Meta/ShopPricing", fileName = "ShopPricing")]
public sealed class ShopPricingSO : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        public string pieceId;
        public int price;
    }

    public List<Entry> prices;

    public int GetPrice(string id)
    {
        foreach (var e in prices)
            if (!string.IsNullOrEmpty(e.pieceId) && e.pieceId == id)
                return Mathf.Max(0, e.price);
        return 0;
    }

    // --- Uudet metodit ---

    public int GetPriceForPiece(string pieceId, PlayerData pd)
    {
        return GetPrice(pieceId, pd); // k‰yt‰ mit‰ sulla jo on ñ tai palauta vakio debugiksi
    }

    public int GetPriceForPowerup(string powerupId, PlayerData pd)
    {
        return GetPrice(powerupId, pd); // sama idea kuin yll‰
    }

    // Jos ei ole PlayerData-spesifi‰ logiikkaa viel‰, k‰ytet‰‰n fallbackia
    public int GetPrice(string id, PlayerData pd)
    {
        // TODO: korvaa oikealla hinnoittelulogiikalla (esim. pelaajan edistymisen mukaan)
        return GetPrice(id); // toistaiseksi sama kuin perus GetPrice
    }
}
