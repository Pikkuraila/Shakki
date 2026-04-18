# Phase 0 Baseline

This document records the current live runtime path before larger refactors.
The goal is to protect the current playable loop and make future cleanup safer.

## Build Baseline

- The build currently contains one playable scene: `Assets/Scenes/SampleScene.unity`.
- The project uses Unity 6 (`6000.0.46f1`).
- There is no dedicated test assembly or existing automated test suite in the repo yet.

## Current Live Runtime Path

### Run Orchestration

- `RunController` is the current top-level coordinator for the live game loop.
- It owns battle startup, macro phase entry, shop opening, alchemist flow, dialogue hooks, balance lookups and bestiary hookups.
- This is the primary class that later refactors must split without changing current behavior.

### Battle Runtime

- `GameState` owns board state, move application, capture handling and game-end resolution.
- `BoardView` renders the board and pieces and delegates move legality to `GameState` through the active rules resolver.
- `DefRegistryRulesResolver` is the live rules source used by `RunController.BuildRules()`.

### Player / Meta Runtime

- `PlayerService` owns persistent player state and save/load.
- `loadoutSlots` is the live UI/runtime representation currently used by encounter assembly and drag-and-drop flows.
- `loadout` still exists as a compact meta representation and is rebuilt/expanded in multiple places.

### Encounter Assembly

- `LoadoutAssembler.BuildFromPlayerDataDrop()` is the live path that converts player loadout slots plus `SlotMapSO` plus `EnemySpec` into an `EncounterSO`.
- `EncounterLoader.Apply()` places encounter spawns onto the active `GameState`.

### Shop / UI Runtime

- `LoadoutGridView` is the live drag-and-drop target for player pieces and shop purchases.
- `ShopGridView` is the live shop presentation and purchase flow.
- `DropSlot` is the currently active drop abstraction in the UI layer.

### Bestiary Runtime

- The live bestiary path is the lightweight one:
  - `BestiaryService`
  - `BestiaryMatchHooks`
  - `RunController` hover gate via `BestiaryService.IsMoveKnown(...)`
- The richer archetype/intel branch exists in the repo but is not part of the active gameplay loop.

## Current Critical Invariants

These are the behaviors that phase 0 should protect before deeper refactors:

1. `LoadoutModel.Expand(...)` must preserve slot count and inject the implicit king when requested.
2. `LoadoutAssembler.BuildFromSlotsDrop(...)` must place white pieces according to `SlotMapSO`.
3. Preset enemy encounters must only contribute valid black spawns when assembled through `EnemySpec.Mode.PresetEncounter`.
4. Capturing a required king must immediately end the game with `EndReason.KingCaptured`.
5. When a king is not required, `GameState.CheckGameEnd()` must be able to resolve battles via annihilation.

## Known Non-Baseline Areas

These areas exist in the repo, but they are not part of the trusted phase 0 live path:

- `SoRulesResolver`
- `BoardGenerator`
- `EnemyBudgetGenerator`
- `InventoryGridView`
- `LoadoutSlot` / `ShopSlot`
- richer bestiary archetype + intel resolver path
- TMP example scenes/assets
- scene template assets

## Manual Smoke Checklist

When opening the project in Unity after phase 0:

1. Open `Assets/Scenes/SampleScene.unity`.
2. Enter play mode and confirm the run starts without missing reference errors.
3. Confirm macro phase can enter a battle.
4. Confirm a legal move updates the board correctly.
5. Confirm a shop purchase can be dropped into loadout.
6. Confirm returning from battle/shop still works.

## Automated Smoke Coverage Added In Phase 0

The editor smoke tests added in phase 0 cover:

- `LoadoutModel` slot expansion baseline
- `LoadoutAssembler` slot-map based encounter assembly
- `GameState` king-capture end condition
- `GameState` annihilation end condition

## Intent For Next Phases

Phase 1 can now safely remove clearly dead paths.
Phase 2 should make loadout state follow a single source of truth.
Phase 3 should collapse rules resolution to one runtime path.
Phase 4 should split `RunController` into smaller flow services.
