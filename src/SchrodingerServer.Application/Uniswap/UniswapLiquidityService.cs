using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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
    Task CreateSnapshotForOnceAsync(string bizDate, string poolId);
    Task<List<string>> GetValidPositionIdsAsync(string poolId, string bizDate);
    Task CreateSnapshotAsync(string bizDate, string poolId);
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
    
    public async Task CreateSnapshotForOnceAsync(string bizDate, string poolId)
    {
        var postisons = new List<Position>();
        
        var datetime = DateTime.ParseExact(bizDate, "yyyyMMdd", null);
        var snapshotTs = new DateTimeOffset(datetime).ToUnixTimeSeconds();

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
        
        var poolDto = await _uniswapLiquidityProvider.GetPoolAsync(poolId, blockNoInt);
        var pool = poolDto.Pool;
        _logger.LogDebug("pool data {ts}", JsonConvert.SerializeObject(pool));

        var validPositions = new List<ValidPosition>();

        foreach (var position in postisons)
        {
            if (double.Parse(position.Liquidity) <= 0)
            {
                continue;
            }
            
            var intersection = GetIntersectionPrices(pool, position);
            if (intersection.Count < 2)
            {
                continue;
            }

            var validPosition = new ValidPosition
            {
                Position = position,
                IntersectionPrices = intersection
            };
            
            validPositions.Add(validPosition);

        }
        
        _logger.LogDebug("valid position count: {cnt}", validPositions.Count);
        
        var snapshotIndexList = new List<UniswapPositionSnapshotIndex>();
        foreach (var validPosition in validPositions)
        {
            var value = CalculatePositionValueV2(validPosition, pool);
            var snapshot = new UniswapPositionSnapshotIndex()
            {   
                Id = GetId(pool.Id, validPosition.Position.Id, bizDate),
                PoolId = pool.Id,
                Token0Symbol = pool.Token0.Symbol,
                Token1Symbol = pool.Token1.Symbol,
                PositionOwner = validPosition.Position.Owner,
                PositionId = validPosition.Position.Id,
                CurrentPrice = pool.Token1Price,
                PositionLowPrice = validPosition.Position.TickLower.Price0,
                PositionHighPrice = validPosition.Position.TickUpper.Price0,
                PointAmount = value.ToString(CultureInfo.InvariantCulture),
                BizDate = bizDate,
                CreateTime = DateTime.UtcNow
            };
            snapshotIndexList.Add(snapshot);
        }

        await _uniswapLiquidityProvider.AddPositionsSnapshotAsync(snapshotIndexList);
    }
    
    
    public async Task CreateSnapshotAsync(string bizDate, string poolId)
    {
        var postisons = new List<Position>();
        
        var now = DateTime.UtcNow;
        DateTime snapshotTime = now.AddMinutes(-5);  // calculate snapshot time 5 minutes earlier in case we don't have data
        var snapshotTs = new DateTimeOffset(snapshotTime).ToUnixTimeSeconds();

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
        
        var poolDto = await _uniswapLiquidityProvider.GetPoolAsync(poolId, blockNoInt);
        var pool = poolDto.Pool;
        _logger.LogDebug("pool data {ts}", JsonConvert.SerializeObject(pool));

        var validPositions = new List<ValidPosition>();

        foreach (var position in postisons)
        {
            if (double.Parse(position.Liquidity) <= 0)
            {
                continue;
            }
            
            var intersection = GetIntersectionPrices(pool, position);
            if (intersection.Count < 2)
            {
                continue;
            }

            var validPosition = new ValidPosition
            {
                Position = position,
                IntersectionPrices = intersection
            };
            
            validPositions.Add(validPosition);

        }
        
        _logger.LogDebug("valid position count: {cnt}", validPositions.Count);
        
        var snapshotIndexList = new List<UniswapPositionSnapshotIndex>();
        foreach (var validPosition in validPositions)
        {
            var value = CalculatePositionValueV2(validPosition, pool);
            var snapshot = new UniswapPositionSnapshotIndex()
            {   
                Id = GetId(pool.Id, validPosition.Position.Id, snapshotTs),
                PoolId = pool.Id,
                Token0Symbol = pool.Token0.Symbol,
                Token1Symbol = pool.Token1.Symbol,
                PositionOwner = validPosition.Position.Owner,
                PositionId = validPosition.Position.Id,
                CurrentPrice = pool.Token1Price,
                PositionLowPrice = validPosition.Position.TickLower.Price0,
                PositionHighPrice = validPosition.Position.TickUpper.Price0,
                PointAmount = value.ToString(CultureInfo.InvariantCulture),
                BizDate = bizDate,
                CreateTime = DateTime.UtcNow
            };
            snapshotIndexList.Add(snapshot);
        }

        await _uniswapLiquidityProvider.AddPositionsSnapshotAsync(snapshotIndexList);
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
    
    private List<double> GetIntersectionPrices(Pool pool, Position position)
    {
        var positionLow = double.Parse(position.TickLower.Price0);
        var positionHigh = double.Parse(position.TickUpper.Price0);
        
        var currentPrice = double.Parse(pool.Token1Price)/Math.Pow(10, UniswapConstants.SGRDecimal-UniswapConstants.USDTDecimal);
        var currentValue = _optionsMonitor.CurrentValue;
        double targetHigh = currentPrice * (1+currentValue.UpperShift);
        double targetLow = currentPrice * (1-currentValue.LowerShift);
        
        // var intersection = GetIntersectionPrices(targetLow, targetHigh, positionLow, positionHigh);
        _logger.LogDebug("targetLow: {targetLow}, targetHigh: {targetHigh}, positionLow: {positionLow}, positionHigh: {positionHigh}", 
            targetLow, targetHigh, positionLow, positionHigh);
        
        var ret = new List<double>();

        if (targetLow >= positionHigh || positionLow >= targetHigh)
        {
            return ret;
        }

        var intersectionLow = Math.Max(targetLow, positionLow);
        var intersectionHigh = Math.Min(targetHigh, positionHigh);
        
        ret.Add(intersectionLow);

        if (intersectionHigh != intersectionLow)
        {
            ret.Add(intersectionHigh);
        }
        
        return ret;
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
    
    
    private double CalculatePositionValueV2(ValidPosition validPosition, Pool pool)
    {
        var position = validPosition.Position;
        var intersectionPrices = validPosition.IntersectionPrices;
        
        var liquidity = position.Liquidity;
        double value;
        
        var positionLowPrice = double.Parse(position.TickLower.Price0);
        var positionHighPrice = double.Parse(position.TickUpper.Price0);


        var tokensAmountAtLowPrice = GetTokensAmountAtPrice(liquidity, positionLowPrice, positionHighPrice,intersectionPrices[0]);
        var tokensAmountAtHighPrice = GetTokensAmountAtPrice(liquidity, positionLowPrice, positionHighPrice,intersectionPrices[1]);


        var deltaAmount0 = tokensAmountAtLowPrice[0] - tokensAmountAtHighPrice[0];
        var deltaAmount1 = tokensAmountAtHighPrice[1] - tokensAmountAtLowPrice[1];

        var valueInUSD = deltaAmount0 * double.Parse(pool.Token1Price) + deltaAmount1;

        var amountOfPoint10 = valueInUSD * Math.Pow(10, UniswapConstants.SGRDecimal) * 99;
        
        return amountOfPoint10;
    }
    
    
    private List<double> GetTokensAmountAtPrice(string liquidity, double positionLowPrice, double positionHighPrice, double targetPrice)
    {
        var ret = new List<double>();
        if (targetPrice <= positionLowPrice)
        {
            var token0Amount = _uniswapLiquidityProvider.CalculateToken0Amount(liquidity, positionLowPrice, positionHighPrice) / Math.Pow(10, UniswapConstants.SGRDecimal);
            ret.Add(token0Amount);
            ret.Add(0);
        }
        else if (targetPrice >= positionHighPrice)
        {
            var token1Amount = _uniswapLiquidityProvider.CalculateToken1Amount(liquidity, positionLowPrice, positionHighPrice) / Math.Pow(10, UniswapConstants.USDTDecimal) ;
            ret.Add(0);
            ret.Add(token1Amount);
        }
        else
        {
            var token0Amount = _uniswapLiquidityProvider.CalculateToken0Amount(liquidity, targetPrice, positionHighPrice) / Math.Pow(10, UniswapConstants.SGRDecimal);
            var token1Amount = _uniswapLiquidityProvider.CalculateToken1Amount(liquidity, positionLowPrice, targetPrice) / Math.Pow(10, UniswapConstants.USDTDecimal);
            ret.Add(token0Amount);
            ret.Add(token1Amount);
        }

        return ret;
    }

    private string GetId(params object[] inputs)
    {
        var rawId = inputs.JoinAsString("-");
        return rawId;
    }

    
    public async Task<List<string>> GetValidPositionIdsAsync(string poolId, string bizDate)
    {
        DateTime currentUtc = DateTime.ParseExact(bizDate, "yyyyMMdd", null); 
        DateTime targetUtc = currentUtc.AddHours(-24).Date;

        var snapshotTs = new DateTimeOffset(targetUtc).ToUnixTimeSeconds();

        var blockNo = await _etherscanProvider.GetBlockNoByTimeAsync(snapshotTs);
        int blockNoInt = int.Parse(blockNo);
        
        var cnt = 0;
        var offset = 0;
        var postisons = new List<Position>();
        var ret = new  List<string>();
        while (cnt == Step || offset == 0)
        {
            var next = await _uniswapLiquidityProvider.GetPositionsAsync(poolId, blockNoInt, offset, Step);
            cnt = next.Positions.Count;
            postisons.AddRange(next.Positions);
            offset += Step;
        } 
        
        if (postisons.IsNullOrEmpty())
        {
            return ret;
        }
        
        ret = postisons.Where(position => long.Parse(position.Liquidity) > 0).Select(position => position.Id).ToList();
        _logger.LogDebug("valid position count: {cnt}", ret.Count);
        return ret;
    }
}
