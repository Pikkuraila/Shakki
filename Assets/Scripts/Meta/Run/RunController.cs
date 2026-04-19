using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Shakki.Core;
using System.Linq;
using Shakki.Meta.Bestiary;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;


public sealed class RunController : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private BoardView boardView;
    [SerializeField] private GameCatalogSO catalog;

    [Header("Encounter Sources")]
    [SerializeField] private EncounterSO encounterOverride;   // jos asetettu, käytetään tätä
    [SerializeField] private SlotMapSO slotMap;               // dynaamiselle loadoutista luomiselle
    [SerializeField] private global::EnemySpec enemySpec;

    [Header("Board")]
    [SerializeField] private BoardTemplateSO template; // voi olla null
    [SerializeField] private int width = 8;
    [SerializeField] private int height = 8;
    [SerializeField] private int? seedOverride;

    [Header("Rules/Defs")]
    [SerializeField] private List<PieceDefSO> allPieceDefsForRules;
    [SerializeField] private BoardView.AiMode aiMode = BoardView.AiMode.Random;

    [Header("Shop UI (Canvas)")]
    [SerializeField] private GameObject shopPanel;          // Canvas/Panel (inactive)
    [SerializeField] private LoadoutGridView loadoutView;   // 8×2
    [SerializeField] private ShopGridView shopView;         // 4×1
    [SerializeField] private GameObject itemInventoryPanel;
    [SerializeField] private InventoryGridView itemInventoryView;

    [Header("Shop Data")]
    [SerializeField] private ShopPoolSO defaultShopPool;

    [Header("Usable Items")]
    [SerializeField] private string stoneHeadItemId = "IT_StoneHead";
    [SerializeField] private string stoneHeadObstaclePieceId = "StoneHeadObstacle";

    [Header("Alchemist UI (Canvas)")]
    [SerializeField] private GameObject alchemistPanel;              // Canvas/Panel (inactive)
    [SerializeField] private AlchemistEncounterView alchemistView;   // panelin sisältä
    [SerializeField] private LoadoutGridView alchemistLoadoutView;


    [Header("Alchemist Data")]
    [SerializeField] private PieceDefSO amalgamBaseDef;              // Amalgam.asset (typeName="Amalgam")

    [SerializeField] private DragController dragController;
    private bool _returningToMacro;

    public enum MacroBuildMode
    {
        UsePreset,
        GenerateRandom
    }

    [Header("Macro")]
    [SerializeField] private MacroBuildMode macroBuildMode = MacroBuildMode.GenerateRandom;

    // kun mode=UsePreset
    [SerializeField] private MacroMapSO macroPreset;

    // nykyiset:
    [SerializeField] private MacroMapSO macroMap;
    [SerializeField] private MacroBoardView macroView;
    [SerializeField] private MacroMapGeneratorSO macroGenerator;

    [Header("Balance")]
    [SerializeField] private RunBalanceSO runBalance;

    [Header("Encounter Pools")]
    [SerializeField] private EncounterPoolSO battlePool;
    [SerializeField] private EncounterPoolSO bossPool;

    private EncounterSO _pendingEncounterOverride;



    [SerializeField] private DialogueController dialogue;
    [SerializeField] private DialoguePackSO hermitRestDialogue;


    [SerializeField] private PlayerService playerService;


    [Header("Bestiary")]
    [SerializeField] private BestiaryProgressionRulesSO bestiaryRules; // luo asset inspectorista
    [SerializeField] private string enemyOwnerForBestiary = "black";

    private BestiaryService _bestiary;
    private BestiaryMatchHooks _bestiaryHooks;

    private EnemySpec _pendingEnemySpecOverride;

    [Header("Debug Overlay")]
    [SerializeField] private TMP_Text debugOverlayText;

    private string _debugCurrentPhase = "Boot";
    private string _debugEncounterName = "-";
    private string _debugEncounterSource = "-";

    private const int ItemInventorySlots = 16;
    private const string NeutralObstacleOwner = "neutral";

    

    public enum PieceDefeatSource
    {
        Capture,
        Spell,
        Trap,
        Sacrifice,
        Scripted,
    }

    public sealed class PieceDefeatContext
    {
        public Piece victim;
        public Piece attacker;
        public Coord? victimPos;
        public Coord? attackerFrom;
        public PieceDefeatSource source;

        public bool forcePermanent;
        public bool forceInjury;
    }


    private bool PieceHasLethalTag(Piece piece)
    {
        if (piece == null || catalog == null || catalog.pieces == null)
            return false;

        var def = catalog.pieces.Find(x => x != null && x.typeName == piece.TypeName);
        if (def == null)
            return false;

        return def.identityTags.HasTag(Shakki.Core.IdentityTag.Lethal);
    }

    private bool HasAuthoritativeRuntimePieceState()
    {
        if (playerService == null || playerService.Data == null)
            return false;

        int totalSlots = playerService.Data.loadoutSlotInstances?.Count ?? Slots;
        if (totalSlots <= 0)
            totalSlots = Slots;

        return playerService.HasAuthoritativeInstanceModel(totalSlots);
    }

    private static bool ShouldFallbackToLegacyDefeat(bool hasAuthoritativeInstanceModel, bool instanceUpdateSucceeded)
    {
        return !hasAuthoritativeInstanceModel && !instanceUpdateSucceeded;
    }

    private static bool ShouldSkipLegacyDefeatFallbackForMissingInstanceId(
        bool hasAuthoritativeInstanceModel,
        string victimInstanceId)
    {
        return hasAuthoritativeInstanceModel && string.IsNullOrEmpty(victimInstanceId);
    }

    private static Dictionary<string, int> BuildLegacyRuntimeInjuryBudget(
        bool hasAuthoritativeInstanceModel,
        IEnumerable<InjuredPieceStack> injuredPieces)
    {
        var budget = new Dictionary<string, int>();
        if (hasAuthoritativeInstanceModel || injuredPieces == null)
            return budget;

        foreach (var stack in injuredPieces)
        {
            if (stack == null || string.IsNullOrEmpty(stack.pieceId) || stack.count <= 0)
                continue;

            budget[stack.pieceId] = stack.count;
        }

        return budget;
    }

    private void ApplyPersistentDefeat(Piece victim, bool permanentDeath)
    {
        if (victim == null || playerService == null)
            return;

        bool hasAuthoritativeInstanceModel = HasAuthoritativeRuntimePieceState();
        bool instanceUpdateSucceeded = false;

        if (!string.IsNullOrEmpty(victim.InstanceId))
        {
            instanceUpdateSucceeded = permanentDeath
                ? playerService.RemovePieceInstancePermanently(victim.InstanceId)
                : playerService.MarkInstanceInjured(victim.InstanceId);

            if (instanceUpdateSucceeded)
                return;

            if (hasAuthoritativeInstanceModel)
            {
                Debug.LogWarning(
                    $"[Defeat] Instance update failed for {victim.TypeName} instance={victim.InstanceId} while authoritative instance model is active; skipping type-based fallback.");
                return;
            }

            Debug.LogWarning(
                $"[Defeat] Instance update failed for {victim.TypeName} instance={victim.InstanceId}, falling back to type-based update.");
        }
        else if (ShouldSkipLegacyDefeatFallbackForMissingInstanceId(hasAuthoritativeInstanceModel, victim.InstanceId))
        {
            Debug.LogWarning(
                $"[Defeat] Missing instance id for {victim.TypeName} while authoritative instance model is active; skipping type-based fallback.");
            return;
        }

        if (!ShouldFallbackToLegacyDefeat(hasAuthoritativeInstanceModel, instanceUpdateSucceeded))
            return;

        if (permanentDeath)
            playerService.RemovePiecePermanently(victim.TypeName, 1);
        else
            playerService.MarkPieceInjured(victim.TypeName, 1);
    }

    private void ResolvePlayerPieceDefeat(PieceDefeatContext ctx)
    {
        if (ctx == null || ctx.victim == null) return;
        if (ctx.victim.Owner != "white") return;
        if (playerService == null) return;

        bool permanentDeath;

        if (ctx.forcePermanent)
        {
            permanentDeath = true;
        }
        else if (ctx.forceInjury)
        {
            permanentDeath = false;
        }
        else if (ctx.victim.IsInjured)
        {
            permanentDeath = true;
        }
        else if (ctx.source == PieceDefeatSource.Sacrifice)
        {
            permanentDeath = true;
        }
        else if (ctx.attacker != null && PieceHasLethalTag(ctx.attacker))
        {
            permanentDeath = true;
        }
        else
        {
            permanentDeath = false;
        }

        ApplyPersistentDefeat(ctx.victim, permanentDeath);

        Debug.Log(
            $"[Defeat] source={ctx.source} victim={ctx.victim.TypeName} " +
            $"instance={ctx.victim.InstanceId ?? "-"} injured={ctx.victim.IsInjured} permanent={permanentDeath} " +
            $"attacker={(ctx.attacker != null ? ctx.attacker.TypeName : "-")}"
        );
    }

    private void HandlePieceCaptured(Coord victimPos, Piece victim, Coord attackerFrom, Piece attacker)
    {
        ResolvePlayerPieceDefeat(new PieceDefeatContext
        {
            victim = victim,
            attacker = attacker,
            victimPos = victimPos,
            attackerFrom = attackerFrom,
            source = PieceDefeatSource.Capture,
            forcePermanent = false,
            forceInjury = false
        });
    }

    public void DefeatPlayerPieceBySpell(Piece victim, Piece attacker = null, Coord? victimPos = null)
    {
        ResolvePlayerPieceDefeat(new PieceDefeatContext
        {
            victim = victim,
            attacker = attacker,
            victimPos = victimPos,
            attackerFrom = null,
            source = PieceDefeatSource.Spell,
            forcePermanent = false,
            forceInjury = false
        });
    }

    public void DefeatPlayerPieceByTrap(Piece victim, Piece attacker = null, Coord? victimPos = null)
    {
        ResolvePlayerPieceDefeat(new PieceDefeatContext
        {
            victim = victim,
            attacker = attacker,
            victimPos = victimPos,
            attackerFrom = null,
            source = PieceDefeatSource.Trap,
            forcePermanent = false,
            forceInjury = false
        });
    }

    public void SacrificePlayerPiece(Piece victim, Coord? victimPos = null)
    {
        ResolvePlayerPieceDefeat(new PieceDefeatContext
        {
            victim = victim,
            attacker = null,
            victimPos = victimPos,
            attackerFrom = null,
            source = PieceDefeatSource.Sacrifice,
            forcePermanent = true,
            forceInjury = false
        });
    }

    public void DefeatPlayerPieceScripted(Piece victim, bool permanent, Coord? victimPos = null)
    {
        if (victim == null || victim.Owner != "white" || playerService == null) return;

        ApplyPersistentDefeat(victim, permanent);

        Debug.Log($"[Defeat] Scripted victim={victim.TypeName} instance={victim.InstanceId ?? "-"} permanent={permanent}");
    }

    // ===== Dialogue wrappers =====

    public void TriggerShopFromDialogue()
    {
        OpenShop();
    }

    public void TriggerBattleFromDialogue()
    {
        StartNewEncounter();
    }

    public void TriggerReturnToMacro()
    {
        EnterMacroPhase();
        macroView.SetVisible(true);
        var pd = playerService != null ? playerService.Data : null;
        macroView.Init(macroMap, pd != null ? pd.macroIndex : 0);

    }


    public Shakki.Core.IRulesResolver Rules => _rules;




    // lasketaan aina kun siirrytään uuteen macro-ruutuun
    private int _pendingBattleDifficulty = 1;
    private int _pendingShopTier = 1;


    private GameState _state;
    private IRulesResolver _rules;
    private const int Slots = 16;



    

    private RunBalanceSO.BattleTuning GetBattleTuningForTier(int tier)
    {
        if (runBalance != null)
        {
            return new RunBalanceSO.BattleTuning
            {
                tier = tier,
                budget = runBalance.GetBudgetForTier(tier),
                capCost = runBalance.GetCapCostForTier(tier),
                cheapPieceBiasPower = runBalance.cheapPieceBiasPower,
                forceBlackKingThreshold = runBalance.forceBlackKingThreshold
            };
        }

        return new RunBalanceSO.BattleTuning
        {
            tier = tier,
            budget = 2 + tier * 2,
            capCost = Mathf.Max(1, 1 + tier),
            cheapPieceBiasPower = 1.2f,
            forceBlackKingThreshold = 5
        };
    }

    private void LogMacroDifficulty(MacroTileDef tile, int row, int battleTier, int shopTier, bool isBoss = false)
    {
        string tileType = tile.type.ToString();
        int diffOffset = tile.difficultyOffset;
        int shopOffset = tile.shopTierOffset;

        if (runBalance != null)
        {
            var tuning = runBalance.GetBattleTuning(row, diffOffset, isBoss);

            Debug.Log(
                $"[Balance] tileType={tileType} row={row} " +
                $"diffOffset={diffOffset} shopOffset={shopOffset} isBoss={isBoss} " +
                $"battleTier={battleTier} shopTier={shopTier} " +
                $"budget={tuning.budget} capCost={tuning.capCost} " +
                $"cheapBias={tuning.cheapPieceBiasPower:F2} " +
                $"forceBlackKingThreshold={tuning.forceBlackKingThreshold}"
            );
        }
        else
        {
            var tuning = GetBattleTuningForTier(battleTier);

            Debug.Log(
                $"[Balance] tileType={tileType} row={row} " +
                $"diffOffset={diffOffset} shopOffset={shopOffset} isBoss={isBoss} " +
                $"battleTier={battleTier} shopTier={shopTier} " +
                $"budget={tuning.budget} capCost={tuning.capCost} " +
                $"cheapBias={tuning.cheapPieceBiasPower:F2} " +
                $"forceBlackKingThreshold={tuning.forceBlackKingThreshold} " +
                $"(fallback values)"
            );
        }
    }

    private int GetBattleDifficultyForTile(int row, MacroTileDef tile, bool isBoss = false)
    {
        if (runBalance != null)
            return runBalance.GetBattleDifficulty(row, tile.difficultyOffset, isBoss);

        int value = 1 + row + tile.difficultyOffset + (isBoss ? 0 : 0);
        return Mathf.Max(1, value);
    }

    private int GetShopTierForTile(int row, MacroTileDef tile)
    {
        if (runBalance != null)
            return runBalance.GetShopTier(row, tile.shopTierOffset);

        int value = 1 + row + tile.shopTierOffset;
        return Mathf.Max(1, value);
    }

    // ---------- LIFECYCLE ----------

    private void Awake()
    {
        if (playerService == null)
            playerService = PlayerService.Instance;

        // Bestiary init (once)
        if (_bestiary == null && bestiaryRules != null)
        {
            _bestiary = new BestiaryService(bestiaryRules, new PlayerPrefsBestiaryStore());
            _bestiaryHooks = new BestiaryMatchHooks(_bestiary, enemyOwnerForBestiary);
        }
    }


    private void Start()
    {
        BuildRules();
        EnsureItemInventoryUi();
        RefreshItemInventoryUi();
        SetItemInventoryVisible(true);
        _debugCurrentPhase = "Starting";
        RefreshDebugOverlay();

        Debug.Log($"[RunController] Start macroMap={macroMap}, macroView={macroView}");

        if (macroMap != null && macroView != null)
        {
            StartOrResumeRun();
        }
        else
        {
            StartNewEncounter();
        }
    }

    private void OnDestroy()
    {
        if (_state != null) _state.OnGameEnded -= OnGameEnded;
    }

    private void EnsureItemInventoryUi()
    {
        if (itemInventoryView != null && itemInventoryPanel != null)
            return;

        if (itemInventoryPanel == null)
            itemInventoryPanel = CreateRuntimeItemInventoryPanel();

        if (itemInventoryPanel == null)
            return;

        itemInventoryView = itemInventoryPanel.GetComponent<InventoryGridView>();
        if (itemInventoryView == null)
            itemInventoryView = itemInventoryPanel.AddComponent<InventoryGridView>();

        itemInventoryView.columns = 2;
        itemInventoryView.rows = 8;
        itemInventoryView.Bind(playerService != null ? playerService : PlayerService.Instance, catalog, this);
        itemInventoryView.BuildIfNeeded();
    }

    private GameObject CreateRuntimeItemInventoryPanel()
    {
        var rootCanvas = ResolveUiRootCanvas();
        if (rootCanvas == null)
        {
            Debug.LogWarning("[Items] Could not resolve UI canvas for runtime inventory panel.");
            return null;
        }

        var panel = new GameObject("ItemInventoryPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.SetParent(rootCanvas.transform, false);
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-10f, 0f);
        panelRect.sizeDelta = new Vector2(108f, 168f);

        var panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.10f, 0.12f, 0.14f, 0.92f);

        var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(panel.transform, false);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -4f);
        titleRect.sizeDelta = new Vector2(0f, 12f);

        var titleText = title.GetComponent<TextMeshProUGUI>();
        titleText.text = "Items";
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 9f;
        titleText.color = new Color(0.95f, 0.92f, 0.84f, 1f);
        titleText.raycastTarget = false;

        var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGo.transform.SetParent(panel.transform, false);
        var gridRect = gridGo.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0f, 0f);
        gridRect.anchorMax = new Vector2(1f, 1f);
        gridRect.offsetMin = new Vector2(6f, 6f);
        gridRect.offsetMax = new Vector2(-6f, -16f);

        var inventory = panel.AddComponent<InventoryGridView>();
        inventory.grid = gridGo.GetComponent<GridLayoutGroup>();
        inventory.columns = 2;
        inventory.rows = 8;
        inventory.cellSize = new Vector2(22f, 22f);
        inventory.spacing = new Vector2(2f, 2f);

        return panel;
    }

    private Canvas ResolveUiRootCanvas()
    {
        if (shopPanel != null)
            return shopPanel.GetComponentInParent<Canvas>(true)?.rootCanvas;

        if (loadoutView != null)
            return loadoutView.GetComponentInParent<Canvas>(true)?.rootCanvas;

        if (itemInventoryPanel != null)
            return itemInventoryPanel.GetComponentInParent<Canvas>(true)?.rootCanvas;

        return FindObjectOfType<Canvas>(true)?.rootCanvas;
    }

    private void RefreshItemInventoryUi()
    {
        EnsureItemInventoryUi();
        itemInventoryView?.Bind(playerService != null ? playerService : PlayerService.Instance, catalog, this);
        itemInventoryView?.RefreshAll();
    }

    private void SetItemInventoryVisible(bool visible)
    {
        EnsureItemInventoryUi();
        if (itemInventoryPanel != null)
        {
            if (visible)
                itemInventoryPanel.transform.SetAsLastSibling();

            itemInventoryPanel.SetActive(visible);
        }

        if (visible)
            RefreshItemInventoryUi();
    }

    public bool TryHandleBoardItemDrop(UIDraggablePiece drag, PointerEventData eventData)
    {
        if (drag == null || eventData == null)
            return false;

        if (drag.payloadKind != DragPayloadKind.Item || drag.originKind != SlotKind.Inventory)
            return false;

        if (!string.Equals(drag.payloadId, stoneHeadItemId, System.StringComparison.Ordinal))
            return false;

        if (_state == null || boardView == null || !boardView.enabled || _state.IsGameOver)
            return false;

        if (_state.CurrentPlayer != "white")
            return false;

        var world = Camera.main != null
            ? Camera.main.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, Mathf.Abs(Camera.main.transform.position.z)))
            : Vector3.zero;
        world.z = 0f;

        var target = boardView.WorldToBoardPublic(world);
        var coord = new Coord(target.x, target.y);
        if (!_state.InBounds(coord))
            return false;

        if (_state.Get(coord) != null)
            return false;

        if (playerService == null || !playerService.ConsumeInventoryItemAt(drag.originIndex, drag.payloadId, ItemInventorySlots))
            return false;

        var obstacle = new Piece(NeutralObstacleOwner, stoneHeadObstaclePieceId, System.Array.Empty<IMoveRule>(), PieceTag.Obstacle);
        _state.Set(coord, obstacle);

        if (boardView != null)
            boardView.RefreshFromState();

        RefreshItemInventoryUi();
        Debug.Log($"[Items] Placed Stone Head obstacle at {coord.X},{coord.Y}");
        return true;
    }



    // ---------- RUN + MACRO ----------

    private void StartOrResumeRun()
    {
        BuildRules();

        var ps = PlayerService.Instance;
        var pd = ps != null ? ps.Data : null;

        if (RunStatePersistence.TryBuildSavedMacroMap(pd, macroBuildMode, macroPreset, macroGenerator, out var resumedMap))
        {
            macroMap = resumedMap;
            Debug.Log($"[Run] Resuming existing run seed={pd.lastRunSeed ?? "-"} macroIndex={pd.macroIndex}");
            EnterMacroPhase();
            return;
        }

        StartRun();
    }

    private void StartRun()
    {
        BuildRules();
        var ps = PlayerService.Instance;
        var pd = ps != null ? ps.Data : null;

        int? generatedSeed = null;

        // 1) Valitse map lähde
        if (macroBuildMode == MacroBuildMode.UsePreset)
        {
            if (macroPreset == null)
            {
                Debug.LogWarning("[Run] MacroBuildMode=UsePreset but macroPreset is NULL -> fallback to GenerateRandom.");
                macroBuildMode = MacroBuildMode.GenerateRandom;
            }
            else
            {
                macroMap = RunStatePersistence.CloneMacroMap(macroPreset);
                Debug.Log($"[Run] Using macro PRESET '{macroPreset.name}' rows={macroMap.rows} cols={macroMap.columns} tiles={macroMap.tiles?.Length}");
            }
        }

        if (macroBuildMode == MacroBuildMode.GenerateRandom)
        {
            if (macroGenerator != null)
            {
                int seed = seedOverride ?? UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                generatedSeed = seed;
                macroMap = macroGenerator.Generate(seed);
                Debug.Log($"[Run] Generated macroMap seed={seed} rows={macroMap.rows} cols={macroMap.columns} tiles={macroMap.tiles?.Length}");
            }
            else
            {
                Debug.LogWarning("[Run] macroGenerator is NULL -> cannot generate. Will use existing macroMap reference.");
            }
        }

        // 2) start index kuten ennen
        if (ps != null)
        {
            int startIndex = RunStatePersistence.GetRunStartIndex(macroMap);

            pd.macroIndex = startIndex;
            pd.lastRunSeed = RunStatePersistence.FormatStoredRunSeed(macroBuildMode, generatedSeed);
            ps.Save();
        }

        EnterMacroPhase();
    }


    public void OnContinueButtonPressed()
    {
        Debug.Log("[Debug] Continue pressed");

        // Jos shop auki -> käytä olemassa olevaa jatkoa
        if (shopPanel != null && shopPanel.activeInHierarchy)
        {
            ContinueFromShop();
            return;
        }

        // Jos alchemist auki -> käytä olemassa olevaa jatkoa
        if (alchemistPanel != null && alchemistPanel.activeInHierarchy)
        {
            ContinueFromAlchemist();
            return;
        }

        // Jos ollaan battlessa, lopeta se testimielessä ja palaa macroon
        if (boardView != null && boardView.gameObject.activeInHierarchy)
        {
            dragController?.CancelActiveDragImmediately();

            if (boardView != null)
            {
                boardView.Teardown(destroySelfGO: false);
                boardView.gameObject.SetActive(false);
                boardView.enabled = false;
            }

            if (macroMap != null && macroView != null)
                EnterMacroPhase();
            else
                StartNewEncounter();

            return;
        }

        // Jos makrossa tai muuten "tyhjässä" tilassa, käynnistä seuraava encounter
        StartNewEncounter();
    }

    /// <summary>
    /// Makrofase: piilota event-UI:t, näytä makrolauta ja anna pelaajan siirtää nappulaa.
    /// </summary>
    private void EnterMacroPhase()
    {
        UIDraggablePiece.s_IsDraggingAny = false;
        SetItemInventoryVisible(true);

        if (macroView == null || macroMap == null)
        {
            Debug.LogWarning("[RunController] MacroView/MacroMap puuttuu, fallback → StartNewEncounter.");
            StartNewEncounter();
            return;
        }

        var pd = PlayerService.Instance != null ? PlayerService.Instance.Data : null;
        if (pd == null)
        {
            Debug.LogWarning("[RunController] PlayerData puuttuu, fallback → StartNewEncounter.");
            StartNewEncounter();
            return;
        }

        // Varmista ettei macro-palanen jää drag-tilaan
        var piece = macroView.macroPiece;
        if (piece != null)
            piece.SendMessage("ForceStopDrag", SendMessageOptions.DontRequireReceiver);

        Debug.Log("[RunController] EnterMacroPhase()");

        // Palauta macro-canvas varmasti overlayksi
        var macroCanvas = macroView.GetComponentInParent<Canvas>(true);
        if (macroCanvas != null)
        {
            if (macroCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                Debug.LogWarning($"[Macro FIX] Canvas renderMode was {macroCanvas.renderMode}, restoring to ScreenSpaceOverlay.");
            }

            macroCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            macroCanvas.worldCamera = null;
            macroCanvas.planeDistance = 100f;

            macroCanvas.gameObject.SetActive(true);
            macroCanvas.enabled = true;

            var canvasRT = macroCanvas.GetComponent<RectTransform>();
            if (canvasRT != null)
            {
                canvasRT.localRotation = Quaternion.identity;
                canvasRT.localScale = Vector3.one;
            }
        }

        // Tyhjennä DragLayer varmuuden vuoksi
        if (macroCanvas != null)
        {
            var dragLayer = macroCanvas.transform.Find("DragLayer");
            if (dragLayer != null)
            {
                Debug.Log("[MacroPhase] Clearing DragLayer children");
                for (int i = dragLayer.childCount - 1; i >= 0; i--)
                    Destroy(dragLayer.GetChild(i).gameObject);
            }
        }

        // Sulje muut UI:t
        if (shopPanel != null) shopPanel.SetActive(false);
        if (alchemistPanel != null) alchemistPanel.SetActive(false);

        if (boardView != null)
        {
            boardView.gameObject.SetActive(false);
            boardView.enabled = false;
        }

        // Palauta macroviewn kaikki tärkeät osat näkyviin
        macroView.gameObject.SetActive(true);

        if (macroView.cellsRoot != null)
            macroView.cellsRoot.gameObject.SetActive(true);

        if (macroView.loadoutRoot != null)
            macroView.loadoutRoot.gameObject.SetActive(true);

        if (macroView.loadoutGrid != null)
            macroView.loadoutGrid.gameObject.SetActive(true);

        if (macroView.macroPiece != null)
            macroView.macroPiece.gameObject.SetActive(true);

        // Pakota RectTransform järkevään tilaan
        var rt = macroView.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
        }

        Debug.Log($"[RunController] MacroView={macroView.name}, activeBefore={macroView.gameObject.activeSelf}");

        macroView.SetVisible(true);
        macroView.Init(macroMap, pd.macroIndex);
        macroView.OnAdvance = HandleMacroAdvance;

        if (macroView.macroPiece != null)
            macroView.macroPiece.SuppressDragForSeconds(0.15f);

        Debug.Log($"[RunController] MacroView activeAfter={macroView.gameObject.activeSelf}");

        if (macroView.macroPiece != null)
            UIDraggablePiece.EnsureIconVisible(macroView.macroPiece.gameObject);

        var drag = FindObjectOfType<DragController>();
        if (drag != null)
            drag.enabled = true;

        var dragPieces = GameObject.FindObjectsOfType<UIDraggablePiece>(true);
        foreach (var d in dragPieces)
            d.SendMessage("ForceStopDrag", SendMessageOptions.DontRequireReceiver);

        UIDraggablePiece.s_IsDraggingAny = false;

        // Debug visibility
        var parents = macroView.GetComponentsInParent<Transform>(true);
        for (int i = 0; i < parents.Length; i++)
        {
            var t = parents[i];
            Debug.Log($"[Macro VIS] parent={t.name} activeSelf={t.gameObject.activeSelf} activeInHierarchy={t.gameObject.activeInHierarchy}");
        }

        var cgList = macroView.GetComponentsInParent<CanvasGroup>(true);
        for (int i = 0; i < cgList.Length; i++)
        {
            var cg = cgList[i];
            Debug.Log($"[Macro VIS] CanvasGroup on {cg.gameObject.name}: alpha={cg.alpha} interactable={cg.interactable} blocksRaycasts={cg.blocksRaycasts}");
        }

        if (macroCanvas != null)
        {
            Debug.Log($"[Macro VIS] Canvas name={macroCanvas.name} enabled={macroCanvas.enabled} activeInHierarchy={macroCanvas.gameObject.activeInHierarchy} renderMode={macroCanvas.renderMode} sortingOrder={macroCanvas.sortingOrder}");
            Debug.Log($"[Macro VIS] Canvas worldCamera={(macroCanvas.worldCamera != null ? macroCanvas.worldCamera.name : "NULL")}");
        }

        if (rt != null)
        {
            Debug.Log($"[Macro VIS] RectTransform anchoredPos={rt.anchoredPosition} localPos={rt.localPosition} sizeDelta={rt.sizeDelta} scale={rt.localScale}");
        }

        _debugCurrentPhase = "Macro";
        _debugEncounterName = "-";
        _debugEncounterSource = "Macro";
        RefreshDebugOverlay();
    }



    /// <summary>
    /// Kutsutaan kun macroPiece siirtyy seuraavaan ruutuun.
    /// </summary>
    private void HandleMacroAdvance(int newIndex)
    {
        var ps = PlayerService.Instance;
        if (ps != null)
        {
            ps.Data.macroIndex = newIndex;
            ps.Save();
        }

        if (macroView != null)
            macroView.gameObject.SetActive(false);

        if (macroMap == null)
        {
            Debug.LogWarning("[RunController] MacroMap puuttuu, fallback → StartNewEncounter.");
            StartNewEncounter();
            return;
        }

        var tile = macroMap.GetTile(newIndex);

        int columns = macroMap.columns;
        int row = newIndex / columns;

        _pendingBattleDifficulty = GetBattleDifficultyForTile(row, tile, isBoss: false);
        _pendingShopTier = GetShopTierForTile(row, tile);

        LogMacroDifficulty(tile, row, _pendingBattleDifficulty, _pendingShopTier, isBoss: false);

        Debug.Log(
            $"[RunController] MacroAdvance index={newIndex}, row={row}, type={tile.type}, " +
            $"battleDiff={_pendingBattleDifficulty}, shopTier={_pendingShopTier}"
        );

        _debugCurrentPhase = "MacroAdvance";
        _debugEncounterName = tile.type.ToString();
        _debugEncounterSource = "MacroTile";
        RefreshDebugOverlay();

        OpenEventFor(tile);
    }

    private void ExitMacroPhaseUI()
    {
        if (macroView == null) return;

        Debug.Log("[RunController] ExitMacroPhaseUI()");
        macroView.SetVisible(false);
    }



    /// <summary>
    /// Makroruudun määrittämä eventti.
    /// </summary>
    private void OpenEventFor(MacroTileDef tile)
    {
        playerService?.SetLastMacroEvent(tile.type.ToString());

        bool opensAnotherView =
            tile.type == MacroEventType.Battle ||
            tile.type == MacroEventType.Boss ||
            tile.type == MacroEventType.Shop ||
            tile.type == MacroEventType.Alchemist ||
            tile.type == MacroEventType.Rest;

        if (opensAnotherView)
            ExitMacroPhaseUI();

        switch (tile.type)
        {
            case MacroEventType.Battle:
                {
                    // 1) Yritä preset-poolia (scripted/handmade/preset tierit)
                    _pendingEncounterOverride = battlePool != null ? battlePool.Pick(_pendingBattleDifficulty) : null;

                    if (_pendingEncounterOverride != null)
                    {
                        Debug.Log($"[Macro] Battle tier={_pendingBattleDifficulty} picked preset={_pendingEncounterOverride.name}");
                        StartNewEncounter();
                        break;
                    }

                    // 2) Muuten budjetti → EnemySpec (Mode.Slots) (generoit tämän itse)
                    //    Tämä ohittaa presetin ja käyttää LoadoutAssembler + drop-spawn logiikkaa.
                    _pendingEnemySpecOverride = BuildEnemySpecFromBudget(_pendingBattleDifficulty);

                    Debug.Log($"[Macro] Battle tier={_pendingBattleDifficulty} picked budget spec (mode={_pendingEnemySpecOverride?.mode})");
                    StartNewEncounter();
                    break;
                }

            case MacroEventType.Boss:
                {
                    var ps = PlayerService.Instance;
                    int currentIndex = ps != null ? ps.Data.macroIndex : 0;
                    int row = (macroMap != null && macroMap.columns > 0)
                        ? currentIndex / macroMap.columns
                        : 0;

                    int bossTier = GetBattleDifficultyForTile(row, tile, isBoss: true);

                    LogMacroDifficulty(tile, row, bossTier, _pendingShopTier, isBoss: true);

                    _pendingEncounterOverride = bossPool != null ? bossPool.Pick(bossTier) : null;
                    Debug.Log($"[Macro] Boss tier={bossTier} picked={(_pendingEncounterOverride ? _pendingEncounterOverride.name : "NULL")}");
                    StartNewEncounter();
                    break;
                }

            case MacroEventType.Shop:
                OpenShop();
                break;

            case MacroEventType.Alchemist:
                OpenAlchemist();
                break;

            case MacroEventType.Rest:
                _debugCurrentPhase = "Rest";
                _debugEncounterName = "Hermit Rest";
                _debugEncounterSource = "Rest";
                RefreshDebugOverlay();

                // dialogue -> palauttaa EnterMacroPhase callbackilla
                if (dialogue != null && hermitRestDialogue != null)
                    dialogue.StartDialogue(hermitRestDialogue, "Hermit", EnterMacroPhase);
                else
                    EnterMacroPhase();
                break;

            case MacroEventType.RandomEvent:
            case MacroEventType.None:
            default:
                EnterMacroPhase();
                break;
        }
    }


    // RunController.cs
    private EnemySpec BuildEnemySpecFromBudget(int tier)
    {
        var tuning = GetBattleTuningForTier(tier);

        int budget = tuning.budget;
        int capCost = tuning.capCost;
        float cheapBiasPower = tuning.cheapPieceBiasPower;

        Debug.Log(
            $"[Balance] BuildEnemySpecFromBudget tier={tier} " +
            $"budget={budget} capCost={capCost} cheapBias={cheapBiasPower:F2}"
        );

        var all = (catalog != null && catalog.pieces != null)
            ? catalog.pieces
            : new List<PieceDefSO>();

        var candidates = all
            .Where(p => p != null)
            .Where(p => !string.IsNullOrEmpty(p.typeName))
            .Where(p => p.typeName != "King")
            .Where(p => p.cost > 0 && p.cost <= capCost)
            .ToList();

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[Balance] No candidates found for procedural enemy build -> fallback Pawn.");

            return new EnemySpec
            {
                mode = EnemySpec.Mode.Slots,
                blackSlots = new List<string> { "Pawn" },
                useDropPlacement = true,
                forbidWhiteAndAllyRows = 3,
                backBiasPower = 2f,
                fallbackFillBlackPawnsRow = false
            };
        }

        var picks = new List<string>();

        int originalBudget = budget;
        int safety = 0;

        while (budget > 0 && safety++ < 200)
        {
            var fit = candidates.Where(p => p.cost <= budget).ToList();
            if (fit.Count == 0)
                break;

            float totalW = 0f;
            foreach (var p in fit)
                totalW += 1f / Mathf.Pow(p.cost, cheapBiasPower);

            float roll = UnityEngine.Random.value * totalW;
            PieceDefSO chosen = fit[0];

            foreach (var p in fit)
            {
                float w = 1f / Mathf.Pow(p.cost, cheapBiasPower);
                if (roll < w)
                {
                    chosen = p;
                    break;
                }
                roll -= w;
            }

            picks.Add(chosen.typeName);
            budget -= chosen.cost;
        }

        if (picks.Count == 0)
            picks.Add("Pawn");

        Debug.Log(
            $"[Balance] Procedural enemy result tier={tier} " +
            $"startBudget={originalBudget} remainingBudget={budget} " +
            $"pieceCount={picks.Count} picks=[{string.Join(",", picks)}]"
        );

        return new EnemySpec
        {
            mode = EnemySpec.Mode.Slots,
            blackSlots = picks,
            useDropPlacement = true,
            forbidWhiteAndAllyRows = 3,
            backBiasPower = 2.2f,
            fallbackFillBlackPawnsRow = false,
            fallbackBlackPawnsRelY = 1
        };
    }


    // ---------- SLOTIT ----------

    private void EnsureSlotsOnce(int count)
    {
        var ps = PlayerService.Instance;
        var pd = ps != null ? ps.Data : null;
        if (ps == null || pd == null) return;

        var desiredSlots = ps.GetLoadoutSlotPieceIds(count).ToList();
        bool rebuilt = false;

        if (!SlotListsMatch(pd.loadoutSlots, desiredSlots))
        {
            ps.SetLoadoutSlotPieceIds(desiredSlots, count);
            desiredSlots = ps.GetLoadoutSlotPieceIds(count).ToList();
            rebuilt = true;
        }

        if (rebuilt)
            Debug.Log("[RunController] Rebuilt loadout slot mirror from player state.");

        string S(string x) => string.IsNullOrEmpty(x) ? "-" : x;
        Debug.Log($"[RunController] loadoutSlots[{desiredSlots.Count}] = [{string.Join(",", desiredSlots.Select(S))}]");
    }

    private static bool SlotListsMatch(IReadOnlyList<string> current, IReadOnlyList<string> desired)
    {
        if (current == null || desired == null)
            return false;
        if (current.Count != desired.Count)
            return false;

        for (int i = 0; i < current.Count; i++)
        {
            var left = current[i] ?? string.Empty;
            var right = desired[i] ?? string.Empty;
            if (!string.Equals(left, right, System.StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private void TeardownPrevious()
    {
        SetItemInventoryVisible(true);

        // Bestiary: detach from old GameState
        if (_bestiaryHooks != null)
            _bestiaryHooks.Detach();

        if (_state != null)
        {
            _state.OnPieceMoved -= HandlePieceMoved;
            _state.OnPieceCaptured -= HandlePieceCaptured;
            _state.OnGameEnded -= OnGameEnded;
        }

        if (boardView != null)
        {
            boardView.Teardown(destroySelfGO: false);
            boardView.gameObject.SetActive(false);
            boardView.enabled = false;
        }
    }

    private void HandlePieceMoved(Coord from, Coord to, Piece piece)
    {
        if (piece == null || playerService == null || _state == null)
            return;

        if (piece.Owner != "white")
            return;

        if (!piece.IsInjured)
            return;

        if (_state.IsGameOver)
            return;

        RequestInjuredMoveRoll(
            piece,
            from,
            to,
            onSuccess: () =>
            {
                Debug.Log($"[Injury] {piece.TypeName} survived after moving {from} -> {to}");
            },
            onFail: () =>
            {
                Debug.Log($"[Injury] {piece.TypeName} died after moving {from} -> {to}");

                dragController?.CancelActiveDragImmediately();
                ApplyPersistentDefeat(piece, permanentDeath: true);

                var current = _state.Get(to);
                if (current != null &&
                    current.Owner == piece.Owner &&
                    ((!string.IsNullOrEmpty(piece.InstanceId) && current.InstanceId == piece.InstanceId) ||
                     (string.IsNullOrEmpty(piece.InstanceId) && current.TypeName == piece.TypeName)))
                {
                    _state.Set(to, null);
                }
                else
                {
                    Debug.LogWarning(
                        $"[Injury] Expected injured mover at {to} but found {(current != null ? $"{current.Owner}:{current.TypeName}#{current.InstanceId ?? "-"}" : "null")}");
                }

                _state.CheckGameEnd();

                if (boardView != null)
                    boardView.RefreshFromState();
            }
        );
    }

    private void RequestRoll(
    string label,
    int sides,
    int targetValue,
    int modifier,
    RollVisualType visualType,
    System.Action onSuccess,
    System.Action onFail)
    {
        if (RandomResolutionService.Instance == null)
        {
            Debug.LogWarning("[Roll] RandomResolutionService missing, resolving as success.");
            onSuccess?.Invoke();
            return;
        }

        RandomResolutionService.Instance.RequestRoll(new RandomRollRequest
        {
            sides = sides,
            targetValue = targetValue,
            modifier = modifier,
            higherOrEqualWins = true,
            visualType = visualType,
            label = label,
            onResolved = value =>
            {
                Debug.Log($"[Roll] {label} resolved with {value}");
            },
            onSuccess = onSuccess,
            onFail = onFail
        });
    }

    private void RequestInjuredMoveRoll(Piece piece, Coord from, Coord to, System.Action onSuccess, System.Action onFail)
    {
        RequestRoll(
            label: $"{piece.TypeName} injured movement",
            sides: 2,
            targetValue: 2,
            modifier: 0,
            visualType: RollVisualType.Coin,
            onSuccess: onSuccess,
            onFail: onFail
        );
    }

    private void RequestPoisonRoll(Piece piece, System.Action onSuccess, System.Action onFail)
    {
        RequestRoll(
            label: $"{piece.TypeName} poison check",
            sides: 6,
            targetValue: 4,   // 4+
            modifier: 0,
            visualType: RollVisualType.Die,
            onSuccess: onSuccess,
            onFail: onFail
        );
    }

    private void RequestTrapEvadeRoll(Piece piece, System.Action onSuccess, System.Action onFail)
    {
        RequestRoll(
            label: $"{piece.TypeName} trap evade",
            sides: 6,
            targetValue: 5,   // 5+
            modifier: 0,
            visualType: RollVisualType.Die,
            onSuccess: onSuccess,
            onFail: onFail
        );
    }

    private void ApplyRuntimeInjuriesToWhitePieces()
    {
        if (_state == null || playerService == null || playerService.Data == null)
            return;

        bool hasAuthoritativeInstanceModel = HasAuthoritativeRuntimePieceState();
        var budget = BuildLegacyRuntimeInjuryBudget(hasAuthoritativeInstanceModel, playerService.Data.injuredPieces);

        foreach (var c in _state.AllCoords())
        {
            var p = _state.Get(c);
            if (p == null || p.Owner != "white")
                continue;

            p.IsInjured = false;

            if (!string.IsNullOrEmpty(p.InstanceId))
            {
                if (playerService.HasPersistentStatus(p.InstanceId, PlayerInstanceSync.WoundedStatusId))
                {
                    p.IsInjured = true;
                    Debug.Log($"[Injury] Applied runtime injury to {p.TypeName}#{p.InstanceId} at {c}");
                }
                continue;
            }

            if (hasAuthoritativeInstanceModel)
            {
                Debug.LogWarning(
                    $"[Injury] Missing instance id for {p.TypeName} at {c} while authoritative instance model is active; skipping legacy injury fallback.");
                continue;
            }

            if (budget.TryGetValue(p.TypeName, out var count) && count > 0)
            {
                p.IsInjured = true;
                budget[p.TypeName] = count - 1;
                Debug.Log($"[Injury] Applied runtime injury to {p.TypeName} at {c}");
            }
        }
    }

    // ---------- PELI → MACRO ----------

    private void OnGameEnded(GameEndInfo info)
    {
        SetItemInventoryVisible(true);

        bool playerWon = info.WinnerColor == "white";

        if (playerWon)
        {
            if (_returningToMacro) return;
            _returningToMacro = true;
            StartCoroutine(CoHandleWinAndReturnToMacro());
        }
        else
        {
            Debug.Log("[Run] Player lost → hard reset run + restart");
            ResetRunAndRestart();
        }
    }

    private IEnumerator CoHandleWinAndReturnToMacro()
    {
        var reward = 10;
        PlayerService.Instance.AddCoins(reward);

        // Lopeta mahdollinen aktiivinen drag ENNEN kuin battle-view puretaan
        dragController?.CancelActiveDragImmediately();

        // Anna ApplyMove/FinishDrag stackin purkautua
        yield return null;

        if (boardView != null)
        {
            boardView.Teardown(destroySelfGO: false);
            boardView.gameObject.SetActive(false);
            boardView.enabled = false;
        }

        yield return null;

        EnterMacroPhase();
        _returningToMacro = false;
    }

    // ---------- SHOP ----------

    private void OpenShop()
    {
        Debug.Log($"[OpenShop] Open shop with macroShopTier={_pendingShopTier}");
        SetItemInventoryVisible(true);

        // 0) Estä pelilaudan input
        if (boardView != null) boardView.enabled = false;
        var drag = FindObjectOfType<DragController>();
        if (drag != null) { drag.StopAllCoroutines(); drag.enabled = false; }

        // 1) Täytä slotit kerran jos tyhjät
        EnsureSlotsOnce(Slots);

        // 2) Diagnoosi mitä sloteissa on
        var slotPreview = PlayerService.Instance.GetLoadoutSlotPieceIds(Slots).ToList();
        Debug.Log($"[ShopOpen] slots={slotPreview.Count} sample=[{string.Join(",", slotPreview.Take(8).Select(x => string.IsNullOrEmpty(x) ? "-" : x))}]");

        // 3) Loadout-grid pystyyn
        if (loadoutView != null)
        {
            loadoutView.BuildIfNeeded();
            loadoutView.RefreshAll();
        }

        // 4) Resolvaa shop-näkymä
        ShopGridView shop = shopView;
        if (shop == null)
        {
            if (shopPanel != null)
                shop = shopPanel.GetComponentInChildren<ShopGridView>(true);
            if (shop == null)
                shop = FindObjectOfType<ShopGridView>(true); // viimeinen oljenkorsi
        }

        // 5) Resolvaa depsit
        var svc = PlayerService.Instance != null ? PlayerService.Instance : FindObjectOfType<PlayerService>(true);
        var pool = defaultShopPool;

        // 6) Selkeä DI-loki ennen setuppia
        var svcName = svc != null ? svc.name : "NULL";
        var poolName = pool != null ? pool.name : "NULL";
        var shopPath = shop != null ? shop.gameObject.scene.name + "/" + shop.gameObject.name : "NULL";
        Debug.Log($"[OpenShop] DI → svc={svcName}, pool={poolName}, shop={shopPath}");

        if (shop == null) { Debug.LogError("[OpenShop] ShopGridView puuttuu scenestä."); return; }
        if (svc == null) { Debug.LogError("[OpenShop] PlayerService puuttuu."); return; }
        if (pool == null) { Debug.LogError("[OpenShop] ShopPoolSO (defaultShopPool) puuttuu. Aseta Inspectorissa."); return; }

        // 7) Anna depsit ENSIN
        shop.Setup(svc, pool);

        // 8) Aktivoi UI
        if (shopPanel != null) shopPanel.SetActive(true);

        _debugCurrentPhase = "Shop";
        _debugEncounterName = "-";
        _debugEncounterSource = "Shop";
        RefreshDebugOverlay();
    }

    // UI-nappi: “Jatka” kaupan jälkeen
    public void ContinueFromShop()
    {
        var ps = PlayerService.Instance;
        if (ps != null)
        {
            ps.PersistCurrentLoadoutState();
        }

        // 2) Sulje shop
        if (shopPanel != null) shopPanel.SetActive(false);
        SetItemInventoryVisible(true);
        dragController?.CancelActiveDragImmediately();
        UIDraggablePiece.s_IsDraggingAny = false;

        StartCoroutine(CoResumeAfterOverlayClosed());
    }



    // Alchemist ui

    private void OpenAlchemist()
    {
        Debug.Log("[OpenAlchemist] Open alchemist encounter");
        SetItemInventoryVisible(true);

        // 0) Estä pelilaudan input (varmuus)
        if (boardView != null) boardView.enabled = false;
        var drag = FindObjectOfType<DragController>();
        if (drag != null) { drag.StopAllCoroutines(); drag.enabled = false; }

        // 1) Täytä slotit kerran jos tyhjät
        EnsureSlotsOnce(Slots);

        // 2) VALITSE alchemist-loadout view (vaihtoehto A)
        var lv = alchemistLoadoutView != null ? alchemistLoadoutView : null;
        if (lv == null)
        {
            Debug.LogError("[OpenAlchemist] alchemistLoadoutView puuttuu (inspector).");
            return;
        }

        // 3) Resolvaa alchemistView
        var view = alchemistView;
        if (view == null)
        {
            if (alchemistPanel != null)
                view = alchemistPanel.GetComponentInChildren<AlchemistEncounterView>(true);
            if (view == null)
                view = FindObjectOfType<AlchemistEncounterView>(true);
        }

        if (view == null) { Debug.LogError("[OpenAlchemist] AlchemistEncounterView puuttuu."); return; }
        if (catalog == null) { Debug.LogError("[OpenAlchemist] GameCatalogSO puuttuu RunControllerista."); return; }
        if (amalgamBaseDef == null) { Debug.LogError("[OpenAlchemist] amalgamBaseDef puuttuu (aseta Amalgam.asset)."); return; }

        // 4) Aktivoi UI-paneeli (shop pois OK nyt)
        if (shopPanel != null) shopPanel.SetActive(false);
        if (alchemistPanel != null) alchemistPanel.SetActive(true);

        // 5) Loadout-grid pystyyn (MUTTA alchemistLoadoutView:lla)
        lv.gameObject.SetActive(true);
        lv.BuildIfNeeded();
        lv.RefreshAll();

        // 6) Setup view (syötä LV + runtime registry tähän)
        Shakki.Core.IRuntimeRulesRegistry registry = _rules as Shakki.Core.IRuntimeRulesRegistry;
        view.Setup(lv, catalog, amalgamBaseDef, registry);

        Debug.Log($"[OpenAlchemist] registry={(registry != null ? "OK" : "NULL")} rulesType={_rules?.GetType().Name}");



        // 7) Wire loadout-slotit alchemistille (päivitetty helper-signature)
        WireLoadoutDropSlotsToAlchemist(lv, view);

        Debug.Log($"[OpenAlchemist] Using loadoutView={lv.name}, active={lv.gameObject.activeInHierarchy}");
        Debug.Log($"dragLayer={(lv.dragLayer != null ? lv.dragLayer.name : "NULL")}");

        _debugCurrentPhase = "Alchemist";
        _debugEncounterName = "-";
        _debugEncounterSource = "Alchemist";
        RefreshDebugOverlay();
    }


    private void WireLoadoutDropSlotsToAlchemist(LoadoutGridView lv, AlchemistEncounterView view)
    {
        if (lv == null || view == null) return;
        lv.BuildIfNeeded();
        var drops = lv.GetComponentsInChildren<DropSlot>(true);
        foreach (var d in drops)
            if (d != null && d.kind == SlotKind.Loadout)
                d.alchemistView = view;
    }


    public void ContinueFromAlchemist()
    {
        var ps = PlayerService.Instance;
        if (ps != null)
        {
            ps.PersistCurrentLoadoutState();
        }

        // Sulje UI
        if (alchemistPanel != null) alchemistPanel.SetActive(false);
        dragController?.CancelActiveDragImmediately();
        UIDraggablePiece.s_IsDraggingAny = false;

        StartCoroutine(CoResumeAfterOverlayClosed());
    }

    private IEnumerator CoResumeAfterOverlayClosed()
    {
        yield return null;

        if (macroMap != null && macroView != null)
            EnterMacroPhase();
        else
            StartNewEncounter();

        RefreshDebugOverlay();
    }

    // RunController.cs
    private static void EnforceBlackKingInDropSpecIfManyPieces(EnemySpec spec, int threshold = 5)
    {
        if (spec == null) return;

        // Tämä koskee nimenomaan slot-pohjaisia / drop-pohjaisia speksejä
        if (spec.mode != EnemySpec.Mode.Slots) return;

        if (spec.blackSlots == null)
            spec.blackSlots = new List<string>();

        int planned = 0;
        for (int i = 0; i < spec.blackSlots.Count; i++)
        {
            if (!string.IsNullOrEmpty(spec.blackSlots[i]))
                planned++;
        }

        if (planned < threshold) return;

        bool hasKing = false;
        for (int i = 0; i < spec.blackSlots.Count; i++)
        {
            if (spec.blackSlots[i] == "King") { hasKing = true; break; }
        }

        if (!hasKing)
        {
            // Laita King listan alkuun niin “tärkein” palanen tulee mukaan varmasti.
            spec.blackSlots.Insert(0, "King");
            Debug.LogWarning($"[RunController] DropSpec enforced: plannedBlack={planned} >= {threshold} -> inserted black King into EnemySpec.");
        }
    }

 

    private static void ForceBlackKingToBackRank(EncounterSO enc, int boardWidth, int boardHeight)
    {
        if (enc == null || enc.spawns == null || enc.spawns.Count == 0) return;

        int kingIndex = -1;
        for (int i = 0; i < enc.spawns.Count; i++)
        {
            var s = enc.spawns[i];
            if (s.owner == "black" && s.pieceId == "King")
            {
                kingIndex = i;
                break;
            }
        }
        if (kingIndex < 0) return;

        int targetY = boardHeight - 1;

        var occupied = new HashSet<(int x, int y)>();
        for (int i = 0; i < enc.spawns.Count; i++)
        {
            if (i == kingIndex) continue;

            var s = enc.spawns[i];
            if (s.owner == "black")
                occupied.Add((s.x, s.y));
        }

        int mid = boardWidth / 2;
        int chosenX = -1;

        for (int dx = 0; dx < boardWidth; dx++)
        {
            int x1 = mid - dx;
            int x2 = mid + dx;

            if (x1 >= 0 && x1 < boardWidth && !occupied.Contains((x1, targetY)))
            {
                chosenX = x1;
                break;
            }

            if (x2 >= 0 && x2 < boardWidth && !occupied.Contains((x2, targetY)))
            {
                chosenX = x2;
                break;
            }
        }

        if (chosenX < 0)
        {
            Debug.LogWarning("[RunController] Wanted to force black King to back rank but no free slot on that rank. Leaving as-is.");
            return;
        }

        var king = enc.spawns[kingIndex];
        king.x = chosenX;
        king.y = targetY;
        enc.spawns[kingIndex] = king;

        Debug.Log($"[RunController] Forced black King to absolute back rank at ({chosenX},{targetY}).");
    }




    // ---------- ENCOUNTERIN RAKENNUS ----------

    private void StartNewEncounter()
    {
        Debug.Log($"[RunController] StartNewEncounter with macroDifficulty={_pendingBattleDifficulty}");
        TeardownPrevious();

        Debug.Log($"[RunController] StartNewEncounter resolver={_rules?.GetType().Name}");

        if (_state != null) _state.OnGameEnded -= OnGameEnded;

        // 1) Luo GameState (template → custom geom, muuten width/height)
        if (template != null)
        {
            var allowed = template.BuildAllowedMask();
            var tags = template.BuildTags(allowed, seedOverride);
            var geom = new GridGeometry(template.width, template.height, allowed);
            _state = new GameState(geom, tags);
        }
        else
        {
            var geom = new GridGeometry(width, height);
            _state = new GameState(geom);
        }

        // 2) Valitse / rakenna Encounter
        EncounterSO enc = null;

        


        // --- A) Macro-eventin antama ENEMY SPEC (budjetti / slots) ---
                if (_pendingEnemySpecOverride != null)
        {
            var spec = _pendingEnemySpecOverride;
            _pendingEnemySpecOverride = null;

            _debugEncounterSource = "PendingEnemySpec";

            EnforceBlackKingInDropSpecIfManyPieces(
                spec,
                threshold: runBalance != null ? runBalance.forceBlackKingThreshold : 5
                );

            if (slotMap != null && PlayerService.Instance != null && PlayerService.Instance.Data != null)
            {
                EnsureSlotsOnce(16);
                var pdata = PlayerService.Instance.Data;

                // ✅ Drop-assembleri (tarvitsee board dimensiot)
                enc = LoadoutAssembler.BuildFromPlayerDataDrop(
                    pdata,
                    slotMap,
                    spec,
                    boardWidth: _state.Width,
                    boardHeight: _state.Height,
                    implicitKingId: "King",
                    totalSlots: 16
                );
                // ✅ Budget-battle on kingless, ELLEI mustia ole >=5 (silloin aina king)
                enc.requireBlackKing = (spec.mode == EnemySpec.Mode.Slots && spec.blackSlots != null && spec.blackSlots.Count(id => !string.IsNullOrEmpty(id)) >= 5);


                Debug.Log($"[RunController] Built encounter from player loadout + pending EnemySpec DROP (mode={spec.mode}).");
            }
            else
            {
                enc = BuildMinimalFallbackEncounter();
                Debug.LogWarning("[RunController] slotMap/playerData missing -> using minimal fallback encounter (pending EnemySpec ignored).");
            }

        }


        // --- B) Macro-eventin valitsema PRESET encounter (nykyinen logiikka) ---
        else if (_pendingEncounterOverride != null)
        {
            var enemyPreset = _pendingEncounterOverride;
            _pendingEncounterOverride = null;

            _debugEncounterSource = "PresetEncounter";

            if (slotMap != null && PlayerService.Instance != null && PlayerService.Instance.Data != null)
            {
                EnsureSlotsOnce(16);
                var pdata = PlayerService.Instance.Data;

                var spec = new EnemySpec
                {
                    mode = EnemySpec.Mode.PresetEncounter,
                    preset = enemyPreset
                };

                enc = LoadoutAssembler.BuildFromPlayerDataDrop(
                    pdata,
                    slotMap,
                    spec,
                    boardWidth: _state.Width,
                    boardHeight: _state.Height,
                    implicitKingId: "King",
                    totalSlots: 16
                );

                // ✅ Budget-enemy: king ei pakollinen
                enc.requireBlackKing = false;

                Debug.Log($"[RunController] Built encounter from player loadout + enemy preset '{enemyPreset.name}'.");
            }
            else
            {
                enc = enemyPreset;
                Debug.LogWarning("[RunController] slotMap/playerData missing -> using enemy preset as-is (white side may be empty).");
            }
        }

        // --- C) Inspector override (debug) ---
        else if (_pendingEncounterOverride != null)
        {
            var enemyPreset = _pendingEncounterOverride;
            _pendingEncounterOverride = null;

            enc = enemyPreset;

            Debug.Log($"[RunController] TEMP: using enemy preset directly '{enemyPreset.name}' without LoadoutAssembler.");
        }

        // --- D) Muuten dynaamisesti normaalilla enemySpecillä ---
        else
        {
            _debugEncounterSource = "DefaultEnemySpec";

            EnsureSlotsOnce(16);
            var pdata = PlayerService.Instance.Data;

            if (slotMap != null)
            {
                var spec = enemySpec ?? new EnemySpec { mode = EnemySpec.Mode.Classic };

                EnforceBlackKingInDropSpecIfManyPieces(
    spec,
    threshold: runBalance != null ? runBalance.forceBlackKingThreshold : 5
);

                enc = LoadoutAssembler.BuildFromPlayerDataDrop(
                    pdata,
                    slotMap,
                    spec,
                    boardWidth: _state.Width,
                    boardHeight: _state.Height,
                    implicitKingId: "King",
                    totalSlots: 16
                );
            }
            else
            {
                enc = BuildMinimalFallbackEncounter();
            }
        }

        if (enc == null)
        {
            Debug.LogError("[RunController] Encounter build failed -> using minimal fallback.");
            _debugEncounterSource = "Fallback";
            enc = BuildMinimalFallbackEncounter();
        }



        Debug.Log($"[RunController] EndRules requireWhiteKing={_state.RequireWhiteKing} requireBlackKing={_state.RequireBlackKing}");


        // --- YHTEINEN: defaultit / turvallisuus ---
        // SlotMap-pohjaiset encit on käytännössä aina relativeRanks = true.
        // Älä kuitenkaan jyrää jos joku scripted/preset haluaa toisin.
        // (Jos haluat väkisin: pidä tämä.)
       

        // Fallbackfill: vain mustat pawnit, ja vain jos encounterissa pyydetty.
        // (ÄLÄ enää pakota aina true täällä.)

        RehydrateRuntimeEncounterDefs(enc);
        // Jos haluat yleisdefaultin budget-battleille: aseta se EnemySpecissä/Assemblerissa.
        // enc.fillBlackPawnsAtY ja enc.blackPawnsY jätetään Encounter/Assemblerin vastuulle.

        ForceBlackKingToBackRank(enc, _state.Width, _state.Height);

        // 3) Sijoittele nappulat
        EncounterLoader.Apply(_state, enc, catalog);
        ApplyRuntimeInjuriesToWhitePieces();



        // ✅ DROP-FIX: päätä “king required” vasta spawnin jälkeen.
        // Sääntö: jos kuningas on laudalla -> sen kaappaus päättää.
        // Jos kuningasta ei ole -> annihilation.
        _state.RequireWhiteKing = enc.requireWhiteKing || _state.HasKingOnBoard("white");
        _state.RequireBlackKing = enc.requireBlackKing || _state.HasKingOnBoard("black");

        Debug.Log($"[RunController] EndRules(post-spawn) requireW={_state.RequireWhiteKing} requireB={_state.RequireBlackKing} " +
                  $"hasWKing={_state.HasKingOnBoard("white")} hasBKing={_state.HasKingOnBoard("black")}");


        int whiteCount = 0, blackCount = 0, whiteKings = 0, blackKings = 0;
        foreach (var c in _state.AllCoords())
        {
            var p = _state.Get(c);
            if (p == null) continue;
            if (p.Owner == "white") { whiteCount++; if (p.TypeName == "King") whiteKings++; }
            if (p.Owner == "black") { blackCount++; if (p.TypeName == "King") blackKings++; }
        }

        Debug.Log($"[EL] Counts white={whiteCount} (kings={whiteKings}) black={blackCount} (kings={blackKings}) " +
                  $"requireW={_state.RequireWhiteKing} requireB={_state.RequireBlackKing}");

        // Turvakaide: jos encounter väittää että black king vaaditaan,
        // mutta sitä ei spawnautunut, vaihda kingless-tilaan.
        if (_state.RequireBlackKing && !_state.HasKingOnBoard("black"))
        {
            Debug.LogWarning("[RunController] requireBlackKing=true but no black King on board -> forcing RequireBlackKing=false (annihilation).");
            _state.RequireBlackKing = false;
        }

        // --- Bestiary hook ---
        if (_bestiaryHooks != null)
        {
            _bestiaryHooks.Attach(_state);
            _bestiaryHooks.ScanInitialSeen();
        }

        // 4) Piirto & input
        if (boardView == null)
            boardView = FindObjectOfType<BoardView>(true);

        if (boardView != null)
        {
            boardView.SetCatalog(catalog);
            boardView.gameObject.SetActive(true);

            boardView.Init(_state, _rules, aiMode);

            boardView.CanRevealEnemyMovesOnHover = (pv) =>
            {
                if (pv == null || _bestiary == null) return false;
            #if UNITY_EDITOR
            Debug.Log($"[HoverGate] type={pv.TypeLabel} moveKnown={_bestiary.IsMoveKnown(pv.TypeLabel)}");
            #endif
                return _bestiary.IsMoveKnown(pv.TypeLabel);
            };

            boardView.CanPlayerMoveEnemyPiece = (pv) => false;
            boardView.enabled = true;
            SetItemInventoryVisible(true);
            RefreshItemInventoryUi();
        }
        else
        {
            Debug.LogError("[RunController] BoardView puuttuu scenestä – ei voida piirtää lautaa.");
            SetItemInventoryVisible(true);
        }

        // 5) Game over -kuuntelu
        _state.OnPieceMoved += HandlePieceMoved;
        _state.OnPieceCaptured += HandlePieceCaptured;
        _state.OnGameEnded += OnGameEnded;
        RefreshDebugOverlay();

    }


    private void BuildRules()
    {
        // käytä nimenomaan Core-luokkaa
        var resolver = new Shakki.Core.DefRegistryRulesResolver();

        if (catalog != null && catalog.pieces != null)
        {
            foreach (var def in catalog.pieces)
            {
                if (def == null) continue;
                resolver.RegisterOrReplace(def);
            }
        }

        _rules = resolver;

        Debug.Log($"[RunController] registry check: {_rules is Shakki.Core.IRuntimeRulesRegistry}");
    }


    private EncounterSO BuildMinimalFallbackEncounter()
    {
        var e = ScriptableObject.CreateInstance<EncounterSO>();

        e.requireWhiteKing = true;
        e.requireBlackKing = true;

        e.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = "King", x = 4, y = 0 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = "Pawn", x = 3, y = 1 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = "Pawn", x = 4, y = 1 });

        e.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = "King", x = 3, y = 7 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = "Pawn", x = 3, y = 6 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = "Pawn", x = 4, y = 6 });

        return e;
    }

    private void RefreshDebugOverlay()
    {
        if (debugOverlayText == null)
            return;

        var ps = PlayerService.Instance;
        var pd = ps != null ? ps.Data : null;

        int macroIndex = pd != null ? pd.macroIndex : -1;

        int row = -1;
        int col = -1;
        string tileType = "-";
        int difficultyOffset = 0;
        int shopOffset = 0;

        if (macroMap != null && macroMap.columns > 0 && macroIndex >= 0)
        {
            row = macroIndex / macroMap.columns;
            col = macroIndex % macroMap.columns;

            var tile = macroMap.GetTile(macroIndex);
            tileType = tile.type.ToString();
            difficultyOffset = tile.difficultyOffset;
            shopOffset = tile.shopTierOffset;
        }

        int whiteCount = 0;
        int blackCount = 0;
        int whiteKings = 0;
        int blackKings = 0;

        if (_state != null)
        {
            foreach (var c in _state.AllCoords())
            {
                var p = _state.Get(c);
                if (p == null) continue;

                if (p.Owner == "white")
                {
                    whiteCount++;
                    if (p.TypeName == "King") whiteKings++;
                }
                else if (p.Owner == "black")
                {
                    blackCount++;
                    if (p.TypeName == "King") blackKings++;
                }
            }
        }

        var tuning = GetBattleTuningForTier(_pendingBattleDifficulty);

        string loadoutSummary = "-";
        if (ps != null)
        {
            var slots = ps.GetLoadoutSlotPieceIds(Slots);
            if (slots.Count > 0)
                loadoutSummary = string.Join(",", slots.Select(x => string.IsNullOrEmpty(x) ? "-" : x));
        }

        string enemySpecSummary = "-";
        var shownSpec = _pendingEnemySpecOverride != null ? _pendingEnemySpecOverride : enemySpec;
        if (shownSpec != null)
        {
            string slots = shownSpec.blackSlots != null
                ? string.Join(",", shownSpec.blackSlots.Select(x => string.IsNullOrEmpty(x) ? "-" : x))
                : "-";

            enemySpecSummary =
                $"mode={shownSpec.mode}, " +
                $"drop={shownSpec.useDropPlacement}, " +
                $"forbidRows={shownSpec.forbidWhiteAndAllyRows}, " +
                $"backBias={shownSpec.backBiasPower:F2}, " +
                $"fallbackPawns={shownSpec.fallbackFillBlackPawnsRow}, " +
                $"fallbackRelY={shownSpec.fallbackBlackPawnsRelY}, " +
                $"blackSlots=[{slots}]";
        }

        int coins = pd != null ? pd.coins : 0;

        string text =
            $"PHASE: {_debugCurrentPhase}\n" +
            $"Encounter: {_debugEncounterName}\n" +
            $"Encounter Source: {_debugEncounterSource}\n" +
            $"Battle Tier: {_pendingBattleDifficulty}\n" +
            $"Shop Tier: {_pendingShopTier}\n" +
            $"Macro Build Mode: {macroBuildMode}\n" +
            $"Macro Index: {macroIndex}\n" +
            $"Macro Row/Col: {row}/{col}\n" +
            $"Tile Type: {tileType}\n" +
            $"Tile Difficulty Offset: {difficultyOffset}\n" +
            $"Tile Shop Offset: {shopOffset}\n" +
            $"Budget: {tuning.budget}\n" +
            $"Cap Cost: {tuning.capCost}\n" +
            $"Cheap Bias: {tuning.cheapPieceBiasPower:F2}\n" +
            $"Force King Threshold: {tuning.forceBlackKingThreshold}\n" +
            $"Board: {(_state != null ? _state.Width : 0)}x{(_state != null ? _state.Height : 0)}\n" +
            $"AI Mode: {aiMode}\n" +
            $"Require White King: {(_state != null ? _state.RequireWhiteKing : false)}\n" +
            $"Require Black King: {(_state != null ? _state.RequireBlackKing : false)}\n" +
            $"White Pieces: {whiteCount} (Kings: {whiteKings})\n" +
            $"Black Pieces: {blackCount} (Kings: {blackKings})\n" +
            $"Coins: {coins}\n" +
            $"Pending Encounter Override: {(_pendingEncounterOverride != null ? _pendingEncounterOverride.name : "-")}\n" +
            $"Pending EnemySpec Override: {(_pendingEnemySpecOverride != null ? _pendingEnemySpecOverride.mode.ToString() : "-")}\n" +
            $"Loadout Slots: {loadoutSummary}\n" +
            $"EnemySpec: {enemySpecSummary}";

        debugOverlayText.text = text;
    }

    public void OnResetButtonPressed()
    {
        ResetRunAndRestart();
    }

    public void ResetRunAndRestart()
    {
        var ps = PlayerService.Instance;
        if (ps != null)
        {
            ps.ResetRun();
            ps.Data.loadout = new List<LoadoutEntry>();
            ps.Data.loadoutSlots = null;
            ps.Data.pieceInstances = new List<PieceInstanceData>();
            ps.Data.loadoutSlotInstances = new List<LoadoutSlotInstanceData>();
            ps.Data.nextPieceInstanceNumber = 1;

            var startSlots = LoadoutModel.Expand(
                new List<LoadoutEntry>
                {
                    new LoadoutEntry { pieceId = "King", count = 1 },
                    new LoadoutEntry { pieceId = "Pawn", count = 2 },
                    new LoadoutEntry { pieceId = "Rook", count = 1 },
                },
                Slots,
                "");
            ps.SetLoadoutSlotPieceIds(startSlots, Slots);

            // --- Resetoi macroIndex kuten ennen ---
            int startIndex = RunStatePersistence.GetRunStartIndex(macroMap);

            // --- RESET BESTIARY (for testing) --- TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST
            if (_bestiaryHooks != null) _bestiaryHooks.Detach();
            if (_bestiary != null)
            {
                _bestiary.HardReset();
                Debug.Log("[Bestiary] Hard reset done.");
            }

            ps.Data.macroIndex = startIndex;
            ps.PersistCurrentLoadoutState();
        }

        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (macroMap != null && macroView != null)
            StartRun();
        else
            StartNewEncounter();

        if (alchemistPanel != null)
            alchemistPanel.SetActive(false);

        _debugCurrentPhase = "Reset";
        _debugEncounterName = "-";
        _debugEncounterSource = "ResetRun";
        RefreshDebugOverlay();
    }

    public void ForceRefreshDebugOverlay()
    {
        RefreshDebugOverlay();
    }

    public void RegisterRuntimePieceRules(PieceDefSO runtimeDef)
    {
        if (_rules is Shakki.Core.IRuntimeRulesRegistry reg)
        {
            reg.RegisterOrReplace(runtimeDef);
            Debug.Log($"[RunController] Runtime rules registered for {runtimeDef.typeName}");
        }
        else
        {
            Debug.LogWarning($"[RunController] _rules doesn't support runtime registry ({_rules?.GetType().FullName}).");
        }
    }

    private void RehydrateRuntimeEncounterDefs(EncounterSO encounter)
    {
        if (encounter?.spawns == null || catalog == null)
            return;

        foreach (var pieceId in encounter.spawns
                     .Where(sp => !string.IsNullOrEmpty(sp.pieceId))
                     .Select(sp => sp.pieceId)
                     .Distinct())
        {
            if (!pieceId.StartsWith("Amalgam_", System.StringComparison.OrdinalIgnoreCase))
                continue;

            var runtimeDef = catalog.GetPieceById(pieceId);
            if (runtimeDef == null)
            {
                Debug.LogWarning($"[RunController] Failed to rehydrate runtime encounter def: {pieceId}");
                continue;
            }

            RegisterRuntimePieceRules(runtimeDef);
        }
    }





}
