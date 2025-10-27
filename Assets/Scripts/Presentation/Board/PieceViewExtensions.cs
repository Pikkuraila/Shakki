using UnityEngine;

public static class PieceViewExtensions
{
    // Jos sulla ei ole PieceView.Bind-metodia, tämä extension toimii "Bindinä"
    public static void Bind(this PieceView pv, PieceDefSO def, string color = "White")
    {
        if (pv == null || def == null) return;

        // Aseta sprite
        var sr = pv.GetComponentInChildren<SpriteRenderer>(true) ?? pv.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.sprite = (color == "White") ? def.whiteSprite : def.blackSprite;

        // Päivitä world-dragille tunniste
        var drag = pv.GetComponent<PieceDragHandle>();
        if (drag != null)
            drag.pieceId = def.typeName;  // <<< KÄYTÄ pieceId, ei typeName-kenttää

        // (valinnainen) jos lisäät PieceViewiin public wrapperin kollarin päivitykseen:
        // pv.RefreshColliderForSprite();
    }
}
