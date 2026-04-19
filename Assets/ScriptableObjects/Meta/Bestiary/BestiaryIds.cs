namespace Shakki.Meta.Bestiary
{
    public static class BestiaryIds
    {
        public static string Normalize(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return string.Empty;

            id = id.Trim();
            if (id.Length == 1)
                return id.ToUpperInvariant();

            return char.ToUpperInvariant(id[0]) + id.Substring(1);
        }
    }
}
