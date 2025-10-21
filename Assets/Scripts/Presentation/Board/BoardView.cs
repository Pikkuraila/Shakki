using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Shakki.Core;
using System.Linq;



public class BoardView : MonoBehaviour
{
    // --- DEBUG SWITCH ---
    [SerializeField] private bool debugTrace = true;

    private void Trace(string msg)
    {
        if (!debugTrace) return;
        Debug.Log($"[TRACE f{Time.frameCount}] {msg}");
    }

    public Vector3 MouseWorldPublic(Camera cam = null)
    {
        var c = cam != null ? cam : Camera.main;
        var w = c.ScreenToWorldPoint(Input.mousePosition);
        w.z = 0f;
        return w;
    }

    public IEnumerable<Move> GenerateMovesFrom((int x, int y) from)
    {
        return GenerateMovesFrom(_state, new Coord(from.x, from.y)) ?? Enumerable.Empty<Move>();
    }

    public (int x, int y) WorldToBoardPublic(Vector3 w)
    {
        return (Mathf.RoundToInt(w.x), Mathf.RoundToInt(w.y));
    }

    private bool InBounds((int x, int y) c)
        => _state != null && c.x >= 0 && c.x < _state.Width && c.y >= 0 && c.y < _state.Height;




    public GameState State => _state;

    private IRulesResolver _rules;

    [Header("Prefabs")]
    public GameObject TileLightPrefab;
    public GameObject TileDarkPrefab;
    public GameObject HighlightPrefab;
    public GameObject PiecePrefab; // oletus, ellei overridea

    [Header("Board Source")]
    public BoardTemplateSO template;       // jätä tyhjäksi → käytetään width/height
    public int width = 8;
    public int height = 8;
    public int? seedOverride;              // jos käytät random tageja

    [Header("View")]
    public float tileSize = 1f;
    public float cameraPaddingTiles = 0.5f; // extra “tyhjää” reunoille


    // Välimuisti, jotta voidaan siivota/päivittää laattoja
    readonly Dictionary<(int x, int y), GameObject> _tiles = new();


    [Header("Start Pieces")]

    public PieceDefSO WhiteRookDef;
    public PieceDefSO WhiteKingDef;
    public PieceDefSO WhiteBishopDef;
    public PieceDefSO WhiteKnightDef;
    public PieceDefSO WhitePawnDef;
    public PieceDefSO WhiteQueenDef;


    public PieceDefSO BlackBishopDef;
    public PieceDefSO BlackKnightDef;
    public PieceDefSO BlackPawnDef;
    public PieceDefSO BlackRookDef;
    public PieceDefSO BlackKingDef;
    public PieceDefSO BlackQueenDef;

    [Header("Custom Pieces")]
    public PieceDefSO WhiteAlfilDef;
    public PieceDefSO BlackAlfilDef;
    public PieceDefSO WhiteDabbabaDef;
    public PieceDefSO BlackDabbabaDef;
    public PieceDefSO WhiteWazirDef;
    public PieceDefSO BlackWazirDef;
    public PieceDefSO WhiteKnightmareDef;
    public PieceDefSO BlackKnightmareDef;
    public PieceDefSO WhiteCannonDef;
    public PieceDefSO BlackCannonDef;
    public PieceDefSO WhiteGrasshopperDef;
    public PieceDefSO BlackGrasshopperDef;
    public PieceDefSO WhiteAmazonDef;
    public PieceDefSO BlackAmazonDef;
    public PieceDefSO WhiteEmpressDef;
    public PieceDefSO BlackEmpressDef;
    public PieceDefSO WhiteArchbishopDef;
    public PieceDefSO BlackArchbishopDef;
    public PieceDefSO WhiteJokerDef;
    public PieceDefSO BlackJokerDef;



    [Header("Def Registry")]
    public List<PieceDefSO> AllPieceDefs = new(); // vedä tänne kaikki käyttämäsi PieceDefSO:t

    private Dictionary<string, PieceDefSO> _defByType = new();
    private GameState _state;
    private readonly Dictionary<(int, int), PieceView> _pieceViews = new();
    private readonly List<GameObject> _highlights = new();
    private Coord? _selected;


