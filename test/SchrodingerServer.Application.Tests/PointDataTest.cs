using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.Newtonsoft;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SchrodingerServer.Common;
using SchrodingerServer.Common.GraphQL;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Users;
using SchrodingerServer.Users.Index;
using Xunit;
using Xunit.Abstractions;

namespace SchrodingerServer;

public class PointDataTest : SchrodingerServerApplicationTestBase
{
    private const int MaxResultCount = 800;
    
    private const string PrePath = "/Users/dengxiaofeng/Downloads/swaplpindex/";
    private const string HolderPath =  PrePath + "HolderDailyChanges.json";
 
    private readonly IGraphQLClient _client;
    private readonly ILogger<GraphQlHelper> _graphQlLogger;
    private readonly PointTradeOptions PointTradeOptions;
    
    private Dictionary<string, Dictionary<string, decimal>> BizDatePriceDict = new ();
    private Dictionary<string, HolderDailyChangeDto> HolderBalanceDict = new ();
    private readonly Dictionary<string, PointDailyRecordIndex> Source = new ();
    private readonly Dictionary<string, PointDailyRecordIndex> Target = new ();
    private readonly List<SwapLPIndex> SwapLpIndices = new ();

    public PointDataTest(ITestOutputHelper output) : base(output)
    {
        _graphQlLogger = GetRequiredService<ILogger<GraphQlHelper>>();

        _client = new GraphQLHttpClient(
            "https://indexer.schrodingernft.ai/SchrodingerIndexer_DApp/SchrodingerIndexerPluginSchema/graphql",
            new NewtonsoftJsonSerializer());
 
       string appJson = File.ReadAllText(PrePath + "appsettings.json");
       
       var optionsRoot = JsonConvert.DeserializeObject<OptionsRoot>(appJson);
       
       PointTradeOptions =  optionsRoot.PointTradeOptions;
    }
    
    [Fact]
    public async Task PrepareData_Test()
    {
        var chainId = "tDVV";
        string[] bizDateArray = { "20240319" };

        foreach (var bizDate in bizDateArray)
        {
            await GetHolderDailyChangeListAsync_Save(chainId, bizDate);
        }
    }
    
    [Fact]
    public async Task LoadPrice_Test()
    {
        LoadPriceData();
        foreach (var dateEntry in BizDatePriceDict)
        {
            Console.WriteLine($"Date: {dateEntry.Key}");
            foreach (var symbolEntry in dateEntry.Value)
            {
                Console.WriteLine($"\tSymbol: {symbolEntry.Key}, Price: {symbolEntry.Value}");
            }
        }
    }
    
    [Fact]
    public async Task LoadPointDailyData_Test()
    {
        LoadPointDailyData("20240320");
    }
    
    
    [Fact]
    public async Task LpSwapData_Test()
    {
        string[] bizDateList = { "20240419", "20240420", "20240421", "20240422",  "20240423", "20240424", "20240425", "20240426", "20240427", "20240428", "20240429", "20240430"};
        foreach (var bizDate in bizDateList)
        {
            LoadLpSwapData(bizDate);
        }
        var groupedData = SwapLpIndices
            .Where(x => x.Symbol == "ALP ELF-SGR-1")
            .GroupBy(slp => slp.ContractAddress)  
            .Select(group => new
            {
                ContractAddress = group.Key,
                LPAddresses = group
                    .GroupBy(slp => slp.LPAddress)
                    .Select(g => new
                    {
                        LPAddress = g.Key,
                        Points = g.Sum(item => ToPrice(item.Balance, item.Decimals) * 99)
                    })
                    .OrderByDescending(lp => lp.Points)   
                    .ToList()
            });
        foreach (var group in groupedData)
        {
            _output.WriteLine($"Contract Address: {group.ContractAddress}");
            foreach (var lp in group.LPAddresses)
            {
                _output.WriteLine($"{lp.LPAddress},{lp.Points}");
            }
        }
        
    }
    
    private void LoadPriceData()
    {
        string jsonData = File.ReadAllText(PrePath + "symboldaypriceindex.json");
        
        ParsePriceJsonAndFillDictionary(jsonData);
    }
    
    
    private void LoadLpSwapData(string bizDate)
    {
        string jsonData = File.ReadAllText(PrePath + bizDate + "_swaplpindex.json");
        
        ParseSwapLpJson(jsonData);
    }
    
