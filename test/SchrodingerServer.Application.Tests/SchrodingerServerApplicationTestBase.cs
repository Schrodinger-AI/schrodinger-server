using Xunit.Abstractions;

namespace SchrodingerServer;

public abstract partial class SchrodingerServerApplicationTestBase : SchrodingerServerOrleansTestBase<SchrodingerServerApplicationTestModule>
{

    public  readonly ITestOutputHelper Output;
    protected SchrodingerServerApplicationTestBase(ITestOutputHelper output) : base(output)
    {
        Output = output;
    }
}