using UnityEngine;
using Shakki.Core;

public static class EncounterLoader
{
    public static void Apply(GameState state, EncounterSO enc, GameCatalogSO catalog)
    {
        foreach (var c in state.AllCoords()) state.Set(c, null);
        if (enc == null) return;

        if (enc.fillWhitePawnsAtY)
            FillPawnRank(state, catalog, "white", enc.relativeRanks ? 1 : enc.whitePawnsY);
        if (enc.fillBlackPawnsAtY)
            FillPawnRank(state, catalog, "black", enc.relativeRanks ? state.Height - 2 : enc.blackPawnsY);

        foreach (var s in enc.spawns)
        {
            var coord = new Coord(s.x, s.y);
            if (!state.InBounds(coord)) continue;
            var def = catalog.GetPieceById(s.pieceId);
            if (!def) { Debug.LogWarning($"[Encounter] PieceDef not found: {s.pieceId}"); continue; }
            state.Set(coord, def.Build(s.owner));
        }
    }

    static void FillPawnRank(GameState state, GameCatalogSO catalog, string owner, int y)
    {
        var pawnDef = catalog.GetPieceById("Pawn");
        if (!pawnDef) { Debug.LogWarning("[Encounter] Pawn def missing"); return; }
        for (int x = 0; x < state.Width; x++)
        {
            var c = new Coord(x, y);
            if (!state.InBounds(c)) continue;
            state.Set(c, pawnDef.Build(owner));
        }
    }
}
