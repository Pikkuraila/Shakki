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
    RectTransform _dragSpace;   // ← missä koordinaatistossa liikutaan




    Vector2 _frozenSize;
    bool _dragging;
    bool _consumed;

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
        Debug.Log($"[Drag] BeginDrag for {name}, parentBefore={transform.parent.name}");


        if (_dragging) return;

        Debug.Log($"[UIDraggablePiece] OnBeginDrag {name}, useDragLayer={useDragLayer}, parent={transform.parent?.name}");

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

        // varmistetaan, että itse ikoni on näkyvä
        if (_cg != null)
        {
            _cg.alpha = 1f;
            _cg.ignoreParentGroups = true;
            _cg.blocksRaycasts = false;  // vedettäessä ei blokkaa droppia
        }
        if (_img != null)
        {
            _img.enabled = true;
            _img.raycastTarget = false;  // slotit saavat raycastit
        }

        _originalParent = transform.parent;

        Debug.Log($"[Drag] ParentAfter = {transform.parent.name}");


        // Selvitetään missä koordinaatistossa liikutaan
        if (useDragLayer)
        {
            // SHOP / LOADOUT: käytetään erillistä DragLayeria canvasissa
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

                transform.SetParent(dragLayer, worldPositionStays: false);
                _rt.SetAsLastSibling();
                Debug.Log($"[UIDraggablePiece] {name} parent → DragLayer ({dragLayer.name})");
            }
            else
            {
                // fallback: pysytään parentissa
                _dragSpace = parentRT;
                Debug.LogWarning($"[UIDraggablePiece] {name} no Canvas, using parent rect");
            }
        }
        else
        {
            // MACRO: EI vaihdeta parentia, liikutaan vain parentin rectissä
            _dragSpace = parentRT;
            Debug.Log($"[UIDraggablePiece] {name} useDragLayer=FALSE, dragSpace={_dragSpace?.name}");
        }

        // Aseta aloituspaikka
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

        // Jos tämä drag on kulutettu (ostettu / siirretty), älä snapbackaa
        if (_consumed)
        {
            _dragging = false;
            s_IsDraggingAny = false;

            try { AnyDragEnded?.Invoke(); } catch { }

            // Tämä on vain visuaalinen kopio, gridi piirtää oikean ikoninsa itse
            Destroy(gameObject);
            return;
        }

        // Palauta raycastit
        _cg.blocksRaycasts = true;
        if (_img) _img.raycastTarget = true;

        // Palauta parent ja layout
        if (_originalParent != null)
        {
            transform.SetParent(_originalParent, worldPositionStays: false);

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

    public void ForceStopDrag()
    {
        _dragging = false;
        _consumed = false;

        if (_cg != null) _cg.blocksRaycasts = true;
        if (_img != null) _img.raycastTarget = true;
    }


    // --- Nämä on jätetty tyhjiksi, koska muu koodi kutsuu joskus näitä ---
    public void MarkConsumed(int newIndex)
    {
        _consumed = true;   // 👈 nyt tämä tekee jotain
    }

    // --- Hinta-apumetodi shopille ---
    public int GetPrice(PlayerData pd)
    {
        return shopDef != null ? shopDef.GetPrice(pd) : 0;
    }

    public System.Collections.IEnumerator AcceptUIFx() { yield break; }
    public System.Collections.IEnumerator RejectUIFx(RectTransform back) { yield break; }
    public void MarkFxControlled() { /* no-op */ }
}