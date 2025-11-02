// Assets/Scripts/Presentation/UI/UIDraggablePiece.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public sealed class UIDraggablePiece :
    MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // --- Julkiset metatiedot (näitä muu koodi täyttää) ---
    public string typeName;
    public SlotKind originKind;
    public int originIndex = -1;
    public LoadoutGridView loadoutView;  // saa olla null
    public ShopGridView shopView;     // saa olla null
    public static event System.Action AnyDragEnded;


    public DragPayloadKind payloadKind = DragPayloadKind.Piece;
    public string payloadId; // piece.typeName / powerup.id / item.id

    [Header("Drag Layer (Canvas child)")]
    public RectTransform dragLayer;      // jätä tyhjäksi → luodaan/runtime

    // --- Yhteinen "drag on/off" lippu ---
    public static bool s_IsDraggingAny;

    // --- Private kentät ---
    RectTransform _rt;
    Image _img;
    CanvasGroup _cg;
    LayoutElement _le;
    Transform _originalParent;

    Vector2 _frozenSize;
    bool _dragging;

    // ----------------------------------------------------
    // Pomminvarma komponenttien varmistus
    // ----------------------------------------------------
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

        // perusasetukset — ei kaadu vaikka olisi väärässä paikassa
        _cg.alpha = 1f;
        _cg.blocksRaycasts = true;
        _cg.interactable = true;
        _cg.ignoreParentGroups = false;
    }

    // ----------------------------------------------------
    // Yleisapu: varmista että ikoni on näkyvissä (ja sillä on CanvasGroup)
    // ----------------------------------------------------
    public static void EnsureIconVisible(GameObject go)
    {
        if (go == null) return;

        // Luo/hae Image
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.raycastTarget = true;               // tarvitaan, että drag tarttuu
        img.enabled = true;

        // Luo/hae CanvasGroup
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        cg.ignoreParentGroups = false;
    }


    // ----------------------------------------------------
    // Drag-käyttöliittymä
    // ----------------------------------------------------
    public void OnBeginDrag(PointerEventData e)
    {
        if (_dragging) return;

        // Freeze koko: otetaan vanhemman solun koko, muuten ikonista
        var parentRT = transform.parent as RectTransform;
        _frozenSize = parentRT != null ? parentRT.rect.size : _rt.rect.size;
        if (_frozenSize == Vector2.zero) _frozenSize = new Vector2(80, 80);

        // Layoutista irti ja kiinteä koko
        _le.ignoreLayout = true;
        _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
        _rt.pivot = new Vector2(0.5f, 0.5f);
        _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _frozenSize.x);
        _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _frozenSize.y);

        // Hae/tee dragLayer (Canvasin lapsi, koko ruutu)
        if (dragLayer == null)
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
                dragLayer = found;
            }
        }

        _originalParent = transform.parent;
        transform.SetParent(dragLayer, worldPositionStays: false);
        _rt.SetAsLastSibling();

        // Vedon aikana ei blokata raycasteja
        _cg.blocksRaycasts = false;
        if (_img) _img.raycastTarget = false;

        // Aseta aloituspaikka
        if (dragLayer != null)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragLayer, e.position, e.pressEventCamera, out var local))
            {
                _rt.anchoredPosition = local;
            }
        }

        // vedettävässä ikonissa
        var img = GetComponent<UnityEngine.UI.Image>();
        var cg = GetComponent<CanvasGroup>();
        if (cg != null) cg.blocksRaycasts = false; // ettei slotit “menetä” droppia

        _dragging = true;
        s_IsDraggingAny = true;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!_dragging || dragLayer == null) return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                dragLayer, e.position, e.pressEventCamera, out var local))
        {
            _rt.anchoredPosition = local;
        }
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!_dragging) return;

        // Palauta raycastit
        _cg.blocksRaycasts = true;
        if (_img) _img.raycastTarget = true;

        // Palauta parent ja layout
        if (_originalParent != null)
        {
            transform.SetParent(_originalParent, worldPositionStays: false);

            // venytä takaisin solun täyteen kokoon
            _rt.anchorMin = Vector2.zero;
            _rt.anchorMax = Vector2.one;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;

            _le.ignoreLayout = false;
        }

        _dragging = false;
        s_IsDraggingAny = false;
        try { AnyDragEnded?.Invoke(); } catch { }
    }

    // --- Nämä on jätetty tyhjiksi, koska muu koodi kutsuu joskus näitä ---
    public void MarkConsumed(int newIndex) { /* no-op UI-versiossa */ }
    public System.Collections.IEnumerator AcceptUIFx() { yield break; }
    public System.Collections.IEnumerator RejectUIFx(RectTransform back) { yield break; }
    public void MarkFxControlled() { /* no-op */ }
}
