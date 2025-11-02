using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class LoadoutService
{
    readonly PlayerService _player;
    readonly GameCatalogSO _catalog;
    readonly ShopPricingSO _pricing;

    // --- UI:n käyttämä 16-slotin tasattu lista ("" = tyhjä) ---
    private List<string> _flat;

    // ✅ Overload ilman pricingia (korjaa CS7036)
    public LoadoutService(PlayerService player, GameCatalogSO catalog)
        : this(player, catalog, null) { }

    public LoadoutService(PlayerService player, GameCatalogSO catalog, ShopPricingSO pricing)
    {
        _player = player;
        _catalog = catalog;
        _pricing = pricing;
    }

    // ---- Coins / hinnoittelu ----
    public int GetCoins() => _player.Data.coins;
    public void AddCoins(int delta) => _player.Data.coins = Mathf.Max(0, _player.Data.coins + delta);

    // SHIM: alias vanhalle nimelle
    public int Coins => GetCoins();

    // Hinnoittelu on valinnainen
    public int GetPrice(string pieceId) => _pricing != null ? _pricing.GetPrice(pieceId) : 0;
    public bool CanAfford(string pieceId) => GetCoins() >= GetPrice(pieceId);

    public bool TryBuy(string pieceId)
    {
        int p = GetPrice(pieceId);
        if (GetCoins() < p) return false;
        AddCoins(-p);
        return true;
    }

    public void Refund(string pieceId, float ratio = 1f)
    {
        int p = Mathf.RoundToInt(GetPrice(pieceId) * Mathf.Clamp01(ratio));
        AddCoins(p);
    }

    // --- Flat 16 UI slottien ylläpito ---
    public void EnsureLoadoutSize(int slots = 16, string implicitKingId = null)
    {
        if (_flat != null && _flat.Count == slots) return;
        _flat = Expand(slots, implicitKingId);
    }

    private List<string> Expand(int slots, string implicitKingId)
    {
        var flat = new List<string>(slots);

        // Pakkaa PlayerData.loadout -> flat
        foreach (var e in _player.Data.loadout)
        {
            if (string.IsNullOrEmpty(e.pieceId) || e.count <= 0) continue;
            for (int i = 0; i < e.count; i++) flat.Add(e.pieceId);
        }

        // Varmista kuningas tarvittaessa
        if (!string.IsNullOrEmpty(implicitKingId) && !flat.Contains(implicitKingId))
            flat.Add(implicitKingId);

        // Leikkaus/täyttö slottimäärään
        if (flat.Count > slots) flat = flat.Take(slots).ToList();
        while (flat.Count < slots) flat.Add(string.Empty);

        return flat;
    }

    // SHIM: UI odottaa näitä nimiä
    public List<string> ExpandToSlots(int slots = 16, string implicitKingId = null)
    {
        EnsureLoadoutSize(slots, implicitKingId);
        return _flat;
    }

    public void SaveFromSlots(List<string> flat16)
    {
        if (flat16 == null) return;
        _flat = new List<string>(flat16);
        SaveBack();
    }

    // Kompressio takaisin PlayerDataan
    public void SaveBack()
    {
        // Varmista että _flat on olemassa jottei null-viitteitä synny
        if (_flat == null) EnsureLoadoutSize();

        _player.Data.loadout = _flat
            .Where(id => !string.IsNullOrEmpty(id))
            .GroupBy(id => id)
            .Select(g => new LoadoutEntry { pieceId = g.Key, count = g.Count() })
            .ToList();
    }

    // Slot-apurit (UI käyttää näitä)
    public string GetAt(int index)
    {
        if (_flat == null) EnsureLoadoutSize();
        return _flat[index];
    }

    public void SetAt(int index, string id)
    {
        if (_flat == null) EnsureLoadoutSize();
        _flat[index] = id ?? string.Empty;
    }

    public void Swap(int a, int b)
    {
        if (_flat == null) EnsureLoadoutSize();
        (_flat[a], _flat[b]) = (_flat[b], _flat[a]);
    }

    // SHIM: haku katalogista (molemmat nimet tuettu)
    public PieceDefSO TryGetPieceDef(string id) => _catalog.GetPieceById(id);
    public PieceDefSO TryGetPiece(string id) => _catalog.GetPieceById(id);


    public bool ApplyPowerupToSlot(int slotIndex, string powerupId)
    {
        var slots = _player.Data.loadoutSlots;
        if (slots == null || slotIndex < 0 || slotIndex >= slots.Count) return false;
        if (string.IsNullOrEmpty(slots[slotIndex])) return false; // tyhjässä ei ole nappulaa

        var def = _catalog.GetPowerupById(powerupId); // toteutetaan alla
        if (def == null) return false;

        EnsureSlotPowerupListExists(slotIndex);
        var list = _player.Data.slotPowerups[slotIndex];
        if (!list.Contains(powerupId)) list.Add(powerupId);

        _player.Save();
        return true;
    }

    private void EnsureSlotPowerupListExists(int slotIndex)
    {
        if (_player.Data.slotPowerups == null) _player.Data.slotPowerups = new List<List<string>>();
        while (_player.Data.slotPowerups.Count <= slotIndex) _player.Data.slotPowerups.Add(new List<string>());
    }



}
