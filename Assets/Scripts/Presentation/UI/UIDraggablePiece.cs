// Assets/Scripts/Presentation/UI/UIDraggablePiece.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Shakki.Presentation.Inspect;

public sealed class UIDraggablePiece :
    MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler

{
    // --- Julkiset metatiedot (näitä muu koodi täyttää) ---
    public string typeName;
    public SlotKind originKind;
    public int originIndex = -1;
    public LoadoutGridView loadoutView;  // saa olla null
    public ShopGridView shopView;        // saa olla null
    public static event System.Action AnyDragEnded;

    public DragPayloadKind payloadKind = DragPayloadKind.Piece;
    public string payloadId; // piece.typeName / powerup.id / item.id

    public ShopItemDefSO shopDef;

    [Header("Drag Layer (Canvas child)")]
    public RectTransform dragLayer;      // jätä tyhjäksi → luodaan/runtime

    [Header("Behavior")]
    public bool useDragLayer = true; // Shop/loadoutille true, macroPiece:lle false

    public AlchemistEncounterView alchemistView;

    // --- Yhteinen "drag on/off" lippu ---
    public static bool s_IsDraggingAny;

    // --- Private kentät ---
    RectTransform _rt;
    Image _img;
    CanvasGroup _cg;
    LayoutElement _le;
    Transform _originalParent;
    RectTransform _dragSpace;

    // --- Restore snapshot (so we don't squash / stretch) ---
    Vector2 _origAnchorMin, _origAnchorMax, _origPivot;
    Vector2 _origSizeDelta;
    Vector2 _origOffsetMin, _origOffsetMax;
    Vector3 _origLocalScale;
    Vector2 _origAnchoredPos;

    Vector2 _frozenSize;
    bool _dragging;
    bool _consumed;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        if (_rt == null) _rt = gameObject.AddComponent<RectTransform>();

        _img = GetComponent<Image>();
        if (_img == null) _img = gameObject.AddComponent<Image>();

        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

        _le = GetComponent<LayoutElement>();
        if (_le == null) _le = gameObject.AddComponent<LayoutElement>();

        _cg.alpha = 1f;
        _cg.blocksRaycasts = true;
        _cg.interactable = true;
        _cg.ignoreParentGroups = false;
    }

    public static void EnsureIconVisible(GameObject go)
    {
        if (go == null) return;

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.raycastTarget = true;
        img.enabled = true;

        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        cg.ignoreParentGroups = false;
    }

    public void OnBeginDrag(PointerEventData e)
    {
        Debug.Log($"[Drag] BeginDrag for {name}, parentBefore={transform.parent?.name}");

        if (_dragging) return;

        Debug.Log($"[UIDraggablePiece] OnBeginDrag {name}, useDragLayer={useDragLayer}, parent={transform.parent?.name}");

        // Snapshot original layout/transform so we can restore perfectly
        _originalParent = transform.parent;

        _origAnchorMin = _rt.anchorMin;
        _origAnchorMax = _rt.anchorMax;
        _origPivot = _rt.pivot;
        _origSizeDelta = _rt.sizeDelta;
        _origOffsetMin = _rt.offsetMin;
        _origOffsetMax = _rt.offsetMax;
        _origLocalScale = transform.localScale;
        _origAnchoredPos = _rt.anchoredPosition;

        // Force layout calculation before we freeze size (prevents "0,0" rect in same frame)
        var parentRT = _originalParent as RectTransform;
        if (parentRT != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRT);

        // ✅ Freeze current *visible* size of the icon itself.
        // This prevents squashing tall/wide icons to slot size (shop/loadout/macro).
        _frozenSize = _rt.rect.size;

        // Fallbacks if rect isn't ready
        if (_frozenSize == Vector2.zero)
        {
            // If LayoutElement has a preferred size, prefer that
            if (_le != null)
            {
                float w = _le.preferredWidth;
                float h = _le.preferredHeight;
                if (w > 0f && h > 0f) _frozenSize = new Vector2(w, h);
            }
        }
        if (_frozenSize == Vector2.zero) _frozenSize = new Vector2(80, 80);

        // Detach from layout and lock size (without forcing parent cell size)
        _le.ignoreLayout = true;

        // Use centered anchors/pivot during drag so anchoredPosition works consistently in drag space
        _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
        _rt.pivot = new Vector2(0.5f, 0.5f);

        // Lock size to the icon's own frozen size
        _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _frozenSize.x);
        _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _frozenSize.y);

        // Ensure visibility and that drop targets can receive raycasts
        if (_cg != null)
        {
            _cg.alpha = 1f;
            _cg.ignoreParentGroups = true;
            _cg.blocksRaycasts = false;
        }
        if (_img != null)
        {
            _img.enabled = true;
            _img.raycastTarget = false;
        }

        // Determine drag coordinate space + (optional) reparent to DragLayer
        if (useDragLayer)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var found = canvas.transform.Find("DragLayer") as RectTransform;
                if (found == null)
                {
                    var go = new GameObject("DragLayer", typeof(RectTransform));
                    found = go.GetComponent<RectTransform>();
                    found.SetParent(canvas.transform, false);
                    found.anchorMin = Vector2.zero;
                    found.anchorMax = Vector2.one;
                    found.offsetMin = Vector2.zero;
                    found.offsetMax = Vector2.zero;
                }

                var layerCg = found.GetComponent<CanvasGroup>();
                if (layerCg == null) layerCg = found.gameObject.AddComponent<CanvasGroup>();
                layerCg.alpha = 1f;
                layerCg.interactable = false;
                layerCg.blocksRaycasts = false;

                dragLayer = found;
                _dragSpace = dragLayer;

                // ✅ Keep world position to avoid scale/size surprises from different parent scaling
                transform.SetParent(dragLayer, worldPositionStays: true);

                // ✅ Keep original local scale (some UIs rely on parent scale)
                transform.localScale = _origLocalScale;

                _rt.SetAsLastSibling();
                Debug.Log($"[UIDraggablePiece] {name} parent → DragLayer ({dragLayer.name})");
            }
            else
            {
                _dragSpace = parentRT;
                Debug.LogWarning($"[UIDraggablePiece] {name} no Canvas, using parent rect");
            }
        }
        else
        {
            // MACRO: keep parent
            _dragSpace = parentRT;
            Debug.Log($"[UIDraggablePiece] {name} useDragLayer=FALSE, dragSpace={_dragSpace?.name}");
        }

        // Set initial drag position
        if (_dragSpace != null)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _dragSpace, e.position, e.pressEventCamera, out var local))
            {
                _rt.anchoredPosition = local;
            }
        }

        _dragging = true;
        s_IsDraggingAny = true;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_dragging || _dragSpace == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _dragSpace, e.position, e.pressEventCamera, out var local))
        {
            _rt.anchoredPosition = local;
        }
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!_dragging) return;

        if (_consumed)
        {
            _dragging = false;
            s_IsDraggingAny = false;

            try { AnyDragEnded?.Invoke(); } catch { }

            Destroy(gameObject);
            return;
        }

        // Restore raycasts
        if (_cg != null) _cg.blocksRaycasts = true;
        if (_img != null) _img.raycastTarget = true;

        // Restore parent
        if (_originalParent != null)
            transform.SetParent(_originalParent, worldPositionStays: false);

        // Restore transform + rect exactly as it was (no stretching, no squashing)
        transform.localScale = _origLocalScale;

        _rt.anchorMin = _origAnchorMin;
        _rt.anchorMax = _origAnchorMax;
        _rt.pivot = _origPivot;

        _rt.sizeDelta = _origSizeDelta;
        _rt.offsetMin = _origOffsetMin;
        _rt.offsetMax = _origOffsetMax;

        _rt.anchoredPosition = _origAnchoredPos;

        if (_le != null) _le.ignoreLayout = false;

        _dragging = false;
        s_IsDraggingAny = false;
        try { AnyDragEnded?.Invoke(); } catch { }
    }

    public void ForceStopDrag()
    {
        _dragging = false;
        _consumed = false;

        if (_cg != null) _cg.blocksRaycasts = true;
        if (_img != null) _img.raycastTarget = true;
    }

    public void MarkConsumed(int newIndex)
    {
        _consumed = true;
    }

    public int GetPrice(PlayerData pd)
    {
        return shopDef != null ? shopDef.GetPrice(pd) : 0;
    }

    public System.Collections.IEnumerator AcceptUIFx() { yield break; }
    public System.Collections.IEnumerator RejectUIFx(RectTransform back) { yield break; }
    public void MarkFxControlled() { /* no-op */ }

    public void OnPointerClick(PointerEventData e)
    {
        // Jos tämä oli dragin sivutuote, älä tee inspectiä
        if (_dragging || s_IsDraggingAny) return;

        // Rakennetaan InspectData: shopDef jos löytyy, muuten payload/typeName
        string title = null;
        Sprite portrait = null;

        if (shopDef != null)
        {
            if (shopDef.overrideIcon != null) portrait = shopDef.overrideIcon;

            if (shopDef.piece != null)
            {
                title = shopDef.piece.typeName;
                if (portrait == null) portrait = shopDef.piece.whiteSprite;
            }
            else if (shopDef.powerup != null)
            {
                title = shopDef.powerup.id;
            }
            else if (shopDef.item != null)
            {
                title = shopDef.item.id;
            }
        }

        if (string.IsNullOrEmpty(title))
            title = !string.IsNullOrEmpty(payloadId) ? payloadId : typeName;

        var data = new InspectData
        {
            id = title,
            title = title,
            portrait = portrait,
            tags = shopDef != null ? shopDef.tags : null,
            lore = ""
        };

        InspectService.Select(data);
    }

}


