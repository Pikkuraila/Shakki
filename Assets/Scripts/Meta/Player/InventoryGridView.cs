using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InventoryGridView : MonoBehaviour
{
    [Header("Refs")]
    public GridLayoutGroup grid;
    public PlayerService playerService;
    public GameCatalogSO gameCatalog;
    public RunController runController;

    [Header("Config")]
    public int columns = 8;
    public int rows = 2;
    public Vector2 cellSize = new Vector2(72f, 72f);
    public Vector2 spacing = new Vector2(6f, 6f);
    public RectTransform dragLayer;

    readonly List<GameObject> _slots = new();
    bool _built;

    PlayerService PS => playerService != null ? playerService : PlayerService.Instance;
    int TotalSlots => Mathf.Max(1, columns * rows);

    public void Bind(PlayerService service, GameCatalogSO catalog, RunController controller)
    {
        playerService = service;
        gameCatalog = catalog;
        runController = controller;
    }

    public void BuildIfNeeded()
    {
        if (_built)
            return;

        EnsureGrid();
        EnsureDragLayer();
        EnsureSlots();
        _built = true;
        RefreshAll();
    }

    public void RefreshAll()
    {
        if (!_built)
            BuildIfNeeded();

        var service = PS;
        if (service == null)
            return;

        for (int i = 0; i < _slots.Count; i++)
        {
            var cell = _slots[i];
            for (int c = cell.transform.childCount - 1; c >= 0; c--)
                Destroy(cell.transform.GetChild(c).gameObject);

            var itemId = service.GetInventoryItemIdAt(i, TotalSlots);
            if (string.IsNullOrEmpty(itemId))
                continue;

            CreateIcon(cell.transform, itemId, i);
        }
    }

    public void HandleDropToInventory(DropSlot target, UIDraggablePiece drag)
    {
        if (target == null || drag == null)
            return;

        Debug.Log($"[Inventory] Drop target={target.index} origin={drag.originKind} payloadKind={drag.payloadKind} payload={drag.payloadId}");

        if (drag.originKind != SlotKind.Shop || drag.payloadKind != DragPayloadKind.Item)
            return;

        var service = PS;
        if (service == null || !service.HasInventorySpace(TotalSlots))
        {
            Debug.Log("[Inventory] Drop rejected: no service or no space.");
            return;
        }

        var shop = drag.shopView;
        if (shop == null)
        {
            Debug.LogWarning("[Inventory] Drop rejected: drag.shopView missing.");
            return;
        }

        if (!string.IsNullOrEmpty(service.GetInventoryItemIdAt(target.index, TotalSlots)))
        {
            Debug.Log($"[Inventory] Drop rejected: slot {target.index} occupied.");
            return;
        }

        if (!shop.TryPurchase(drag, out _, out _))
        {
            Debug.Log("[Inventory] Drop rejected: purchase failed.");
            return;
        }

        if (!service.TryAddInventoryItem(drag.payloadId, target.index, TotalSlots))
        {
            Debug.Log($"[Inventory] Drop rejected: could not add item {drag.payloadId} to slot {target.index}.");
            return;
        }

        RefreshAll();
        drag.MarkConsumed(target.index);
        Debug.Log($"[Inventory] Added item {drag.payloadId} to slot {target.index}.");
    }

    private void EnsureGrid()
    {
        if (grid != null)
            return;

        var gridObject = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridObject.transform.SetParent(transform, false);
        var rect = gridObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(12f, 12f);
        rect.offsetMax = new Vector2(-12f, -12f);
        grid = gridObject.GetComponent<GridLayoutGroup>();
    }

    private void EnsureSlots()
    {
        if (grid == null)
            return;

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.cellSize = cellSize;
        grid.spacing = spacing;

        for (int i = grid.transform.childCount - 1; i >= 0; i--)
            Destroy(grid.transform.GetChild(i).gameObject);

        _slots.Clear();

        for (int i = 0; i < TotalSlots; i++)
        {
            var slotGO = CreateSlot(i);
            _slots.Add(slotGO);
        }
    }

    private GameObject CreateSlot(int index)
    {
        var slotGO = new GameObject($"InventorySlot_{index}", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(DropSlot));
        slotGO.transform.SetParent(grid.transform, false);

        var image = slotGO.GetComponent<Image>();
        image.color = ((index + Mathf.FloorToInt(index / Mathf.Max(1, columns))) & 1) == 0
            ? new Color(0.86f, 0.82f, 0.74f, 0.92f)
            : new Color(0.70f, 0.63f, 0.52f, 0.92f);

        var layout = slotGO.GetComponent<LayoutElement>();
        layout.preferredWidth = cellSize.x;
        layout.preferredHeight = cellSize.y;

        var drop = slotGO.GetComponent<DropSlot>();
        drop.kind = SlotKind.Inventory;
        drop.index = index;
        drop.inventoryView = this;

        return slotGO;
    }

    private void CreateIcon(Transform parent, string itemId, int slotIndex)
    {
        var iconGO = new GameObject($"Item_{itemId}", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(LayoutElement), typeof(UIDraggablePiece));
        iconGO.transform.SetParent(parent, false);

        var rect = iconGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var layout = iconGO.GetComponent<LayoutElement>();
        layout.preferredWidth = cellSize.x;
        layout.preferredHeight = cellSize.y;

        var image = iconGO.GetComponent<Image>();
        image.preserveAspect = true;
        image.color = Color.white;

        var def = gameCatalog != null ? gameCatalog.GetItemById(itemId) : null;
        if (def != null && def.icon != null)
        {
            image.sprite = def.icon;
        }
        else
        {
            image.color = new Color(0.45f, 0.46f, 0.48f, 1f);
            CreateFallbackLabel(iconGO.transform, def != null ? def.displayName : itemId);
        }

        var drag = iconGO.GetComponent<UIDraggablePiece>();
        drag.payloadKind = DragPayloadKind.Item;
        drag.payloadId = itemId;
        drag.typeName = itemId;
        drag.originKind = SlotKind.Inventory;
        drag.originIndex = slotIndex;
        drag.dragLayer = dragLayer;
        drag.useDragLayer = dragLayer != null;
        drag.runController = runController;
    }

    private static void CreateFallbackLabel(Transform parent, string text)
    {
        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(parent, false);

        var rect = labelGO.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 4f);
        rect.offsetMax = new Vector2(-4f, -4f);

        var label = labelGO.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 8f;
        label.fontSizeMax = 18f;
        label.color = Color.white;
        label.raycastTarget = false;
    }

    private void EnsureDragLayer()
    {
        if (dragLayer != null)
            return;

        var rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas == null)
            return;

        var existing = rootCanvas.transform.Find("DragLayer") as RectTransform;
        if (existing != null)
        {
            dragLayer = existing;
            return;
        }

        var go = new GameObject("DragLayer", typeof(RectTransform), typeof(CanvasGroup));
        dragLayer = go.GetComponent<RectTransform>();
        dragLayer.SetParent(rootCanvas.transform, false);
        dragLayer.anchorMin = Vector2.zero;
        dragLayer.anchorMax = Vector2.one;
        dragLayer.offsetMin = Vector2.zero;
        dragLayer.offsetMax = Vector2.zero;

        var group = go.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
    }
}
