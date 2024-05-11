using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dtos.Uniswap;
using SchrodingerServer.Uniswap.Index;
using SchrodingerServer.Uniswap.Provider;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace SchrodingerServer.Uniswap;

public interface IUniswapLiquidityService
{
    Task CreateSnapshotOfPool(string poolId, DateTime snapshotTime);
    Task<GetUniswapLiquidityDto> GetSnapshotAsync(GetUniswapLiquidityInput input);
}

public class UniswapLiquidityService : IUniswapLiquidityService, ISingletonDependency
{
    private const int Step = 100;
    
    private readonly ILogger<UniswapLiquidityService> _logger;
    private readonly IEtherscanProvider _etherscanProvider;
    private readonly IUniswapLiquidityProvider _uniswapLiquidityProvider;
    private readonly IOptionsMonitor<UniswapPriceRangeOptions> _optionsMonitor;

    public UniswapLiquidityService(ILogger<UniswapLiquidityService> logger,
        IUniswapLiquidityProvider uniswapLiquidityProvider,
        IEtherscanProvider etherscanProvider,
        IOptionsMonitor<UniswapPriceRangeOptions> optionsMonitor)
    {
        _logger = logger;
        _etherscanProvider = etherscanProvider;
        _uniswapLiquidityProvider = uniswapLiquidityProvider;
        _optionsMonitor = optionsMonitor;
    }
    
    public async Task CreateSnapshotOfPool(string poolId, DateTime snapshotTime)
    {
        var postisons = new List<Position>();
        var snapshotTs = ((DateTimeOffset)snapshotTime).ToUnixTimeSeconds();

        var blockNo = await _etherscanProvider.GetBlockNoByTimeAsync(snapshotTs);
        int blockNoInt = int.Parse(blockNo);
        
        var cnt = 0;
        var offset = 0;
        while (cnt == Step || offset == 0)
        {
            var next = await _uniswapLiquidityProvider.GetPositionsAsync(poolId, blockNoInt, offset, Step);
            cnt = next.Positions.Count;
            postisons.AddRange(next.Positions);
            offset += Step;
        } 
        _logger.LogDebug("total position count: {cnt}", postisons.Count);
        if (postisons.IsNullOrEmpty())
        {
            return;
        }
        
        var ts = GetYesterdayTimestamp();
        _logger.LogDebug("yesterday ts：{ts}", ts);
        
        var poolDatas = await _uniswapLiquidityProvider.GetPoolDayDataAsync(poolId, ts);
        if (poolDatas.PoolDayDatas.Count < 1)
        {
            _logger.LogError("empty Pool Data");
            return;
        } 

        var poolData = poolDatas.PoolDayDatas[0];
        _logger.LogDebug("pool day data {ts}", JsonConvert.SerializeObject(poolData));

        var validPositions = postisons.Where(position => long.Parse(position.Liquidity) > 0 && IsPositionValid(poolData, position)).ToList();
        _logger.LogDebug("valid position count: {cnt}", validPositions.Count);

        var poolDto = await _uniswapLiquidityProvider.GetPoolAsync(poolId, blockNoInt);
        var pool = poolDto.Pool;
        _logger.LogDebug("pool data {ts}", JsonConvert.SerializeObject(pool));
        
        var snapshotIndexList = new List<UniswapPositionSnapshotIndex>();
        foreach (var position in validPositions)
        {
            var value = CalculatePositionValue(position, pool);
            var snapshot = new UniswapPositionSnapshotIndex()
            {   
                Id = GetId(pool.Id, position.Id, snapshotTime),
                PoolId = pool.Id,
                Token0Symbol = pool.Token0.Symbol,
                Token1Symbol = pool.Token1.Symbol,
                PositionOwner = position.Owner,
                PositionId = position.Id,
                CurrentPrice = pool.Token1Price,
                PositionLowPrice = position.TickLower.Price0,
                PositionHighPrice = position.TickUpper.Price0,
                PositionValueUSD = value,
                SnapshotTime = snapshotTime,
                CreateTime = DateTime.UtcNow
            };
            snapshotIndexList.Add(snapshot);
        }

        await _uniswapLiquidityProvider.AddPositionsSnapshotAsync(snapshotIndexList);
    }
    
    
    
