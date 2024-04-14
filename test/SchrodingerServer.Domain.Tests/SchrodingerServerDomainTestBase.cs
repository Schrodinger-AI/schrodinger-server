using Xunit.Abstractions;

namespace SchrodingerServer;

public abstract class SchrodingerServerDomainTestBase : SchrodingerServerTestBase<SchrodingerServerDomainTestModule>
{
    protected SchrodingerServerDomainTestBase(ITestOutputHelper output) : base(output)
    {
    }
}