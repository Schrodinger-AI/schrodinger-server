using System.Threading.Tasks;

namespace SchrodingerServer.Data;

public interface ISchrodingerServerDbSchemaMigrator
{
    Task MigrateAsync();
}
