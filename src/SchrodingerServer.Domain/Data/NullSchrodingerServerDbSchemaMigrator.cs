using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Data;

/* This is used if database provider does't define
 * ISchrodingerServerDbSchemaMigrator implementation.
 */
public class NullSchrodingerServerDbSchemaMigrator : ISchrodingerServerDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
