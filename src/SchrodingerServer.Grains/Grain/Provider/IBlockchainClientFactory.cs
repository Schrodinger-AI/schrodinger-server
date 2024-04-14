namespace SchrodingerServer.Grains.Grain.Provider
{
    public interface IBlockchainClientFactory<T>
        where T : class
    {
        T GetClient(string chainName);
    }
}