    public async Task CreateSnapshotForOnce(string bizDate, string poolId, string pointName)
    {
        var postisons = new List<Position>();
        
        
        var datetime = DateTime.ParseExact(bizDate, "yyyyMMdd", null);
        var snapshotTs = new DateTimeOffset(datetime).ToUnixTimeSeconds();
        
        // var snapshotTs = ((DateTimeOffset)snapshotTime).ToUnixTimeSeconds();

        var blockNo = await _etherscanProvider.GetBlockNoByTimeAsync(snapshotTs);
        int blockNoInt = int.Parse(blockNo);
        
        var cnt = 0;
        var offset = 0;
        while (cnt == Step || offset == 0)
        {
            var next = await _uniswapLiquidityProvider.GetPositionsAsync(poolId, blockNoInt, offset, Step);
            cnt = next.Positions.Count;
            postisons.AddRange(next.Positions);
            offset += Step;
        } 
        _logger.LogDebug("total position count: {cnt}", postisons.Count);
        if (postisons.IsNullOrEmpty())
        {
            return;
        }
        
        var ts = snapshotTs;
        
        var poolDatas = await _uniswapLiquidityProvider.GetPoolDayDataAsync(poolId, ts);
        if (poolDatas.PoolDayDatas.Count < 1)
        {
            _logger.LogError("empty Pool Data");
            return;
        } 

        var poolData = poolDatas.PoolDayDatas[0];
        _logger.LogDebug("pool day data {ts}", JsonConvert.SerializeObject(poolData));

        var validPositions = postisons.Where(position => long.Parse(position.Liquidity) > 0 && IsPositionValid(poolData, position)).ToList();
        _logger.LogDebug("valid position count: {cnt}", validPositions.Count);

        var poolDto = await _uniswapLiquidityProvider.GetPoolAsync(poolId, blockNoInt);
        var pool = poolDto.Pool;
        _logger.LogDebug("pool data {ts}", JsonConvert.SerializeObject(pool));
        
        var snapshotIndexList = new List<UniswapPositionSnapshotIndex>();
        foreach (var position in validPositions)
        {
            var value = CalculatePositionValue(position, pool);
            var snapshot = new UniswapPositionSnapshotIndex()
            {   
                Id = GetId(pool.Id, position.Id, bizDate),
                PoolId = pool.Id,
                Token0Symbol = pool.Token0.Symbol,
                Token1Symbol = pool.Token1.Symbol,
                PositionOwner = position.Owner,
                PositionId = position.Id,
                CurrentPrice = pool.Token1Price,
                PositionLowPrice = position.TickLower.Price0,
                PositionHighPrice = position.TickUpper.Price0,
                PositionValueUSD = value,
                // SnapshotTime = snapshotTime,
                CreateTime = DateTime.UtcNow
            };
            snapshotIndexList.Add(snapshot);
        }

        await _uniswapLiquidityProvider.AddPositionsSnapshotAsync(snapshotIndexList);
    }
    
    

    public async Task<GetUniswapLiquidityDto> GetSnapshotAsync(GetUniswapLiquidityInput input)
    {
        
        DateTime dateObj;
        if (!DateTime.TryParseExact(input.Date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out dateObj))
        {
            
            _logger.LogError("wrong date format: {date}", input.Date);
            throw new UserFriendlyException("wrong date format");
        }
        DateTime snapshotTime = new DateTime(dateObj.Year, dateObj.Month, dateObj.Day, 0, 0, 0, DateTimeKind.Utc);
            
        var now = DateTime.UtcNow.Date;
        bool afterNow = DateTime.Compare(snapshotTime, now) > 0;
            
        var poolCreatedDate = new DateTime(2024, 3, 21, 0, 0, 0, DateTimeKind.Utc);
        bool beforePoolEverCreated = DateTime.Compare(snapshotTime, poolCreatedDate) < 0;
            
        if (afterNow || beforePoolEverCreated)
        {
            _logger.LogError("invalid date: {date}", snapshotTime);
            throw new UserFriendlyException("invalid date");
        }
            
        var ret =  await _uniswapLiquidityProvider.GetPositionsSnapshotAsync(snapshotTime);
        if (ret.TotalCount == 0)
        {
            await CreateSnapshotOfPool(UniswapConstants.PoolId, snapshotTime);
        }
            
        return ret;
    }

