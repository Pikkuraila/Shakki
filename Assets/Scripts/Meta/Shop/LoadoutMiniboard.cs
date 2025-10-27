using System.Collections.Generic;
using UnityEngine;

public sealed class LoadoutMiniboard : MonoBehaviour
{
    [Header("Refs")]
    public PlayerService playerService;
    public GameCatalogSO catalog;
    public ShopPricingSO pricing;
    public Transform loadoutRoot;     // parent 16 slotille
    public Transform shopRoot;        // parent 4 slotille
    public PieceView pieceViewPrefab; // visuaalinen nappula (SpriteRenderer tms.)
    public TMPro.TMP_Text coinsText;

    [Header("Layout")]
    public Vector2 slotSpacing = new(1f, 1f); // miten asetellaan 8×2

    LoadoutService _svc;
    List<LoadoutSlot> _loadoutSlots = new();
    List<ShopSlot> _shopSlots = new();
    DragController _drag;

    // Flat 16 id:t muistissa (näyttö + editointi)
    List<string> _flat;



    void Awake()
    {
        _svc = new LoadoutService(playerService, catalog, pricing);
        _drag = FindObjectOfType<DragController>();
        BuildSlots();
        RebuildFromPlayerData();
    }

    void Start()
    {
        if (_drag != null)
        {
            // DragController.Dropped: Action<PieceView, Vector2>
            _drag.Dropped += HandleDrop;
        }
    }

    void OnDestroy()
    {
        if (_drag != null) _drag.Dropped -= HandleDrop;
    }

    void BuildSlots()
    {
        _loadoutSlots.Clear();
        _loadoutSlots.AddRange(loadoutRoot.GetComponentsInChildren<LoadoutSlot>(includeInactive: true));

        _shopSlots.Clear();
        _shopSlots.AddRange(shopRoot.GetComponentsInChildren<ShopSlot>(includeInactive: true));
    }

    void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
    }

    void RebuildFromPlayerData()
    {
        _flat = _svc.ExpandToSlots(16, implicitKingId: "King"); // vaihda omaan king-id:hen jos eri
        RefreshCoins();

        foreach (var s in _loadoutSlots) s.currentPieceId = "";
        ClearChildren(loadoutRoot);

        for (int i = 0; i < _loadoutSlots.Count && i < _flat.Count; i++)
        {
            var id = _flat[i];
            _loadoutSlots[i].index = i;
            _loadoutSlots[i].currentPieceId = id;

            if (!string.IsNullOrEmpty(id))
                SpawnPieceInSlot(_loadoutSlots[i], id, fromShop: false);
        }

        ClearChildren(shopRoot);
        foreach (var ss in _shopSlots)
        {
            var id = ss.pieceId;
            if (string.IsNullOrEmpty(id)) continue;
            SpawnPieceInShop(ss, id);
        }
    }

    void RefreshCoins() => coinsText.text = _svc.Coins.ToString();

    void SpawnPieceInSlot(LoadoutSlot slot, string pieceId, bool fromShop)
    {
        var def = _svc.TryGetPieceDef(pieceId);
        if (def == null) return;

        var pv = Instantiate(pieceViewPrefab, slot.transform);
        pv.Bind(def, color: "White");

        var dragH = pv.gameObject.GetComponent<PieceDragHandle>();
        if (dragH == null) dragH = pv.gameObject.AddComponent<PieceDragHandle>();

        dragH.pieceId = pieceId; // <<< TÄRKEÄ: aseta tunniste
    }

    void SpawnPieceInShop(ShopSlot slot, string pieceId)
    {
        var def = _svc.TryGetPieceDef(pieceId);
        if (def == null) return;

        var pv = Instantiate(pieceViewPrefab, slot.transform);
        pv.Bind(def, color: "White");

        var dragH = pv.gameObject.GetComponent<PieceDragHandle>();
        if (dragH == null) dragH = pv.gameObject.AddComponent<PieceDragHandle>();

        dragH.pieceId = pieceId; // <<< sama täällä
    }


    // *** TÄRKEÄ: signatuurin pitää olla tarkalleen tämä ***
    void HandleDrop(PieceView pv, Vector2 worldPos)
    {
        if (pv == null) return;

        // minne pudottiin?
        var hit = Physics2D.OverlapPoint(worldPos, ~0);
        if (hit == null) return;

        var toLoadout = hit.GetComponentInParent<LoadoutSlot>();
        var toShop = hit.GetComponentInParent<ShopSlot>();

        // mistä tultiin?
        var fromLoadout = pv.GetComponentInParent<LoadoutSlot>();
        var fromShop = pv.GetComponentInParent<ShopSlot>();

        string pieceId = pv.GetComponent<PieceDragHandle>()?.pieceId;
        if (string.IsNullOrEmpty(pieceId)) return;

        // CASE A: Shop → Loadout (osta)
        if (fromShop && toLoadout)
        {
            if (toLoadout.locked || !string.IsNullOrEmpty(toLoadout.currentPieceId)) return;
            if (!_svc.CanAfford(pieceId)) return;

            if (_svc.TryBuy(pieceId))
            {
                _flat[toLoadout.index] = pieceId;
                toLoadout.currentPieceId = pieceId;
                RefreshCoins();
                SpawnPieceInSlot(toLoadout, pieceId, fromShop: true);
            }
            return;
        }

        // CASE B: Loadout → Loadout (swap/siirto)
        if (fromLoadout && toLoadout)
        {
            if (toLoadout.locked) return;

            int a = fromLoadout.index;
            int b = toLoadout.index;
            (_flat[a], _flat[b]) = (_flat[b], _flat[a]);

            RebuildFromPlayerData();
            return;
        }

        // CASE C: Loadout → Shop (myy takaisin)
        if (fromLoadout && toShop)
        {
            _svc.Refund(fromLoadout.currentPieceId, ratio: 1f); // muuta halutessa esim. 0.5f
            _flat[fromLoadout.index] = "";
            RefreshCoins();
            RebuildFromPlayerData();
            return;
        }

        // muuten: ei sallittu → DragController hoitaa palautuksen
    }

    // Kutsu OK/Start-Run napista
    public void CommitAndClose()
    {
        _svc.SaveFromSlots(_flat);
        // PlayerService.Save(); jos käytössä
        // Käynnistä seuraava vaihe...
    }
}
