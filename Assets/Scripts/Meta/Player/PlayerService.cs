// Assets/Scripts/Meta/Player/PlayerService.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public sealed class PlayerService : MonoBehaviour
{
    public static PlayerService Instance { get; private set; }

    [SerializeField] private GameCatalogSO catalog;
    private IDataStore _store;
    public PlayerData Data { get; private set; }

    public event Action OnChanged; // UI voi kuunnella

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _store = new JsonDataStore();
        if (!_store.TryLoad(out var loaded)) loaded = CreateDefault();
        Data = loaded;

        if (Data.version < 3)
        {
            // päivitys haluamaasi “uuteen totuuteen”
            Data.ownedPieceIds = new List<string> { "King", "Pawn", "Rook" };  // pieni starttipooli
                                                                               // jos haluat, päivitä myös meta:
            Data.loadout = new List<LoadoutEntry> {
        new(){ pieceId="King",  count=1 },
        new(){ pieceId="Pawn",  count=2 },
        new(){ pieceId="Rook",  count=1 },
    };
            // pakota slotit rakentumaan Expandista (ei käytetä vanhaa klassista listaa)
            Data.loadoutSlots = null;

            Data.version = 3;
            Save();
        }
    }

    private PlayerData CreateDefault()
    {
        return new PlayerData
        {
            coins = 0,
            // Omistaa vain muutaman perusnappulan alussa:
            ownedPieceIds = new List<string> { "King", "Pawn", "Rook" }, // esimerkki
                                                                         // Ei täyttä pawnlineä:
            loadout = new List<LoadoutEntry> {
            new(){ pieceId="King",  count=1 },
            new(){ pieceId="Pawn",  count=2 },
            new(){ pieceId="Rook",  count=1 },
        },
            upgrades = new List<UpgradeInstance>(),
            powerups = new List<PowerupStack>(),
            lastRunSeed = null,
            runsCompleted = 0
        };
    }

    public void Save() => _store.Save(Data);
    public void Wipe() { _store.Wipe(); Data = CreateDefault(); OnChanged?.Invoke(); }

    // --- Coins ---
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        Data.coins += amount;
        OnChanged?.Invoke(); Save();
    }

    public bool SpendCoins(int amount)
    {
        if (amount <= 0) return true;
        if (Data.coins < amount) return false;
        Data.coins -= amount;
        OnChanged?.Invoke(); Save();
        return true;
    }

    // --- Pieces ---
    public bool GrantPiece(string pieceId)
    {
        if (string.IsNullOrEmpty(pieceId)) return false;
        if (!Data.ownedPieceIds.Contains(pieceId)) Data.ownedPieceIds.Add(pieceId);
        OnChanged?.Invoke(); Save();
        return true;
    }

    public bool HasPiece(string pieceId) => Data.ownedPieceIds.Contains(pieceId);

    // --- Upgrades ---
    public int GetUpgradeLevel(string upgradeId)
    {
        var u = Data.upgrades.Find(x => x.upgradeId == upgradeId);
        return u?.level ?? 0;
    }

    public bool BuyUpgrade(string upgradeId)
    {
        var def = catalog.GetUpgradeById(upgradeId);
        if (def == null) return false;

        int current = GetUpgradeLevel(upgradeId);
        int price = CalcUpgradePrice(def, current + 1);
        if (!SpendCoins(price)) return false;

        var inst = Data.upgrades.Find(x => x.upgradeId == upgradeId);
        if (inst == null) Data.upgrades.Add(new UpgradeInstance { upgradeId = upgradeId, level = 1 });
        else inst.level++;

        OnChanged?.Invoke(); Save();
        return true;
    }

    int CalcUpgradePrice(UpgradeDefSO def, int nextLevel)
    {
        // yksinkertainen: baseCost * curve(nextLevel)
        var k = def.costCurve.Evaluate(nextLevel);
        return Mathf.Max(1, Mathf.RoundToInt(def.baseCost * k));
    }

    public void SetLoadout(IEnumerable<LoadoutEntry> entries)
    {
        Data.loadout = entries?.ToList() ?? new List<LoadoutEntry>();
        OnChanged?.Invoke(); Save();
    }

    public IReadOnlyList<LoadoutEntry> GetLoadout() => Data.loadout;

    public void AddPowerup(string powerupId, int amount)
    {
        if (string.IsNullOrEmpty(powerupId) || amount <= 0) return;
        var s = Data.powerups.Find(x => x.powerupId == powerupId);
        if (s == null) Data.powerups.Add(new PowerupStack { powerupId = powerupId, count = amount });
        else s.count += amount;
        OnChanged?.Invoke(); Save();
    }

    public bool ConsumePowerup(string powerupId, int amount = 1)
    {
        var s = Data.powerups.Find(x => x.powerupId == powerupId);
        if (s == null || s.count < amount) return false;
        s.count -= amount;
        if (s.count <= 0) Data.powerups.Remove(s);
        OnChanged?.Invoke(); Save();
        return true;
    }

    // --- Run reset (coins + tämän runin lauta) ---
    public void ResetRun()
    {
        // 1) Rahat nollaan
        Data.coins = 0;

        // 2) Tyhjennä tämän runin gridit
        if (Data.inventoryIds != null)
            Data.inventoryIds.Clear();

        if (Data.slotPowerups != null)
            Data.slotPowerups.Clear();

        // 3) Pakota loadoutSlots rakentumaan uudestaan metasta
        //    (EnsureSlotsOnce huomaa nullin → Expand loadoutista)
        Data.loadoutSlots = null;

        // 4) Halutessa voidaan myös nollata seed tms.
        Data.lastRunSeed = null;

        OnChanged?.Invoke();
        Save();
    }



}
