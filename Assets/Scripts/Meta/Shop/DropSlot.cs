using UnityEngine;
using UnityEngine.EventSystems;


public class DropSlot : MonoBehaviour, IDropHandler
{

    [Header("Meta")]
    public SlotKind kind;              // <-- t�m� kentt� oli puuttunut
    public int index = -1;

    [Header("Refs")]
    public LoadoutGridView loadoutView;
    public ShopGridView shopView;

    public void OnDrop(PointerEventData eventData)
    {
        var drag = eventData.pointerDrag?.GetComponent<UIDraggablePiece>();
        if (drag == null) return;

        switch (kind)
        {
            case SlotKind.Loadout when loadoutView != null:
                loadoutView.HandleDropToLoadout(this, drag);
                break;

            case SlotKind.Shop when shopView != null:
                shopView.HandleDropToShop(this, drag);
                break;

            default:
                Debug.LogWarning($"[DropSlot] Unknown kind={kind} or missing reference.");
                break;
        }
    }
}
