using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ShopGridView : MonoBehaviour
{
    [Header("Deps (inject or drag in Inspector)")]
    [SerializeField] private ShopPoolSO pool;              // ShopPool.asset
    [SerializeField] private PlayerService playerService;  // scene-instanssi

    private bool _depsReady;

    [Header("Core")]
    [SerializeField] private GridLayoutGroup grid;
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private GameObject pieceIconPrefab;
    [SerializeField] private GameCatalogSO gameCatalog;

    [Header("Config")]
    [Min(1)] public int visibleSlots = 4;              // montako myyntipaikkaa näkyy
    public bool preventDuplicatesInRow = true;         // ei samaa itemiä kahdesti

    [Header("Tile skin")]
    public Sprite lightTile;
    public Sprite darkTile;

    [Header("UI Layers")]
    public RectTransform dragLayer;

    // --- internal ---
    private const int Columns = 4;                     // staattinen 4×1
    private bool _built;
    private Dictionary<string, PieceDefSO> _defByType;
    private readonly List<ShopItemDefSO> _currentRoll = new(); // tämän session valinta
    private List<ShopItemDefSO> _roll = new();     // 0..3 näkyvät
    private Dictionary<int, ShopItemDefSO> _uiToItem = new(); // uiIndex -> item

    // ===== Lifecycle =====
    private void Awake()
    {
        // Hae varalta palvelu jos ei injektoitu
        if (playerService == null)
            playerService = FindObjectOfType<PlayerService>();

        // Lookupit käyttöön vasta kun catalog on
        if (gameCatalog != null)
            _defByType = gameCatalog.BuildPieceLookup();

        // Perus grid-asetukset
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Columns;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;

            if (grid.cellSize.x < 8f || grid.cellSize.y < 8f)
                grid.cellSize = new Vector2(80, 80);
            if (grid.spacing == Vector2.zero)
                grid.spacing = new Vector2(4, 4);
        }

        // Ei kutsuta RebuildFromPool() vielä — se tehdään vasta Setupissa.
    }

    // ===== Public DI =====
    public void Setup(PlayerService svc, ShopPoolSO poolAsset)
    {
        playerService = svc != null ? svc : playerService;
        pool = poolAsset != null ? poolAsset : pool;

        // Pyynnön mukaisesti asetetaan _depsReady todeksi ja suojataan Rebuild try/catchilla,
        // jotta UI-paneelin aktivointi ei riipu rebuildin onnistumisesta.
        _depsReady = true;

        if (playerService == null || pool == null)
            Debug.LogWarning("[ShopGrid] Setup(): missing deps (playerService or pool). Rebuild may log errors.");

        BuildIfNeeded();

        try
        {
            RebuildFromPool();
        }
        catch (Exception ex)
        {
            Debug.LogError("[ShopGrid] RebuildFromPool threw: " + ex);
        }
    }

    // ===== UI helpers =====
    private RectTransform EnsureDragLayer()
    {
        if (dragLayer != null) return dragLayer;

        var root = GetComponentInParent<Canvas>()?.rootCanvas;
        if (root == null) return null;

        var existing = root.transform.Find("DragLayer") as RectTransform;
        if (existing != null) { existing.SetAsLastSibling(); return dragLayer = existing; }

        var go = new GameObject("DragLayer", typeof(RectTransform), typeof(CanvasGroup));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(root.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false; // dropit läpäisee

        return dragLayer = rt;
    }

    public void BuildIfNeeded()
    {
        if (_built || grid == null || slotPrefab == null) return;

        // Tyhjennä varmuuden vuoksi
        for (int i = grid.transform.childCount - 1; i >= 0; i--)
            Destroy(grid.transform.GetChild(i).gameObject);

        // Luo täsmälleen 4 slottia
        for (int i = 0; i < Columns; i++)
        {
            var slotGO = Instantiate(slotPrefab, grid.transform);

            // Tausta/skin
            var skin = slotGO.GetComponent<ChessSlotSkin>();
            if (skin == null) skin = slotGO.AddComponent<ChessSlotSkin>();
            skin.lightSprite = lightTile;
            skin.darkSprite = darkTile;
            skin.index = i;
            skin.columnsOverride = Columns; // Varmista ruutujen vuorottelu
            skin.Apply();

            // DropSlot
            var slot = slotGO.GetComponent<DropSlot>() ?? slotGO.AddComponent<DropSlot>();
            slot.kind = SlotKind.Shop;
            slot.index = -1;
            slot.shopView = this;
        }

        EnsureDragLayer();
        _built = true;
    }

    private void RefreshAllSkins()
    {
        if (grid == null) return;
        for (int ui = 0; ui < grid.transform.childCount; ui++)
        {
            var go = grid.transform.GetChild(ui).gameObject;
            var skin = go.GetComponent<ChessSlotSkin>();
            if (skin != null) { skin.index = ui; skin.columnsOverride = Columns; skin.Apply(); }
        }
    }

    public void RefreshAll()
    {
        // TODO: päivitä lukitukset/hinnat jos lisäät varastosaldot jne.
        RefreshAllSkins();
    }

    // ===== Core: build from pool =====
    public void RebuildFromPool()
    {
        if (!_depsReady) { Debug.LogWarning("[ShopGrid] deps not ready"); return; }

        var pd = playerService.Data;
        var candidates = pool.Filtered(pd).ToList();
        Debug.Log($"[ShopGrid] pool='{pool.name}' items={pool.items?.Count ?? 0} -> candidates={candidates.Count}");

        _roll.Clear();
        PickItems(candidates, count: 4, result: _roll, noDupes: preventDuplicatesInRow);

        // Piirrä 4 slottia
        BuildIfNeeded();
        for (int ui = 0; ui < Columns; ui++) RefreshSlot(ui);
        RefreshAllSkins();
    }


    private void PickItems(List<ShopItemDefSO> candidates, int count, List<ShopItemDefSO> result, bool noDupes)
    {
        var rng = new System.Random();
        int safe = 1000;
        while (result.Count < count && safe-- > 0 && candidates.Count > 0)
        {
            var pick = candidates[rng.Next(candidates.Count)];
            if (noDupes && result.Contains(pick)) continue;
            result.Add(pick);
        }
    }

    private void BindShopIcon(Transform parent, ShopItemDefSO item)
    {
        if (pieceIconPrefab == null)
        {
            Debug.LogError("[ShopGrid] pieceIconPrefab is NULL – set it in Inspector.");
            return;
        }

        var iconGO = Instantiate(pieceIconPrefab, parent);

        // --- 1) DRAG PAYLOAD ENNEN UI:TA ---
        var drag = iconGO.GetComponent<UIDraggablePiece>() ?? iconGO.AddComponent<UIDraggablePiece>();

        if (item != null)
        {
            // UUSI: talletetaan koko ShopItemDefSO dragille
            drag.shopDef = item;

            if (item.piece != null)
            {
                drag.payloadKind = DragPayloadKind.Piece;
                drag.payloadId = item.piece.typeName;
                drag.typeName = item.piece.typeName; // back-compat
            }
            else if (item.powerup != null)
            {
                drag.payloadKind = DragPayloadKind.Powerup;
                drag.payloadId = item.powerup.id;
                drag.typeName = null;
            }
            else if (item.item != null)
            {
                drag.payloadKind = DragPayloadKind.Item;
                drag.payloadId = item.item.id;
                drag.typeName = null;
            }
        }

        drag.originKind = SlotKind.Shop;
        drag.originIndex = parent.GetSiblingIndex();   // UI-slotin indeksi
        drag.shopView = this;
        drag.loadoutView = drag.loadoutView ?? FindObjectOfType<LoadoutGridView>();
        if (drag.loadoutView != null)
            drag.dragLayer = drag.loadoutView.dragLayer ?? drag.dragLayer;

        // --- 2) UI-KOMPONENTIT & LAYOUT ---
        var img = iconGO.GetComponent<Image>() ?? iconGO.AddComponent<Image>();
        img.raycastTarget = true;
        img.enabled = true;

        if (iconGO.GetComponent<CanvasGroup>() == null)
            iconGO.AddComponent<CanvasGroup>(); // tarvitaan dragin aikana läpinäisyys/raycast-ohjaukseen

        var rt = iconGO.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        var le = iconGO.GetComponent<LayoutElement>() ?? iconGO.AddComponent<LayoutElement>();
        le.ignoreLayout = false;
        le.flexibleWidth = 0;
        le.flexibleHeight = 0;
        if (grid != null)
        {
            le.preferredWidth = grid.cellSize.x;
            le.preferredHeight = grid.cellSize.y;
        }

        // --- 3) ICON LOGIIKKA (PieceIcon + hinta) ---
        var icon = iconGO.GetComponent<PieceIcon>();
        if (icon == null)
        {
            Debug.LogError("[ShopGrid] pieceIconPrefab is missing PieceIcon component. Add it to the prefab.", iconGO);
            return;
        }

        // Valitse sprite
        var sprite = (item != null && item.overrideIcon != null)
            ? item.overrideIcon
            : (item != null && item.piece != null ? item.piece.whiteSprite : null);

        // Laske hinta ShopItemDefSO:n kautta
        int price = 0;
        if (item != null)
        {
            if (playerService != null)
            {
                var pd = playerService.Data;
                // edellyttää: public int GetPrice(PlayerData data) ShopItemDefSO:ssa
                price = item.GetPrice(pd);
            }
            else
            {
                // fallback jos playerService puuttuu
                price = item.price;
            }
        }

        // Bindaa kuva + hinta; showPrice aina true shopissa
        icon.Bind(sprite, price, showPrice: true);
    }



    // Palautus shoppiin (drop shopin päälle) — valinnainen myynti
    public void HandleDropToShop(DropSlot target, UIDraggablePiece drag)
    {
        if (drag.originKind != SlotKind.Loadout) return;
        if (drag.loadoutView == null)
        {
            Debug.LogWarning("[ShopGrid] HandleDropToShop: missing loadoutView on drag.");
            return;
        }

        var svcUI = drag.loadoutView;
        var lsvc = svcUI.GetType()
                        .GetField("_loadout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.GetValue(svcUI) as LoadoutService;

        if (lsvc == null)
        {
            Debug.LogWarning("[ShopGrid] HandleDropToShop: LoadoutService not found via reflection.");
            return;
        }

        lsvc.Refund(drag.typeName, ratio: 1f);
        lsvc.SetAt(drag.originIndex, string.Empty);
        svcUI.RefreshAll();
        RefreshAll();
        drag.MarkConsumed(-1);
    }


    private void RefreshSlot(int uiIndex)
    {
        var slotTr = grid.transform.GetChild(uiIndex);
        // tyhjennä
        for (int i = slotTr.childCount - 1; i >= 0; i--) Destroy(slotTr.GetChild(i).gameObject);

        _uiToItem.Remove(uiIndex);

        if (uiIndex >= _roll.Count || _roll[uiIndex] == null) return; // tyhjä slotti

        var item = _roll[uiIndex];
        _uiToItem[uiIndex] = item;
        BindShopIcon(slotTr, item); // luo ikonit & drag
    }

    public bool TryPurchase(UIDraggablePiece drag, out int price, out string reason)
    {
        price = 0;
        reason = null;

        if (playerService == null)
        {
            reason = "player svc missing";
            Debug.LogError("[Shop] PlayerService missing in TryPurchase");
            return false;
        }

        // 1) Selvitä mikä ShopItemDefSO on kyseessä
        ShopItemDefSO picked = null;

        // a) ensisijaisesti suoraan dragista
        if (drag != null && drag.shopDef != null)
        {
            picked = drag.shopDef;
        }

        // b) fallback: slot-indeksin kautta
        if (picked == null && drag != null && _uiToItem.TryGetValue(drag.originIndex, out var byUi))
        {
            picked = byUi;
        }

        // c) fallback: etsi _roll-listasta payloadId:n perusteella
        if (picked == null && drag != null)
        {
            picked = _roll.FirstOrDefault(it =>
                (it?.piece != null && it.piece.typeName == drag.payloadId) ||
                (it?.powerup != null && it.powerup.id == drag.payloadId) ||
                (it?.item != null && it.item.id == drag.payloadId));
        }

        if (picked == null)
        {
            reason = "item not in roll";
            Debug.LogWarning($"[Shop] TryPurchase: item not found for payloadId={drag?.payloadId}");
            return false;
        }

        var pd = playerService.Data;
        var coinsBefore = pd.coins;
        Debug.Log($"[Shop] TryPurchase payloadId={drag?.payloadId}, picked={picked.name}, coinsBefore={coinsBefore}");

        // 2) Laske hinta suoraan ShopItemDefSO:n kautta
        price = picked.GetPrice(pd);
        Debug.Log($"[Shop] Computed price={price} for {picked.name}");

        // Turva: 0- tai negatiivinen hinta EI ole ok
        if (price <= 0)
        {
            reason = "invalid price";
            Debug.LogError($"[Shop] INVALID PRICE {price} for {picked.name} (payloadId={drag?.payloadId}). Check ShopItemDefSO.price.");
            return false;
        }

        // 3) Yritä veloittaa kolikot
        if (!playerService.SpendCoins(price))
        {
            reason = "not enough coins";
            Debug.Log($"[Shop] SpendCoins FAILED: coinsBefore={coinsBefore}, needed={price}");
            return false;
        }

        Debug.Log($"[Shop] SpendCoins OK: {coinsBefore} -> {playerService.Data.coins}");

        // 4) Poista ostettu itemi tämän runin rollista (tilalle ei tule uutta)
        int idx = _roll.IndexOf(picked);
        if (idx >= 0)
        {
            _roll[idx] = null;
            RefreshSlot(idx);
        }

        return true;
    }




}
