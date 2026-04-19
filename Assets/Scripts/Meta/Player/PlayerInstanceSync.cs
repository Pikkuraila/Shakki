using System;
using System.Collections.Generic;
using System.Linq;

public static class PlayerInstanceSync
{
    public const int CurrentDataVersion = 5;
    public const int DefaultLoadoutSlotCount = 16;
    public const string WoundedStatusId = "wounded";

    private const string DefaultAmalgamBaseDefId = "amalgam";
    private const string LegacyAmalgamPrefix = "Amalgam_";

    public static void EnsureInitialized(PlayerData data, int totalSlots = DefaultLoadoutSlotCount)
    {
        if (data == null)
            return;

        if (data.loadout == null)
            data.loadout = new List<LoadoutEntry>();
        if (data.injuredPieces == null)
            data.injuredPieces = new List<InjuredPieceStack>();
        if (data.pieceInstances == null)
            data.pieceInstances = new List<PieceInstanceData>();
        if (data.loadoutSlotInstances == null)
            data.loadoutSlotInstances = new List<LoadoutSlotInstanceData>();
        if (data.nextPieceInstanceNumber < 1)
            data.nextPieceInstanceNumber = 1;

        NormalizeSlotEntries(data, totalSlots);
    }

    public static void SyncInstancesFromLegacy(PlayerData data, int totalSlots = DefaultLoadoutSlotCount)
    {
        EnsureInitialized(data, totalSlots);

        var legacySlots = NormalizeLegacySlots(data, totalSlots);
        data.loadoutSlots = legacySlots.ToList();
        var desiredTotals = BuildDesiredTotalsByPieceId(data, legacySlots);
        var desiredInjured = BuildInjuredCountsByPieceId(data);
        var assignedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var slot in data.loadoutSlotInstances)
        {
            string desiredPieceId = legacySlots[slot.slotIndex];
            if (string.IsNullOrEmpty(desiredPieceId))
            {
                slot.pieceInstanceId = string.Empty;
                continue;
            }

            var current = FindAliveInstance(data, slot.pieceInstanceId);
            if (current != null &&
                string.Equals(GetLegacyPieceId(current), desiredPieceId, StringComparison.Ordinal) &&
                assignedIds.Add(current.instanceId))
            {
                continue;
            }

            var chosen = FindReusableInstance(data, desiredPieceId, assignedIds);
            if (chosen == null)
                chosen = CreateInstanceForLegacyPieceId(data, desiredPieceId);

            slot.pieceInstanceId = chosen.instanceId;
            assignedIds.Add(chosen.instanceId);
        }

        var activePieceIds = data.pieceInstances
            .Where(x => x != null && !x.isDead)
            .Select(GetLegacyPieceId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct()
            .ToList();

        foreach (var pieceId in activePieceIds)
            if (!desiredTotals.ContainsKey(pieceId))
                desiredTotals[pieceId] = 0;

        foreach (var kv in desiredTotals)
            ReconcilePieceCount(data, kv.Key, kv.Value, assignedIds);

        ApplyInjuries(data, desiredInjured, assignedIds);
        data.version = Math.Max(data.version, CurrentDataVersion);
    }

    public static void SyncLegacyFromInstances(PlayerData data, int totalSlots = DefaultLoadoutSlotCount)
    {
        EnsureInitialized(data, totalSlots);

        var loadoutSlots = Enumerable.Repeat(string.Empty, totalSlots).ToList();
        var loadoutCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var injuredCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var instance in data.pieceInstances)
        {
            if (instance == null || instance.isDead)
                continue;

            string pieceId = GetLegacyPieceId(instance);
            if (string.IsNullOrEmpty(pieceId))
                continue;

            IncrementCount(loadoutCounts, pieceId, 1);
            if (HasStatus(instance, WoundedStatusId))
                IncrementCount(injuredCounts, pieceId, 1);
        }

        foreach (var slot in data.loadoutSlotInstances)
        {
            if (slot == null || slot.slotIndex < 0 || slot.slotIndex >= loadoutSlots.Count)
                continue;

            var instance = FindAliveInstance(data, slot.pieceInstanceId);
            loadoutSlots[slot.slotIndex] = instance != null ? GetLegacyPieceId(instance) : string.Empty;
        }

