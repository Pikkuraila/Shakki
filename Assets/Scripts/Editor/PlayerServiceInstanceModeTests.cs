using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PlayerServiceInstanceModeTests
{
    [TearDown]
    public void TearDown()
    {
        ResetPlayerServiceSingleton();
    }

    [Test]
    public void GetHealthyLoadout_UsesInstanceStatusesWhenAuthoritative()
    {
        var data = CreateAuthoritativeData(
            new PieceInstanceData
            {
                instanceId = "piece_000001",
                pieceDefId = "King",
                statuses = new List<PersistentPieceStatusData>()
            },
            new PieceInstanceData
            {
                instanceId = "piece_000002",
                pieceDefId = "Rook",
                statuses = new List<PersistentPieceStatusData>
                {
                    new PersistentPieceStatusData { statusId = PlayerInstanceSync.WoundedStatusId }
                }
            },
            new PieceInstanceData
            {
                instanceId = "piece_000003",
                pieceDefId = "Rook",
                statuses = new List<PersistentPieceStatusData>()
            });

        data.loadoutSlotInstances[0].pieceInstanceId = "piece_000001";
        data.loadoutSlotInstances[1].pieceInstanceId = "piece_000002";
        data.loadoutSlotInstances[2].pieceInstanceId = "piece_000003";
        PlayerInstanceSync.SyncLegacyFromInstances(data, totalSlots: 3);

        var service = CreatePlayerService(data);

        try
        {
            var healthy = service.GetHealthyLoadout().OrderBy(x => x.pieceId).ToList();

            Assert.That(service.GetInjuredCount("Rook"), Is.EqualTo(1));
            Assert.That(healthy.Count, Is.EqualTo(2));
            Assert.That(healthy.Single(x => x.pieceId == "King").count, Is.EqualTo(1));
            Assert.That(healthy.Single(x => x.pieceId == "Rook").count, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(service.gameObject);
        }
    }

    [Test]
    public void MarkPieceInjured_PrefersSlottedInstanceWhenAuthoritative()
    {
        var data = CreateAuthoritativeData(
            new PieceInstanceData
            {
                instanceId = "piece_000010",
                pieceDefId = "Rook",
                statuses = new List<PersistentPieceStatusData>()
            },
            new PieceInstanceData
            {
                instanceId = "piece_000011",
                pieceDefId = "Rook",
                statuses = new List<PersistentPieceStatusData>()
            });

        data.loadoutSlotInstances[0].pieceInstanceId = "piece_000010";
        PlayerInstanceSync.SyncLegacyFromInstances(data, totalSlots: 2);

        var service = CreatePlayerService(data);

        try
        {
            service.MarkPieceInjured("Rook", 1);

            Assert.That(service.GetInjuredCount("Rook"), Is.EqualTo(1));
            Assert.That(service.HasPersistentStatus("piece_000010", PlayerInstanceSync.WoundedStatusId), Is.True);
            Assert.That(service.HasPersistentStatus("piece_000011", PlayerInstanceSync.WoundedStatusId), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(service.gameObject);
        }
    }

    [Test]
    public void LoadoutService_SaveFromSlots_PreservesWoundedInstanceWhenAuthoritative()
    {
        var data = CreateAuthoritativeData(
            new PieceInstanceData
            {
                instanceId = "piece_000020",
                pieceDefId = "King",
                statuses = new List<PersistentPieceStatusData>()
            },
            new PieceInstanceData
            {
                instanceId = "piece_000021",
                pieceDefId = "Rook",
                statuses = new List<PersistentPieceStatusData>
                {
                    new PersistentPieceStatusData { statusId = PlayerInstanceSync.WoundedStatusId }
                }
            });

        data.loadoutSlotInstances[0].pieceInstanceId = "piece_000021";
        data.loadoutSlotInstances[1].pieceInstanceId = "piece_000020";
        PlayerInstanceSync.SyncLegacyFromInstances(data, totalSlots: 2);

        var service = CreatePlayerService(data);

        try
        {
            var loadoutService = new LoadoutService(service, catalog: null);
            loadoutService.SaveFromSlots(new List<string> { "King", "Rook" });

            Assert.That(service.GetLoadoutInstanceAtSlot(0)?.instanceId, Is.EqualTo("piece_000020"));
            Assert.That(service.GetLoadoutInstanceAtSlot(1)?.instanceId, Is.EqualTo("piece_000021"));
            Assert.That(service.HasPersistentStatus("piece_000021", PlayerInstanceSync.WoundedStatusId), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(service.gameObject);
        }
    }

    [Test]
    public void SetLoadoutSlotPieceIds_PrunesLegacyInjuryMirrorToExplicitSlots()
    {
        var data = CreateAuthoritativeData(
            new PieceInstanceData
            {
                instanceId = "piece_000030",
                pieceDefId = "King",
                statuses = new List<PersistentPieceStatusData>()
            },
            new PieceInstanceData
            {
                instanceId = "piece_000031",
                pieceDefId = "Rook",
                statuses = new List<PersistentPieceStatusData>
                {
                    new PersistentPieceStatusData { statusId = PlayerInstanceSync.WoundedStatusId }
                }
            });

        data.loadoutSlotInstances[0].pieceInstanceId = "piece_000030";
        data.loadoutSlotInstances[1].pieceInstanceId = "piece_000031";
        PlayerInstanceSync.SyncLegacyFromInstances(data, totalSlots: 2);

        var service = CreatePlayerService(data);

        try
        {
            service.SetLoadoutSlotPieceIds(new[] { "King", string.Empty }, totalSlots: 2);
            int slotCount = service.Data.loadoutSlotInstances.Count;

            Assert.That(service.GetLoadoutPieceIdAtSlot(0, slotCount), Is.EqualTo("King"));
            Assert.That(service.GetLoadoutPieceIdAtSlot(1, slotCount), Is.EqualTo(string.Empty));
            Assert.That(service.GetInjuredCount("Rook"), Is.EqualTo(0));
            Assert.That(service.GetHealthyLoadout().Single(x => x.pieceId == "King").count, Is.EqualTo(1));
            Assert.That(service.GetHealthyLoadout().Any(x => x.pieceId == "Rook"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(service.gameObject);
        }
    }

    [Test]
    public void ResetRun_ClearsLegacyInjuriesForFreshRun()
    {
        var data = CreateAuthoritativeData(
            new PieceInstanceData
            {
                instanceId = "piece_000040",
                pieceDefId = "King",
                statuses = new List<PersistentPieceStatusData>()
            });

        data.injuredPieces = new List<InjuredPieceStack>
        {
            new InjuredPieceStack { pieceId = "Rook", count = 1 }
        };
        data.lastRunSeed = "123";
        data.coins = 42;

        var service = CreatePlayerService(data);

        try
        {
            service.ResetRun();

            Assert.That(service.Data.coins, Is.EqualTo(0));
            Assert.That(service.Data.lastRunSeed, Is.Null);
            Assert.That(service.Data.injuredPieces, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(service.gameObject);
        }
    }

    private static PlayerData CreateAuthoritativeData(params PieceInstanceData[] instances)
    {
        var totalSlots = Mathf.Max(instances.Length, 1);
        var data = new PlayerData
        {
            version = PlayerInstanceSync.CurrentDataVersion,
            pieceInstances = instances.ToList(),
            loadoutSlotInstances = Enumerable.Range(0, totalSlots)
                .Select(i => new LoadoutSlotInstanceData { slotIndex = i, pieceInstanceId = string.Empty })
                .ToList(),
            loadout = new List<LoadoutEntry>(),
            loadoutSlots = new List<string>(),
            injuredPieces = new List<InjuredPieceStack>()
        };

        PlayerInstanceSync.EnsureInitialized(data, totalSlots);
        return data;
    }

    private static PlayerService CreatePlayerService(PlayerData data)
    {
        ResetPlayerServiceSingleton();

        var go = new GameObject("PlayerServiceInstanceModeTests");
        var service = go.AddComponent<PlayerService>();

        SetPrivateField(service, "_store", new MemoryDataStore(data));
        SetAutoPropertyBackingField(service, "Data", data);
        SetAutoPropertyBackingField(typeof(PlayerService), "Instance", service);

        return service;
    }

    private static void ResetPlayerServiceSingleton()
    {
        var instanceField = typeof(PlayerService).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        var existing = instanceField?.GetValue(null) as PlayerService;
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        instanceField?.SetValue(null, null);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
        field.SetValue(target, value);
    }

    private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
    {
        var field = target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing backing field for '{propertyName}'.");
        field.SetValue(target, value);
    }

    private static void SetAutoPropertyBackingField(System.Type type, string propertyName, object value)
    {
        var field = type.GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing static backing field for '{propertyName}'.");
        field.SetValue(null, value);
    }

    private sealed class MemoryDataStore : IDataStore
    {
        private PlayerData _data;

        public MemoryDataStore(PlayerData data)
        {
            _data = data;
        }

        public bool TryLoad(out PlayerData data)
        {
            data = _data;
            return data != null;
        }

        public void Save(PlayerData data)
        {
            _data = data;
        }

        public void Wipe()
        {
            _data = null;
        }
    }
}
