namespace Shakki.Meta.Bestiary
{
    public interface IBestiaryStore
    {
        BestiarySaveData Load();
        void Save(BestiarySaveData data);
    }
}
