using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Shakki.Core;

public sealed class Phase0BaselineTests
{
    [Test]
    public void LoadoutModel_Expand_InsertsImplicitKingAndPadsRequestedSlotCount()
    {
        var entries = new List<LoadoutEntry>
        {
            new LoadoutEntry { pieceId = "Pawn", count = 2 },
            new LoadoutEntry { pieceId = "Rook", count = 1 }
        };

        var slots = LoadoutModel.Expand(entries, totalSlots: 6, implicitKingId: "King");

        Assert.That(slots, Has.Count.EqualTo(6));
        CollectionAssert.AreEqual(
            new[] { "King", "Pawn", "Pawn", "Rook", "", "" },
            slots);
    }

    [Test]
    public void LoadoutAssembler_BuildFromSlotsDrop_UsesSlotMapAndValidBlackPresetSpawns()
    {
        var slotMap = ScriptableObject.CreateInstance<SlotMapSO>();
        var preset = ScriptableObject.CreateInstance<EncounterSO>();

        try
        {
            slotMap.whiteSlotCoords = new Vector2Int[16];
            for (int i = 0; i < slotMap.whiteSlotCoords.Length; i++)
                slotMap.whiteSlotCoords[i] = new Vector2Int(i % 8, i / 8);

            slotMap.whiteSlotCoords[0] = new Vector2Int(4, 0);
            slotMap.whiteSlotCoords[1] = new Vector2Int(3, 1);

            var whiteSlots = Enumerable.Repeat("", 16).ToList();
            whiteSlots[0] = "King";
            whiteSlots[1] = "Pawn";

            preset.spawns = new List<EncounterSO.Spawn>
            {
                new EncounterSO.Spawn { owner = "black", pieceId = "Rook", x = 5, y = 7 },
                new EncounterSO.Spawn { owner = "white", pieceId = "Pawn", x = 0, y = 6 },
                new EncounterSO.Spawn { owner = "black", pieceId = "Knight", x = 2, y = 1 },
                new EncounterSO.Spawn { owner = "black", pieceId = "Bishop", x = 99, y = 99 }
            };

            var enemy = new EnemySpec
            {
                mode = EnemySpec.Mode.PresetEncounter,
                preset = preset,
                forbidWhiteAndAllyRows = 3
            };

            var encounter = LoadoutAssembler.BuildFromSlotsDrop(
                whiteSlots,
                slotMap,
                enemy,
                boardWidth: 8,
                boardHeight: 8);

            try
            {
                Assert.That(encounter.spawns.Count, Is.EqualTo(3));
                Assert.That(encounter.spawns.Any(s => s.owner == "white" && s.pieceId == "King" && s.x == 4 && s.y == 0), Is.True);
                Assert.That(encounter.spawns.Any(s => s.owner == "white" && s.pieceId == "Pawn" && s.x == 3 && s.y == 1), Is.True);
                Assert.That(encounter.spawns.Any(s => s.owner == "black" && s.pieceId == "Rook" && s.x == 5 && s.y == 7), Is.True);
                Assert.That(encounter.spawns.Any(s => s.owner == "white" && s.x == 0 && s.y == 6), Is.False);
                Assert.That(encounter.spawns.Any(s => s.owner == "black" && s.pieceId == "Knight"), Is.False);
                Assert.That(encounter.spawns.Any(s => s.owner == "black" && s.pieceId == "Bishop"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(encounter);
            }
        }
        finally
        {
            Object.DestroyImmediate(slotMap);
            Object.DestroyImmediate(preset);
        }
    }

    [Test]
    public void GameState_ApplyMove_WhenCapturingRequiredKing_EndsWithKingCaptured()
    {
        var state = new GameState(new GridGeometry(2, 1));
        var endInfo = default(GameEndInfo?);

        state.Set(
            new Coord(0, 0),
            new Piece(
                "white",
                "Attacker",
                new List<IMoveRule> { new FixedTargetRule(new Coord(1, 0)) }));

        state.Set(
            new Coord(1, 0),
            new Piece("black", "King", new List<IMoveRule>()));

        state.OnGameEnded += info => endInfo = info;

        var applied = state.ApplyMove(new Move(new Coord(0, 0), new Coord(1, 0)), rules: null);

        Assert.That(applied, Is.True);
        Assert.That(state.IsGameOver, Is.True);
        Assert.That(state.WinnerColor, Is.EqualTo("white"));
        Assert.That(state.LoserColor, Is.EqualTo("black"));
        Assert.That(endInfo.HasValue, Is.True);
        Assert.That(endInfo.Value.Reason, Is.EqualTo(EndReason.KingCaptured));
    }

    [Test]
    public void GameState_CheckGameEnd_WhenBlackKingNotRequired_UsesAnnihilation()
    {
        var state = new GameState(new GridGeometry(1, 1));
        var endInfo = default(GameEndInfo?);

        state.RequireWhiteKing = false;
        state.RequireBlackKing = false;
        state.Set(new Coord(0, 0), new Piece("white", "Pawn", new List<IMoveRule>()));
        state.OnGameEnded += info => endInfo = info;

        var ended = state.CheckGameEnd();

        Assert.That(ended, Is.True);
        Assert.That(state.IsGameOver, Is.True);
        Assert.That(state.WinnerColor, Is.EqualTo("white"));
        Assert.That(endInfo.HasValue, Is.True);
        Assert.That(endInfo.Value.Reason, Is.EqualTo(EndReason.Annihilation));
    }

    private sealed class FixedTargetRule : IMoveRule
    {
        private readonly Coord _target;

        public FixedTargetRule(Coord target)
        {
            _target = target;
        }

        public IEnumerable<Move> Generate(RuleContext ctx)
        {
            yield return new Move(ctx.From, _target);
        }
    }
}
