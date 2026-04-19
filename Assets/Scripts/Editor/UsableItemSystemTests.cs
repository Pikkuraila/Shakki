using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Shakki.Core;

public sealed class UsableItemSystemTests
{
    [TearDown]
    public void TearDown()
    {
        ResetPlayerServiceSingleton();
    }

    [Test]
    public void ResetRun_ClearsUsableItemInventory()
    {
        var data = new PlayerData
        {
            version = PlayerInstanceSync.CurrentDataVersion,
            inventoryIds = new List<string> { "IT_StoneHead", string.Empty, "IT_StoneHead" },
            pieceInstances = new List<PieceInstanceData>
            {
                new PieceInstanceData { instanceId = "piece_000001", pieceDefId = "King" }
            },
            loadoutSlotInstances = new List<LoadoutSlotInstanceData>
            {
                new LoadoutSlotInstanceData { slotIndex = 0, pieceInstanceId = "piece_000001" }
            }
        };

        var service = CreatePlayerService(data);

        try
        {
            service.ResetRun();

            Assert.That(service.GetInventoryItemIds(3), Is.All.EqualTo(string.Empty));
        }
        finally
        {
            Object.DestroyImmediate(service.gameObject);
        }
    }

    [Test]
    public void TryAddInventoryItem_UsesPreferredSlotWhenFree()
    {
        var data = new PlayerData
        {
            version = PlayerInstanceSync.CurrentDataVersion,
            inventoryIds = new List<string> { string.Empty, string.Empty, string.Empty },
            pieceInstances = new List<PieceInstanceData>
            {
                new PieceInstanceData { instanceId = "piece_000001", pieceDefId = "King" }
            },
            loadoutSlotInstances = new List<LoadoutSlotInstanceData>
            {
                new LoadoutSlotInstanceData { slotIndex = 0, pieceInstanceId = "piece_000001" }
            }
        };

        var service = CreatePlayerService(data);

        try
        {
            bool added = service.TryAddInventoryItem("IT_StoneHead", preferredSlot: 1, totalSlots: 3);

            Assert.That(added, Is.True);
            Assert.That(service.GetInventoryItemIdAt(1, 3), Is.EqualTo("IT_StoneHead"));
        }
        finally
        {
            Object.DestroyImmediate(service.gameObject);
        }
    }

    [Test]
    public void RayRule_ObstacleBlocksSlidingAndCannotBeCaptured()
    {
        var state = new GameState(new GridGeometry(8, 8));
        var rook = new Piece("white", "Rook", new IMoveRule[] { new RayRule(new[] { (1, 0) }) });
        var obstacle = new Piece("neutral", "StoneHeadObstacle", System.Array.Empty<IMoveRule>(), PieceTag.Obstacle);
        var enemy = new Piece("black", "Pawn", System.Array.Empty<IMoveRule>());

        state.Set(new Coord(0, 0), rook);
        state.Set(new Coord(2, 0), obstacle);
        state.Set(new Coord(3, 0), enemy);

        var moves = state.GenerateLegalMoves(new Coord(0, 0), null).ToList();

        Assert.That(moves.Any(m => m.To.X == 1 && m.To.Y == 0), Is.True);
        Assert.That(moves.Any(m => m.To.X == 2 && m.To.Y == 0), Is.False);
        Assert.That(moves.Any(m => m.To.X == 3 && m.To.Y == 0), Is.False);
    }

    [Test]
    public void KnightJumpRule_CannotLandOnObstacleButStillKeepsOtherJumps()
    {
        var state = new GameState(new GridGeometry(8, 8));
        var knight = new Piece("white", "Knight", new IMoveRule[] { new KnightJumpRule() }, PieceTag.Jumper);
        var obstacle = new Piece("neutral", "StoneHeadObstacle", System.Array.Empty<IMoveRule>(), PieceTag.Obstacle);

        state.Set(new Coord(1, 0), knight);
        state.Set(new Coord(2, 2), obstacle);

        var moves = state.GenerateLegalMoves(new Coord(1, 0), null).ToList();

        Assert.That(moves.Any(m => m.To.X == 2 && m.To.Y == 2), Is.False);
        Assert.That(moves.Any(m => m.To.X == 3 && m.To.Y == 1), Is.True);
        Assert.That(moves.Any(m => m.To.X == 0 && m.To.Y == 2), Is.True);
    }

    private static PlayerService CreatePlayerService(PlayerData data)
    {
        ResetPlayerServiceSingleton();

        var go = new GameObject("UsableItemSystemTests");
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
