using UnityEngine;

public class TileView : MonoBehaviour
{
    public int X;
    public int Y;
    private BoardView _board;

    public void Init(int x, int y, BoardView board, Color color)
    {
        X = x; Y = y; _board = board;
        var sr = GetComponent<SpriteRenderer>();
        if (sr) sr.color = color;
        name = $"Tile_{x}_{y}";
    }

    void OnMouseDown()
    {
        _board?.OnTileClicked(X, Y);
    }
}
