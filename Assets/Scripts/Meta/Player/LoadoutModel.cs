// Assets/Scripts/Meta/Player/LoadoutModel.cs
using System.Collections.Generic;
using System.Linq;

public static class LoadoutModel
{
    // NEW: Expand ñ ei t‰yt‰ pawnline‰ automaattisesti
    // - Jakaa entryjen mukaiset kappaleet vasemmalta oikealle indeksij‰rjestykseen (0..totalSlots-1)
    // - Jos King puuttuu, lis‰‰ 1 kpl ja sijoita ensimm‰iseen vapaaseen slottiin
    public static List<string> Expand(List<LoadoutEntry> entries, int totalSlots = 16, string implicitKingId = "King")
    {
        var outSlots = Enumerable.Repeat("", totalSlots).ToList(); // "" tyhj‰
        entries ??= new();

        var flat = new List<string>();
        foreach (var e in entries)
        {
            if (string.IsNullOrEmpty(e.pieceId) || e.count <= 0) continue;
            for (int i = 0; i < e.count; i++) flat.Add(e.pieceId);
        }

        if (!flat.Any(id => id == implicitKingId))
            flat.Insert(0, implicitKingId);

        int w = 0;
        for (int i = 0; i < outSlots.Count && w < flat.Count; i++)
            outSlots[i] = flat[w++];

        return outSlots;
    }

    public static List<LoadoutEntry> Compact(List<string> slots)
    {
        var res = new List<LoadoutEntry>();
        if (slots == null) return res;

        var groups = slots.Where(s => !string.IsNullOrEmpty(s))
                          .GroupBy(s => s);
        foreach (var g in groups)
            res.Add(new LoadoutEntry { pieceId = g.Key, count = g.Count() });

        return res;
    }
}
