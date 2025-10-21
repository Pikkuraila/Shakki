using System.Collections.Generic;
using UnityEngine;
using Shakki.Core;

public sealed class RunController : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private BoardView boardView;
    [SerializeField] private GameCatalogSO catalog;

    [Header("Encounter Sources")]
    [SerializeField] private EncounterSO encounterOverride;   // jos asetettu, k‰ytet‰‰n t‰t‰
    [SerializeField] private SlotMapSO slotMap;               // dynaamiselle loadoutista luomiselle

    [Header("Board")]
    [SerializeField] private BoardTemplateSO template; // voi olla null
    [SerializeField] private int width = 8;
    [SerializeField] private int height = 8;
    [SerializeField] private int? seedOverride;

    [Header("Rules/Defs")]
    [SerializeField] private List<PieceDefSO> allPieceDefsForRules;
    [SerializeField] private BoardView.AiMode aiMode = BoardView.AiMode.Random;

    private GameState _state;
    private IRulesResolver _rules;

    void Start()
    {
        // 1) GameState
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

        // 2) Rules
        var map = new Dictionary<string, PieceDefSO>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var def in allPieceDefsForRules)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.typeName)) continue;
            map[def.typeName] = def;
        }
        _rules = new DefRegistryRulesResolver(map);

        // 3) Valitse encounter: override -> loadout -> fallback
        EncounterSO encounterToUse = encounterOverride;
        if (encounterToUse == null)
        {
            var pdata = PlayerService.Instance.Data;
            if (slotMap != null)
                encounterToUse = LoadoutAssembler.BuildEncounterFromLoadout(pdata, slotMap, fillEnemyClassic: true);
            else
                encounterToUse = BuildMinimalFallbackEncounter(); // jos slotMap puuttuu
        }

        // 4) Asettele nappulat
        EncounterLoader.Apply(_state, encounterToUse, catalog);

        // 5) Piirto & input
        boardView.Init(_state, _rules, aiMode);

        // 6) Game over -kuuntelu
        _state.OnGameEnded += OnGameEnded;
    }

    void OnDestroy()
    {
        if (_state != null) _state.OnGameEnded -= OnGameEnded;
    }

    private void OnGameEnded(GameEndInfo info)
    {
        var reward = (info.WinnerColor == "white") ? 10 : 3;
        PlayerService.Instance.AddCoins(reward);
        // TODO: shop/scene siirtym‰
    }

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

    // Viimeinen h‰t‰vara jos kumpaakaan l‰hdett‰ ei ole m‰‰ritetty
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
}