    public enum AiMode
    {
        None,
        Random,
        Greedy
    }
    [SerializeField] private AiMode aiMode = AiMode.Random;

    private IAiPlayer ai;

    public void Init(GameState state, IRulesResolver rules, AiMode mode)
    {
        _state = state;
        _rules = rules;
        aiMode = mode;

        // Eventit ennen synckiä
        _state.OnTurnChanged += HandleTurnChanged;
        _state.OnCaptured += HandleCapture;

        // Laatta- ja nappulapiirto
        BuildBoardTiles();
        SyncAllPiecesFromState();
        CenterAndFitCamera();

        // AI-mode
        switch (aiMode)
        {
            case AiMode.Random: ai = new RandomAi(); break;
            case AiMode.Greedy: ai = new GreedyAi(); break;
            default: ai = null; break;
        }

        DumpStateSnapshot("init");
    }



    void Awake()
    {
        _defByType.Clear();
        foreach (var def in AllPieceDefs)
        {
            if (def == null || string.IsNullOrEmpty(def.typeName)) continue;
            _defByType[def.typeName] = def;
        }
    }

    void OnDestroy()
    {
        if (_state != null)
        {
            _state.OnTurnChanged -= HandleTurnChanged;
            _state.OnCaptured    -= HandleCapture;
        }
    }

    void CenterAndFitCamera()
    {
        var cam = Camera.main;
        if (!cam) return;

        // Keskipiste maailman­koordinaateissa
        float worldW = (_state.Width)  * tileSize;
        float worldH = (_state.Height) * tileSize;
        float cx = worldW * 0.5f - tileSize * 0.5f;
        float cy = worldH * 0.5f - tileSize * 0.5f;

        cam.transform.position = new Vector3(cx, cy, -10f);
        cam.orthographic = true;

        // Fittaa koko lauta ruutuun aspectin mukaan + padding
        float pad = cameraPaddingTiles * tileSize;
        float halfW = worldW * 0.5f + pad;
        float halfH = worldH * 0.5f + pad;

        float aspect = (float)Screen.width / Screen.height;
        // orthoSize on “puoli-korkeus”; leveysvaatimus tulee jakamalla aspectilla
        float needByHeight = halfH;
        float needByWidth  = halfW / aspect;

        cam.orthographicSize = Mathf.Max(needByHeight, needByWidth);
    }

    // Varmista että BuildBoardTiles ja SetupStartPosition käyttävät:
    // foreach (var c in _state.AllCoords()) { ... }
    // eikä kiinteitä for (y < 8) -silmukoita.

    private bool _aiRunning = false;

    private void HandleTurnChanged(string who)
    {
        DumpStateSnapshot("OnTurnChanged.enter");
        Trace($"OnTurnChanged(who={who}), ai={(ai == null ? "null" : ai.GetType().Name)}, _aiRunning={_aiRunning}");
        if (ai != null && who == "black" && !_aiRunning)
        {
            Trace("StartCoroutine(DoAiMove)");
            StartCoroutine(DoAiMove());
        }
    }


