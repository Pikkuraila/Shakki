using System.Collections.Generic;
using UnityEngine;
using Shakki.Core;
using System.Linq;

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

    [Header("Macro")]
    [SerializeField] private MacroMapSO macroMap;
    [SerializeField] private MacroBoardView macroView;
    [SerializeField] private MacroMapGeneratorSO macroGenerator;

    [Header("Macro Scaling")]
    [SerializeField] private int baseBattleDifficulty = 1;
    [SerializeField] private int baseShopTier = 1;

    // lasketaan aina kun siirrytään uuteen macro-ruutuun
    private int _pendingBattleDifficulty = 1;
    private int _pendingShopTier = 1;


    private GameState _state;
    private IRulesResolver _rules;
    private const int Slots = 16;

    // ---------- LIFECYCLE ----------

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
        BuildRules(); // varmistus, jos haluat, tai jätä kuten on – tämä voi myös olla jo kutsuttu Startissa


        if (macroGenerator != null)
        {
            macroMap = macroGenerator.Generate();
        }


        var ps = PlayerService.Instance;
        if (ps != null)
        {
            var pd = ps.Data;

            int startIndex = 0;
            if (macroMap != null)
            {
                int startRow = 0;
                int startCol = macroMap.columns / 2; // keskikolumni (3 -> 1)
                startIndex = macroMap.GetIndex(startRow, startCol);
            }

            pd.macroIndex = startIndex;
            ps.Save();
        }

        EnterMacroPhase();
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

        macroView.gameObject.SetActive(true);
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


    /// <summary>
    /// Makroruudun määrittämä eventti.
    /// </summary>
    private void OpenEventFor(MacroTileDef tile)
    {
        switch (tile.type)
        {
            case MacroEventType.Battle:
                StartNewEncounter();
                break;

            case MacroEventType.Shop:
                OpenShop();
                break;

            case MacroEventType.Rest:
                // TODO: tee oma rest-event-UI. Nyt vaan heti takaisin makroon.
                Debug.Log("[RunController] Rest-tile → takaisin makrofaseen.");
                EnterMacroPhase();
                break;

            case MacroEventType.RandomEvent:
                // TODO: random event -paneeli. Nyt placeholder.
                Debug.Log("[RunController] RandomEvent-tile ei vielä toteutettu → takaisin makroon.");
                EnterMacroPhase();
                break;

            case MacroEventType.Boss:
                // Toistaiseksi sama kuin Battle, myöhemmin erillinen boss encounter
                Debug.Log("[RunController] Boss-tile → StartNewEncounter (placeholder).");
                StartNewEncounter();
                break;

            case MacroEventType.None:
            default:
                Debug.Log("[RunController] Tyhjä macro-tile → hyppää suoraan seuraavaan makrofaseen.");
                EnterMacroPhase();
                break;
        }
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

    // ---------- ENCOUNTERIN RAKENNUS ----------

    private void StartNewEncounter()
    {
        Debug.Log($"[RunController] StartNewEncounter with macroDifficulty={_pendingBattleDifficulty}");
        TeardownPrevious();

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

        // 2) Valitse Encounter: override → slotMap+PlayerData → fallback
        EncounterSO enc;
        if (encounterOverride != null)
        {
            enc = encounterOverride;
        }
        else
        {
            EnsureSlotsOnce(16);
            var pd = PlayerService.Instance.Data;
            Debug.Log("[Run] loadoutSlots: " + string.Join(",", pd.loadoutSlots.Select(x => string.IsNullOrEmpty(x) ? "-" : x)));

            var pdata = PlayerService.Instance.Data;
            if (slotMap != null)
            {
                var spec = enemySpec ?? new EnemySpec { mode = EnemySpec.Mode.Classic };

                enc = LoadoutAssembler.BuildFromPlayerData(
                    pdata,
                    slotMap,
                    spec,
                    implicitKingId: "King",
                    totalSlots: 16
                );
            }
            else
            {
                enc = BuildMinimalFallbackEncounter();
            }

            var drag = FindObjectOfType<DragController>();
            if (drag != null) drag.enabled = true;
        }

        enc.relativeRanks = true;
        enc.fillBlackPawnsAtY = true;
        enc.blackPawnsY = 1; // 8x8: absY = 7 - 1 = 6

        // 3) Sijoittele nappulat
        EncounterLoader.Apply(_state, enc, catalog);

        // 4) Piirto & input
        if (boardView == null)
            boardView = FindObjectOfType<BoardView>(true);

        if (boardView != null)
        {
            boardView.gameObject.SetActive(true);
            boardView.Init(_state, _rules, aiMode);
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
        var map = new Dictionary<string, PieceDefSO>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var def in allPieceDefsForRules)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.typeName)) continue;
            map[def.typeName] = def;
        }
        _rules = new DefRegistryRulesResolver(map);
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

    // ---- Rules-resolver sisäänrakennettuna ----
    private sealed class DefRegistryRulesResolver : IRulesResolver
    {
        private readonly Dictionary<string, PieceDefSO> _defs;
        public DefRegistryRulesResolver(Dictionary<string, PieceDefSO> defs) { _defs = defs; }

        public IEnumerable<IMoveRule> GetRulesFor(string typeName)
        {
            if (string.IsNullOrEmpty(typeName) || _defs == null) yield break;
            if (!_defs.TryGetValue(typeName, out var def) || def == null || def.rules == null) yield break;

            foreach (var so in def.rules)
            {
                if (so == null) continue;
                var rule = so.Build();
                if (rule != null) yield return rule;
            }
        }
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

            int startIndex = 0;
            if (macroMap != null)
            {
                int startRow = 0;
                int startCol = macroMap.columns / 2;
                startIndex = macroMap.GetIndex(startRow, startCol);
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
    }

}
