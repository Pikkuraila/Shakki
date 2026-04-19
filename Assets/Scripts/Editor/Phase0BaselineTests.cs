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
    public void LoadoutAssembler_BuildFromPlayerDataDrop_PrefersInstanceSlotsWhenPresent()
    {
        var slotMap = ScriptableObject.CreateInstance<SlotMapSO>();

        try
        {
            slotMap.whiteSlotCoords = new Vector2Int[16];
            for (int i = 0; i < slotMap.whiteSlotCoords.Length; i++)
                slotMap.whiteSlotCoords[i] = new Vector2Int(i % 8, i / 8);

            slotMap.whiteSlotCoords[0] = new Vector2Int(4, 0);
            slotMap.whiteSlotCoords[1] = new Vector2Int(3, 1);

            const string amalgamId = "Amalgam_Pawn_Rook_test";
            var data = new PlayerData
            {
                version = PlayerInstanceSync.CurrentDataVersion,
                loadoutSlots = new List<string> { "King", "", "", "" },
                pieceInstances = new List<PieceInstanceData>
                {
                    new PieceInstanceData
                    {
                        instanceId = "piece_000001",
                        pieceDefId = "King"
                    },
                    new PieceInstanceData
                    {
                        instanceId = "piece_000002",
                        kind = PieceInstanceKind.Amalgam,
                        pieceDefId = "amalgam",
                        amalgam = new AmalgamPieceData
                        {
                            runtimePieceDefId = amalgamId
                        }
                    }
                },
                loadoutSlotInstances = new List<LoadoutSlotInstanceData>
                {
                    new LoadoutSlotInstanceData { slotIndex = 0, pieceInstanceId = "piece_000002" },
                    new LoadoutSlotInstanceData { slotIndex = 1, pieceInstanceId = "piece_000001" },
                    new LoadoutSlotInstanceData { slotIndex = 2, pieceInstanceId = string.Empty },
                    new LoadoutSlotInstanceData { slotIndex = 3, pieceInstanceId = string.Empty },
                }
            };

            var encounter = LoadoutAssembler.BuildFromPlayerDataDrop(
                data,
                slotMap,
                new EnemySpec { mode = EnemySpec.Mode.Classic },
                boardWidth: 8,
                boardHeight: 8,
                totalSlots: 4);

            try
            {
                Assert.That(encounter.spawns.Count, Is.EqualTo(2));
                Assert.That(encounter.spawns.Any(s => s.owner == "white" && s.pieceId == amalgamId && s.x == 4 && s.y == 0), Is.True);
                Assert.That(encounter.spawns.Any(s => s.owner == "white" && s.pieceId == "King" && s.x == 3 && s.y == 1), Is.True);
                Assert.That(encounter.spawns.Any(s => s.owner == "white" && s.pieceId == "King" && s.x == 4 && s.y == 0), Is.False);
                Assert.That(encounter.spawns.Any(s => s.owner == "white" && s.pieceId == amalgamId && s.pieceInstanceId == "piece_000002"), Is.True);
                Assert.That(encounter.spawns.Any(s => s.owner == "white" && s.pieceId == "King" && s.pieceInstanceId == "piece_000001"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(encounter);
            }
        }
        finally
        {
            Object.DestroyImmediate(slotMap);
        }
    }

    [Test]
    public void ShopPool_Filtered_UsesInstanceSlotCopiesWhenPresent()
    {
        var pool = ScriptableObject.CreateInstance<ShopPoolSO>();
        var item = ScriptableObject.CreateInstance<ShopItemDefSO>();
        var rook = ScriptableObject.CreateInstance<PieceDefSO>();

        try
        {
            rook.name = "Rook";
            rook.typeName = "Rook";

            item.name = "ShopItemRook";
            item.piece = rook;

            pool.items = new List<ShopItemDefSO> { item };
            pool.maxCopiesFromPlayer = 1;

            var data = new PlayerData
            {
                version = PlayerInstanceSync.CurrentDataVersion,
                loadoutSlots = new List<string> { "", "", "", "" },
                pieceInstances = new List<PieceInstanceData>
                {
                    new PieceInstanceData
                    {
                        instanceId = "piece_000001",
                        pieceDefId = "Rook"
                    }
                },
                loadoutSlotInstances = new List<LoadoutSlotInstanceData>
                {
                    new LoadoutSlotInstanceData { slotIndex = 0, pieceInstanceId = "piece_000001" },
                    new LoadoutSlotInstanceData { slotIndex = 1, pieceInstanceId = string.Empty },
                    new LoadoutSlotInstanceData { slotIndex = 2, pieceInstanceId = string.Empty },
                    new LoadoutSlotInstanceData { slotIndex = 3, pieceInstanceId = string.Empty },
                }
            };

            var filtered = pool.Filtered(data).ToList();

            Assert.That(filtered, Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(pool);
            Object.DestroyImmediate(item);
            Object.DestroyImmediate(rook);
        }
    }

    [Test]
    public void RunStatePersistence_TryBuildSavedMacroMap_RehydratesRandomMacroMapFromStoredSeed()
    {
        var generator = ScriptableObject.CreateInstance<MacroMapGeneratorSO>();
        MacroMapSO expected = null;
        MacroMapSO resumed = null;

        try
        {
            generator.rows = 4;
            generator.columns = 3;

            var data = new PlayerData
            {
                lastRunSeed = "123456",
                macroIndex = 7
            };

            var restored = RunStatePersistence.TryBuildSavedMacroMap(
                data,
                RunController.MacroBuildMode.GenerateRandom,
                macroPreset: null,
                macroGenerator: generator,
                out resumed);

            expected = generator.Generate(123456);

            Assert.That(restored, Is.True);
            Assert.That(resumed, Is.Not.Null);
            AssertMacroMapsMatch(expected, resumed);
        }
        finally
        {
            if (expected != null) Object.DestroyImmediate(expected);
            if (resumed != null) Object.DestroyImmediate(resumed);
            Object.DestroyImmediate(generator);
        }
    }

    [Test]
    public void RunStatePersistence_TryBuildSavedMacroMap_RejectsInvalidStoredSeed()
    {
        var generator = ScriptableObject.CreateInstance<MacroMapGeneratorSO>();

        try
        {
            var data = new PlayerData
            {
                lastRunSeed = "not-a-seed",
                macroIndex = 4
            };

            var restored = RunStatePersistence.TryBuildSavedMacroMap(
                data,
                RunController.MacroBuildMode.GenerateRandom,
                macroPreset: null,
                macroGenerator: generator,
                out var resumed);

            Assert.That(restored, Is.False);
            Assert.That(resumed, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(generator);
        }
    }

    [Test]
    public void GameCatalog_GetPieceById_RehydratesRuntimeAmalgamFromId()
    {
        var catalog = ScriptableObject.CreateInstance<GameCatalogSO>();
        var baseAmalgam = ScriptableObject.CreateInstance<PieceDefSO>();
        var pawn = ScriptableObject.CreateInstance<PieceDefSO>();
        var rook = ScriptableObject.CreateInstance<PieceDefSO>();

        try
        {
            baseAmalgam.name = "Amalgam";
            baseAmalgam.typeName = "amalgam";
            baseAmalgam.identityTags = IdentityTag.Amalgam;
            baseAmalgam.rules = System.Array.Empty<MoveRuleSO>();

            pawn.name = "Pawn";
            pawn.typeName = "Pawn";
            pawn.identityTags = IdentityTag.Living;
            pawn.rules = System.Array.Empty<MoveRuleSO>();

            rook.name = "Rook";
            rook.typeName = "Rook";
            rook.identityTags = IdentityTag.Living;
            rook.rules = System.Array.Empty<MoveRuleSO>();

            catalog.pieces = new List<PieceDefSO> { baseAmalgam, pawn, rook };
            catalog.upgrades = new List<UpgradeDefSO>();
            catalog.powerups = new List<PowerupDefSO>();

            var hydrated = catalog.GetPieceById("Amalgam_Pawn_Rook_test");

            Assert.That(hydrated, Is.Not.Null);
            Assert.That(hydrated.typeName, Is.EqualTo("Amalgam_Pawn_Rook_test"));
            Assert.That((hydrated.identityTags & IdentityTag.Amalgam) != 0, Is.True);
            Assert.That((hydrated.identityTags & IdentityTag.Living) != 0, Is.True);
            Assert.That(catalog.GetPieceById("Amalgam_Pawn_Rook_test"), Is.SameAs(hydrated));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(baseAmalgam);
            Object.DestroyImmediate(pawn);
            Object.DestroyImmediate(rook);
        }
    }

    [Test]
    public void GameCatalog_GetPieceById_RehydratesRuntimeAmalgamFromFallbackBaseDef()
    {
        var catalog = ScriptableObject.CreateInstance<GameCatalogSO>();
        var baseAmalgam = ScriptableObject.CreateInstance<PieceDefSO>();
        var pawn = ScriptableObject.CreateInstance<PieceDefSO>();
        var rook = ScriptableObject.CreateInstance<PieceDefSO>();
        var baseRule = ScriptableObject.CreateInstance<TestRuleSO>();
        var pawnRule = ScriptableObject.CreateInstance<TestRuleSO>();
        var rookRule = ScriptableObject.CreateInstance<TestRuleSO>();
        var whiteTexture = new Texture2D(2, 2);
        var blackTexture = new Texture2D(2, 2);
        var portraitTexture = new Texture2D(2, 2);
        var whiteSprite = Sprite.Create(whiteTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        var blackSprite = Sprite.Create(blackTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        var portraitSprite = Sprite.Create(portraitTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));

        try
        {
            baseAmalgam.name = "Amalgam";
            baseAmalgam.typeName = "amalgam";
            baseAmalgam.identityTags = IdentityTag.Amalgam;
            baseAmalgam.rules = new MoveRuleSO[] { baseRule };
            baseAmalgam.whiteSprite = whiteSprite;
            baseAmalgam.blackSprite = blackSprite;
            baseAmalgam.portraitSprite = portraitSprite;

            pawn.name = "Pawn";
            pawn.typeName = "Pawn";
            pawn.identityTags = IdentityTag.Living;
            pawn.rules = new MoveRuleSO[] { pawnRule };

            rook.name = "Rook";
            rook.typeName = "Rook";
            rook.identityTags = IdentityTag.Living;
            rook.rules = new MoveRuleSO[] { rookRule };

            catalog.pieces = new List<PieceDefSO> { pawn, rook };
            catalog.upgrades = new List<UpgradeDefSO>();
            catalog.powerups = new List<PowerupDefSO>();
            catalog.amalgamBaseDef = baseAmalgam;

            var hydrated = catalog.GetPieceById("Amalgam_Pawn_Rook_test");

            Assert.That(hydrated, Is.Not.Null);
            Assert.That(hydrated.typeName, Is.EqualTo("Amalgam_Pawn_Rook_test"));
            Assert.That((hydrated.identityTags & IdentityTag.Amalgam) != 0, Is.True);
            Assert.That((hydrated.identityTags & IdentityTag.Living) != 0, Is.True);
            Assert.That(hydrated.whiteSprite, Is.SameAs(whiteSprite));
            Assert.That(hydrated.blackSprite, Is.SameAs(blackSprite));
            Assert.That(hydrated.portraitSprite, Is.SameAs(portraitSprite));
            CollectionAssert.AreEqual(new MoveRuleSO[] { baseRule, pawnRule, rookRule }, hydrated.rules);
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(baseAmalgam);
            Object.DestroyImmediate(pawn);
            Object.DestroyImmediate(rook);
            Object.DestroyImmediate(baseRule);
            Object.DestroyImmediate(pawnRule);
            Object.DestroyImmediate(rookRule);
            Object.DestroyImmediate(whiteSprite);
            Object.DestroyImmediate(blackSprite);
            Object.DestroyImmediate(portraitSprite);
            Object.DestroyImmediate(whiteTexture);
            Object.DestroyImmediate(blackTexture);
            Object.DestroyImmediate(portraitTexture);
        }
    }

    [Test]
    public void EncounterLoader_Apply_AssignsInstanceIdsToSpawnedWhitePieces()
    {
        var catalog = ScriptableObject.CreateInstance<GameCatalogSO>();
        var rook = ScriptableObject.CreateInstance<PieceDefSO>();
        var state = new GameState(new GridGeometry(8, 8));
        var encounter = ScriptableObject.CreateInstance<EncounterSO>();

        try
        {
            rook.name = "Rook";
            rook.typeName = "Rook";
            rook.rules = System.Array.Empty<MoveRuleSO>();

            catalog.pieces = new List<PieceDefSO> { rook };
            catalog.upgrades = new List<UpgradeDefSO>();
            catalog.powerups = new List<PowerupDefSO>();

            encounter.spawns = new List<EncounterSO.Spawn>
            {
                new EncounterSO.Spawn
                {
                    owner = "white",
                    pieceId = "Rook",
                    pieceInstanceId = "piece_000123",
                    x = 2,
                    y = 3
                }
            };

            EncounterLoader.Apply(state, encounter, catalog);

            var piece = state.Get(new Coord(2, 3));
            Assert.That(piece, Is.Not.Null);
            Assert.That(piece.TypeName, Is.EqualTo("Rook"));
            Assert.That(piece.InstanceId, Is.EqualTo("piece_000123"));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(rook);
            Object.DestroyImmediate(encounter);
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

    private static void AssertMacroMapsMatch(MacroMapSO expected, MacroMapSO actual)
    {
        Assert.That(actual, Is.Not.Null);
        Assert.That(actual.rows, Is.EqualTo(expected.rows));
        Assert.That(actual.columns, Is.EqualTo(expected.columns));
        Assert.That(actual.tiles, Has.Length.EqualTo(expected.tiles.Length));

        for (int i = 0; i < expected.tiles.Length; i++)
        {
            Assert.That(actual.tiles[i].type, Is.EqualTo(expected.tiles[i].type), $"Tile type mismatch at index {i}");
            Assert.That(actual.tiles[i].difficultyOffset, Is.EqualTo(expected.tiles[i].difficultyOffset), $"Difficulty offset mismatch at index {i}");
            Assert.That(actual.tiles[i].shopTierOffset, Is.EqualTo(expected.tiles[i].shopTierOffset), $"Shop tier offset mismatch at index {i}");
            Assert.That(actual.tiles[i].param, Is.EqualTo(expected.tiles[i].param), $"Param mismatch at index {i}");
        }
    }

    private sealed class TestRuleSO : MoveRuleSO
    {
        public override IMoveRule Build()
        {
            return new NoOpRule();
        }
    }

    private sealed class NoOpRule : IMoveRule
    {
        public IEnumerable<Move> Generate(RuleContext ctx)
        {
            yield break;
        }
    }
}