    public void SetupClassicStart()
    {
        // Valkoinen
        _state.Set(new Coord(0, 0), WhiteRookDef.Build("white"));
        _state.Set(new Coord(4, 0), WhiteKingDef.Build("white"));
        _state.Set(new Coord(3, 0), WhiteQueenDef.Build("white"));
        _state.Set(new Coord(7, 0), WhiteRookDef.Build("white"));
        _state.Set(new Coord(2, 0), WhiteBishopDef.Build("white"));
        _state.Set(new Coord(5, 0), WhiteBishopDef.Build("white"));
        _state.Set(new Coord(1,0), WhiteKnightDef.Build("white"));
        _state.Set(new Coord(6,0), WhiteKnightDef.Build("white"));
        _state.Set(new Coord(0, 1), WhitePawnDef.Build("white"));
        _state.Set(new Coord(1, 1), WhitePawnDef.Build("white"));
        _state.Set(new Coord(2, 1), WhitePawnDef.Build("white"));
        _state.Set(new Coord(3, 1), WhitePawnDef.Build("white"));
        _state.Set(new Coord(4, 1), WhitePawnDef.Build("white"));
        _state.Set(new Coord(5, 1), WhitePawnDef.Build("white"));
        _state.Set(new Coord(6, 1), WhitePawnDef.Build("white"));
        _state.Set(new Coord(7, 1), WhitePawnDef.Build("white"));

        //testi erikoisnappulat




        // Musta
        _state.Set(new Coord(0, 7), BlackRookDef.Build("black"));
        _state.Set(new Coord(3, 7), BlackKingDef.Build("black"));
        _state.Set(new Coord(4, 7), BlackQueenDef.Build("black"));
        _state.Set(new Coord(7, 7), BlackRookDef.Build("black"));
        _state.Set(new Coord(2, 7), BlackBishopDef.Build("black"));
        _state.Set(new Coord(5, 7), BlackBishopDef.Build("black"));
        _state.Set(new Coord(1,7), BlackKnightDef.Build("black"));
        _state.Set(new Coord(6,7), BlackKnightDef.Build("black"));
        _state.Set(new Coord(0, 6), BlackPawnDef.Build("black"));
        _state.Set(new Coord(1, 6), BlackPawnDef.Build("black"));
        _state.Set(new Coord(2, 6), BlackPawnDef.Build("black"));
        _state.Set(new Coord(3, 6), BlackPawnDef.Build("black"));
        _state.Set(new Coord(4, 6), BlackPawnDef.Build("black"));
        _state.Set(new Coord(5, 6), BlackPawnDef.Build("black"));
        _state.Set(new Coord(6, 6), BlackPawnDef.Build("black"));
        _state.Set(new Coord(7, 6), BlackPawnDef.Build("black"));


    }


    void BuildBoardTiles()
    {
        // 0) Siivoa vanhat
        foreach (var go in _tiles.Values) if (go) Destroy(go);
        _tiles.Clear();

        // 1) Rakenna laatat vain sallituille koordinaateille
        foreach (var c in _state.AllCoords()) // huom! ei for y<x GameState.H/W
        {
            bool isLight = ((c.X + c.Y) & 1) == 0;
            var prefab = isLight ? TileLightPrefab : TileDarkPrefab;

            var pos = new Vector3(c.X * tileSize, c.Y * tileSize, 0f);
            var go = Instantiate(prefab, pos, Quaternion.identity, transform);
            go.name = $"Tile_{c.X}_{c.Y}";

            // 2) Aina alle nappuloiden
            if (go.TryGetComponent<SpriteRenderer>(out var sr))
                sr.sortingOrder = 0;

            // 3) TileView init (jos käytössä)
            var tv = go.GetComponent<TileView>() ?? go.AddComponent<TileView>();
            var color = isLight ? new Color(0.92f, 0.92f, 0.92f) : new Color(0.6f, 0.63f, 0.65f);
            tv.Init(c.X, c.Y, this, color);

            _tiles[(c.X, c.Y)] = go;
        }
    }


    public void ClearHighlightsPublic()
    {
        foreach (var h in _highlights) Destroy(h);
        _highlights.Clear();
    }

    public void ShowHighlightsPublic(IEnumerable<Move> moves)
    {
        ClearHighlightsPublic();
        foreach (var m in moves)
        {
            var go = Instantiate(HighlightPrefab, new Vector3(m.To.X, m.To.Y, -0.1f), Quaternion.identity, transform);
            _highlights.Add(go);
        }
    }


    public bool CanHumanMove(PieceView pv)
    {
        if (pv == null || _state == null) return false;
        if (ai != null && _state.CurrentPlayer == "black") return false; // AI musta vuorolla
        return pv.Owner == _state.CurrentPlayer;
    }

