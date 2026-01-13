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


    private readonly List<GameObject> _highlightPool = new();


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


    [Header("Catalog (runtime defs fallback)")]
    [SerializeField] private GameCatalogSO catalog;
    public void SetCatalog(GameCatalogSO cat) => catalog = cat;

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

    // --- Intel hooks (injected from RunController) ---
    // Return true if enemy moves may be revealed on hover (bestiary/item/perk)
    public System.Func<PieceView, bool> CanRevealEnemyMovesOnHover;

    // Return true if player is allowed to MOVE enemy pieces (e.g. item that allows acting on enemy turn)
    public System.Func<PieceView, bool> CanPlayerMoveEnemyPiece;

    private PieceView _hoverPV;
    private (int x, int y)? _hoverBoard;
    private List<Move> _hoverMoves = new();



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
        EnsureRuntimeRoots();
        _defByType.Clear();
        foreach (var def in AllPieceDefs)
        {
            if (def == null || string.IsNullOrEmpty(def.typeName)) continue;
            _defByType[def.typeName] = def;
        }
    }

    private void Update()
    {
        if (_state == null || _rules == null) return;

        // Dragatessa ei hover-highlightteja (drag hoitaa omat highlightit)
        if (Input.GetMouseButton(0)) return;

        UpdateHoverIntel();
    }

    private void UpdateHoverIntel()
    {
        var w = MouseWorldPublic(Camera.main);
        var c = WorldToBoardPublic(w);
        if (!InBounds(c))
        {
            ClearHoverHighlightsIfNeeded();
            return;
        }

        // Onko ruudussa nappulaa?
        if (!_pieceViews.TryGetValue((c.x, c.y), out var pv) || pv == null)
        {
            ClearHoverHighlightsIfNeeded();
            return;
        }

        // Vain vihollinen hover-intelillä (kuten pyysit)
        // (jos joskus haluat oman nappulan hoveriin myös, tee tästä optio)
        // ... pv löytyi ...

        bool isEnemy = pv.Owner != _state.CurrentPlayer;

        if (isEnemy)
        {
            bool allowed = (CanRevealEnemyMovesOnHover != null) && CanRevealEnemyMovesOnHover(pv);
            if (!allowed)
            {
                ClearHoverHighlightsIfNeeded();
                return;
            }
        }

        // Jos hover on sama ruutu kuin viime frame, ei lasketa uudestaan
        if (_hoverBoard.HasValue && _hoverBoard.Value.x == c.x && _hoverBoard.Value.y == c.y)
            return;

        _hoverBoard = c;
        _hoverPV = pv;

        // Legal moves (sama kuin muu peli)
        _hoverMoves = _state.GenerateLegalMoves(new Coord(c.x, c.y), _rules)?.ToList() ?? new List<Move>();

        ShowHighlightsPublic(_hoverMoves);

    }

    private void ClearHoverHighlightsIfNeeded()
    {
        if (_hoverBoard.HasValue)
        {
            _hoverBoard = null;
            _hoverPV = null;
            _hoverMoves.Clear();
            ClearHighlightsPublic();
        }
    }



    void OnDestroy()
    {
        // Siivotaan elegantisti, mutta ei tuhoa itseään uudelleen jos Unity jo tuhoaa
        try { Teardown(destroySelfGO: false); } catch { }

        // (Ei tarvitse enää erikseen -= eventit, Teardown hoitaa sen.)
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


    // Pidä nämä public/serialized kentät BoardViewissä:
    [SerializeField] private string tilesSortingLayer = "BoardTiles";
    [SerializeField] private int tilesSortingOrder = 0;
    [SerializeField] private float tilesZ = 0f;

    // ...BuildBoardTiles sisällä:
    void BuildBoardTiles()
    {
        // 0) Siivoa vanhat
        foreach (var go in _tiles.Values) if (go) Destroy(go);
        _tiles.Clear();

        // Varmista että parent on päällä
        if (TilesParent != null && !TilesParent.gameObject.activeSelf)
            TilesParent.gameObject.SetActive(true);

        // 1) Rakenna laatat vain sallituille koordinaateille
        foreach (var c in _state.AllCoords())
        {
            bool isLight = ((c.X + c.Y) & 1) == 0;
            var prefab = isLight ? TileLightPrefab : TileDarkPrefab;

            // Turvasuoja: älä kaadu vaikka prefab puuttuisi
            if (prefab == null)
            {
                Debug.LogWarning($"[BoardView] Tile prefab missing for {(isLight ? "Light" : "Dark")} – using empty GO.");
                prefab = new GameObject("Tile_Fallback");
                prefab.AddComponent<SpriteRenderer>(); // tyhjä SR, väri täytetään alla
            }

            var pos = new Vector3(c.X * tileSize, c.Y * tileSize, tilesZ);
            var go = Instantiate(prefab, pos, Quaternion.identity, TilesParent);
            go.name = $"Tile_{c.X}_{c.Y}";

            // 2) Aina alle nappuloiden: pakota layer & order & enabled
            if (!go.TryGetComponent<SpriteRenderer>(out var sr))
                sr = go.AddComponent<SpriteRenderer>();

            // Pakota perusmateriaali (jos FX on rikkonut sen)
            if (sr.sharedMaterial == null || sr.sharedMaterial.shader == null)
                sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

            sr.sortingLayerName = tilesSortingLayer; // esim. "BoardTiles"
            sr.sortingOrder = tilesSortingOrder;     // esim. 0
            sr.enabled = true;                       // varmistus

            // 3) TileView init + kovitetut värit (täysi alpha)
            var tv = go.GetComponent<TileView>() ?? go.AddComponent<TileView>();
            var color = isLight ? new Color(0.92f, 0.92f, 0.92f, 1f)
                                : new Color(0.60f, 0.63f, 0.65f, 1f);
            tv.Init(c.X, c.Y, this, color);

            // Jos prefabissa ei ole spriteä (fallback), täytä yksivärisellä (SpriteRenderer.color toimii ilman spriteäkin)
            if (sr.sprite == null)
                sr.color = color;

            _tiles[(c.X, c.Y)] = go;
        }
    }



    public void ClearHighlightsPublic()
    {
        foreach (var h in _highlights)
            if (h) { h.SetActive(false); _highlightPool.Add(h); }
        _highlights.Clear();
    }


    public void ShowHighlightsPublic(IEnumerable<Move> moves)
    {
        ClearHighlightsPublic();

        foreach (var m in moves)
        {
            GameObject go = null;

            // ota poolista jos löytyy
            if (_highlightPool.Count > 0)
            {
                var last = _highlightPool.Count - 1;
                go = _highlightPool[last];
                _highlightPool.RemoveAt(last);
                go.SetActive(true);
            }
            else
            {
                go = Instantiate(HighlightPrefab, HLParent);
            }

            go.transform.position = new Vector3(m.To.X, m.To.Y, -0.1f);
            go.transform.SetParent(HLParent, worldPositionStays: true);
            _highlights.Add(go);
        }
    }



    public bool CanHumanMove(PieceView pv)
    {
        if (pv == null || _state == null) return false;

        bool isPlayersTurn = (pv.Owner == _state.CurrentPlayer);

        // Normaalisti: et voi siirtää vihollista
        bool canMoveEnemy = (!isPlayersTurn) &&
                            (CanPlayerMoveEnemyPiece != null) &&
                            CanPlayerMoveEnemyPiece(pv);

        // AI estää ihmistä mustan vuorolla, ELLEI erikoisitem anna toimia mustan vuorolla
        if (ai != null && _state.CurrentPlayer == "black" && !canMoveEnemy)
            return false;

        return isPlayersTurn || canMoveEnemy;
    }


    public bool TryDropPublic(PieceView pv, (int x, int y) from, (int x, int y) to, List<Move> cached)
    {
        if (_state == null || _state.IsGameOver) return false; // jos sinulla on IsGameOver-lippu


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
        // Tyhjennä vain nappulajuuri, älä koko BoardView’n lapsia
        for (int i = PiecesParent.childCount - 1; i >= 0; i--)
            Destroy(PiecesParent.GetChild(i).gameObject);

        _pieceViews.Clear();

        foreach (var c in _state.AllCoords())
        {
            var p = _state.Get(c);
            if (p == null) continue;

            if (!_defByType.TryGetValue(p.TypeName, out var def) || def == null)
            {
                // Fallback: hae myös catalogista (sis. runtime registry)
                if (catalog != null)
                {
                    def = catalog.GetPieceById(p.TypeName);
                    if (def != null)
                    {
                        _defByType[p.TypeName] = def;
                        Debug.Log($"[BoardView] Fallback-registered def from catalog: {p.TypeName}");
                    }
                }

                if (def == null)
                {
                    Debug.LogWarning($"Puuttuu PieceDefSO tyypille {p.TypeName}");
                    continue;
                }
            }


            var prefab = def.viewPrefabOverride != null ? def.viewPrefabOverride : PiecePrefab;
            var go = Instantiate(prefab, new Vector3(c.X, c.Y, -1f), Quaternion.identity, PiecesParent);

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
        if (me == null)
            yield break;

        int ruleCount = 0;
        int moveCount = 0;

        // Generoi siirrot suoraan resolverilta (JokerRule pääsee mukaan)
        foreach (var rule in _rules.GetRulesFor(me.TypeName))
        {
            if (rule == null) continue;
            ruleCount++;

            foreach (var m in rule.Generate(ctx))
            {
                moveCount++;
                yield return m;
            }
        }

        Debug.Log($"[Moves] type={me.TypeName} rules={ruleCount} moves={moveCount}");
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

    [Header("Runtime Roots (optional)")]
    public Transform tilesRoot;
    public Transform piecesRoot;
    public Transform highlightsRoot;
    public Transform overlaysRoot; // jos käytät

    bool _teardownDone;

    // Luo puuttuvat rootit automaattisesti
    void EnsureRuntimeRoots()
    {
        Transform Make(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, worldPositionStays: false);
            return t;
        }

        if (tilesRoot == null) tilesRoot = transform.Find("Tiles") ?? Make("Tiles");
        if (piecesRoot == null) piecesRoot = transform.Find("Pieces") ?? Make("Pieces");
        if (highlightsRoot == null) highlightsRoot = transform.Find("Highlights") ?? Make("Highlights");
        if (overlaysRoot == null) overlaysRoot = transform.Find("Overlays") ?? Make("Overlays");
    }

    public void Teardown(bool destroySelfGO = true)
    {
        if (_teardownDone) return;
        _teardownDone = true;

        // 1) pysäytä boardiin liittyvät koroutinet
        try { StopAllCoroutines(); } catch { }

        // 2) irrota eventit _statelta (jos vielä kiinni)
        try
        {
            if (_state != null)
            {
                _state.OnTurnChanged -= HandleTurnChanged;
                _state.OnCaptured -= HandleCapture;
            }
        }
        catch { }

        // 3) tyhjennä highlightit
        try { ClearHighlightsPublic(); } catch { }

        // 4) tuhoa kaikki runtime-lapset juurista
        void DestroyChildrenOf(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        DestroyChildrenOf(highlightsRoot);
        DestroyChildrenOf(piecesRoot);
        DestroyChildrenOf(tilesRoot);
        DestroyChildrenOf(overlaysRoot);

        // 5) tyhjennä välimuistit / viitteet
        _pieceViews.Clear();
        _tiles.Clear();
        _highlights.Clear();
        _selected = null;
        _cachedMoves?.Clear();
        _aiRunning = false;
        ai = null;
        _rules = null;
        _state = null;

        // 6) disabloi ja haluttaessa tuhoa oma GO
        enabled = false;
        if (destroySelfGO)
            Destroy(gameObject);
    }

    // Pieni apu: valitse parent aina rootista jos se on olemassa
    Transform PiecesParent => piecesRoot != null ? piecesRoot : transform;
    Transform TilesParent => tilesRoot != null ? tilesRoot : transform;
    Transform HLParent => highlightsRoot != null ? highlightsRoot : transform;




    public void RegisterRuntimeDef(PieceDefSO def)
    {
        if (def == null || string.IsNullOrEmpty(def.typeName)) return;
        _defByType[def.typeName] = def;
        Debug.Log($"[BoardView] Registered runtime def: {def.typeName}");
    }

}