    private long GetYesterdayTimestamp()
    {
        DateTime currentDate = DateTime.UtcNow;  
        DateTime yesterdayDate = currentDate.Date.AddDays(-1);  
        DateTime yesterdayMidnight = new DateTime(yesterdayDate.Year, yesterdayDate.Month, yesterdayDate.Day, 0, 0, 0, DateTimeKind.Utc); 

        long timeStamp = ((DateTimeOffset)yesterdayMidnight).ToUnixTimeSeconds();
        return timeStamp;
    }

    private bool IsPositionValid(PoolDayData poolDayData, Position position)
    {
        Console.WriteLine($"low: {_optionsMonitor.CurrentValue.LowerShift}");
        Console.WriteLine($"upp: {_optionsMonitor.CurrentValue.UpperShift}");
        var positionLow = decimal.Parse(position.TickLower.Price0)*(decimal)Math.Pow(10, UniswapConstants.SGRDecimal-UniswapConstants.USDTDecimal);
        
        // the high price of full range positionHigh may be super huge
        decimal positionHigh;
        try
        {
            positionHigh = decimal.Parse(position.TickUpper.Price0)*(decimal)Math.Pow(10, UniswapConstants.SGRDecimal-UniswapConstants.USDTDecimal);
        }
        catch (OverflowException ex)
        {
            positionHigh = decimal.MaxValue;
        }

        decimal poolLow;
        decimal poolHigh;
        
        if (poolDayData.TxCount > 0)
        {
            poolHigh = 1/decimal.Parse(poolDayData.Low);
            poolLow = 1/decimal.Parse(poolDayData.High);
        }
        else
        {
            var currentPrice = decimal.Parse(poolDayData.Token1Price);
            var currentValue = _optionsMonitor.CurrentValue;
            poolHigh = currentPrice * (decimal)(1+currentValue.UpperShift);
            poolLow = currentPrice * (decimal)(1-currentValue.LowerShift);
        }
        
        if ((poolLow < positionHigh && poolHigh > positionHigh) || (poolLow < positionLow && poolHigh > positionLow) || 
            (poolLow > positionLow && poolHigh < positionHigh))
        {
            return true;
        }

        return false;
    }
    
    private bool IsPositionValidV2(PoolDayData poolDayData, Position position)
    {
        Console.WriteLine($"low: {_optionsMonitor.CurrentValue.LowerShift}");
        Console.WriteLine($"upp: {_optionsMonitor.CurrentValue.UpperShift}");
        var positionLow = decimal.Parse(position.TickLower.Price0)*(decimal)Math.Pow(10, UniswapConstants.SGRDecimal-UniswapConstants.USDTDecimal);
        
        // the high price of full range positionHigh may be so large that parse to decimal could fail
        decimal positionHigh;
        try
        {
            positionHigh = decimal.Parse(position.TickUpper.Price0)*(decimal)Math.Pow(10, UniswapConstants.SGRDecimal-UniswapConstants.USDTDecimal);
        }
        catch (OverflowException ex)
        {
            positionHigh = decimal.MaxValue;
        }

        decimal poolLow;
        decimal poolHigh;
        
        var currentPrice = decimal.Parse(poolDayData.Token1Price);
        var currentValue = _optionsMonitor.CurrentValue;
        poolHigh = currentPrice * (decimal)(1+currentValue.UpperShift);
        poolLow = currentPrice * (decimal)(1-currentValue.LowerShift);
        
        _logger.LogDebug("poolLow: {poolLow}, poolHigh: {poolHigh}, positionLow: {positionLow}, positionHigh: {positionHigh}", 
            poolLow, poolHigh, positionLow, positionHigh);
        
        if ((poolLow < positionHigh && poolHigh > positionHigh) || (poolLow < positionLow && poolHigh > positionLow) || 
            (poolLow > positionLow && poolHigh < positionHigh))
        {
            return true;
        }

        return false;
    }

