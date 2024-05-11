using System.Collections.Generic;

namespace SchrodingerServer.Dtos.Uniswap;

public class GetBlockNoDto
{
    public string Status { get; set; }
    public string Message { get; set; }
    public string Result { get; set; }
}

public class PoolsDto
{
    public List<Pool> Pools { get; set; }
}

public class PoolDto
{
    public Pool Pool { get; set; }
}

public class Pool
{
    public string Id { get; set; }
    public Token Token0 { get; set; }
    public Token Token1 { get; set; }
    public string Tick { get; set; }
    
    public string Token0Price { get; set; }
    public string Token1Price { get; set; }

    public class Token
    {
        public string Id { get; set; }
        public string Symbol { get; set; }
        public string Decimals { get; set; }
        public string DerivedETH { get; set; }
    }
}


public class PoolDayDatasDto
{
    public List<PoolDayData> PoolDayDatas { get; set; }
}

public class PoolDayData
{
    public long Date { get; set; }
    public string High { get; set; }
    public string Low { get; set; }
    public string Token0Price { get; set; }
    public string Token1Price { get; set; }
    public long TxCount { get; set; }
}

public class PositionsDto
{
    public List<Position> Positions { get; set; }
}

public class Position
{
    public string Id { get; set; }
    public string Liquidity { get; set; }
    public string Owner { get; set; }
    public Tick TickLower { get; set; }
    public Tick TickUpper { get; set; }
    
    public class Tick
    {
        public string Price0 { get; set; }
        public string Price1 { get; set; }
        public string TickIdx { get; set; }
    }
}