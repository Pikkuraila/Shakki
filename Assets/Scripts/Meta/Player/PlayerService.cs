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

        // turvallisuus vanhoille saveille
        if (Data.injuredPieces == null)
            Data.injuredPieces = new List<InjuredPieceStack>();

        if (Data.version < 4)
        {
            // vanhan datan migraatio nykyiseen
            Data.ownedPieceIds = new List<string> { "King", "Pawn", "Rook" };
            Data.loadout = new List<LoadoutEntry> {
                new(){ pieceId="King",  count=1 },
                new(){ pieceId="Pawn",  count=2 },
                new(){ pieceId="Rook",  count=1 },
            };
            Data.loadoutSlots = null;

            if (Data.injuredPieces == null)
                Data.injuredPieces = new List<InjuredPieceStack>();

            Data.version = 4;
            Save();
        }

        EnsurePieceInstanceModel();
    }

    private PlayerData CreateDefault()
    {
        var data = new PlayerData
        {
            version = PlayerInstanceSync.CurrentDataVersion,
            coins = 0,
            ownedPieceIds = new List<string> { "King", "Pawn", "Rook" },
            loadout = new List<LoadoutEntry> {
                new(){ pieceId="King",  count=1 },
                new(){ pieceId="Pawn",  count=2 },
                new(){ pieceId="Rook",  count=1 },
            },
            injuredPieces = new List<InjuredPieceStack>(),
            upgrades = new List<UpgradeInstance>(),
            powerups = new List<PowerupStack>(),
            lastRunSeed = null,
            runsCompleted = 0
        };

        PlayerInstanceSync.SyncInstancesFromLegacy(data);
        return data;
    }

    public void Save() => SaveFromLegacyData();

    public void Wipe()
    {
        _store.Wipe();
        Data = CreateDefault();
        OnChanged?.Invoke();
    }

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

    // --- Narrative / NPC state ---
    public int GetAlignment(string npcId, int defaultValue = 0)
    {
        if (string.IsNullOrEmpty(npcId)) return defaultValue;
        var s = Data.npcStates?.Find(x => x != null && x.npcId == npcId);
        return s != null ? s.alignment : defaultValue;
    }

    public void AddAlignment(string npcId, int delta, int min = -100, int max = 100)
    {
        if (string.IsNullOrEmpty(npcId) || delta == 0) return;

        if (Data.npcStates == null) Data.npcStates = new List<NpcState>();

        var s = Data.npcStates.Find(x => x != null && x.npcId == npcId);
        if (s == null)
        {
            s = new NpcState { npcId = npcId, alignment = 0, timesMet = 0 };
            Data.npcStates.Add(s);
        }

        s.alignment = Mathf.Clamp(s.alignment + delta, min, max);

        OnChanged?.Invoke();
        Save();
    }

    public void IncrementTimesMet(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return;

        if (Data.npcStates == null) Data.npcStates = new List<NpcState>();

        var s = Data.npcStates.Find(x => x != null && x.npcId == npcId);
        if (s == null)
        {
            s = new NpcState { npcId = npcId, alignment = 0, timesMet = 0 };
            Data.npcStates.Add(s);
        }

        s.timesMet++;

        OnChanged?.Invoke();
        Save();
    }

    public bool HasFlag(string flagId)
    {
        if (string.IsNullOrEmpty(flagId)) return false;
        return Data.storyFlags != null && Data.storyFlags.Contains(flagId);
    }

    public void SetFlag(string flagId, bool value = true)
    {
        if (string.IsNullOrEmpty(flagId)) return;
        if (Data.storyFlags == null) Data.storyFlags = new List<string>();

        if (value)
        {
            if (!Data.storyFlags.Contains(flagId))
                Data.storyFlags.Add(flagId);
        }
        else
        {
            Data.storyFlags.Remove(flagId);
        }

        OnChanged?.Invoke();
        Save();
    }

    public bool RemovePiecePermanently(string pieceId, int amount = 1)
    {
        if (string.IsNullOrEmpty(pieceId) || amount <= 0) return false;

        bool changed = false;
        int remainingToRemove = amount;

        // 1) Poista terveistä loadout-kappaleista ensin
        if (Data.loadout != null)
        {
            for (int i = Data.loadout.Count - 1; i >= 0 && remainingToRemove > 0; i--)
            {
                var e = Data.loadout[i];
                if (e == null || e.pieceId != pieceId) continue;

                int take = Mathf.Min(Mathf.Max(0, e.count), remainingToRemove);
                if (take <= 0) continue;

                e.count -= take;
                remainingToRemove -= take;
                changed = true;

                if (e.count <= 0)
                    Data.loadout.RemoveAt(i);
            }
        }

        // 2) Jos piti poistaa enemmän kuin terveitä oli,
        // vähennä myös injured-listasta
        if (remainingToRemove > 0 && Data.injuredPieces != null)
        {
            for (int i = Data.injuredPieces.Count - 1; i >= 0 && remainingToRemove > 0; i--)
            {
                var e = Data.injuredPieces[i];
                if (e == null || e.pieceId != pieceId) continue;

                int take = Mathf.Min(Mathf.Max(0, e.count), remainingToRemove);
                if (take <= 0) continue;

                e.count -= take;
                remainingToRemove -= take;
                changed = true;

                if (e.count <= 0)
                    Data.injuredPieces.RemoveAt(i);
            }
        }

        // 3) Siivoa mahdolliset negatiiviset / tyhjät loadout-slotit myöhemmin rebuildillä
        if (changed)
        {
            Data.loadoutSlots = null;
            Debug.Log($"[Permadeath] Removed permanently: {pieceId} x{amount - remainingToRemove}");
            OnChanged?.Invoke();
            Save();
        }

        return changed;
    }

    public void SetLastMacroEvent(string eventId)
    {
        Data.lastMacroEvent = eventId;
        OnChanged?.Invoke();
        Save();
    }

    public IReadOnlyList<PieceInstanceData> GetPieceInstances(bool includeDead = false)
    {
        EnsurePieceInstanceModel();
        if (Data?.pieceInstances == null)
            return Array.Empty<PieceInstanceData>();

        return includeDead
            ? Data.pieceInstances.Where(x => x != null).ToList()
            : Data.pieceInstances.Where(x => x != null && !x.isDead).ToList();
    }

    public IReadOnlyList<LoadoutSlotInstanceData> GetLoadoutSlotInstances()
    {
        EnsurePieceInstanceModel();
        if (Data?.loadoutSlotInstances == null)
            return Array.Empty<LoadoutSlotInstanceData>();

        return Data.loadoutSlotInstances
            .Where(x => x != null)
            .OrderBy(x => x.slotIndex)
            .ToList();
    }

    public PieceInstanceData GetPieceInstance(string instanceId, bool includeDead = false)
    {
        if (string.IsNullOrEmpty(instanceId))
            return null;

        if (Data?.pieceInstances == null)
            return null;

        return Data.pieceInstances.Find(x =>
            x != null &&
            x.instanceId == instanceId &&
            (includeDead || !x.isDead));
    }

    public PieceInstanceData GetLoadoutInstanceAtSlot(int slotIndex)
    {
        EnsurePieceInstanceModel();
        var slot = Data?.loadoutSlotInstances?.Find(x => x != null && x.slotIndex == slotIndex);
        return slot != null ? PlayerInstanceSync.FindAliveInstance(Data, slot.pieceInstanceId) : null;
    }

    public bool SwapLoadoutInstances(int slotIndexA, int slotIndexB)
    {
        EnsurePieceInstanceModel();
        var slotA = GetOrCreateLoadoutSlot(slotIndexA);
        var slotB = GetOrCreateLoadoutSlot(slotIndexB);
        if (slotA == null || slotB == null || slotIndexA == slotIndexB)
            return false;

        (slotA.pieceInstanceId, slotB.pieceInstanceId) = (slotB.pieceInstanceId, slotA.pieceInstanceId);
        SaveFromInstanceModel();
        OnChanged?.Invoke();
        return true;
    }

    public bool ClearLoadoutSlot(int slotIndex)
    {
        EnsurePieceInstanceModel();
        var slot = GetOrCreateLoadoutSlot(slotIndex);
        if (slot == null || string.IsNullOrEmpty(slot.pieceInstanceId))
            return false;

        slot.pieceInstanceId = string.Empty;
        SaveFromInstanceModel();
        OnChanged?.Invoke();
        return true;
    }

    public bool TryAssignNewPieceToLoadoutSlot(int slotIndex, string legacyPieceId)
    {
        if (string.IsNullOrEmpty(legacyPieceId))
            return false;

        EnsurePieceInstanceModel();
        var slot = GetOrCreateLoadoutSlot(slotIndex);
        if (slot == null || !string.IsNullOrEmpty(slot.pieceInstanceId))
            return false;

        var instance = PlayerInstanceSync.CreatePieceInstance(Data, legacyPieceId);
        if (instance == null)
            return false;

        slot = GetOrCreateLoadoutSlot(slotIndex);
        if (slot == null || !string.IsNullOrEmpty(slot.pieceInstanceId))
            return false;

        slot.pieceInstanceId = instance.instanceId;
        SaveFromInstanceModel();
        OnChanged?.Invoke();
        return true;
    }

    public PieceInstanceData TakeLoadoutPieceInstanceAtSlot(int slotIndex)
    {
        EnsurePieceInstanceModel();
        var slot = GetOrCreateLoadoutSlot(slotIndex);
        if (slot == null || string.IsNullOrEmpty(slot.pieceInstanceId))
            return null;

        var instanceId = slot.pieceInstanceId;
        var instance = PlayerInstanceSync.FindAliveInstance(Data, instanceId);
        slot = GetOrCreateLoadoutSlot(slotIndex);
        if (slot == null || slot.pieceInstanceId != instanceId)
            return null;

        slot.pieceInstanceId = string.Empty;
        SaveFromInstanceModel();
        OnChanged?.Invoke();
        return instance;
    }

    public bool HasPersistentStatus(string instanceId, string statusId)
    {
        var instance = GetPieceInstance(instanceId);
        return PlayerInstanceSync.HasStatus(instance, statusId);
    }

    public bool MarkInstanceInjured(string instanceId)
    {
        var instance = GetPieceInstance(instanceId);
        if (instance == null)
            return false;

        PlayerInstanceSync.SetStatus(instance, PlayerInstanceSync.WoundedStatusId, true);
        SaveFromInstanceModel();
        OnChanged?.Invoke();
        return true;
    }

    public bool HealInstance(string instanceId)
    {
        var instance = GetPieceInstance(instanceId);
        if (instance == null)
            return false;

        PlayerInstanceSync.SetStatus(instance, PlayerInstanceSync.WoundedStatusId, false);
        SaveFromInstanceModel();
        OnChanged?.Invoke();
        return true;
    }

    public bool RemovePieceInstancePermanently(string instanceId)
    {
        var instance = GetPieceInstance(instanceId);
        if (instance == null)
            return false;

        instance.isDead = true;
        if (Data?.loadoutSlotInstances != null)
        {
            foreach (var slot in Data.loadoutSlotInstances)
            {
                if (slot != null && slot.pieceInstanceId == instanceId)
                    slot.pieceInstanceId = string.Empty;
            }
        }

        SaveFromInstanceModel();
        OnChanged?.Invoke();
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

    // --- Injuries ---
    private void EnsureInjuryList()
    {
        if (Data.injuredPieces == null)
            Data.injuredPieces = new List<InjuredPieceStack>();
    }

    private int GetTotalLoadoutCount(string pieceId)
    {
        if (string.IsNullOrEmpty(pieceId) || Data.loadout == null) return 0;

        int total = 0;
        for (int i = 0; i < Data.loadout.Count; i++)
        {
            var e = Data.loadout[i];
            if (e != null && e.pieceId == pieceId)
                total += Mathf.Max(0, e.count);
        }
        return total;
    }

    public int GetInjuredCount(string pieceId)
    {
        EnsureInjuryList();
        if (string.IsNullOrEmpty(pieceId)) return 0;

        var s = Data.injuredPieces.Find(x => x != null && x.pieceId == pieceId);
        return s != null ? Mathf.Max(0, s.count) : 0;
    }

    public bool IsPieceInjured(string pieceId) => GetInjuredCount(pieceId) > 0;

    public int GetHealthyCount(string pieceId)
    {
        int total = GetTotalLoadoutCount(pieceId);
        int injured = GetInjuredCount(pieceId);
        return Mathf.Max(0, total - injured);
    }

    public IReadOnlyList<LoadoutEntry> GetHealthyLoadout()
    {
        var result = new List<LoadoutEntry>();
        if (Data.loadout == null) return result;

        foreach (var e in Data.loadout)
        {
            if (e == null || string.IsNullOrEmpty(e.pieceId)) continue;

            int healthy = GetHealthyCount(e.pieceId);
            if (healthy > 0 && !result.Any(x => x.pieceId == e.pieceId))
            {
                result.Add(new LoadoutEntry
                {
                    pieceId = e.pieceId,
                    count = healthy
                });
            }
        }

        return result;
    }

    public void MarkPieceInjured(string pieceId, int amount = 1)
    {
        if (string.IsNullOrEmpty(pieceId) || amount <= 0) return;

        EnsureInjuryList();

        int totalOwnedInLoadout = GetTotalLoadoutCount(pieceId);
        if (totalOwnedInLoadout <= 0) return;

        var s = Data.injuredPieces.Find(x => x != null && x.pieceId == pieceId);
        if (s == null)
        {
            s = new InjuredPieceStack { pieceId = pieceId, count = 0 };
            Data.injuredPieces.Add(s);
        }

        s.count = Mathf.Clamp(s.count + amount, 0, totalOwnedInLoadout);

        Debug.Log($"[Injury] Marked injured: {pieceId} +{amount} => injured={s.count}/{totalOwnedInLoadout}");

        OnChanged?.Invoke();
        Save();
    }

    public void HealPiece(string pieceId, int amount = 1)
    {
        if (string.IsNullOrEmpty(pieceId) || amount <= 0) return;

        EnsureInjuryList();

        var s = Data.injuredPieces.Find(x => x != null && x.pieceId == pieceId);
        if (s == null) return;

        s.count = Mathf.Max(0, s.count - amount);
        if (s.count == 0)
            Data.injuredPieces.Remove(s);

        Debug.Log($"[Injury] Healed: {pieceId} -{amount} => injured={GetInjuredCount(pieceId)}");

        OnChanged?.Invoke();
        Save();
    }

    public void HealAllPieces()
    {
        EnsureInjuryList();
        Data.injuredPieces.Clear();
        OnChanged?.Invoke();
        Save();
    }

    /// <summary>
    /// Tallenna UI:ssa muokattu TERVE loadout takaisin niin,
    /// että olemassa olevat haavoittuneet säilyvät erillään.
    /// total = healthy + injured
    /// </summary>
    public void SetHealthyLoadout(IEnumerable<LoadoutEntry> healthyEntries)
    {
        EnsureInjuryList();

        var healthy = healthyEntries?
            .Where(x => x != null && !string.IsNullOrEmpty(x.pieceId) && x.count > 0)
            .GroupBy(x => x.pieceId)
            .Select(g => new LoadoutEntry
            {
                pieceId = g.Key,
                count = g.Sum(x => Mathf.Max(0, x.count))
            })
            .ToList()
            ?? new List<LoadoutEntry>();

        var injuredById = Data.injuredPieces
            .Where(x => x != null && !string.IsNullOrEmpty(x.pieceId) && x.count > 0)
            .GroupBy(x => x.pieceId)
            .ToDictionary(g => g.Key, g => g.Sum(x => Mathf.Max(0, x.count)));

        var allPieceIds = new HashSet<string>(healthy.Select(x => x.pieceId));
        foreach (var kv in injuredById)
            allPieceIds.Add(kv.Key);

        var merged = new List<LoadoutEntry>();
        foreach (var pieceId in allPieceIds)
        {
            int healthyCount = healthy.Where(x => x.pieceId == pieceId).Sum(x => x.count);
            int injuredCount = injuredById.TryGetValue(pieceId, out var v) ? v : 0;
            int total = healthyCount + injuredCount;

            if (total > 0)
            {
                merged.Add(new LoadoutEntry
                {
                    pieceId = pieceId,
                    count = total
                });
            }
        }

        Data.loadout = merged;
        OnChanged?.Invoke();
        Save();
    }

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
        var k = def.costCurve.Evaluate(nextLevel);
        return Mathf.Max(1, Mathf.RoundToInt(def.baseCost * k));
    }

    public void SetLoadout(IEnumerable<LoadoutEntry> entries)
    {
        Data.loadout = entries?.ToList() ?? new List<LoadoutEntry>();
        OnChanged?.Invoke(); Save();
    }

    public IReadOnlyList<LoadoutEntry> GetLoadout() => Data.loadout;

    public bool HasAuthoritativeInstanceModel(int totalSlots = PlayerInstanceSync.DefaultLoadoutSlotCount)
    {
        return Data != null &&
               Data.version >= PlayerInstanceSync.CurrentDataVersion &&
               Data.pieceInstances != null &&
               Data.pieceInstances.Count > 0 &&
               Data.loadoutSlotInstances != null &&
               Data.loadoutSlotInstances.Count == totalSlots;
    }

    public IReadOnlyList<string> GetLoadoutSlotPieceIds(int totalSlots = PlayerInstanceSync.DefaultLoadoutSlotCount, string implicitKingId = "")
    {
        if (Data == null)
            return Array.Empty<string>();

        if (HasAuthoritativeInstanceModel(totalSlots))
        {
            PlayerInstanceSync.EnsureInitialized(Data, totalSlots);

            return Enumerable.Range(0, totalSlots)
                .Select(slotIndex =>
                {
                    var slot = Data.loadoutSlotInstances.Find(x => x != null && x.slotIndex == slotIndex);
                    var instance = slot != null ? PlayerInstanceSync.FindAliveInstance(Data, slot.pieceInstanceId) : null;
                    return instance != null ? PlayerInstanceSync.GetLegacyPieceId(instance) : string.Empty;
                })
                .ToList();
        }

        if (Data.loadoutSlots != null && Data.loadoutSlots.Count == totalSlots)
            return Data.loadoutSlots.Select(x => x ?? string.Empty).ToList();

        return LoadoutModel.Expand(Data.loadout ?? new List<LoadoutEntry>(), totalSlots, implicitKingId ?? string.Empty);
    }

    public string GetLoadoutPieceIdAtSlot(int slotIndex, int totalSlots = PlayerInstanceSync.DefaultLoadoutSlotCount, string implicitKingId = "")
    {
        var slots = GetLoadoutSlotPieceIds(totalSlots, implicitKingId);
        if (slotIndex < 0 || slotIndex >= slots.Count)
            return string.Empty;

        return slots[slotIndex] ?? string.Empty;
    }

    public int CountLoadoutCopies(string pieceId, int totalSlots = PlayerInstanceSync.DefaultLoadoutSlotCount)
    {
        if (string.IsNullOrEmpty(pieceId))
            return 0;

        return GetLoadoutSlotPieceIds(totalSlots).Count(x => string.Equals(x, pieceId, StringComparison.Ordinal));
    }

    public void PersistCurrentLoadoutState()
    {
        if (HasAuthoritativeInstanceModel(Data?.loadoutSlotInstances?.Count ?? PlayerInstanceSync.DefaultLoadoutSlotCount))
            SaveFromInstanceModel();
        else
            SaveFromLegacyData();
    }

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
        Data.coins = 0;

        if (Data.inventoryIds != null)
            Data.inventoryIds.Clear();

        if (Data.slotPowerups != null)
            Data.slotPowerups.Clear();

        Data.lastRunSeed = null;

        OnChanged?.Invoke();

        bool hasInstanceModel =
            Data.version >= PlayerInstanceSync.CurrentDataVersion &&
            Data.pieceInstances != null &&
            Data.pieceInstances.Count > 0 &&
            Data.loadoutSlotInstances != null &&
            Data.loadoutSlotInstances.Count > 0;

        if (hasInstanceModel)
            SaveFromInstanceModel();
        else
            Save();
    }

    private void EnsurePieceInstanceModel()
    {
        PlayerInstanceSync.EnsureInitialized(Data);

        bool needsMigration =
            Data.version < PlayerInstanceSync.CurrentDataVersion ||
            Data.pieceInstances == null ||
            Data.pieceInstances.Count == 0 ||
            Data.loadoutSlotInstances == null ||
            Data.loadoutSlotInstances.Count == 0;

        if (needsMigration)
        {
            PlayerInstanceSync.SyncInstancesFromLegacy(Data);
            Data.version = PlayerInstanceSync.CurrentDataVersion;
            _store.Save(Data);
            return;
        }

        PlayerInstanceSync.SyncLegacyFromInstances(Data);
    }

    private LoadoutSlotInstanceData GetOrCreateLoadoutSlot(int slotIndex)
    {
        if (slotIndex < 0)
            return null;

        PlayerInstanceSync.EnsureInitialized(Data);

        var slot = Data?.loadoutSlotInstances?.Find(x => x != null && x.slotIndex == slotIndex);
        if (slot != null)
            return slot;

        if (Data?.loadoutSlotInstances == null)
            return null;

        slot = new LoadoutSlotInstanceData
        {
            slotIndex = slotIndex,
            pieceInstanceId = string.Empty
        };

        Data.loadoutSlotInstances.Add(slot);
        Data.loadoutSlotInstances = Data.loadoutSlotInstances
            .Where(x => x != null)
            .OrderBy(x => x.slotIndex)
            .ToList();
        return slot;
    }

    private void SaveFromLegacyData()
    {
        if (Data != null)
        {
            PlayerInstanceSync.SyncInstancesFromLegacy(Data);
            Data.version = Math.Max(Data.version, PlayerInstanceSync.CurrentDataVersion);
        }

        _store.Save(Data);
    }

    private void SaveFromInstanceModel()
    {
        if (Data != null)
        {
            PlayerInstanceSync.SyncLegacyFromInstances(Data);
            Data.version = Math.Max(Data.version, PlayerInstanceSync.CurrentDataVersion);
        }

        _store.Save(Data);
    }
}