    private double CalculatePositionValue(Position position, Pool pool)
    {
        var currentTickIdx = decimal.Parse(pool.Tick);
        var positionLowTickIdx = decimal.Parse(position.TickLower.TickIdx);
        var positionHighTickIdx = decimal.Parse(position.TickUpper.TickIdx);
        var liquidity = position.Liquidity;
        double value;

        var lowPrice = double.Parse(position.TickLower.Price0);
        var highPrice = double.Parse(position.TickUpper.Price0);
        var currentPrice = double.Parse(pool.Token1Price)/Math.Pow(10, UniswapConstants.SGRDecimal-UniswapConstants.USDTDecimal);
        if (currentTickIdx < positionLowTickIdx)
        {
            var token0Amount = _uniswapLiquidityProvider.CalculateToken0Amount(liquidity, lowPrice, highPrice) / Math.Pow(10, UniswapConstants.SGRDecimal);

            value = token0Amount * double.Parse(pool.Token1Price);
        }
        else if (currentTickIdx > positionHighTickIdx)
        {
            var token1Amount = _uniswapLiquidityProvider.CalculateToken1Amount(liquidity, lowPrice, highPrice) / Math.Pow(10, UniswapConstants.USDTDecimal) ;
            value = token1Amount;
        }
        else
        {
            var token0Amount = _uniswapLiquidityProvider.CalculateToken0Amount(liquidity, currentPrice, highPrice) / Math.Pow(10, UniswapConstants.SGRDecimal);
            var token1Amount = _uniswapLiquidityProvider.CalculateToken1Amount(liquidity, lowPrice, currentPrice) / Math.Pow(10, UniswapConstants.USDTDecimal);
            value = token0Amount  * double.Parse(pool.Token1Price) + token1Amount;
        }

        return value;
    }
    
    
    private double CalculatePositionValueV2(Position position, Pool pool)
    {
        var currentTickIdx = decimal.Parse(pool.Tick);
        var positionLowTickIdx = decimal.Parse(position.TickLower.TickIdx);
        var positionHighTickIdx = decimal.Parse(position.TickUpper.TickIdx);
        var liquidity = position.Liquidity;
        double value;

        var lowPrice = double.Parse(position.TickLower.Price0);
        var highPrice = double.Parse(position.TickUpper.Price0);
        var currentPrice = double.Parse(pool.Token1Price)/Math.Pow(10, UniswapConstants.SGRDecimal-UniswapConstants.USDTDecimal);
        if (currentTickIdx < positionLowTickIdx)
        {
            var token0Amount = _uniswapLiquidityProvider.CalculateToken0Amount(liquidity, lowPrice, highPrice) / Math.Pow(10, UniswapConstants.SGRDecimal);

            value = token0Amount * double.Parse(pool.Token1Price);
        }
        else if (currentTickIdx > positionHighTickIdx)
        {
            var token1Amount = _uniswapLiquidityProvider.CalculateToken1Amount(liquidity, lowPrice, highPrice) / Math.Pow(10, UniswapConstants.USDTDecimal) ;
            value = token1Amount;
        }
        else
        {
            var token0Amount = _uniswapLiquidityProvider.CalculateToken0Amount(liquidity, currentPrice, highPrice) / Math.Pow(10, UniswapConstants.SGRDecimal);
            var token1Amount = _uniswapLiquidityProvider.CalculateToken1Amount(liquidity, lowPrice, currentPrice) / Math.Pow(10, UniswapConstants.USDTDecimal);
            value = token0Amount  * double.Parse(pool.Token1Price) + token1Amount;
        }

        return value;
    }

    private string GetId(params object[] inputs)
    {
        var rawId = inputs.JoinAsString("-");
        return HashHelper.ComputeFrom(rawId).ToHex();
    }
}
