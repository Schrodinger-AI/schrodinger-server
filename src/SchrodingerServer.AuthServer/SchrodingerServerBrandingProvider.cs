using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace SchrodingerServer;

[Dependency(ReplaceServices = true)]
public class SchrodingerServerBrandingProvider : DefaultBrandingProvider
{
    public override string AppName => "SchrodingerServer";
}
