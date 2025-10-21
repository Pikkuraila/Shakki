public interface IDataStore
{
    bool TryLoad(out PlayerData data);
    void Save(PlayerData data);
    void Wipe();
}