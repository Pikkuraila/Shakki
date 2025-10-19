using UnityEngine;
using System;
using System.Collections.Generic;
using Shakki.Core;

[CreateAssetMenu(fileName = "BoardTemplate", menuName = "Shakki/Board Template")]
public class BoardTemplateSO : ScriptableObject
{
    [Header("Grid")]
    public int width = 8;
    public int height = 8;

    [Tooltip("Jos annetaan, käytä tästä maskia: valkea=allowed, musta=blocked. Tekstuurin koko = width x height.")]
    public Texture2D maskTexture;  // valinnainen

    [Tooltip("Vaihtoehtoinen ASCII—rivi/kolumni, esim. '.'=allowed, '#'=blocked, kirjaimet tageille. Koko = width x height.")]
    public TextAsset asciiLayout;  // valinnainen

    [Header("Fixed holes (optional)")]
    public RectInt[] blockedRects; // esim. keskellä 2x2 → {new RectInt(3,3,2,2)}

    [Header("Fixed tags")]
    public FixedTag[] fixedTags;   // tarkat koordit tageille

    [Header("Random tags (spawn rules)")]
    public RandomTagRule[] randomTags;

    [Header("Random")]
    public int defaultSeed = 12345;

    [Serializable]
    public struct FixedTag
    {
        public int x, y;
        public TileTag tag;
    }

    [Serializable]
    public struct RandomTagRule
    {
        public TileTag tag;
        public int countMin;
        public int countMax;

        [Tooltip("Mistä alueelta arvotaan. Tyhjä = koko kenttä.")]
        public RectInt area;

        [Tooltip("Vältä näitä alueita (esim. lähtörivit).")]
        public RectInt[] avoidAreas;

        [Tooltip("Vain allowed-laatat (maski true).")]
        public bool onlyAllowedTiles;

        [Tooltip("Vältä päällekkäisyyksiä muiden tagien kanssa.")]
        public bool noOverlapWithOtherTags;

        [Tooltip("Minimietäisyys muihin saman tagin spawneihin.")]
        public int minDistance; // ruutua (Manhattan tai Chebyshev)
    }

    // Rakenna maski (true = allowed)
    public bool[,] BuildAllowedMask()
    {
        var allowed = new bool[width, height];

        // oletuksena kaikki true
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                allowed[x, y] = true;

        // texture → maski
        if (maskTexture != null)
        {
            if (maskTexture.width != width || maskTexture.height != height)
                Debug.LogWarning($"Mask texture size {maskTexture.width}x{maskTexture.height} != {width}x{height}");
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var c = maskTexture.GetPixel(x, y);
                    allowed[x, y] = c.grayscale > 0.5f; // valkea = allowed
                }
        }

        // ascii → maski ja tagit (kirjaimet tageiksi)
        if (asciiLayout != null)
        {
            var lines = asciiLayout.text.Replace("\r", "").Split('\n');
            if (lines.Length != height)
                Debug.LogWarning($"ASCII height {lines.Length} != {height}");

            for (int y = 0; y < Mathf.Min(height, lines.Length); y++)
            {
                var row = lines[y];
                for (int x = 0; x < Mathf.Min(width, row.Length); x++)
                {
                    char ch = row[x];
                    if (ch == '#') allowed[x, y] = false;
                    // voit myös tulkita kirjaimia tageiksi → lisää FixedTag build-vaiheessa,
                    // esim. 'L' => Lava, 'H' => Heal jne. (ks. BuildTags)
                }
            }
        }

        // blokkisuorat/alueet
        if (blockedRects != null)
        {
            foreach (var r in blockedRects)
            {
                for (int y = r.y; y < r.y + r.height; y++)
                    for (int x = r.x; x < r.x + r.width; x++)
                        if (x >= 0 && y >= 0 && x < width && y < height)
                            allowed[x, y] = false;
            }
        }

        return allowed;
    }

    public TileTags BuildTags(bool[,] allowed, int? seedOverride = null)
    {
        var tags = new TileTags(width, height);

        // ASCII:stä tagit (esimerkkikartoitus, muokkaa makusi mukaan)
        if (asciiLayout != null)
        {
            var lines = asciiLayout.text.Replace("\r", "").Split('\n');
            for (int y = 0; y < Mathf.Min(height, lines.Length); y++)
            {
                var row = lines[y];
                for (int x = 0; x < Mathf.Min(width, row.Length); x++)
                {
                    char ch = row[x];
                    switch (ch)
                    {
                        case 'L': tags.Add(new Coord(x, y), TileTag.None); /* esim. TileTag.Lava jos käytössä */ break;
                        case 'B': tags.Add(new Coord(x, y), TileTag.None); /* Boss tms. */ break;
                            // jne – jos haluat käyttää erillistä TileTag-enumia, lisää se Boardiin
                    }
                }
            }
        }

        // Kiinteät tagit
        if (fixedTags != null)
            foreach (var ft in fixedTags)
                if (InBounds(ft.x, ft.y))
                    tags.Add(new Coord(ft.x, ft.y), ft.tag);

        // Satunnaiset tagit
        var rng = new System.Random(seedOverride ?? defaultSeed);
        if (randomTags != null)
        {
            var occupied = new bool[width, height]; // ettei samaan ruutuun spawnaa, jos noOverlap
            foreach (var rule in randomTags)
            {
                int count = rng.Next(rule.countMin, rule.countMax + 1);

                // ehdokaslista
                var pool = new List<Coord>(width * height);
                RectInt area = rule.area.width > 0 && rule.area.height > 0
                    ? rule.area
                    : new RectInt(0, 0, width, height);

                for (int y = area.yMin; y < area.yMax; y++)
                    for (int x = area.xMin; x < area.xMax; x++)
                    {
                        if (!InBounds(x, y)) continue;
                        if (rule.onlyAllowedTiles && !allowed[x, y]) continue;
                        if (IsInAny(x, y, rule.avoidAreas)) continue;
                        if (rule.noOverlapWithOtherTags && occupied[x, y]) continue;

                        pool.Add(new Coord(x, y));
                    }

                // satunnainen valinta
                Shuffle(rng, pool);

                int placed = 0;
                foreach (var c in pool)
                {
                    if (placed >= count) break;

                    if (rule.noOverlapWithOtherTags && occupied[c.X, c.Y]) continue;
                    if (rule.minDistance > 0 && !RespectsDistance(tags, c, rule.tag, rule.minDistance))
                        continue;

                    tags.Add(c, rule.tag);
                    occupied[c.X, c.Y] = true;
                    placed++;
                }
            }
        }

        return tags;

        bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;
        static bool IsInAny(int x, int y, RectInt[] areas)
        {
            if (areas == null) return false;
            foreach (var r in areas) if (r.Contains(new Vector2Int(x, y))) return true;
            return false;
        }
        static void Shuffle(System.Random rng, List<Coord> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
        static bool RespectsDistance(TileTags tags, Coord c, TileTag tag, int minDist)
        {
            // Chebyshev-etäisyys
            // käy läpi nopeasti alueen; tehokkuutta varten voit ylläpitää listaa sijoitetuista
            return true; // jätetään no-opiksi perusversiossa; halutessasi implementoi
        }
    }
}