    public bool TryDropPublic(PieceView pv, (int x, int y) from, (int x, int y) to, List<Move> cached)
    {
        try
        {
            // --- perusguardit ---
            if (pv == null) { Debug.LogWarning("[Drop] pv == null"); return false; }
            if (_state == null) { Debug.LogWarning("[Drop] _state == null"); return false; }
            if (_rules == null) { Debug.LogWarning("[Drop] _rules == null"); return false; }
            if (!InBounds(from) || !InBounds(to)) { Debug.Log("[Drop] out of bounds"); return false; }

            // varmista että lähdössä on edelleen sama view (vedon aikana tilanne ei muuttunut)
            if (!_pieceViews.TryGetValue((from.x, from.y), out var pvAtStart) || pvAtStart != pv)
            {
                Debug.Log("[Drop] start view changed during drag");
                return false;
            }

            // --- hae siirto cachetista ---
            var move = cached?.FirstOrDefault(m => m.To.X == to.x && m.To.Y == to.y) ?? default;
            if (move.Equals(default(Move)))
            {
                // ei cache-osumaa -> ei hyväksytä
                return false;
            }

            // --- null-safe laillisuustsekki tuoreena ---
            var fresh = _state.GenerateLegalMoves(new Coord(from.x, from.y), _rules) ?? Enumerable.Empty<Move>();
            bool stillOk = fresh.Any(m => m.To.X == to.x && m.To.Y == to.y);
            if (!stillOk) return false;

            // --- mahdollinen kaappaus ---
            _pieceViews.TryGetValue((to.x, to.y), out var capturedPV);

            // --- suorita siirto ---
            if (!_state.ApplyMove(move, _rules))
                return false;

            // --- päivitä view’t ---
            if (capturedPV != null) { Destroy(capturedPV.gameObject); _pieceViews.Remove((to.x, to.y)); }
            _pieceViews.Remove((from.x, from.y));
            pv.SetBoardPos(to.x, to.y);
            _pieceViews[(to.x, to.y)] = pv;

            return true; // OnTurnChanged hoitaa AI:n
        }
        catch (System.Exception ex)
        {
            // ÄLÄ kaada koroutinia – palauta false → DragController tekee snapbackin
            Debug.LogError($"[Drop] TryDropPublic exception (returning false): {ex}");
            return false;
        }
    }



    void SyncAllPiecesFromState()
    {
        foreach (var pv in _pieceViews.Values) if (pv) Destroy(pv.gameObject);
        _pieceViews.Clear();

        foreach (var c in _state.AllCoords())
        {
            var p = _state.Get(c);
            if (p == null) continue;

            // hae def
            if (!_defByType.TryGetValue(p.TypeName, out var def))
            {
                Debug.LogWarning($"Puuttuu PieceDefSO tyypille {p.TypeName}");
                continue;
            }

            var prefab = def.viewPrefabOverride != null ? def.viewPrefabOverride : PiecePrefab;
            var go = Instantiate(prefab, new Vector3(c.X, c.Y, -1f), Quaternion.identity, transform);

            // SpriteRenderer – varmista että löytyy juuresta tai lapsesta
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.GetComponentInChildren<SpriteRenderer>(true);

            // ASETETAAN VARMASTI
            if (sr != null)
            {
                sr.sprite = def.GetSpriteFor(p.Owner);  // <-- spriten asetus SO:sta
                sr.color = Color.white;                 // <-- ei tummennuksia
                sr.sortingOrder = 10;                   // <-- näkyy laatan päällä
                                                        // (tarvittaessa sr.sortingLayerName = "Pieces"; jos käytät omia layereita)
            }
            else
            {
                Debug.LogError("PiecePrefabista puuttuu SpriteRenderer. Lisää se juureen.", go);
            }

            // PieceView (ilman tekstejä, se vain kantaa koordit)
            var pv = go.GetComponent<PieceView>();
            if (!pv) pv = go.AddComponent<PieceView>();
            pv.Init(c.X, c.Y, p.Owner, p.TypeName, Color.white);

            _pieceViews[(c.X, c.Y)] = pv;

            // varmistetaan collider vedolle
            var col = go.GetComponent<Collider2D>();
            if (col == null)
            {
                var b = go.AddComponent<BoxCollider2D>();
                // säädetään collider spriten mukaan (jos löytyy)
                var sr2 = go.GetComponent<SpriteRenderer>() ?? go.GetComponentInChildren<SpriteRenderer>(true);
                if (sr2 != null && sr2.sprite != null)
                {
                    b.size = sr2.sprite.bounds.size;
                    b.offset = sr2.sprite.bounds.center;
                }
            }

            // varmistetaan drag handle
            if (go.GetComponent<PieceDragHandle>() == null)
                go.AddComponent<PieceDragHandle>();
        }
    }



