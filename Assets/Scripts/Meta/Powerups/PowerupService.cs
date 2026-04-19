using Shakki.Core;
using UnityEngine;

public static class PowerupService
{
    // Vaihda ruudun oma nappula toiseen, jos säännöt sallivat
    public static bool TrySwapPiece(GameState s, GameCatalogSO catalog, Coord at, string newPieceId, string owner)
    {
        if (s == null || catalog == null) return false;
        if (!s.InBounds(at)) return false;
        if (s.IsGameOver) return false;

        var p = s.Get(at);
        if (p == null || p.Owner != owner) return false;

        // rajoitus: kuningasta ei saa poistaa
        if (p.TypeName == "King") return false;

        var def = catalog.GetPieceById(newPieceId);
        if (!def) return false;

        // Vaihto: sailyta sama yksilo ja loukkaantumisstatus
        var replacement = def.Build(owner, p.InstanceId);
        replacement.HasMoved = p.HasMoved;
        replacement.IsInjured = p.IsInjured;
        s.Set(at, replacement);

        // turva: jos vaihdoit itsesi kuninkaattomaksi (ei pitaisi tapahtua), ala salli
        if (!HasKing(s, owner)) { s.Set(at, p); return false; }

        return true;
    }

    static bool HasKing(GameState s, string color)
    {
        foreach (var c in s.AllCoords())
        {
            var q = s.Get(c);
            if (q != null && q.Owner == color && q.TypeName == "King") return true;
        }
        return false;
    }
}