        data.loadoutSlots = loadoutSlots;
        data.loadout = loadoutCounts
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new LoadoutEntry { pieceId = kv.Key, count = kv.Value })
            .ToList();
        data.injuredPieces = injuredCounts
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new InjuredPieceStack { pieceId = kv.Key, count = kv.Value })
            .ToList();
        data.version = Math.Max(data.version, CurrentDataVersion);
    }

    public static PieceInstanceData FindAliveInstance(PlayerData data, string instanceId)
    {
        if (data?.pieceInstances == null || string.IsNullOrEmpty(instanceId))
            return null;

        return data.pieceInstances.Find(x => x != null && !x.isDead && x.instanceId == instanceId);
    }

    public static string GetLegacyPieceId(PieceInstanceData instance)
    {
        if (instance == null)
            return string.Empty;

        if (instance.kind == PieceInstanceKind.Amalgam && !string.IsNullOrEmpty(instance.amalgam?.runtimePieceDefId))
            return instance.amalgam.runtimePieceDefId;

        return instance.pieceDefId ?? string.Empty;
    }

    public static bool HasStatus(PieceInstanceData instance, string statusId)
    {
        if (instance?.statuses == null || string.IsNullOrEmpty(statusId))
            return false;

        return instance.statuses.Any(x =>
            x != null &&
            string.Equals(x.statusId, statusId, StringComparison.OrdinalIgnoreCase) &&
            x.stacks > 0);
    }

    public static void SetStatus(PieceInstanceData instance, string statusId, bool enabled, int stacks = 1, int duration = -1)
    {
        if (instance == null || string.IsNullOrEmpty(statusId))
            return;

        instance.statuses ??= new List<PersistentPieceStatusData>();
        var existing = instance.statuses.Find(x => x != null && string.Equals(x.statusId, statusId, StringComparison.OrdinalIgnoreCase));

        if (!enabled)
        {
            if (existing != null)
                instance.statuses.Remove(existing);
            return;
        }

        if (existing == null)
        {
            instance.statuses.Add(new PersistentPieceStatusData
            {
                statusId = statusId,
                stacks = Math.Max(1, stacks),
                duration = duration
            });
            return;
        }

        existing.stacks = Math.Max(1, stacks);
        existing.duration = duration;
    }

    public static PieceInstanceData CreatePieceInstance(PlayerData data, string legacyPieceId)
    {
        if (data == null || string.IsNullOrEmpty(legacyPieceId))
            return null;

        EnsureInitialized(data);
        return CreateInstanceForLegacyPieceId(data, legacyPieceId);
    }

    private static void NormalizeSlotEntries(PlayerData data, int totalSlots)
    {
        bool alreadyNormalized = data.loadoutSlotInstances != null &&
                                 data.loadoutSlotInstances.Count == totalSlots;

        if (alreadyNormalized)
        {
            for (int i = 0; i < totalSlots; i++)
            {
                var slot = data.loadoutSlotInstances[i];
                if (slot == null || slot.slotIndex != i)
                {
                    alreadyNormalized = false;
                    break;
                }
            }
        }

        if (alreadyNormalized)
        {
            foreach (var slot in data.loadoutSlotInstances)
                slot.pieceInstanceId ??= string.Empty;
            return;
        }

        var normalized = Enumerable.Range(0, totalSlots)
            .Select(i => new LoadoutSlotInstanceData { slotIndex = i, pieceInstanceId = string.Empty })
            .ToList();

        if (data.loadoutSlotInstances != null)
        {
            foreach (var slot in data.loadoutSlotInstances)
            {
                if (slot == null || slot.slotIndex < 0 || slot.slotIndex >= totalSlots)
                    continue;

                normalized[slot.slotIndex].pieceInstanceId = slot.pieceInstanceId ?? string.Empty;
            }
        }

        data.loadoutSlotInstances = normalized;
    }

    private static List<string> NormalizeLegacySlots(PlayerData data, int totalSlots)
    {
        if (data.loadoutSlots != null && data.loadoutSlots.Count == totalSlots)
            return data.loadoutSlots.Select(x => x ?? string.Empty).ToList();

        var healthyEntries = BuildHealthyEntries(data);
        return LoadoutModel.Expand(healthyEntries, totalSlots, "");
    }

    private static List<LoadoutEntry> BuildHealthyEntries(PlayerData data)
    {
        var result = new List<LoadoutEntry>();
        if (data?.loadout == null)
            return result;

        var injuredById = BuildInjuredCountsByPieceId(data);
        foreach (var entry in data.loadout)
        {
            if (entry == null || string.IsNullOrEmpty(entry.pieceId))
                continue;

            int healthyCount = Math.Max(0, entry.count) -
                               (injuredById.TryGetValue(entry.pieceId, out var injured) ? injured : 0);
            if (healthyCount <= 0)
                continue;

            result.Add(new LoadoutEntry
            {
                pieceId = entry.pieceId,
                count = healthyCount
            });
        }

        return result;
    }

    private static Dictionary<string, int> BuildDesiredTotalsByPieceId(PlayerData data, List<string> legacySlots)
    {
        var loadoutCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var healthySlotCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var injuredCounts = BuildInjuredCountsByPieceId(data);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        if (data?.loadout != null)
        {
            foreach (var entry in data.loadout)
            {
                if (entry == null || string.IsNullOrEmpty(entry.pieceId) || entry.count <= 0)
                    continue;

                IncrementCount(loadoutCounts, entry.pieceId, entry.count);
            }
        }

        if (legacySlots != null)
        {
            foreach (var pieceId in legacySlots)
            {
                if (string.IsNullOrEmpty(pieceId))
                    continue;

                IncrementCount(healthySlotCounts, pieceId, 1);
            }
        }

        var allPieceIds = new HashSet<string>(loadoutCounts.Keys, StringComparer.Ordinal);
        allPieceIds.UnionWith(healthySlotCounts.Keys);
        allPieceIds.UnionWith(injuredCounts.Keys);

        foreach (var pieceId in allPieceIds)
        {
            int loadoutTotal = loadoutCounts.TryGetValue(pieceId, out var loadoutCount) ? loadoutCount : 0;
            int healthyTotal = healthySlotCounts.TryGetValue(pieceId, out var healthyCount) ? healthyCount : 0;
            int injuredTotal = injuredCounts.TryGetValue(pieceId, out var injuredCount) ? injuredCount : 0;

            // Legacy loadout already represents the total active roster for the piece type.
            // Slot data is only another view of that same roster, so once loadout has a value
            // we must not add slot occupancy and injuries on top or we invent duplicate pieces.
            counts[pieceId] = loadoutTotal > 0
                ? loadoutTotal
                : healthyTotal + injuredTotal;
        }

        return counts;
    }

    private static Dictionary<string, int> BuildInjuredCountsByPieceId(PlayerData data)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (data?.injuredPieces == null)
            return counts;

        foreach (var injured in data.injuredPieces)
        {
            if (injured == null || string.IsNullOrEmpty(injured.pieceId) || injured.count <= 0)
                continue;

            IncrementCount(counts, injured.pieceId, injured.count);
        }

        return counts;
    }

    private static void ReconcilePieceCount(PlayerData data, string pieceId, int desiredCount, HashSet<string> assignedIds)
    {
        var alive = data.pieceInstances
            .Where(x => x != null && !x.isDead && string.Equals(GetLegacyPieceId(x), pieceId, StringComparison.Ordinal))
            .ToList();

        while (alive.Count < desiredCount)
            alive.Add(CreateInstanceForLegacyPieceId(data, pieceId));

        if (alive.Count <= desiredCount)
            return;

        var removable = alive
            .Where(x => !assignedIds.Contains(x.instanceId))
            .ToList();

        for (int i = removable.Count - 1; i >= 0 && alive.Count > desiredCount; i--)
        {
            removable[i].isDead = true;
            alive.Remove(removable[i]);
        }
    }

    private static void ApplyInjuries(PlayerData data, Dictionary<string, int> desiredInjured, HashSet<string> assignedIds)
    {
        var aliveByPieceId = data.pieceInstances
            .Where(x => x != null && !x.isDead)
            .GroupBy(GetLegacyPieceId)
            .ToList();

        foreach (var group in aliveByPieceId)
        {
            int target = desiredInjured.TryGetValue(group.Key, out var value) ? value : 0;
            var ordered = group
                .OrderBy(x => assignedIds.Contains(x.instanceId) ? 1 : 0)
                .ThenBy(x => x.instanceId, StringComparer.Ordinal)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
                SetStatus(ordered[i], WoundedStatusId, i < target);
        }
    }

    private static PieceInstanceData FindReusableInstance(PlayerData data, string legacyPieceId, HashSet<string> assignedIds)
    {
        return data.pieceInstances.FirstOrDefault(x =>
            x != null &&
            !x.isDead &&
            !assignedIds.Contains(x.instanceId) &&
            string.Equals(GetLegacyPieceId(x), legacyPieceId, StringComparison.Ordinal));
    }

    private static PieceInstanceData CreateInstanceForLegacyPieceId(PlayerData data, string legacyPieceId)
    {
        var instance = new PieceInstanceData
        {
            instanceId = NextInstanceId(data),
            pieceDefId = legacyPieceId,
            statuses = new List<PersistentPieceStatusData>(),
            attachedPowerupIds = new List<string>()
        };

        if (TryBuildAmalgamData(legacyPieceId, out var amalgam))
        {
            instance.kind = PieceInstanceKind.Amalgam;
            instance.pieceDefId = amalgam.baseDefId;
            instance.amalgam = amalgam;
        }
        else
        {
            instance.kind = PieceInstanceKind.Standard;
        }

        data.pieceInstances.Add(instance);
        return instance;
    }

    private static string NextInstanceId(PlayerData data)
    {
        string id = $"piece_{data.nextPieceInstanceNumber:D6}";
        data.nextPieceInstanceNumber++;
        return id;
    }

    private static bool TryBuildAmalgamData(string legacyPieceId, out AmalgamPieceData amalgam)
    {
        amalgam = null;
        if (string.IsNullOrEmpty(legacyPieceId) || !legacyPieceId.StartsWith(LegacyAmalgamPrefix, StringComparison.Ordinal))
            return false;

        string[] parts = legacyPieceId.Split(new[] { '_' }, 4);
        amalgam = new AmalgamPieceData
        {
            baseDefId = DefaultAmalgamBaseDefId,
            runtimePieceDefId = legacyPieceId,
            sourceAPieceDefId = parts.Length > 1 ? parts[1] : string.Empty,
            sourceBPieceDefId = parts.Length > 2 ? parts[2] : string.Empty
        };
        return true;
    }

    private static void IncrementCount(Dictionary<string, int> counts, string key, int amount)
    {
        if (string.IsNullOrEmpty(key) || amount <= 0)
            return;

        if (counts.TryGetValue(key, out var current))
            counts[key] = current + amount;
        else
            counts[key] = amount;
    }
}