    private void LoadPointDailyData(string bizDate)
    {
        //reset
        Target.Clear();
        string filePrefix = "pointdailyrecordindex_" + bizDate;
        
        string[] filePaths = Directory.GetFiles(PrePath, $"{filePrefix}*.json");
        
        foreach (var filePath in filePaths)
        {
            string jsonData =  File.ReadAllText(filePath);
            ParsePointDailyJsonAndFillDictionary(jsonData);
        }
    }
    
    
    [Fact]
    public async Task CheckPointData_Test()
    {
        var chainId = "tDVV"; 
        //from start date 
        string[] bizDateList = { "20240319", "20240320","20240321",
            "20240322","20240323","20240324","20240325","20240326",
            "20240327","20240328","20240329","20240330","20240331",
            "20240401"
        };

        //1.prepare data
        //clear holder balance data
        if (File.Exists(HolderPath))
        {
            File.Delete(HolderPath);
        }
        //load price data
        LoadPriceData();

        var tradeStr = "";
        foreach (var bizDate in bizDateList)
        {
            int compareCount = 0;
            int pointAmountEqualsCount = 0;
            int pointAmountNotEqualsCount = 0;
            int sourceTotalCount = 0;
            //2.prepare query Data
            //a.read json data 
            var bizDateIndexerData = await GetHolderDailyChangeListAsync_Save(chainId, bizDate);
            
            //3.calc source data 
            await CalcPointDataAsync(chainId, bizDate, bizDateIndexerData);
            
            //4.base target and check
            //load target data
            LoadPointDailyData(bizDate);
            
            //check
            sourceTotalCount = Source.Count;
            if (!Target.IsNullOrEmpty())
            {
                foreach (var (key, value)  in Target)
                {
                    compareCount++;
                    if (Source.TryGetValue(key, out var sourceValue))
                    {
                        if ( !DecimalHelper.ConvertBigInteger(value.PointAmount, 0)
                                .Equals(DecimalHelper.ConvertBigInteger(value.PointAmount, 0)))
                        {
                            pointAmountNotEqualsCount++;
                            Console.WriteLine($"\tId: {value.Id}, target: {value.PointAmount}, source: {sourceValue.PointAmount}");
                        }
                        else
                        {
                            pointAmountEqualsCount++;
                        }

                    }
                }
            }
            
                
            Console.WriteLine($"\tbizDate: {bizDate}, sourceTotalCount: {sourceTotalCount}, compareCount: {compareCount}, " +
                              $"pointAmountEqualsCount: {pointAmountEqualsCount}, pointAmountNotEqualsCount: {pointAmountNotEqualsCount}");


            int XPSGR9Count = Source.Where(pair => pair.Value.PointName.Equals("XPSGR-9")).Count();
            
            tradeStr +=
                $"\tbizDate: {bizDate}, sourceTotalCount: {sourceTotalCount}, XPSGR9Count: {XPSGR9Count}, batchCount: {sourceTotalCount / 20}, XPSGR9BatchCount: {XPSGR9Count / 20}, " +
                $"totalTradeFee: {sourceTotalCount / 20 * 0.0160375} ELF \n";
        }
        Console.WriteLine("统计手续费：");
        Console.WriteLine(tradeStr);
    }
 
    private async Task CalcPointDataAsync(string chainId, string bizDate, List<HolderDailyChangeDto> bizDateIndexerData)
    {
        //clear 
        Source.Clear();
        var priceBizDate = GetPriceBizDate(bizDate);
        var priceDict = BizDatePriceDict[priceBizDate];
        
        //2.calc source data
        //has changed
        foreach (var item in bizDateIndexerData)
        {
            var symbolPrice = DecimalHelper.GetValueFromDict(priceDict, item.Symbol,
                PointTradeOptions.BaseCoin);
            
            HandlePointDailyChange(chainId, item, symbolPrice);
        }
        
        //no change
        var noChanges = HolderBalanceDict.Where(item => item.Value.Balance > 0
                                                        && item.Value.Date.SafeToInt() < bizDate.SafeToInt())
            .Select(item => item.Value).ToList();
        
        foreach (var item in noChanges)
        {
            var symbolPrice = DecimalHelper.GetValueFromDict(priceDict, item.Symbol,
                PointTradeOptions.BaseCoin);
            
            var dto = new HolderDailyChangeDto
            {
                Address = item.Address,
                Date = bizDate,
                Symbol = item.Symbol,
                Balance = item.Balance
            };
            
            HandlePointDailyChange(chainId, dto, symbolPrice);
        }
    }


