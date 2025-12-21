using UnityEngine;
using UnityEngine.EventSystems;


public class DropSlot : MonoBehaviour, IDropHandler
{
    [Header("Meta")]
    public SlotKind kind;
    public int index = -1;

    [Header("Refs")]
    public LoadoutGridView loadoutView;
    public ShopGridView shopView;
    public InventoryGridView inventoryView;

    // UUSI
    public AlchemistEncounterView alchemistView;

    public void OnDrop(PointerEventData eventData)
    {
        // --- CANONICAL DROPSLOT: jos samassa GO:ssa on useampi DropSlot,
        // vain yksi niistä saa käsitellä eventin ---
        var all = GetComponents<DropSlot>();
        if (all.Length > 1)
        {
            DropSlot chosen = null;

            // Loadout: valitse se jolla on validi index
            if (kind == SlotKind.Loadout)
                chosen = System.Array.Find(all, s => s.kind == SlotKind.Loadout && s.index >= 0);

            // Muut: valitse se jolla on sama kind (ja mieluiten validi index)
            if (chosen == null)
                chosen = System.Array.Find(all, s => s.kind == kind && s.index >= 0);

            if (chosen == null)
                chosen = System.Array.Find(all, s => s.kind == kind);

            // Fallback
            if (chosen == null) chosen = all[0];

            if (chosen != this)
                return; // <- estä tuplakäsittely / haamuslotti
        }

        // Nyt on turvallista guardata
        if (kind == SlotKind.Loadout && index < 0)
        {
            Debug.LogWarning($"[DropSlot] Loadout drop hit slot with index={index} on {name}. This object should not be a drop target.");
            return;
        }

        if (alchemistView == null && (kind == SlotKind.AlchemistInput || kind == SlotKind.AlchemistOutput))
            alchemistView = GetComponentInParent<AlchemistEncounterView>(true);

        var drag = eventData.pointerDrag
            ? eventData.pointerDrag.GetComponent<UIDraggablePiece>()
            : null;

        // 1) heti tähän: peruslogit
        Debug.Log($"[DropSlot] OnDrop kind={kind} index={index} drag={(drag ? drag.name : "NULL")}");

        // 2) heti tähän: referenssit (täällä näet alchemistView/shopView/loadoutView nullit)
        Debug.Log($"[DropSlot] refs loadoutView={(loadoutView ? "OK" : "NULL")} shopView={(shopView ? "OK" : "NULL")} alchemistView={(alchemistView ? "OK" : "NULL")}");

        Debug.Log($"[DropSlot] self='{name}' id={GetInstanceID()} kind={kind} index={index} parent={transform.parent?.name}");


        // 3) heti tähän: drag-metat (origin/payload)
        if (drag != null)
            Debug.Log($"[DropSlot] drag originKind={drag.originKind} originIndex={drag.originIndex} payloadId={drag.payloadId} payloadKind={drag.payloadKind}");

        if (drag == null) return;

      
        switch (kind)
        {
            case SlotKind.Loadout when loadoutView != null:
                loadoutView.HandleDropToLoadout(this, drag);
                break;

            case SlotKind.Shop when shopView != null:
                shopView.HandleDropToShop(this, drag);
                break;

            case SlotKind.Inventory when inventoryView != null:
                inventoryView.HandleDropToInventory(this, drag);
                break;

            case SlotKind.AlchemistInput when alchemistView != null:
                alchemistView.HandleDropToInput(this, drag);
                break;

            case SlotKind.AlchemistOutput when alchemistView != null:
                alchemistView.HandleDropToOutput(this, drag);
                break;

            default:
                Debug.LogWarning($"[DropSlot] Unknown kind={kind} or missing reference.");
                break;
        }

        Debug.Log($"[DropSlot] drag originKind={drag.originKind} originIndex={drag.originIndex} payloadId={drag.payloadId}");

        static string Path(Transform t)
        {
            var p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
        Debug.Log($"[DropSlot] path={Path(transform)} comps={GetComponents<DropSlot>().Length}");


    }

}
