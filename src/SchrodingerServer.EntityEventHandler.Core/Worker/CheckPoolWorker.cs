using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Elasticsearch.Net;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchrodingerServer.Cat.Provider;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dto;
using SchrodingerServer.EntityEventHandler.Core.Options;
using SchrodingerServer.Grains.Grain.ApplicationHandler;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DistributedLocking;
using Volo.Abp.Threading;

namespace SchrodingerServer.EntityEventHandler.Core.Worker;

public class CheckPoolWorker : AsyncPeriodicBackgroundWorkerBase
{
    private readonly ILogger<CheckPoolWorker> _logger;
    private readonly IOptionsMonitor<WorkerOptions> _workerOptionsMonitor;
    private readonly IOptionsMonitor<PoolOptions> _schrodingerPoolOptionsMonitor;
    
    private readonly IAbpDistributedLock _distributedLock;
    private readonly ISchrodingerCatProvider _schrodingerCatProvider;
    private readonly IContractProvider _contractProvider;
    private readonly string _lockKey = "CheckPoolWorker";
    private readonly string poolId = "pool1";

    public CheckPoolWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<CheckPoolWorker> logger,
        IOptionsMonitor<WorkerOptions> workerOptionsMonitor,
        IAbpDistributedLock distributedLock,
        ISchrodingerCatProvider schrodingerCatProvider,
        IContractProvider contractProvider,
        IOptionsMonitor<PoolOptions> schrodingerPoolOptionsMonitor
    ) : base(timer,
        serviceScopeFactory)
    {
        _logger = logger;
        _workerOptionsMonitor = workerOptionsMonitor;
        _distributedLock = distributedLock;
        _schrodingerCatProvider = schrodingerCatProvider;
        _contractProvider = contractProvider;
        _schrodingerPoolOptionsMonitor = schrodingerPoolOptionsMonitor;
        timer.Period = _workerOptionsMonitor.CurrentValue.GetWorkerPeriodMinutes(_lockKey) * 60 * 1000;
    }
    
    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        _logger.LogInformation("CheckPoolWorker begin");
        await using var handle = await _distributedLock.TryAcquireAsync(_lockKey);
        var openSwitch = _workerOptionsMonitor.CurrentValue.GetWorkerSwitch(_lockKey);
        
        _logger.LogInformation("CheckPoolWorker start openSwitch {openSwitch}", openSwitch);
        if (!openSwitch)
        {
            _logger.LogWarning("CheckPoolWorker has not open");
            return;
        }
        
        var now = DateTime.UtcNow.ToUtcSeconds();
        if (now >= _schrodingerPoolOptionsMonitor.CurrentValue.Deadline)
        {
            _logger.LogInformation("Pool has expired, deadline:{deadline}", _schrodingerPoolOptionsMonitor.CurrentValue.Deadline);
            return;
        }
        
        var poolData = await _schrodingerCatProvider.GetPoolDataAsync(poolId);
        
        // if we already have a winner, no need to check and update the pool
        if (poolData != null && !poolData.WinnerAddress.IsNullOrEmpty())
        {
            _logger.LogInformation("Pool has winner, {address}, {symbol}", poolData.WinnerAddress, poolData.WinnerSymbol);
            return;
        }
        
        _logger.LogInformation("Pool has no winner for now");
        var balance = await CheckPoolBalance();
        _logger.LogInformation("Pool balance is {balance}", balance);
        
        if (poolData == null)
        {
            poolData = new PoolDataDto
            {
                PoolId = poolId,
                WinnerAddress = "",
                WinnerSymbol = ""
            };
        }
        poolData.Balance = balance;

        long targetRank = _schrodingerPoolOptionsMonitor.CurrentValue.TargetRank;
        var adoptRecords = await _schrodingerCatProvider.GetLatestRareAdoptionAsync(50, _schrodingerPoolOptionsMonitor.CurrentValue.BeginTs);
        var winningList = adoptRecords.Where(o => o.Rank > 0 && o.Rank <= targetRank).OrderBy(o => o.AdoptTime).ToList();
        
        if (!winningList.IsNullOrEmpty())
        {
            var winningOne = winningList.First();
            poolData.WinnerAddress = winningOne.Adopter;
            poolData.WinnerSymbol = winningOne.Symbol;
            poolData.WinnerRank = winningOne.Rank;
            poolData.WinnerLevel = winningOne.Level.IsNullOrEmpty() ? 0 : int.Parse(winningOne.Level);
            _logger.LogInformation("Winner is {address}, {symbol}, {rank}", winningOne.Adopter, winningOne.Symbol, winningOne.Rank);
        }
        
        await _schrodingerCatProvider.SavePoolDataAsync(poolData);
        
        _logger.LogInformation("CheckPoolWorker end");
    }
    
    private async Task<long> CheckPoolBalance()
    {
        var chainId = _schrodingerPoolOptionsMonitor.CurrentValue.ChainId;

        var param = new GetBalanceInput
        {
            Symbol = "SGR-1",
            Owner = Address.FromBase58(_schrodingerPoolOptionsMonitor.CurrentValue.PoolAddress)
        };

        try
        {
            var rawTxResult = await _contractProvider.CreateTransactionAsync(chainId, 
                _schrodingerPoolOptionsMonitor.CurrentValue.PublicKey,
                _schrodingerPoolOptionsMonitor.CurrentValue.TokenContractAddress, 
                MethodName.GetBalance, param.ToByteString().ToBase64());
        
            var balance = await _contractProvider.CallTransactionAsync<GetBalanceOutput>(chainId, rawTxResult.transaction.ToByteArray().ToHex());
            return balance.Balance;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "check balance error, msg:{msg}", e.Message);
            throw;
        }
    }
}