using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class MacroBoardView : MonoBehaviour
{
    [Header("Data")]
    public MacroMapSO map;

    [Header("Layout")]
    public Transform cellsRoot;    // Parent, jolla on esim. GridLayoutGroup
    public GameObject cellPrefab;  // Prefab yhdelle ruudulle (sis. MacroCellView + DropHandler)

    [Header("Piece")]
    public UIDraggablePiece macroPiece; // Makrotason kuningas

    // RunController voi subscibata tähän
    public Action<int> OnAdvance;

    private MacroCellView[] _cells;
    private int _currentIndex = -1;

    /// <summary>
    /// Kutsutaan RunControllerista, kun halutaan näyttää makrolauta tietylle indexille.
    /// </summary>
    public void Init(MacroMapSO map, int currentIndex)
    {
        Debug.Log($"[MacroBoardView] Init map={map}, index={currentIndex}, cellsRoot={cellsRoot}, cellPrefab={cellPrefab}, macroPiece={macroPiece}");
        this.map = map;
        _currentIndex = currentIndex;

        BuildIfNeeded();
        RefreshAll();
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

        // *** UUSI PÄTKÄ ***
        var grid = cellsRoot.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;   // esim. 3
        }
        // *** UUSI PÄTKÄ LOPPU ***

        // Jos lapsia liian vähän tai liikaa -> rebuild
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

        // Päivitä ruutujen visuaalit
        for (int i = 0; i < _cells.Length; i++)
        {
            var tile = map.tiles != null && i < map.tiles.Length
                ? map.tiles[i]
                : default;

            _cells[i].Refresh(tile, _currentIndex);
        }

        // Siirrä nappula oikeaan ruutuun
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

    /// <summary>
    /// Tätä kutsutaan DropHandlerista kun nappula pudotetaan johonkin ruutuun.
    /// </summary>
    public void HandleDropToCell(int targetIndex)
    {
        if (_cells == null || map == null) return;
        if (targetIndex < 0 || targetIndex >= _cells.Length)
        {
            // Palauta nappula
            EnsurePieceOnCurrentCell();
            return;
        }

        if (_currentIndex < 0 || _currentIndex >= _cells.Length)
        {
            EnsurePieceOnCurrentCell();
            return;
        }

        int columns = map.columns;

        // Index -> (row, col)
        int currentRow = _currentIndex / columns;
        int currentCol = _currentIndex % columns;

        int targetRow = targetIndex / columns;
        int targetCol = targetIndex % columns;

        // SALLITAAN VAIN:
        // - yksi rivi eteenpäin
        // - max 1 sarakkeen sivuttaisliike
        bool isForwardOneRow = targetRow == currentRow + 1;
        bool isWithinSideStep = Mathf.Abs(targetCol - currentCol) <= 1;

        if (!isForwardOneRow || !isWithinSideStep)
        {
            // Ei sallittu liike → palautetaan nappula
            EnsurePieceOnCurrentCell();
            return;
        }

        _currentIndex = targetIndex;
        RefreshAll();

        OnAdvance?.Invoke(_currentIndex);
    }



    /// <summary>
    /// Voidaan kutsua ulkopuolelta, jos halutaan pakottaa index ilman dragia.
    /// </summary>
    public void SetIndex(int index)
    {
        _currentIndex = Mathf.Clamp(index, 0, map != null ? map.TileCount - 1 : index);
        RefreshAll();
    }
}
