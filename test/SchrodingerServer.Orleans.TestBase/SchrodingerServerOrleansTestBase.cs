using Orleans.TestingHost;
using Volo.Abp.Modularity;
using Xunit.Abstractions;

namespace SchrodingerServer;

public abstract class SchrodingerServerOrleansTestBase<TStartupModule> : 
    SchrodingerServerTestBase<TStartupModule> where TStartupModule : IAbpModule
{

    protected readonly TestCluster Cluster;
    
    public SchrodingerServerOrleansTestBase(ITestOutputHelper output) : base(output)
    {
        Cluster = GetRequiredService<ClusterFixture>().Cluster;
    }
}