    private async Task<List<HolderDailyChangeDto>> GetHolderDailyChangeListAsync(string chainId, string bizDate)
    {
        var provider = new HolderBalanceProvider(new GraphQlHelper(_client, _graphQlLogger),
            null);
        List<HolderDailyChangeDto> result = new List<HolderDailyChangeDto>();
        List<HolderDailyChangeDto> dailyChanges;
        var skipCount = 0;
        do
        {
            dailyChanges = await provider.GetHolderDailyChangeListAsync(chainId, bizDate, skipCount, MaxResultCount, "TESTSGR-1");
            if (dailyChanges.IsNullOrEmpty())
            {
                break;
            }

            result.AddRange(dailyChanges);
            skipCount += dailyChanges.Count;
        } while (!dailyChanges.IsNullOrEmpty());

        return result;
    }
    
    
    private async Task<List<HolderDailyChangeDto>> GetHolderDailyChangeListAsync_Save(string chainId, string bizDate)
    {
        string bizPath = PrePath + "HolderDailyChanges_" + bizDate + ".json";
        
        var result = new List<HolderDailyChangeDto>();
        
        if (File.Exists(bizPath))
        {
            var bizPathJsonString = await File.ReadAllTextAsync(bizPath);

            if (!bizPathJsonString.IsNullOrEmpty())
            {
                result = JsonConvert.DeserializeObject<List<HolderDailyChangeDto>>(bizPathJsonString);
            }
        }
        else
        {
            result = await GetHolderDailyChangeListAsync(chainId, bizDate);
            
            //add bizPath json
            var resultJsonString = JsonConvert.SerializeObject(result);
            await File.WriteAllTextAsync(bizPath, resultJsonString);
        }
        
        //update holder balance
        foreach (var dto in result)
        {
            var id = IdGenerateHelper.GetHolderBalanceId(chainId, dto.Symbol, dto.Address);
            //replace
            HolderBalanceDict[id] = dto;
        }
        
        var jsonString = JsonConvert.SerializeObject(HolderBalanceDict);
        await File.WriteAllTextAsync(HolderPath, jsonString);
        return result;
    }
    

    public JArray ReadJsonAndExtractHitsAsync(string filePath)
    {
        string jsonData = File.ReadAllText(filePath);
        
        JObject jsonObject = JObject.Parse(jsonData);

        return (JArray)jsonObject["hits"]["hits"];
    }
    
    
    public void HandlePointDailyChange(string chainId, HolderDailyChangeDto dto, decimal? symbolPrice)
    {
        foreach (var (pointName, pointInfo) in PointTradeOptions.PointMapping)
        {
            if (CollectionUtilities.IsNullOrEmpty(pointInfo.ConditionalExp))
            {
                continue;
            }
            
            if (pointInfo.NeedMultiplyPrice && symbolPrice == null)
            {
                 continue;
            }

            var match = Regex.Match(dto.Symbol, pointInfo.ConditionalExp);

            if (!match.Success)
            {
                continue;
            }

            var input = new PointDailyRecordIndex()
            {
                ChainId = chainId,
                PointName = pointName,
                BizDate = dto.Date,
                Address = dto.Address,
                PointAmount = CalcPointAmount(dto, pointInfo, symbolPrice)
            };
            if (input.PointAmount == 0)
            {
                continue;
            }
            input.Id = IdGenerateHelper.GetPointDailyRecord(chainId, input.BizDate, input.PointName, input.Address);
             
             if (Source.TryGetValue(input.Id, out var pointBefore))
            {
                 input.PointAmount += pointBefore.PointAmount;
            }
            Source[input.Id] = input;
        }
    }

    private decimal CalcPointAmount(HolderDailyChangeDto dto, PointInfo pointInfo, decimal? symbolPrice)
    {
        //use balance
        if (pointInfo.UseBalance)
        {
            return (decimal)(dto.Balance * pointInfo.Factor * symbolPrice);
        }
        var changeAmount = Math.Abs(dto.ChangeAmount);
        var pointAmount = (decimal)(changeAmount * pointInfo.Factor);

        if (pointInfo.NeedMultiplyPrice && symbolPrice != null)
        {
            return (decimal)(pointAmount * symbolPrice);
        }

        return pointAmount;
    }
    
    
    private void ParsePriceJsonAndFillDictionary(string jsonString)
    {
        using (JsonDocument doc = JsonDocument.Parse(jsonString))
        {
            JsonElement root = doc.RootElement;
            JsonElement hitsArray = root.GetProperty("hits").GetProperty("hits");
            
            foreach (JsonElement hit in hitsArray.EnumerateArray())
            {
                JsonElement source = hit.GetProperty("_source");
                string symbol = source.GetProperty("symbol").GetString();
                string date = source.GetProperty("date").GetString();
                decimal price = source.GetProperty("price").GetDecimal();

                if (!BizDatePriceDict.ContainsKey(date))
                {
                    BizDatePriceDict[date] = new Dictionary<string, decimal>();
                }

                BizDatePriceDict[date][symbol] = price;
            }
        }
    }
    
