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

    [Header("Macro Scaling")]
    [SerializeField] private int baseBattleDifficulty = 1;
    [SerializeField] private int baseShopTier = 1;

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

        var piece = macroView.macroPiece;
        if (piece != null)
        {
            piece.SendMessage("ForceStopDrag", SendMessageOptions.DontRequireReceiver);
        }



        Debug.Log("[RunController] EnterMacroPhase()");

        // Tyhjennä DragLayer varmuuden vuoksi
        var canvas = macroView.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            var dragLayer = canvas.transform.Find("DragLayer");
            if (dragLayer != null)
            {
                Debug.Log("[MacroPhase] Clearing DragLayer children");
                for (int i = dragLayer.childCount - 1; i >= 0; i--)
                {
                    Destroy(dragLayer.GetChild(i).gameObject);
                }
            }
        }


        var pd = PlayerService.Instance.Data;

        if (shopPanel != null) shopPanel.SetActive(false);
        if (boardView != null)
        {
            boardView.gameObject.SetActive(false);
            boardView.enabled = false;
        }

        if (macroView == null || macroMap == null)
        {
            Debug.LogWarning("[RunController] MacroView/MacroMap puuttuu, fallback → StartNewEncounter.");
            StartNewEncounter();
            return;
        }

        Debug.Log($"[RunController] MacroView={macroView.name}, activeBefore={macroView.gameObject.activeSelf}");

        macroView.SetVisible(true);
        macroView.Init(macroMap, pd.macroIndex);
        macroView.OnAdvance = HandleMacroAdvance;

        Debug.Log($"[RunController] MacroView activeAfter={macroView.gameObject.activeSelf}");

        // 🔧 VARMISTETAAN, että macropiece on oikeasti dragattava
        if (macroView.macroPiece != null)
        {
            UIDraggablePiece.EnsureIconVisible(macroView.macroPiece.gameObject);
        }

        // (valinnainen, jos sulla on DragController käytössä)
        var drag = FindObjectOfType<DragController>();
        if (drag != null)
        {
            drag.enabled = true;
        }

        var dragPieces = GameObject.FindObjectsOfType<UIDraggablePiece>();
        foreach (var d in dragPieces)
        {
            d.SendMessage("ForceStopDrag", SendMessageOptions.DontRequireReceiver);
        }
        UIDraggablePiece.s_IsDraggingAny = false;

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

        // --- SKAALAUS: battle difficulty + shop tier ---
        int columns = macroMap.columns;
        int row = newIndex / columns; // mitä alempana kartalla, sitä isompi rivi

        _pendingBattleDifficulty = baseBattleDifficulty + row + tile.difficultyOffset;
        if (_pendingBattleDifficulty < 1) _pendingBattleDifficulty = 1;

        _pendingShopTier = baseShopTier + row + tile.shopTierOffset;
        if (_pendingShopTier < 1) _pendingShopTier = 1;

        Debug.Log($"[RunController] MacroAdvance index={newIndex}, row={row}, type={tile.type}, " +
                  $"battleDiff={_pendingBattleDifficulty}, shopTier={_pendingShopTier}");

        // --- Avaa kyseinen eventti ---
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
                _pendingEncounterOverride = bossPool != null ? bossPool.Pick(_pendingBattleDifficulty) : null;
                Debug.Log($"[Macro] Boss tier={_pendingBattleDifficulty} picked={(_pendingEncounterOverride ? _pendingEncounterOverride.name : "NULL")}");
                StartNewEncounter();
                break;

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
        // budjetti: säädä vapaasti
        int budget = 2 + tier * 2; // esim tier1=4, tier2=6, tier3=8
        int capCost = Mathf.Max(1, 1 + tier); // ei liian kalliita aikaisin

        // kerää ehdokkaat catalogista
        // (oleta että catalog.pieces listaa PieceDefSO:t)
        var all = (catalog != null && catalog.pieces != null) ? catalog.pieces : new System.Collections.Generic.List<PieceDefSO>();

        // sallitaan Pawn mukaan, King ei ole pakollinen tässä systeemissä
        var candidates = all
            .Where(p => p != null)
            .Where(p => !string.IsNullOrEmpty(p.typeName))
            .Where(p => p.typeName != "King")                 // King ei automaattinen
            .Where(p => p.cost > 0 && p.cost <= capCost)      // tier-cap
            .ToList();

        // jos ei löydy mitään, fallback pawn
        if (candidates.Count == 0)
        {
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

        // pieni “minimi”: vähintään 1 nappula
        int safety = 0;
        while (budget > 0 && safety++ < 200)
        {
            // ehdokkaat jotka mahtuu jäljellä olevaan budjettiin
            var fit = candidates.Where(p => p.cost <= budget).ToList();
            if (fit.Count == 0) break;

            // painotus: halvempi yleisempi
            // w = 1 / cost^1.2
            float totalW = 0f;
            foreach (var p in fit) totalW += 1f / Mathf.Pow(p.cost, 1.2f);

            float roll = UnityEngine.Random.value * totalW;
            PieceDefSO chosen = fit[0];
            foreach (var p in fit)
            {
                float w = 1f / Mathf.Pow(p.cost, 1.2f);
                if (roll < w) { chosen = p; break; }
                roll -= w;
            }

            picks.Add(chosen.typeName);
            budget -= chosen.cost;
        }

        // jos mitään ei tullut, varmista pawn
        if (picks.Count == 0) picks.Add("Pawn");

        // (valinnainen) joskus lisätään King, mutta EI pakollinen:
        // Jos lisäät kingin, sun pitää myöhemmin muuttaa game-end logiikkaa kuten sanoit.
        // if (tier >= 2 && UnityEngine.Random.value < 0.4f) picks.Add("King");

        return new EnemySpec
        {
            mode = EnemySpec.Mode.Slots,
            blackSlots = picks,

            useDropPlacement = true,
            forbidWhiteAndAllyRows = 3,
            backBiasPower = 2.2f,

            // fallback-fill: oletuksena OFF (koska sanoit "vain silloin")
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
            var reward = 10;
            PlayerService.Instance.AddCoins(reward);

            if (boardView != null)
            {
                boardView.Teardown(destroySelfGO: false);
                boardView.gameObject.SetActive(false);
                boardView.enabled = false;
            }

            // HUOM: ei kosketa DragControlleriin

            EnterMacroPhase();
        }
        else
        {
            Debug.Log("[Run] Player lost → hard reset run + restart");
            ResetRunAndRestart();
        }
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
                // ✅ BUDGET-BATTLE = KINGLESS (annihilation)
                enc.requireBlackKing = false;

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
        else if (encounterOverride != null)
        {
            enc = encounterOverride;
        }

        // --- D) Muuten dynaamisesti normaalilla enemySpecillä ---
        else
        {
            EnsureSlotsOnce(16);
            var pdata = PlayerService.Instance.Data;

            if (slotMap != null)
            {
                var spec = enemySpec ?? new EnemySpec { mode = EnemySpec.Mode.Classic };

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
        enc.relativeRanks = true;

        // Fallbackfill: vain mustat pawnit, ja vain jos encounterissa pyydetty.
        // (ÄLÄ enää pakota aina true täällä.)
        // Jos haluat yleisdefaultin budget-battleille: aseta se EnemySpecissä/Assemblerissa.
        // enc.fillBlackPawnsAtY ja enc.blackPawnsY jätetään Encounter/Assemblerin vastuulle.

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
        e.relativeRanks = true;
        e.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = "King", x = 4, y = 0 });
        e.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = "King", x = 3, y = 7 });
        e.fillWhitePawnsAtY = true; e.whitePawnsY = 1;
        e.fillBlackPawnsAtY = true; e.blackPawnsY = 6;
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
