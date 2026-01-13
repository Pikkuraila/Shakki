using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class MacroBoardView : MonoBehaviour
{
    [Header("Data")]
    public MacroMapSO map;

    [Header("Layout")]
    public Transform cellsRoot;
    public GameObject cellPrefab;

    [Header("Piece")]
    public UIDraggablePiece macroPiece;

    [Header("Loadout (UI)")]
    public GameObject loadoutRoot;     // <- tämä on se panel/GO jonka haluat päälle/pois
    public LoadoutGridView loadoutGrid; // <- itse komponentti (optional, refreshiin)


    public Action<int> OnAdvance;

    private MacroCellView[] _cells;
    private int _currentIndex = -1;

    public void Init(MacroMapSO map, int currentIndex)
    {
        Debug.Log($"[MacroBoardView] Init map={map}, index={currentIndex}, cellsRoot={cellsRoot}, cellPrefab={cellPrefab}, macroPiece={macroPiece}");
        this.map = map;
        _currentIndex = currentIndex;

        BuildIfNeeded();
        RefreshAll();

        EnsureLoadoutGrid();
    }

    void Start()
    {
        BuildIfNeeded();
        RefreshAll();

        EnsureLoadoutGrid();
    }

    public void SetVisible(bool visible)
    {
        Debug.Log($"[MacroBoardView] SetVisible({visible}) macroGO={gameObject.name} activeBefore={gameObject.activeSelf} " +
                  $"loadoutRoot={(loadoutRoot != null ? loadoutRoot.name : "NULL")} " +
                  $"loadoutRootActiveBefore={(loadoutRoot != null ? loadoutRoot.activeSelf.ToString() : "NULL")} " +
                  $"loadoutGrid={(loadoutGrid != null ? loadoutGrid.name : "NULL")} " +
                  $"loadoutGridActiveBefore={(loadoutGrid != null ? loadoutGrid.gameObject.activeSelf.ToString() : "NULL")}");

        gameObject.SetActive(visible);

        if (loadoutRoot != null)
            loadoutRoot.SetActive(visible);
        else if (loadoutGrid != null)
            loadoutGrid.gameObject.SetActive(visible);

        if (visible && loadoutGrid != null)
        {
            loadoutGrid.BuildIfNeeded();
            loadoutGrid.RefreshAll();
        }
    }







    private void EnsureLoadoutGrid()
    {
        


        
        if (loadoutGrid == null) return;

        loadoutGrid.allowShopDrops = false;
        loadoutGrid.allowAlchemistDrops = false;
        loadoutGrid.allowPowerupDrops = false;
        loadoutGrid.allowLoadoutSwap = true;

        loadoutGrid.BuildIfNeeded();
        loadoutGrid.RefreshAll();

        Debug.Log($"[MacroBoardView] Loadout ensured. grid={(loadoutGrid.grid ? loadoutGrid.grid.name : "NULL")} children={(loadoutGrid.grid ? loadoutGrid.grid.transform.childCount : -1)}");


    }

    private void BuildIfNeeded()
    {
        if (map == null || cellsRoot == null || cellPrefab == null)
        {
            Debug.LogError("[MacroBoardView] Missing refs (map / cellsRoot / cellPrefab).");
            return;
        }

        int rows = map.rows;
        int columns = map.columns;
        int needed = rows * columns;

        var grid = cellsRoot.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
        }

        bool needRebuild = cellsRoot.childCount != needed;

        if (needRebuild)
        {
            for (int i = cellsRoot.childCount - 1; i >= 0; i--)
                DestroyImmediate(cellsRoot.GetChild(i).gameObject);

            _cells = new MacroCellView[needed];

            for (int i = 0; i < needed; i++)
            {
                var go = Instantiate(cellPrefab, cellsRoot);
                go.name = $"MacroCell_{i}";

                var cell = go.GetComponent<MacroCellView>();
                if (cell == null)
                    cell = go.AddComponent<MacroCellView>();

                cell.Setup(this, i);
                _cells[i] = cell;
            }
        }
        else
        {
            _cells = new MacroCellView[needed];
            for (int i = 0; i < needed; i++)
            {
                var child = cellsRoot.GetChild(i);
                var cell = child.GetComponent<MacroCellView>();
                if (cell == null)
                    cell = child.gameObject.AddComponent<MacroCellView>();

                cell.Setup(this, i);
                _cells[i] = cell;
            }
        }
    }

    private void RefreshAll()
    {
        if (map == null || _cells == null) return;

        for (int i = 0; i < _cells.Length; i++)
        {
            var tile = map.tiles != null && i < map.tiles.Length ? map.tiles[i] : default;
            _cells[i].Refresh(tile, _currentIndex);
        }

        EnsurePieceOnCurrentCell();
    }

    private void EnsurePieceOnCurrentCell()
    {
        if (macroPiece == null || _cells == null) return;
        if (_currentIndex < 0 || _currentIndex >= _cells.Length) return;

        var cell = _cells[_currentIndex];
        Transform target = cell.pieceAnchor != null ? cell.pieceAnchor : cell.transform;

        macroPiece.transform.SetParent(target, false);
        macroPiece.transform.localPosition = Vector3.zero;
    }

    public void HandleDropToCell(int targetIndex)
    {
        if (_cells == null || map == null) return;
        if (targetIndex < 0 || targetIndex >= _cells.Length) { EnsurePieceOnCurrentCell(); return; }
        if (_currentIndex < 0 || _currentIndex >= _cells.Length) { EnsurePieceOnCurrentCell(); return; }

        int columns = map.columns;

        int currentRow = _currentIndex / columns;
        int currentCol = _currentIndex % columns;

        int targetRow = targetIndex / columns;
        int targetCol = targetIndex % columns;

        bool isForwardOneRow = targetRow == currentRow + 1;
        bool isWithinSideStep = Mathf.Abs(targetCol - currentCol) <= 1;

        if (!isForwardOneRow || !isWithinSideStep)
        {
            EnsurePieceOnCurrentCell();
            return;
        }

        _currentIndex = targetIndex;
        RefreshAll();

        OnAdvance?.Invoke(_currentIndex);
    }

    public void SetIndex(int index)
    {
        _currentIndex = Mathf.Clamp(index, 0, map != null ? map.TileCount - 1 : index);
        RefreshAll();
    }
}
