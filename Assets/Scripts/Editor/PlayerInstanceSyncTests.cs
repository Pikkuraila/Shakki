using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

public sealed class PlayerInstanceSyncTests
{
    [Test]
    public void SyncInstancesFromLegacy_CreatesWoundedPieceInstancesAndHealthySlots()
    {
        var data = new PlayerData
        {
            loadout = new List<LoadoutEntry>
            {
                new LoadoutEntry { pieceId = "King", count = 1 },
                new LoadoutEntry { pieceId = "Pawn", count = 2 },
                new LoadoutEntry { pieceId = "Rook", count = 1 },
            },
            injuredPieces = new List<InjuredPieceStack>
            {
                new InjuredPieceStack { pieceId = "Pawn", count = 1 }
            }
        };

        PlayerInstanceSync.SyncInstancesFromLegacy(data);

        var alive = data.pieceInstances.Where(x => x != null && !x.isDead).ToList();
        var pawns = alive.Where(x => x.pieceDefId == "Pawn").ToList();

        Assert.That(alive.Count, Is.EqualTo(4));
        Assert.That(pawns.Count, Is.EqualTo(2));
        Assert.That(pawns.Count(x => PlayerInstanceSync.HasStatus(x, PlayerInstanceSync.WoundedStatusId)), Is.EqualTo(1));
        Assert.That(data.loadoutSlotInstances.Count, Is.EqualTo(PlayerInstanceSync.DefaultLoadoutSlotCount));
        Assert.That(data.loadoutSlotInstances.Count(x => !string.IsNullOrEmpty(x.pieceInstanceId)), Is.EqualTo(3));
    }

    [Test]
    public void SyncInstancesFromLegacy_RecognizesLegacyRuntimeAmalgamIds()
    {
        const string runtimeId = "Amalgam_Pawn_Rook_abc123";
        var data = new PlayerData
        {
            loadout = new List<LoadoutEntry>
            {
                new LoadoutEntry { pieceId = runtimeId, count = 1 }
            },
            loadoutSlots = new List<string> { runtimeId }
        };

        PlayerInstanceSync.SyncInstancesFromLegacy(data, totalSlots: 1);

        var instance = data.pieceInstances.Single(x => x != null && !x.isDead);

        Assert.That(instance.kind, Is.EqualTo(PieceInstanceKind.Amalgam));
        Assert.That(instance.pieceDefId, Is.EqualTo("amalgam"));
        Assert.That(instance.amalgam, Is.Not.Null);
        Assert.That(instance.amalgam.runtimePieceDefId, Is.EqualTo(runtimeId));
        Assert.That(instance.amalgam.sourceAPieceDefId, Is.EqualTo("Pawn"));
        Assert.That(instance.amalgam.sourceBPieceDefId, Is.EqualTo("Rook"));
    }

    [Test]
    public void SyncLegacyFromInstances_RebuildsCountsSlotsAndInjuries()
    {
        var king = new PieceInstanceData
        {
            instanceId = "piece_000001",
            pieceDefId = "King",
            statuses = new List<PersistentPieceStatusData>()
        };
        var pawnHealthy = new PieceInstanceData
        {
            instanceId = "piece_000002",
            pieceDefId = "Pawn",
            statuses = new List<PersistentPieceStatusData>()
        };
        var pawnWounded = new PieceInstanceData
        {
            instanceId = "piece_000003",
            pieceDefId = "Pawn",
            statuses = new List<PersistentPieceStatusData>
            {
                new PersistentPieceStatusData { statusId = PlayerInstanceSync.WoundedStatusId }
            }
        };

        var data = new PlayerData
        {
            version = PlayerInstanceSync.CurrentDataVersion,
            pieceInstances = new List<PieceInstanceData> { king, pawnHealthy, pawnWounded },
            loadoutSlotInstances = new List<LoadoutSlotInstanceData>
            {
                new LoadoutSlotInstanceData { slotIndex = 0, pieceInstanceId = king.instanceId },
                new LoadoutSlotInstanceData { slotIndex = 1, pieceInstanceId = pawnHealthy.instanceId },
                new LoadoutSlotInstanceData { slotIndex = 2, pieceInstanceId = string.Empty },
                new LoadoutSlotInstanceData { slotIndex = 3, pieceInstanceId = string.Empty },
            }
        };

        PlayerInstanceSync.SyncLegacyFromInstances(data, totalSlots: 4);

        Assert.That(data.loadout.Single(x => x.pieceId == "King").count, Is.EqualTo(1));
        Assert.That(data.loadout.Single(x => x.pieceId == "Pawn").count, Is.EqualTo(2));
        Assert.That(data.injuredPieces.Single(x => x.pieceId == "Pawn").count, Is.EqualTo(1));
        Assert.That(data.loadoutSlots[0], Is.EqualTo("King"));
        Assert.That(data.loadoutSlots[1], Is.EqualTo("Pawn"));
        Assert.That(data.loadoutSlots[2], Is.EqualTo(string.Empty));
    }