    private void ParseSwapLpJson(string jsonString)
    {
        using (JsonDocument doc = JsonDocument.Parse(jsonString))
        {
            JsonElement root = doc.RootElement;
            JsonElement hitsArray = root.GetProperty("hits").GetProperty("hits");
            
            foreach (JsonElement hit in hitsArray.EnumerateArray())
            {
                JsonElement source = hit.GetProperty("_source");
                string contractAddress = source.GetProperty("contractAddress").GetString();
                string symbol = source.GetProperty("symbol").GetString();
                string lPAddress = source.GetProperty("lPAddress").GetString();
                int decimals = source.GetProperty("decimals").GetInt32();
                long balance = source.GetProperty("balance").GetInt64();

                SwapLpIndices.Add(new SwapLPIndex
                {
                    ContractAddress = contractAddress,
                    LPAddress = lPAddress,
                    Symbol = symbol,
                    Decimals = decimals,
                    Balance = balance
                });
            }
        }
    }
    
    private void ParsePointDailyJsonAndFillDictionary(string jsonString)
    {
        using (JsonDocument doc = JsonDocument.Parse(jsonString))
        {
            JsonElement root = doc.RootElement;
            JsonElement hitsArray = root.GetProperty("hits").GetProperty("hits");
            
            foreach (JsonElement hit in hitsArray.EnumerateArray())
            {
                JsonElement source = hit.GetProperty("_source");
                string chainId = source.GetProperty("chainId").GetString();
                string pointName = source.GetProperty("pointName").GetString();
                string bizDate = source.GetProperty("bizDate").GetString();
                string address = source.GetProperty("address").GetString();
                string id = source.GetProperty("id").GetString();

                decimal pointAmount = source.GetProperty("pointAmount").GetDecimal();

                var index = new PointDailyRecordIndex
                {
                    Id = id,
                    ChainId = chainId,
                    PointName = pointName,
                    PointAmount = pointAmount,
                    BizDate = bizDate,
                    Address = address
                };
                Target[id] = index;
            }
        }
    }
    
    private static string GetPriceBizDate(string bizDate)
    {
        string priceBizDate;
        if (bizDate.Equals(DateTime.UtcNow.ToString(TimeHelper.Pattern)))
        {
            priceBizDate = TimeHelper.GetDateStrAddDays(bizDate, -1);
        }
        else
        {
            priceBizDate = bizDate;
        }

        return priceBizDate;
    }

    private bool CompareDecimal(decimal from, decimal to)
    {
        decimal epsilon = 0.000001m;  
        if (Math.Abs(from - to) < epsilon)
        {
             return true;
        }

        return false;
    }

    [Fact]
    public void CompareDecimal_Test()
    {
        var from = 85204379367595.485m;
        var to = 85204379367595.4850000000m;
        CompareDecimal(from, to);
    }


    [Fact]
    public void GenerateDateList_Test()
    {
        var list = GenerateDateList("20240320");
        var str = "";
        foreach (var bizDate in list)
        {
            str += '"' + bizDate + '"'  + "," ;
        }
        Console.WriteLine(str);

    }

    public static List<string> GenerateDateList(string startDateStr)
    {
        List<string> dates = new List<string>();

        DateTime startDate = DateTime.ParseExact(startDateStr, "yyyyMMdd", CultureInfo.InvariantCulture);
        DateTime currentDate = DateTime.Now;

         for (DateTime date = startDate; date <= currentDate; date = date.AddDays(1))
        {
            dates.Add(date.ToString("yyyyMMdd")); 
        }

        return dates;
    }
    
    
    private static decimal ToPrice(decimal amount, int decimals)
    {
        return amount / (decimal)Math.Pow(10, decimals);
    }

}

public class SwapLPIndex
{
    public string LPAddress { get; set; }
    public string Symbol { get; set; }
    public string ContractAddress { get; set; }
    public int Decimals { get; set; }
    public long Balance { get; set; }
    public DateTime UpdateTime { get; set; }
}

public class OptionsRoot
{
    public PointTradeOptions PointTradeOptions { get; set; } 
}