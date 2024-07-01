using System.Threading.Tasks;

namespace SchrodingerServer.GateIo;

public interface IGateIoCirculationService
{
    Task<long> GetSgrCirculation();

    Task<decimal> GetSgrPrice();
}