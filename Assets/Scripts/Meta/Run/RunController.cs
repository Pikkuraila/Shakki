using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Shakki.Core;
using System.Linq;
using Shakki.Meta.Bestiary;

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

    [Header("Shop Data")]
    [SerializeField] private ShopPoolSO defaultShopPool;

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
        Debug.Log($"[RunController] Start macroMap={macroMap}, macroView={macroView}");

        if (macroMap != null && macroView != null)
        {
            StartRun();
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

    // ---------- RUN + MACRO ----------

    private void StartRun()
    {
        BuildRules();

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
                macroMap = CloneMacroMap(macroPreset);
                Debug.Log($"[Run] Using macro PRESET '{macroPreset.name}' rows={macroMap.rows} cols={macroMap.columns} tiles={macroMap.tiles?.Length}");
            }
        }

        if (macroBuildMode == MacroBuildMode.GenerateRandom)
        {
            if (macroGenerator != null)
            {
                int seed = seedOverride ?? UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                macroMap = macroGenerator.Generate(seed);
                Debug.Log($"[Run] Generated macroMap seed={seed} rows={macroMap.rows} cols={macroMap.columns} tiles={macroMap.tiles?.Length}");
            }
            else
            {
                Debug.LogWarning("[Run] macroGenerator is NULL -> cannot generate. Will use existing macroMap reference.");
            }
        }

        // 2) start index kuten ennen
        var ps = PlayerService.Instance;
        if (ps != null)
        {
            var pd = ps.Data;

            int startIndex = 0;
            if (macroMap != null)
            {
                int startRow = 0;
                int startCol = macroMap.columns / 2;
                startIndex = macroMap.GetIndex(startRow, startCol);
            }

            pd.macroIndex = startIndex;
            ps.Save();
        }

        EnterMacroPhase();
    }

    private static MacroMapSO CloneMacroMap(MacroMapSO src)
    {
        var m = ScriptableObject.CreateInstance<MacroMapSO>();
        m.rows = Mathf.Max(1, src.rows);
        m.columns = Mathf.Max(1, src.columns);

        int n = m.rows * m.columns;
        m.tiles = new MacroTileDef[n];

        if (src.tiles != null)
        {
            int copy = Mathf.Min(src.tiles.Length, n);
            for (int i = 0; i < copy; i++)
                m.tiles[i] = src.tiles[i];
        }

        return m;
    }




    /// <summary>
    /// Makrofase: piilota event-UI:t, näytä makrolauta ja anna pelaajan siirtää nappulaa.
    /// </summary>
    private void EnterMacroPhase()
    {
        UIDraggablePiece.s_IsDraggingAny = false;

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
        var pd = PlayerService.Instance.Data;
        if (pd == null) return;

        bool needsRebuild = (pd.loadoutSlots == null || pd.loadoutSlots.Count != count);

        // Jos mitta täsmää mutta kaikki ovat tyhjiä JA loadoutissa on sisältöä → rakenna uudelleen
        if (!needsRebuild && pd.loadout != null && pd.loadout.Count > 0)
        {
            bool allEmpty = true;
            for (int i = 0; i < pd.loadoutSlots.Count; i++)
            {
                if (!string.IsNullOrEmpty(pd.loadoutSlots[i])) { allEmpty = false; break; }
            }
            if (allEmpty) needsRebuild = true;
        }

        if (needsRebuild)
        {
            // ❗️Oletus on tyhjä merkkijono, EI "King"
            pd.loadoutSlots = LoadoutModel.Expand(pd.loadout ?? new List<LoadoutEntry>(), count, "");
            PlayerService.Instance.Save();
            Debug.Log("[RunController] Rebuilt loadoutSlots from loadout entries.");
        }

        // Diagnoosi
        string S(string x) => string.IsNullOrEmpty(x) ? "-" : x;
        Debug.Log($"[RunController] loadoutSlots[{pd.loadoutSlots.Count}] = [{string.Join(",", pd.loadoutSlots.ConvertAll(S))}]");
    }

    private void TeardownPrevious()
    {
        // Bestiary: detach from old GameState
        if (_bestiaryHooks != null)
            _bestiaryHooks.Detach();

        if (_state != null) _state.OnGameEnded -= OnGameEnded;

        if (boardView != null)
        {
            boardView.Teardown(destroySelfGO: false);
            boardView.gameObject.SetActive(false);
            boardView.enabled = false;
        }
    }

    // ---------- PELI → MACRO ----------

    private void OnGameEnded(GameEndInfo info)
    {
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

        // 0) Estä pelilaudan input
        if (boardView != null) boardView.enabled = false;
        var drag = FindObjectOfType<DragController>();
        if (drag != null) { drag.StopAllCoroutines(); drag.enabled = false; }

        // 1) Täytä slotit kerran jos tyhjät
        EnsureSlotsOnce(Slots);

        // 2) Diagnoosi mitä sloteissa on
        var pd = PlayerService.Instance.Data;
        Debug.Log($"[ShopOpen] slots={pd.loadoutSlots?.Count ?? 0} sample=[{string.Join(",", (pd.loadoutSlots ?? new System.Collections.Generic.List<string>()).GetRange(0, System.Math.Min(8, pd.loadoutSlots?.Count ?? 0)))}]");

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
    }

    // UI-nappi: “Jatka” kaupan jälkeen
    public void ContinueFromShop()
    {
        var ps = PlayerService.Instance;
        if (ps != null)
        {
            // 1) Kompaktoi sloteista meta-listaksi ja tallenna
            ps.Data.loadout = LoadoutModel.Compact(ps.Data.loadoutSlots);
            ps.Save();
        }

        // 2) Sulje shop
        if (shopPanel != null) shopPanel.SetActive(false);

        // 3) Takaisin makrofaseen jos makrot käytössä, muuten suoraan uuteen matsiin
        if (macroMap != null && macroView != null)
        {
            EnterMacroPhase();
        }
        else
        {
            StartNewEncounter();
        }
    }



    // Alchemist ui

    private void OpenAlchemist()
    {
        Debug.Log("[OpenAlchemist] Open alchemist encounter");

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
            // Kompaktoi ja tallenna
            ps.Data.loadout = LoadoutModel.Compact(ps.Data.loadoutSlots);
            ps.Save();
        }

        // Sulje UI
        if (alchemistPanel != null) alchemistPanel.SetActive(false);

        // Takaisin makroon / fallback encounteriin
        if (macroMap != null && macroView != null)
            EnterMacroPhase();
        else
            StartNewEncounter();
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
            enc = BuildMinimalFallbackEncounter();
        }

        

        Debug.Log($"[RunController] EndRules requireWhiteKing={_state.RequireWhiteKing} requireBlackKing={_state.RequireBlackKing}");


        // --- YHTEINEN: defaultit / turvallisuus ---
        // SlotMap-pohjaiset encit on käytännössä aina relativeRanks = true.
        // Älä kuitenkaan jyrää jos joku scripted/preset haluaa toisin.
        // (Jos haluat väkisin: pidä tämä.)
       

        // Fallbackfill: vain mustat pawnit, ja vain jos encounterissa pyydetty.
        // (ÄLÄ enää pakota aina true täällä.)
        // Jos haluat yleisdefaultin budget-battleille: aseta se EnemySpecissä/Assemblerissa.
        // enc.fillBlackPawnsAtY ja enc.blackPawnsY jätetään Encounter/Assemblerin vastuulle.

        ForceBlackKingToBackRank(enc, _state.Width, _state.Height);

        // 3) Sijoittele nappulat
        EncounterLoader.Apply(_state, enc, catalog);

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
        }
        else
        {
            Debug.LogError("[RunController] BoardView puuttuu scenestä – ei voida piirtää lautaa.");
        }

        // 5) Game over -kuuntelu
        _state.OnGameEnded += OnGameEnded;
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

            // --- RESETOI LOADOUT ---
            // 1) Perus-loadout (vaihda tähän sun haluama startti)
            ps.Data.loadout = new List<LoadoutEntry>
            {
            new LoadoutEntry { pieceId = "King", count = 1 },
            new LoadoutEntry { pieceId = "Pawn", count = 2 },
            new LoadoutEntry { pieceId = "Rook", count = 1 },
            };

            // 2) Rakenna slotit tyhjillä paikoilla (16 slottia, tyhjä = "")
            ps.Data.loadoutSlots = LoadoutModel.Expand(ps.Data.loadout, 16, "");

            // --- Resetoi macroIndex kuten ennen ---
            int startIndex = 0;
            if (macroMap != null)
            {
                int startRow = 0;
                int startCol = macroMap.columns / 2;
                startIndex = macroMap.GetIndex(startRow, startCol);
            }

            // --- RESET BESTIARY (for testing) --- TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST TEST
            if (_bestiaryHooks != null) _bestiaryHooks.Detach();
            if (_bestiary != null)
            {
                _bestiary.HardReset();
                Debug.Log("[Bestiary] Hard reset done.");
            }

            ps.Data.macroIndex = startIndex;
            ps.Save();
        }

        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (macroMap != null && macroView != null)
            StartRun();
        else
            StartNewEncounter();

        if (alchemistPanel != null)
            alchemistPanel.SetActive(false);
    }

    public void RegisterRuntimePieceRules(PieceDefSO runtimeDef)
    {
        if (_rules is IRuntimeRulesRegistry reg)
        {
            reg.RegisterOrReplace(runtimeDef);
            Debug.Log($"[RunController] Runtime rules registered for {runtimeDef.typeName}");
        }
        else
        {
            Debug.LogWarning($"[RunController] _rules doesn't support runtime registry ({_rules?.GetType().FullName}).");
        }
    }





}