    private List<Move> _cachedMoves = new();

    private IEnumerable<Move> GenerateMovesFrom(GameState s, Coord from)
    {
        var ctx = new RuleContext(s, from, _rules);

        var me = s.Get(from);
        if (me == null) yield break;

        // Generoi siirrot suoraan resolverilta (JokerRule pääsee mukaan)
        foreach (var rule in _rules.GetRulesFor(me.TypeName))
        {
            foreach (var m in rule.Generate(ctx))
            {
                // Jos sinulla on erillinen laillisuustarkistus, laita se tähän:
                // if (s.IsLegalMove(m)) yield return m;
                yield return m;
            }
        }
    }

    public void OnTileClicked(int x, int y)
    {
        // 0) Estä ihmisen siirto AI:n vuorolla
        if (ai != null && _state.CurrentPlayer == "black")
        {
            Debug.Log("[CLICK] Ignored: AI's turn (black).");
            return;
        }

        if (_state == null)
        {
            Debug.LogWarning("[CLICK] _state == null");
            return;
        }

        var clicked = new Coord(x, y);

        // 1) VALINTA — ei valittua ruutua vielä
        if (!_selected.HasValue)
        {
            var piece = _state.Get(clicked);
            if (piece != null && piece.Owner == _state.CurrentPlayer)
            {
                _selected = clicked;

                // Luo/kirjoita varmuuden vuoksi uusi lista
                _cachedMoves = GenerateMovesFrom(_state, clicked)?.ToList() ?? new List<Move>();

                ShowHighlightsPublic(_cachedMoves);
            }
            else
            {
                _selected = null;
                _cachedMoves.Clear();
                ClearHighlightsPublic();
            }
            return;
        }

        // 2) SIIRTOYRITYS — meillä on lähtöruutu
        var from = _selected.Value;

        // Hae juuri se move joka vastaa klikattua kohdetta
        Move move = default;
        if (_cachedMoves != null && _cachedMoves.Count > 0)
            move = _cachedMoves.FirstOrDefault(m => m.To.Equals(clicked));

        // Talteen mahdollinen syötävä view ENNEN ApplyMovea
        _pieceViews.TryGetValue((x, y), out var capturedPV);

        // Yritä soveltaa siirtoa vain jos se oli cachetetuissa
        if (!move.Equals(default(Move)))
        {
            // Pre-check: varmista että siirto on edelleen laillinen (cache voi vanheta)
            bool stillLegal = _state.GenerateLegalMoves(move.From, _rules).Any(m => m.To.Equals(move.To));
            if (!stillLegal)
            {
                Debug.Log("[CLICK] Cached move not legal anymore, refreshing selection.");
                _selected = null;
                _cachedMoves.Clear();
                ClearHighlightsPublic();
                return;
            }

            // SUORITA SIIRTO
            bool ok = _state.ApplyMove(move, _rules);
            if (ok)
            {
                // a) Poista syöty view jos sellainen oli
                if (capturedPV != null)
                {
                    Destroy(capturedPV.gameObject);
                    _pieceViews.Remove((x, y));
                }

                // b) Päivitä siirtävän nappulan view (turvallisesti)
                var keyFrom = (from.X, from.Y);
                if (_pieceViews.TryGetValue(keyFrom, out var movingPV) && movingPV != null)
                {
                    _pieceViews.Remove(keyFrom);
                    movingPV.SetBoardPos(x, y);
                    _pieceViews[(x, y)] = movingPV;
                }
                else
                {
                    // Jos view puuttuu (esim. AI ehti päivittää, tai muu epäsynkka) → kova sync
                    Debug.LogWarning("[CLICK] movingPV missing -> full SyncAllPiecesFromState()");
                    SyncAllPiecesFromState();
                }

                // c) Siivoukset
                _selected = null;
                _cachedMoves.Clear();
                ClearHighlightsPublic();

                // AI laukeaa OnTurnChangedissa; ei tehdä mitään tässä
                return;
            }

            // Siirto epäonnistui → jatketaan alas reselektioon
            Debug.Log("[CLICK] ApplyMove returned false; will try reselection.");
        }

        // 3) EI LAILLINEN / OSUI OMAAN → vaihda valinta jos klikkasit omaa
        var clickedPiece = _state.Get(clicked);
        if (clickedPiece != null && clickedPiece.Owner == _state.CurrentPlayer)
        {
            _selected = clicked;
            _cachedMoves = GenerateMovesFrom(_state, clicked)?.ToList() ?? new List<Move>();
            ShowHighlightsPublic(_cachedMoves);
        }
        else
        {
            _selected = null;
            _cachedMoves?.Clear();
            ClearHighlightsPublic();
        }
    }



