// LoadoutGridView.cs (DROP-IN SAFE VERSION)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadoutGridView : MonoBehaviour
{
    [Header("Refs")]
    public GridLayoutGroup grid;
    public GameObject slotPrefab;       // sisältää DropSlot
    public GameObject pieceIconPrefab;  // sisältää PieceIcon / Image (ei pakollinen)
    public PlayerService playerService; // optional: jos tyhjä, fallbackaa singletoniin
    public GameCatalogSO gameCatalog;
    public TMPro.TMP_Text coinsText;

    [Header("Layout / Mapping")]
    public SlotMapSO slotMap;      // <<< aseta Inspectorissa
    public int boardWidth = 8;     // <<< yleensä 8
    public int boardHeight = 2;    // <<< yleensä 2 (backline + pawnline)
    public bool uiOriginTopLeft = false;

    [Header("Drag")]
    public RectTransform dragLayer; // <<< aseta Inspectorissa (saman Canvasin child)

    [Header("Drop Rules")]
    public bool allowLoadoutSwap = true;
    public bool allowShopDrops = true;
    public bool allowAlchemistDrops = true;
    public bool allowPowerupDrops = true;

    readonly List<GameObject> _slots = new(); // UI slot GameObjectit (visuaalinen järjestys)
    bool _built;
    LoadoutService _loadout;
    Dictionary<string, PieceDefSO> _defByType;

    // --- apu: UI-järjestyksen -> data-slotin indeksi ---
    int[] _uiToSlot;

    // Estetään välivälähdys vedon päättyessä:
    readonly HashSet<int> _suppressDataOnce = new();

    // --- Safe accessors (DROP-IN): käytä bindattua, muuten singleton ---
    PlayerService PS => playerService != null ? playerService : PlayerService.Instance;
    PlayerData PD => PS != null ? PS.Data : null;

    void OnEnable() { UIDraggablePiece.AnyDragEnded += OnAnyDragEnded; }
    void OnDisable() { UIDraggablePiece.AnyDragEnded -= OnAnyDragEnded; }

    // Optional: voit kutsua tätä hostista (Shop/Macro), mutta ei pakollinen.
    public void Bind(PlayerService ps, GameCatalogSO catalog, LoadoutService loadout = null)
    {
        playerService = ps;
        gameCatalog = catalog;
        _loadout = loadout;

        _defByType = gameCatalog != null ? gameCatalog.BuildPieceLookup() : null;
    }

    public void Unbind()
    {
        playerService = null;
        gameCatalog = null;
        _loadout = null;
        _defByType = null;
    }

    void OnAnyDragEnded()
    {
        // Vedon aikana siivous ohitettiin → tee varma “post-drag” -refresh
        StartCoroutine(CoRefreshAfterDrag());
    }

    System.Collections.IEnumerator CoRefreshAfterDrag()
    {
        // odotetaan, että UIDraggablePiece ehtii palauttaa parentin jne.
        yield return null;                       // 1 frame
        RefreshAll();                            // piirrä ilman lähdeslottia (suppress päällä)
        yield return new WaitForEndOfFrame();    // anna UIn “asettua”
        _suppressDataOnce.Clear();               // poista estot vasta aivan lopuksi
        RefreshAll();                            // final
    }

    void SuppressDataIndexOnce(int dataIndex)
    {
        if (dataIndex >= 0) _suppressDataOnce.Add(dataIndex);
    }

    void Awake()
    {
        // Älä kaada jos catalog puuttuu (macro/shop voi bindata myöhemmin)
        if (gameCatalog != null)
            _defByType = gameCatalog.BuildPieceLookup();
    }

    public void BuildIfNeeded()
    {
        if (_built) return;

        if (boardWidth * boardHeight != 16)
            Debug.LogWarning($"[LoadoutGridView] Grid is {boardWidth}x{boardHeight} != 16. Mapping/PlayerData assumes 16.");

        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = boardWidth > 0 ? boardWidth : 8;

            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;

            // VARMISTA järkevä cellSize ja spacing
            if (grid.cellSize.x < 8f || grid.cellSize.y < 8f)
                grid.cellSize = new Vector2(80, 80);
            if (grid.spacing == Vector2.zero)
                grid.spacing = new Vector2(0, 0);
        }

        // Tyhjennä
        if (grid != null)
        {
            for (int i = grid.transform.childCount - 1; i >= 0; i--)
                Destroy(grid.transform.GetChild(i).gameObject);
        }
        _slots.Clear();

        EnsureDataSlots(boardWidth * boardHeight);
        BuildUiToSlotMapping();

        // Luo solut UI-järjestyksessä, mutta anna DropSlot.indexiksi DATA-indeksi
        int total = boardWidth * boardHeight;
        for (int ui = 0; ui < total; ui++)
        {
            var slotGO = Instantiate(slotPrefab, grid.transform);
            _slots.Add(slotGO);

            var slotRT = (RectTransform)slotGO.transform;
            slotRT.anchorMin = new Vector2(0, 0);
            slotRT.anchorMax = new Vector2(1, 1);
            slotRT.offsetMin = Vector2.zero;
            slotRT.offsetMax = Vector2.zero;

            var slotLE = slotGO.GetComponent<LayoutElement>() ?? slotGO.AddComponent<LayoutElement>();
            slotLE.ignoreLayout = false;
            slotLE.flexibleWidth = 0;
            slotLE.flexibleHeight = 0;
            slotLE.preferredWidth = grid != null ? grid.cellSize.x : 80f;
            slotLE.preferredHeight = grid != null ? grid.cellSize.y : 80f;

            var bg = slotGO.GetComponent<Image>() ?? slotGO.AddComponent<Image>();
            bg.color = Color.white;

            var skin = slotGO.GetComponent<ChessSlotSkin>();
            if (skin != null)
            {
                skin.index = ui;
                skin.columnsOverride = boardWidth;
                skin.Apply();
            }

            var dataIndex = _uiToSlot[ui];

            // Varmista että slottiin jää tasan yksi DropSlot
            var dss = slotGO.GetComponents<DropSlot>();
            for (int k = 1; k < dss.Length; k++)
                Destroy(dss[k]);

            var slot = dss.Length > 0 ? dss[0] : slotGO.AddComponent<DropSlot>();
            slot.kind = SlotKind.Loadout;
            slot.index = dataIndex;
            slot.loadoutView = this;
        }

        EnsureDragLayer();

        _built = true;
        Debug.Log($"[LoadoutGridView] Built {_slots.Count} UI slots. Children in grid: {grid.transform.childCount}");
        RefreshAll();
    }

    void BuildUiToSlotMapping()
    {
        int total = boardWidth * boardHeight;
        _uiToSlot = Enumerable.Repeat(-1, total).ToArray();

        if (slotMap == null || slotMap.whiteSlotCoords == null || slotMap.whiteSlotCoords.Length != 16)
        {
            Debug.LogError("[LoadoutGridView] SlotMapSO puuttuu tai väärän mittainen.");
            if (total == 16) for (int i = 0; i < 16; i++) _uiToSlot[i] = i;
            Debug.Log($"[LoadoutGridView] UI→DATA map (fallback): [{string.Join(",", _uiToSlot)}]");
            return;
        }

        slotMap.ValidateBounds(boardWidth, boardHeight);

        for (int dataIndex = 0; dataIndex < 16; dataIndex++)
        {
            var c = slotMap.whiteSlotCoords[dataIndex];
            int y = uiOriginTopLeft ? c.y : (boardHeight - 1 - c.y); // Y-FLIP
            int uiIndex = y * boardWidth + c.x;

            if (uiIndex < 0 || uiIndex >= total)
            {
                Debug.LogWarning($"[LoadoutGridView] Coord ({c.x},{c.y}) ulkona {boardWidth}x{boardHeight} (dataIndex {dataIndex})");
                continue;
            }
            if (_uiToSlot[uiIndex] != -1)
                Debug.LogWarning($"[LoadoutGridView] UI-solussa {uiIndex} jo dataIndex={_uiToSlot[uiIndex]} (duplikaatti koordissa {c})");

            _uiToSlot[uiIndex] = dataIndex;
        }

        for (int ui = 0; ui < total; ui++)
            if (_uiToSlot[ui] == -1) _uiToSlot[ui] = ui;

        Debug.Log($"[LoadoutGridView] UI→DATA map: [{string.Join(",", _uiToSlot)}]");
        Debug.Log($"[LoadoutGridView] uiOriginTopLeft={uiOriginTopLeft} (flipY={!uiOriginTopLeft})");
    }

    void EnsureDataSlots(int count)
    {
        if (PD == null)
        {
            Debug.LogWarning("[LoadoutGridView] PlayerData missing (PS null). Ensure PlayerService exists or Bind() before BuildIfNeeded.");
            return;
        }

        if (PD.loadoutSlots == null || PD.loadoutSlots.Count != count)
        {
            PD.loadoutSlots = LoadoutModel.Expand(PD.loadout ?? new(), count, "");
            PS.Save();
            Debug.Log("[LoadoutGridView] Expanded slots from meta.");
        }

        string S(string x) => string.IsNullOrEmpty(x) ? "-" : x;
        Debug.Log($"[LoadoutGridView] Data slots[{PD.loadoutSlots.Count}] = [{string.Join(",", PD.loadoutSlots.Select(S))}]");
    }

    public void RefreshAll()
    {
        if (!_built) { Debug.LogWarning("[LoadoutGridView] RefreshAll before build"); return; }
        if (PD == null) { Debug.LogWarning("[LoadoutGridView] RefreshAll skipped: no PlayerData"); return; }

        if (coinsText != null)
            coinsText.text = _loadout?.GetCoins().ToString() ?? PD.coins.ToString();

        EnsureDataSlots(_slots.Count);

        // 1) Päivitä shakkitaustat (UI-indeksin mukaan)
        for (int ui = 0; ui < _slots.Count; ui++)
        {
            var skin = _slots[ui].GetComponent<ChessSlotSkin>();
            if (skin != null)
            {
                skin.index = ui;
                skin.Apply();
            }
        }

        // 2) Piirrä ikonit DATA-indeksin mukaan
        for (int ui = 0; ui < _slots.Count; ui++)
            RefreshSlotByUiIndex(ui);
    }

    void RefreshSlotByUiIndex(int uiIndex)
    {
        var cell = _slots[uiIndex];
        int dataIndex = _uiToSlot[uiIndex];

        if (_suppressDataOnce.Contains(dataIndex))
            return;

        string type = GetTypeAt(dataIndex);

        // Pidä DropSlot ajan tasalla AINA
        var drop = cell.GetComponent<DropSlot>() ?? cell.AddComponent<DropSlot>();
        drop.kind = SlotKind.Loadout;
        drop.index = dataIndex;
        drop.loadoutView = this;

        if (UIDraggablePiece.s_IsDraggingAny)
        {
            if (!string.IsNullOrEmpty(type))
            {
                bool hasIcon = cell.transform.childCount > 0 &&
                               cell.GetComponentInChildren<Image>(true) != null;
                if (!hasIcon)
                    CreateIconIntoCell(cell, type, dataIndex, uiIndex);
            }
            return;
        }

        for (int i = cell.transform.childCount - 1; i >= 0; i--)
            Destroy(cell.transform.GetChild(i).gameObject);

        if (string.IsNullOrEmpty(type))
            return;

        CreateIconIntoCell(cell, type, dataIndex, uiIndex);
    }

    void CreateIconIntoCell(GameObject cell, string type, int dataIndex, int uiIndex)
    {
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform));
        iconGO.transform.SetParent(cell.transform, false);

        var iconRT = (RectTransform)iconGO.transform;
        StretchRectToParent(iconRT);

        UIDraggablePiece.EnsureIconVisible(iconGO);
        var img = iconGO.GetComponent<Image>();

        var le = iconGO.GetComponent<LayoutElement>() ?? iconGO.AddComponent<LayoutElement>();
        le.ignoreLayout = false;
        le.flexibleWidth = 0f;
        le.flexibleHeight = 0f;
        le.preferredWidth = grid != null ? grid.cellSize.x : 80f;
        le.preferredHeight = grid != null ? grid.cellSize.y : 80f;

        Sprite sprite = null;
        if (gameCatalog != null)
        {
            var def = gameCatalog.GetPieceById(type);
            sprite = def != null ? def.whiteSprite : null;
        }

        if (sprite != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
        }
        else
        {
            img.color = new Color(0.2f, 0.6f, 0.9f, 0.85f);
            var lblGO = new GameObject("Label", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            lblGO.transform.SetParent(iconGO.transform, false);
            var lblRT = (RectTransform)lblGO.transform;
            StretchRectToParent(lblRT);
            var tmp = lblGO.GetComponent<TMPro.TextMeshProUGUI>();
            tmp.text = type;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.raycastTarget = false;
        }

        var drag = iconGO.GetComponent<UIDraggablePiece>() ?? iconGO.AddComponent<UIDraggablePiece>();

        drag.payloadId = type;
        drag.typeName = type;
        drag.payloadKind = DragPayloadKind.Piece;

        drag.originKind = SlotKind.Loadout;
        drag.originIndex = uiIndex; // UI index (muunnetaan swapissa dataindexiksi)

        drag.loadoutView = this;
        drag.dragLayer = this.dragLayer;
        drag.useDragLayer = (this.dragLayer != null);

        iconGO.name = $"Icon_{type}_ui{uiIndex}_data{dataIndex}";
    }

    static void StretchRectToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // --- Data-luku/kirjoitus ---
    string GetTypeAt(int dataIndex)
    {
        if (PD == null || PD.loadoutSlots == null) return "";
        var slots = PD.loadoutSlots;
        if (dataIndex < 0 || dataIndex >= slots.Count) return "";
        return slots[dataIndex] ?? "";
    }

    void SetTypeAt(int dataIndex, string pieceId)
    {
        if (PD == null || PD.loadoutSlots == null) return;
        var slots = PD.loadoutSlots;
        if (dataIndex < 0 || dataIndex >= slots.Count) return;
        slots[dataIndex] = string.IsNullOrEmpty(pieceId) ? "" : pieceId;
        PS.Save();
    }

    public void HandleDropToLoadout(DropSlot target, UIDraggablePiece drag)
    {
        if (drag == null || target == null) return;
        if (PD == null) return;

        // 0) Normalisoi target dataIndex
        int targetDataIndex = target.index;

        if (targetDataIndex < 0)
        {
            int ui = _slots.IndexOf(target.gameObject);
            if (ui >= 0) targetDataIndex = UiToDataIndex(ui);
        }

        if (targetDataIndex < 0 || targetDataIndex >= PD.loadoutSlots.Count)
        {
            Debug.LogWarning($"[LoadoutDrop] Invalid target index={target.index} (resolved={targetDataIndex}) targetGO={target.name}");
            return;
        }

        bool TargetEmpty() => string.IsNullOrEmpty(GetTypeAt(targetDataIndex));

        // --- Policy gates (MACRO SAFE) ---
        if (!allowShopDrops && drag.originKind == SlotKind.Shop) return;
        if (!allowAlchemistDrops && drag.originKind == SlotKind.AlchemistOutput) return;
        if (!allowPowerupDrops && drag.payloadKind == DragPayloadKind.Powerup) return;

        // --- X) ALCHEMIST OUTPUT → LOADOUT ---
        if (allowAlchemistDrops &&
            drag.originKind == SlotKind.AlchemistOutput &&
            drag.payloadKind == DragPayloadKind.Piece)
        {
            if (!TargetEmpty()) return;

            if (drag.alchemistView == null)
            {
                Debug.LogWarning("[LoadoutDrop] AlchemistOutput dropped but drag.alchemistView is NULL");
                return;
            }

            // FIX: käytä normalisoitua targetDataIndex
            SuppressDataIndexOnce(targetDataIndex);

            drag.alchemistView.ConsumeOutputToLoadout(targetDataIndex, drag);

            StartCoroutine(CoRefreshAfterDrag());
            drag.MarkConsumed(targetDataIndex);
            return;
        }

        // --- A) SHOP → LOADOUT: PIECE ostetaan tyhjään ruutuun ---
        if (allowShopDrops &&
            drag.originKind == SlotKind.Shop &&
            drag.payloadKind == DragPayloadKind.Piece)
        {
            if (!TargetEmpty())
            {
                Debug.Log("[LoadoutDrop] target slot not empty, cannot buy.");
                return;
            }

            var shop = drag.shopView;
            if (shop == null)
            {
                Debug.LogWarning("[LoadoutDrop] drag.shopView missing, cannot purchase from shop.");
                return;
            }

            if (!shop.TryPurchase(drag, out var price, out var reason))
            {
                Debug.Log($"[LoadoutDrop] purchase failed: {reason}");
                return;
            }

            SuppressDataIndexOnce(targetDataIndex);
            SetTypeAt(targetDataIndex, drag.payloadId);

            StartCoroutine(CoRefreshAfterDrag());
            drag.MarkConsumed(targetDataIndex);
            return;
        }

        // --- B) SHOP → POWERUP ---
        if (allowShopDrops &&
            allowPowerupDrops &&
            drag.originKind == SlotKind.Shop &&
            drag.payloadKind == DragPayloadKind.Powerup)
        {
            var shop = drag.shopView;
            if (shop == null)
            {
                Debug.LogWarning("[LoadoutDrop] no shopView on drag (powerup)");
                return;
            }

            if (!shop.TryPurchase(drag, out var price, out var reason))
            {
                Debug.Log($"[LoadoutDrop] powerup purchase failed: {reason}");
                return;
            }

            PS.AddPowerup(drag.payloadId, 1);

            StartCoroutine(CoRefreshAfterDrag());
            drag.MarkConsumed(targetDataIndex);
            return;
        }

        // --- D) LOADOUT ↔ LOADOUT swap ---
        if (allowLoadoutSwap && drag.originKind == SlotKind.Loadout)
        {
            int originDataIndex = UiToDataIndex(drag.originIndex);
            if (originDataIndex < 0 || originDataIndex >= PD.loadoutSlots.Count)
                return;

            if (originDataIndex == targetDataIndex) return;

            SuppressDataIndexOnce(originDataIndex);

            var a = GetTypeAt(originDataIndex);
            var b = GetTypeAt(targetDataIndex);
            SetTypeAt(originDataIndex, b);
            SetTypeAt(targetDataIndex, a);

            StartCoroutine(CoRefreshAfterDrag());
            drag.MarkConsumed(targetDataIndex);
            return;
        }
    }

    RectTransform EnsureDragLayer()
    {
        if (dragLayer != null) return dragLayer;

        var root = GetComponentInParent<Canvas>()?.rootCanvas;
        if (root == null) return null;

        var existing = root.transform.Find("DragLayer") as RectTransform;
        if (existing != null)
        {
            dragLayer = existing;
            dragLayer.SetAsLastSibling();
            return dragLayer;
        }

        var go = new GameObject("DragLayer", typeof(RectTransform), typeof(CanvasGroup));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(root.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();

        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        dragLayer = rt;
        return dragLayer;
    }

    public int UiToDataIndex(int uiIndex)
    {
        if (_uiToSlot == null || uiIndex < 0 || uiIndex >= _uiToSlot.Length) return uiIndex;
        return _uiToSlot[uiIndex];
    }
}