    [Test]
    public void SyncLegacyFromInstances_PreservesWoundedInstancesInActiveSlots()
    {
        var woundedRook = new PieceInstanceData
        {
            instanceId = "piece_000010",
            pieceDefId = "Rook",
            statuses = new List<PersistentPieceStatusData>
            {
                new PersistentPieceStatusData { statusId = PlayerInstanceSync.WoundedStatusId }
            }
        };

        var data = new PlayerData
        {
            version = PlayerInstanceSync.CurrentDataVersion,
            pieceInstances = new List<PieceInstanceData> { woundedRook },
            loadoutSlotInstances = new List<LoadoutSlotInstanceData>
            {
                new LoadoutSlotInstanceData { slotIndex = 0, pieceInstanceId = woundedRook.instanceId }
            }
        };

        PlayerInstanceSync.SyncLegacyFromInstances(data, totalSlots: 1);

        Assert.That(data.loadoutSlots[0], Is.EqualTo("Rook"));
        Assert.That(data.loadoutSlotInstances[0].pieceInstanceId, Is.EqualTo(woundedRook.instanceId));
        Assert.That(data.loadout.Single(x => x.pieceId == "Rook").count, Is.EqualTo(1));
        Assert.That(data.injuredPieces.Single(x => x.pieceId == "Rook").count, Is.EqualTo(1));
    }

    [Test]
    public void EnsureInitialized_PreservesOrderedSlotInstanceObjects()
    {
        var slot0 = new LoadoutSlotInstanceData { slotIndex = 0, pieceInstanceId = "piece_000001" };
        var slot1 = new LoadoutSlotInstanceData { slotIndex = 1, pieceInstanceId = string.Empty };
        var data = new PlayerData
        {
            loadoutSlotInstances = new List<LoadoutSlotInstanceData> { slot0, slot1 },
            pieceInstances = new List<PieceInstanceData>()
        };

        PlayerInstanceSync.EnsureInitialized(data, totalSlots: 2);

        Assert.That(ReferenceEquals(data.loadoutSlotInstances[0], slot0), Is.True);
        Assert.That(ReferenceEquals(data.loadoutSlotInstances[1], slot1), Is.True);
        Assert.That(data.loadoutSlotInstances[0].pieceInstanceId, Is.EqualTo("piece_000001"));
        Assert.That(data.loadoutSlotInstances[1].pieceInstanceId, Is.EqualTo(string.Empty));
    }

    [Test]
    public void SyncInstancesFromLegacy_DoesNotDuplicateWoundedSlottedPieceTypes()
    {
        var rook = new PieceInstanceData
        {
            instanceId = "piece_000026",
            pieceDefId = "Rook",
            statuses = new List<PersistentPieceStatusData>()
        };

        var data = new PlayerData
        {
            version = PlayerInstanceSync.CurrentDataVersion,
            loadout = new List<LoadoutEntry>
            {
                new LoadoutEntry { pieceId = "Rook", count = 1 }
            },
            injuredPieces = new List<InjuredPieceStack>
            {
                new InjuredPieceStack { pieceId = "Rook", count = 1 }
            },
            loadoutSlots = new List<string> { "Rook" },
            pieceInstances = new List<PieceInstanceData> { rook },
            loadoutSlotInstances = new List<LoadoutSlotInstanceData>
            {
                new LoadoutSlotInstanceData { slotIndex = 0, pieceInstanceId = rook.instanceId }
            }
        };

        PlayerInstanceSync.SyncInstancesFromLegacy(data, totalSlots: 1);

        var aliveRooks = data.pieceInstances
            .Where(x => x != null && !x.isDead && x.pieceDefId == "Rook")
            .ToList();

        Assert.That(aliveRooks.Count, Is.EqualTo(1));
        Assert.That(aliveRooks[0].instanceId, Is.EqualTo("piece_000026"));
        Assert.That(PlayerInstanceSync.HasStatus(aliveRooks[0], PlayerInstanceSync.WoundedStatusId), Is.True);
        Assert.That(data.loadoutSlotInstances[0].pieceInstanceId, Is.EqualTo("piece_000026"));
    }

    [Test]
    public void SyncInstancesFromLegacy_PreservesWoundedRuntimeAmalgamInActiveSlot()
    {
        const string runtimeId = "Amalgam_Pawn_Rook_resume";

        var data = new PlayerData
        {
            version = PlayerInstanceSync.CurrentDataVersion,
            loadout = new List<LoadoutEntry>
            {
                new LoadoutEntry { pieceId = runtimeId, count = 1 }
            },
            injuredPieces = new List<InjuredPieceStack>
            {
                new InjuredPieceStack { pieceId = runtimeId, count = 1 }
            },
            loadoutSlots = new List<string> { runtimeId }
        };

        PlayerInstanceSync.SyncInstancesFromLegacy(data, totalSlots: 1);

        var alive = data.pieceInstances
            .Where(x => x != null && !x.isDead)
            .ToList();

        Assert.That(alive.Count, Is.EqualTo(1));
        Assert.That(alive[0].kind, Is.EqualTo(PieceInstanceKind.Amalgam));
        Assert.That(alive[0].pieceDefId, Is.EqualTo("amalgam"));
        Assert.That(alive[0].amalgam, Is.Not.Null);
        Assert.That(alive[0].amalgam.runtimePieceDefId, Is.EqualTo(runtimeId));
        Assert.That(PlayerInstanceSync.HasStatus(alive[0], PlayerInstanceSync.WoundedStatusId), Is.True);
        Assert.That(data.loadoutSlotInstances[0].pieceInstanceId, Is.EqualTo(alive[0].instanceId));
    }
}
