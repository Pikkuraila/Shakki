using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShopGridView : MonoBehaviour
{
    public GridLayoutGroup grid;
    public GameObject slotPrefab;
    public GameObject pieceIconPrefab;

    public List<ShopItemDefSO> ShopItems; // täytetään Inspectorissa
    public GameCatalogSO gameCatalog;

    private bool _built = false;
    private const int Columns = 4;

    Dictionary<string, PieceDefSO> _defByType;

    [Header("UI Layers")]
    public RectTransform dragLayer; // voi jättää tyhjäksi → luodaan automaattisesti


    void Awake()
    {
        _defByType = gameCatalog.BuildPieceLookup();
        BuildGrid();
        RefreshAll();
    }

    private RectTransform EnsureDragLayer()
    {
        if (dragLayer != null) return dragLayer;

        var root = GetComponentInParent<Canvas>()?.rootCanvas;
        if (root == null) return null;

        // Etsi valmiiksi olemassa oleva
        var existing = root.transform.Find("DragLayer") as RectTransform;
        if (existing != null) { existing.SetAsLastSibling(); return dragLayer = existing; }

        // Luo pelkkä RectTransform (EI Canvasia)
        var go = new GameObject("DragLayer", typeof(RectTransform), typeof(CanvasGroup));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(root.transform, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling(); // nosta ylimmäksi samassa canvaksessa

        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false; // tipit pääsevät DropSloteille läpi

        return dragLayer = rt;
    }


    public void BuildIfNeeded()
    {
        if (grid == null || slotPrefab == null) return;

        // Pakota 4×1 layout
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;

        // Jos jo rakennettu ja slottien määrä täsmää, älä rakenna uudelleen
        if (_built && grid.transform.childCount == Columns) return;

        // Tyhjennä
        for (int i = grid.transform.childCount - 1; i >= 0; i--)
            Destroy(grid.transform.GetChild(i).gameObject);

        // Luo täsmälleen 4 slottia
        for (int i = 0; i < Columns; i++)
        {
            var slotGO = Instantiate(slotPrefab, grid.transform);
            var slot = slotGO.GetComponent<DropSlot>();
            if (slot != null)
            {
                slot.kind = SlotKind.Shop;
                slot.index = -1;
                slot.shopView = this;
            }
        }

        _built = true;
    }

    void BuildGrid()
    {
        foreach (Transform c in grid.transform) Destroy(c.gameObject);

        for (int i = 0; i < ShopItems.Count; i++)
        {
            var slotGO = Instantiate(slotPrefab, grid.transform);
            var slot = slotGO.GetComponent<DropSlot>();
            slot.kind = SlotKind.Shop;
            slot.index = -1; // shopissa ei tarvita
            slot.shopView = this;

            var item = ShopItems[i];
            if (item?.piece == null) continue;

            var iconGO = Instantiate(pieceIconPrefab, slotGO.transform);



            // === KRIITTINEN: anna gridin layoutata ikonille koko ===
            var iconRT = iconGO.GetComponent<RectTransform>();
            if (iconRT != null)
            {
                iconRT.anchorMin = Vector2.zero;
                iconRT.anchorMax = Vector2.one;
                iconRT.offsetMin = Vector2.zero;
                iconRT.offsetMax = Vector2.zero;
            }

            var le = iconGO.GetComponent<LayoutElement>() ?? iconGO.AddComponent<LayoutElement>();
            le.ignoreLayout = false;                              // <-- tämä pois päältä ennen dragia
            le.flexibleWidth = 0; le.flexibleHeight = 0;
            if (grid != null)
            {
                le.preferredWidth = grid.cellSize.x;            // anna eksplisiittinen koko
                le.preferredHeight = grid.cellSize.y;
            }

            var img = iconGO.GetComponent<Image>();
            if (img != null) { img.raycastTarget = true; img.enabled = true; }

            var icon = iconGO.GetComponent<PieceIcon>();
            var sprite = item.overrideIcon != null ? item.overrideIcon : item.piece.whiteSprite;
            icon.Bind(sprite, item.price, showPrice: true);

            var drag = iconGO.GetComponent<UIDraggablePiece>();
            drag.typeName = item.piece.typeName;
            drag.originKind = SlotKind.Shop;
            drag.originIndex = -1;
            drag.shopView = this;
            drag.loadoutView = FindObjectOfType<LoadoutGridView>();

            var loadout = drag.loadoutView ?? FindObjectOfType<LoadoutGridView>();
            drag.dragLayer = loadout != null ? loadout.dragLayer : drag.dragLayer;
        }
    }



    public void RefreshAll()
    {
        // Jos joskus lisäät varastosaldot, päivitä hinnat/lockit täältä
    }

    // Palautus shoppiin (drop shopin päälle) — valinnainen: myynti
    public void HandleDropToShop(DropSlot target, UIDraggablePiece drag)
    {
        if (drag.originKind != SlotKind.Loadout) return;
        // myydään takaisin täyteen hintaan (tai 50 %) — tässä 100 %
        var svc = drag.loadoutView;
        var lsvc = svc.GetType()
                      .GetField("_loadout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                      .GetValue(svc) as LoadoutService;

        lsvc.Refund(drag.typeName, ratio: 1f);
        lsvc.SetAt(drag.originIndex, string.Empty);
        svc.RefreshAll();
        RefreshAll();
        drag.MarkConsumed(-1); // poistui loadoutista
    }
}
