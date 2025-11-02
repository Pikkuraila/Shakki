using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ShopPool",
    menuName = "Shakki/Meta/Shop Pool",
    order = 0)]
public class ShopPoolSO : ScriptableObject
{
    public List<ShopItemDefSO> items;

    [Header("Filters (optional)")]
    public List<string> includeTags;
    public List<string> excludeTags;
    public bool includePieces = true;
    public bool includePowerups = true;

    [Header("Dupes / Copies")]
    public int maxCopiesFromPlayer = 8;

    public IEnumerable<ShopItemDefSO> Filtered(PlayerData pd)
    {
        foreach (var it in items)
        {
            if (it == null) { Debug.Log("[ShopPool] skip: null item"); continue; }

            bool isPiece = it.piece != null;
            bool isPowerup = it.powerup != null;
            bool isItem = it.item != null;

            if (isPiece && !includePieces) { Debug.Log($"[ShopPool] skip {it.name}: pieces disabled"); continue; }
            if (isPowerup && !includePowerups) { Debug.Log($"[ShopPool] skip {it.name}: powerups disabled"); continue; }

            // Yhdistä taulukko/lista samaan rajapintaan
            IList<string> tags = it.tags as IList<string>; // toimii sekä string[] että List<string>

            // --- includeTags ---
            if (includeTags != null && includeTags.Count > 0)
            {
                if (tags == null || tags.Count == 0)
                {
                    Debug.Log($"[ShopPool] skip {it.name}: no tags for include");
                    continue;
                }

                bool hasAll = true;
                for (int i = 0; i < includeTags.Count; i++)
                {
                    string t = includeTags[i];
                    if (!tags.Contains(t)) { hasAll = false; break; }
                }
                if (!hasAll)
                {
                    Debug.Log($"[ShopPool] skip {it.name}: missing includeTags");
                    continue;
                }
            }

            // --- excludeTags ---
            if (excludeTags != null && excludeTags.Count > 0 && tags != null && tags.Count > 0)
            {
                bool hasExcluded = false;
                for (int i = 0; i < excludeTags.Count; i++)
                {
                    string t = excludeTags[i];
                    if (tags.Contains(t)) { hasExcluded = true; break; }
                }
                if (hasExcluded)
                {
                    Debug.Log($"[ShopPool] skip {it.name}: hit excludeTags");
                    continue;
                }
            }

            // --- Pelaajan kopiot / omistuslogiikka ---
            if (pd != null && isPiece && maxCopiesFromPlayer > 0)
            {
                // Jos sinulla on piece.id, mieluummin käytä sitä
                string id = it.piece.typeName;
                int copies = CountPlayerCopies(pd, id);
                if (copies >= maxCopiesFromPlayer)
                {
                    Debug.Log($"[ShopPool] skip {it.name}: copies {copies}/{maxCopiesFromPlayer}");
                    continue;
                }
            }

            Debug.Log($"[ShopPool] OK {it.name}");
            yield return it;
        }
    }
     
    // Apumetodi: laskee montako kertaa piece-id esiintyy pelaajan loadoutissa
    private int CountPlayerCopies(PlayerData pd, string pieceId)
    {
        if (pd == null || string.IsNullOrEmpty(pieceId) || pd.loadoutSlots == null) return 0;
        int count = 0;
        for (int i = 0; i < pd.loadoutSlots.Count; i++)
            if (pd.loadoutSlots[i] == pieceId) count++;
        return count;
    }
}
