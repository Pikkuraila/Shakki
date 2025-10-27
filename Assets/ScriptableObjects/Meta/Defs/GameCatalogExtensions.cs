using System.Collections.Generic;

public static class GameCatalogExtensions
{
    // Luo nopea haku typeName/name -> PieceDefSO
    public static Dictionary<string, PieceDefSO> BuildPieceLookup(this GameCatalogSO catalog)
    {
        var d = new Dictionary<string, PieceDefSO>();
        if (catalog?.pieces == null) return d;
        foreach (var p in catalog.pieces)
        {
            if (!p) continue;
            // typeName ensisijainen, name varalla
            if (!string.IsNullOrEmpty(p.typeName)) d[p.typeName] = p;
            if (!string.IsNullOrEmpty(p.name)) d[p.name] = p;
        }
        return d;
    }
}