    private void HandleCapture(Coord at, Piece captured)
    {
        // Muodostetaan avain samaan muotoon kuin _pieceViews käyttää
        var key = (at.X, at.Y);

        // Jos tuossa ruudussa on vielä nappulan GameObject (sprite), tuhotaan se
        if (_pieceViews.TryGetValue(key, out var pv))
        {
            Destroy(pv.gameObject);
            _pieceViews.Remove(key);
        }
    }

    // hakee säännöt PieceDefSO:staa
    private sealed class DefRegistryRulesResolver : IRulesResolver
    {
        private readonly Dictionary<string, PieceDefSO> _defs;

        public DefRegistryRulesResolver(Dictionary<string, PieceDefSO> defs)
        {
            _defs = defs;
        }

        public IEnumerable<IMoveRule> GetRulesFor(string typeName)
        {
            if (string.IsNullOrEmpty(typeName) || _defs == null)
                yield break;

            if (!_defs.TryGetValue(typeName, out var def) || def == null || def.rules == null)
                yield break;

            foreach (var so in def.rules)
            {
                if (so == null) continue;
                var rule = so.Build();
                if (rule != null) yield return rule;
            }
        }
    }

    private void TriggerAiIfNeeded()
    {
        // AI vain jos mustan vuoro ja ai != null
        if (ai != null && _state.CurrentPlayer == "black")
            StartCoroutine(DoAiMove());
    }

    private void DumpStateSnapshot(string tag)
    {
        var w = _state.AllMoves("white", _rules).Count();
        var b = _state.AllMoves("black", _rules).Count();
        Trace($"SNAP[{tag}] cur={_state.CurrentPlayer} moves: W={w} B={b} ai={(ai == null ? "null" : ai.GetType().Name)} _aiRunning={_aiRunning} last={_state.LastMoveEffectiveTypeName ?? "∅"}");
    }


    private IEnumerator DoAiMove()
    {
        _aiRunning = true;
        Trace("DoAiMove: begin");
        yield return new WaitForSeconds(0.3f);
        if (ai == null) { Trace("DoAiMove: ai==null -> abort"); _aiRunning = false; yield break; }

        // KATSO montako siirtoa AI:lla oikeasti on resolverilla
        var options = _state.AllMoves("black", _rules).ToList();
        Trace($"DoAiMove: options.Count={options.Count}");

        var move = ai.ChooseMove(_state, "black", _rules);
        if (move.Equals(default(Move)))
        {
            Trace("DoAiMove: AI returned default move (0 siirtoa?) -> game over?");
            _aiRunning = false; yield break;
        }

        Trace($"DoAiMove: try ApplyMove {move.From.X},{move.From.Y}->{move.To.X},{move.To.Y} as={move.AsTypeName ?? "∅"}");
        var ok = _state.ApplyMove(move, _rules);
        Trace($"DoAiMove: ApplyMove returned {ok}");

        if (ok)
        {
            SyncAllPiecesFromState();
            ClearHighlightsPublic();
        }
        _aiRunning = false;
        Trace("DoAiMove: end");
    }
}

