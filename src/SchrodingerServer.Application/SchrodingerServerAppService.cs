using SchrodingerServer.Localization;
using Volo.Abp.Application.Services;

namespace SchrodingerServer;

/* Inherit your application services from this class.
 */
public abstract class SchrodingerServerAppService : ApplicationService
{
    protected SchrodingerServerAppService()
    {
        LocalizationResource = typeof(SchrodingerServerResource);
    }
}
