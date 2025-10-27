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




    private GameState _state;
    private IRulesResolver _rules;
    private const int Slots = 16;

    void Start()
    {
        BuildRules();
        StartNewEncounter();   // käynnistetään eka peli
    }

    void OnDestroy()
    {
        if (_state != null) _state.OnGameEnded -= OnGameEnded;
    }

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

    // ---------- PELI → KAUPPA ----------
    private void OnGameEnded(GameEndInfo info)
    {
        // 0) Palkinto talteen ensin (tai viimeistään tässä)
        var reward = (info.WinnerColor == "white") ? 10 : 3;
        PlayerService.Instance.AddCoins(reward);

        // 1) Siivoa ja piilota lauta
        if (boardView != null)
        {
            // Tyhjentää laatat, nappulat, highlightit, eventit, koroutinet – muttei tuhoa GO:ta
            boardView.Teardown(destroySelfGO: false);
            boardView.gameObject.SetActive(false);   // varma piilotus, mikään ei jää piirtymään
        }

        // 2) Estä mahdolliset dragit
        var drag = FindObjectOfType<DragController>();
        if (drag != null) { drag.StopAllCoroutines(); drag.enabled = false; }

        // 3) Avaa shop
        OpenShop();
    }


    private void OpenShop()
    {
        // Estä input + drags ennen UI:ta
        if (boardView != null) boardView.enabled = false;
        var drag = FindObjectOfType<DragController>();
        if (drag != null) { drag.StopAllCoroutines(); drag.enabled = false; }

        // Täytä slotit kerran jos tyhjät
        EnsureSlotsOnce(16);

        // DIAG: näytä mitä dataan jäi
        var pd = PlayerService.Instance.Data;
        Debug.Log($"[ShopOpen] slots={pd.loadoutSlots?.Count ?? 0} " +
                  $"sample=[{string.Join(",", (pd.loadoutSlots ?? new System.Collections.Generic.List<string>()).GetRange(0, System.Math.Min(8, pd.loadoutSlots?.Count ?? 0)))}]");

        // Näytä UI
        if (shopPanel != null) shopPanel.SetActive(true);

        // Rakenna gridit ennen refreshiä
        if (loadoutView != null) { loadoutView.BuildIfNeeded(); loadoutView.RefreshAll(); }

        var shop = shopView != null ? shopView : FindObjectOfType<ShopGridView>();
        if (shop != null) { shop.BuildIfNeeded(); shop.RefreshAll(); }
    }
         
    // UI-nappi: “Jatka”
    public void ContinueFromShop()
    {
        // 1) Kompaktoi sloteista meta-listaksi ja tallenna
        var ps = PlayerService.Instance;
        ps.Data.loadout = LoadoutModel.Compact(ps.Data.loadoutSlots); // synkkaa meta sloteista
        ps.Save();

        // 2) Sulje shop
        if (shopPanel != null) shopPanel.SetActive(false);

        // 3) Uusi Encounter (slotMap/override käyttää nyt päivitettyä dataa)
        StartNewEncounter();
    }

    // ---------- ENCOUNTERIN RAKENNUS ----------
    private void StartNewEncounter()
    {

        TeardownPrevious(); // NEW
        // Irrota vanhan pelin kuuntelijat
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
            EnsureSlotsOnce(16); // ✅ varmistaa PlayerData.loadoutSlots (16 kpl)
            var pd = PlayerService.Instance.Data;
            Debug.Log("[Run] loadoutSlots: " + string.Join(",", pd.loadoutSlots.Select(x => string.IsNullOrEmpty(x) ? "-" : x)));

            var pdata = PlayerService.Instance.Data;
            if (slotMap != null)
            {
                // ✅ käytä 'spec' eikä 'enemySpec'
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

        // ✅ Lukitse suhteellinen rankkikartta ja mustan sotilasrivi
        enc.relativeRanks = true;
        enc.fillBlackPawnsAtY = true;
        enc.blackPawnsY = 1; // 8x8: absY = 7 - 1 = 6

        // 3) Sijoittele nappulat
        EncounterLoader.Apply(_state, enc, catalog);

        // 4) Piirto & input
        if (boardView == null)
            boardView = FindObjectOfType<BoardView>(true); // etsi myös inaktiivisista

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

    // Viimeinen hätävara jos kumpaakaan lähdettä ei ole määritetty
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
}
