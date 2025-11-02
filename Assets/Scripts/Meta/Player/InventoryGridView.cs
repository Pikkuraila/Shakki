using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class InventoryGridView : MonoBehaviour
{
    public GridLayoutGroup grid;
    public GameObject slotPrefab;
    public GameObject itemIconPrefab;
    public ShopPricingSO pricing;

    readonly List<GameObject> _slots = new();
    List<string> _items; // PlayerData:an esim. List<string> inventoryIds

    void Awake()
    {
        EnsureData();
        Build();
        RefreshAll();
    }

    void EnsureData()
    {
        var pd = PlayerService.Instance.Data;
        if (pd.inventoryIds == null) pd.inventoryIds = new List<string>();
        _items = pd.inventoryIds;
    }

    void Build()
    {
        foreach (Transform c in grid.transform) Destroy(c.gameObject);
        int count = Mathf.Max(_items.Count, 8); // vähintään 8 paikkaa
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(slotPrefab, grid.transform);
            var drop = go.GetComponent<DropSlot>() ?? go.AddComponent<DropSlot>();
            drop.kind = SlotKind.Inventory;   // NEW
            drop.index = i;
            drop.inventoryView = this;        // lisää kenttä DropSlotille jos haluat
            _slots.Add(go);
        }
    }

    public void RefreshAll()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            var cell = _slots[i];
            for (int c = cell.transform.childCount - 1; c >= 0; c--) Destroy(cell.transform.GetChild(c).gameObject);
            if (i >= _items.Count) continue;
            var id = _items[i];
            if (string.IsNullOrEmpty(id)) continue;

            var icon = Instantiate(itemIconPrefab, cell.transform);
            // bindaa sprite nimen/id:n mukaan, ei tarvitse hintaa
        }
    }

    // SHOP → INVENTORY
    public void HandleDropToInventory(DropSlot target, UIDraggablePiece drag)
    {
        if (drag.originKind != SlotKind.Shop) return;
        if (drag.payloadKind == DragPayloadKind.Piece) return; // nappulat eivät mene inventoryyn

        if (!HasSpace(target.index)) return;

        if (PlayerService.Instance != null)
        {
            var svc = new LoadoutService(PlayerService.Instance, /*catalog*/ null, pricing);
            if (!svc.CanAfford(drag.payloadId)) return;
            if (!svc.TryBuy(drag.payloadId)) return;
        }

        AddAt(target.index, drag.payloadId);
        RefreshAll();
        drag.MarkConsumed(target.index);
    }

    bool HasSpace(int index) => index < _items.Count || _items.Count < _slots.Count;

    void AddAt(int index, string id)
    {
        while (_items.Count <= index) _items.Add("");
        _items[index] = id;
        PlayerService.Instance.Save();
    }
}
