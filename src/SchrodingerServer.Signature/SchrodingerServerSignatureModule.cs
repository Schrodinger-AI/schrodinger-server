using Microsoft.Extensions.DependencyInjection;
using SchrodingerServer.Common;
using SchrodingerServer.Signature.Options;
using Volo.Abp.Modularity;

namespace SchrodingerServer.Signature;

[DependsOn(typeof(SchrodingerServerCommonModule))]
public class SchrodingerServerSignatureModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<SignatureServerOptions>(context.Services.GetConfiguration().GetSection("SecurityServer"));
    }
}