using UnityEngine;

public static class PieceViewExtensions
{
    // Jos sulla ei ole PieceView.Bind-metodia, t�m� extension toimii "Bindin�"
    public static void Bind(this PieceView pv, PieceDefSO def, string color = "White")
    {
        if (pv == null || def == null) return;

        // Aseta sprite
        var sr = pv.GetComponentInChildren<SpriteRenderer>(true) ?? pv.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.sprite = (color == "White") ? def.whiteSprite : def.blackSprite;

        // P�ivit� world-dragille tunniste
        var drag = pv.GetComponent<PieceDragHandle>();
        if (drag != null)
            drag.pieceId = def.typeName;  // <<< K�YT� pieceId, ei typeName-kentt��

        // (valinnainen) jos lis��t PieceViewiin public wrapperin kollarin p�ivitykseen:
        // pv.RefreshColliderForSprite();
    }
}
