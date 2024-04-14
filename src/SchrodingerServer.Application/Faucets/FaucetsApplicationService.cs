using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using SchrodingerServer.Dtos.Faucets;
using SchrodingerServer.Grains.Grain.Faucets;
using Volo.Abp;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Faucets;

public class FaucetsApplicationService : SchrodingerServerAppService, IFaucetsApplicationService
{
    private readonly IObjectMapper _objectMapper;
    private readonly IClusterClient _clusterClient;
    private readonly ILogger<FaucetsApplicationService> _logger;
    private readonly IOptionsMonitor<FaucetsOptions> _faucetsOptions;

    public FaucetsApplicationService(IClusterClient clusterClient, ILogger<FaucetsApplicationService> logger,
        IObjectMapper objectMapper, IOptionsMonitor<FaucetsOptions> faucetsOptions)
    {
        _logger = logger;
        _objectMapper = objectMapper;
        _clusterClient = clusterClient;
        _faucetsOptions = faucetsOptions;
    }

    public async Task<FaucetsTransferResultDto> FaucetsTransferAsync(FaucetsTransferDto input)
    {
        if (_faucetsOptions.CurrentValue.Closed) throw new UserFriendlyException("This faucet has been suspended.");

        var transferGrain = _clusterClient.GetGrain<IFaucetsGrain>(input.Address);
        var result = await transferGrain.FaucetsTransfer(new FaucetsTransferGrainDto { Address = input.Address });

        if (result.Success)
        {
            _logger.LogInformation("Address {addr} faucets transfer successful.", input.Address);
            return _objectMapper.Map<FaucetsGrainDto, FaucetsTransferResultDto>(result.Data);
        }

        _logger.LogError("Address {addr} faucets transfer fail, message: {msg}.", input.Address, result.Message);
        throw new UserFriendlyException($"{result.Message}");
    }
}