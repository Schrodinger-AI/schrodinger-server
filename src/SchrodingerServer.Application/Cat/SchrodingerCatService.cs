using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Types;
using Google.Protobuf;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Schrodinger;
using SchrodingerServer.Cat.Provider;
using SchrodingerServer.Cat.Provider.Dtos;
using SchrodingerServer.Common;
using SchrodingerServer.Common.Options;
using SchrodingerServer.Dto;
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.Helper;
using SchrodingerServer.Options;
using SchrodingerServer.Users;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.Caching;
using Volo.Abp.ObjectMapping;

namespace SchrodingerServer.Cat;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class SchrodingerCatService : ApplicationService, ISchrodingerCatService
{
    private readonly ISchrodingerCatProvider _schrodingerCatProvider;
    private readonly ILevelProvider _levelProvider;
    private readonly IOptionsMonitor<LevelOptions> _levelOptions;
    private readonly IObjectMapper _objectMapper;
    private readonly ILogger<SchrodingerCatService> _logger;
    private readonly IUserInformationProvider _userInformationProvider;
    private readonly IUserActionProvider _userActionProvider;
    private readonly IOptionsMonitor<ActivityTraitOptions> _traitsOptions;
    private readonly ISecretProvider _secretProvider;
    private readonly ChainOptions _chainOptions;
    private readonly IOptionsMonitor<PoolOptions> _poolOptionsMonitor;
    private readonly IDistributedCache<string> _distributedCache;

    private static readonly List<string> GenOneTraitTypes = new() { "Background", "Clothes", "Breed" };

    public SchrodingerCatService(ISchrodingerCatProvider schrodingerCatProvider, ILevelProvider levelProvider,
        IObjectMapper objectMapper, ILogger<SchrodingerCatService> logger, IOptionsMonitor<LevelOptions> levelOptions,
        IUserInformationProvider userInformationProvider,IUserActionProvider userActionProvider, 
        IOptionsMonitor<ActivityTraitOptions> traitsOptions, ISecretProvider secretProvider, IOptionsMonitor<ChainOptions> chainOptions, 
        IOptionsMonitor<PoolOptions> poolOptionsMonitor, IDistributedCache<string> distributedCache)
    {
        _schrodingerCatProvider = schrodingerCatProvider;
        _levelProvider = levelProvider;
        _objectMapper = objectMapper;
        _logger = logger;
        _levelOptions = levelOptions;
        _userInformationProvider = userInformationProvider;
        _userActionProvider = userActionProvider;
        _traitsOptions = traitsOptions;
        _secretProvider = secretProvider;
        _chainOptions = chainOptions.CurrentValue;
        _poolOptionsMonitor = poolOptionsMonitor;
        _distributedCache = distributedCache;
    }

    public async Task<SchrodingerListDto> GetSchrodingerCatListAsync(GetCatListInput input)
    {
        var address = await _userActionProvider.GetCurrentUserAddressAsync();
        if (!address.IsNullOrEmpty())
        {
            input.Address = address;
        }
        _logger.LogInformation("GetSchrodingerCatListAsync address:{address}",input.Address);

        List<SchrodingerIndexerDto> indexerList;
        var result = new SchrodingerListDto();

        if (!input.Rarities.IsNullOrEmpty())
        {
            indexerList = await GetSchrodingerCatAllList(input);
            var data = await SetLevelInfoAsync(indexerList, input.Address);
           
            data.ForEach(item =>
            {
                if (item.Generation == 9 && item.Describe.IsNullOrEmpty())
                {
                    item.Describe = "Common,,";
                }
            });
            
            var pageData = data
                .Where(cat => input.Rarities.Contains(cat.Rarity))
                .OrderByDescending(cat => cat.AdoptTime)
                .ToList();
            result.Data = PaginationHelper.Paginate(pageData, input.SkipCount, input.MaxResultCount);
            result.TotalCount = pageData.Count;
        }
        else
        {
            var schrodingerIndexerListDto = await _schrodingerCatProvider.GetSchrodingerCatListAsync(input);
            indexerList = schrodingerIndexerListDto.Data;
            var data = await SetLevelInfoAsync(indexerList, input.Address);
            
            data.ForEach(item =>
            {
                if (item.Generation == 9 && item.Describe.IsNullOrEmpty())
                {
                    item.Describe = "Common,,";
                }
            });
            
            result.Data = data;
            result.TotalCount = schrodingerIndexerListDto.TotalCount;
        }

        foreach (var item in result.Data)
        {
            var specialTag = TraitHelper.GetSpecialTraitOfElection(_traitsOptions.CurrentValue, item.Traits);
            item.SpecialTrait = specialTag;
        }

        return result;
    }

    public async Task<SchrodingerListDto> GetSchrodingerAllCatsListAsync(GetCatListInput input)
    {
        var address = await _userActionProvider.GetCurrentUserAddressAsync();
        if (!address.IsNullOrEmpty())
        {
            input.Address = address;
        }

        input.FilterSgr = true;
        return await GetSchrodingerAllCatsPageList(input);
    }

    public async Task<SchrodingerDetailDto> GetSchrodingerCatDetailAsync(GetCatDetailInput input)
    {
        var detail = new SchrodingerDetailDto();
        var collectionId = input.ChainId == "tDVV" ? "tDVV-SGR-0" : "tDVW-SGR-0";
        var address = await _userActionProvider.GetCurrentUserAddressAsync();
        if (!address.IsNullOrEmpty())
        {
            input.Address = address;
        }
        
        var holderDetail = await _schrodingerCatProvider.GetSchrodingerCatDetailAsync(input);
        //query symbolIndex
        var querySymbolInput = new GetCatListInput
        {
            ChainId = input.ChainId,
            Keyword = input.Symbol,
            SkipCount = 0,
            MaxResultCount = 1
        };
        var symbolIndexerListDto =  await GetSchrodingerAllCatsPageList(querySymbolInput);
        
        if (symbolIndexerListDto == null || symbolIndexerListDto.TotalCount == 0)
        {
            if (holderDetail == null)
            {
                return new SchrodingerDetailDto();
            }

            detail = holderDetail;
            if (!holderDetail.Address.IsNullOrEmpty())
            {
                detail.HolderAmount = holderDetail.Amount;
            }
            
            detail.CollectionId = collectionId;
            return detail;
        }
        
        var amount = symbolIndexerListDto.Data[0].Amount;
        _logger.LogInformation("GetSchrodingerCatDetailAsync address:{address}",address);
        if (input.Address.IsNullOrEmpty())
        {
            detail = holderDetail ?? _objectMapper.Map<SchrodingerDto, SchrodingerDetailDto>(symbolIndexerListDto.Data[0]);
            detail.Amount = amount;
            _logger.LogInformation("GetSchrodingerCatDetailAsync detail:{detail}",JsonConvert.SerializeObject(detail));
            detail.CollectionId = collectionId;
            return detail;
        }
        
        if (holderDetail == null || holderDetail.Address.IsNullOrEmpty())
        {
            detail = holderDetail ?? _objectMapper.Map<SchrodingerDto, SchrodingerDetailDto>(symbolIndexerListDto.Data[0]);
            detail.Amount = amount;
            detail.HolderAmount = 0;
            detail.CollectionId = collectionId;
            return detail;
        }

        detail = holderDetail;
        
        detail.HolderAmount = detail.Amount;
        detail.Amount = amount;
        detail.CollectionId = collectionId;
        return detail;
    }
    
    
    private async Task<SchrodingerListDto> GetSchrodingerAllCatsPageList(GetCatListInput input)
    {
        var result = new SchrodingerListDto();
        var schrodingerIndexerListDto = await _schrodingerCatProvider.GetSchrodingerAllCatsListAsync(input);
        var list = _objectMapper.Map<List<SchrodingerSymbolIndexerDto>, List<SchrodingerDto>>(schrodingerIndexerListDto.Data);
        //get awaken price
        var price = await _levelProvider.GetAwakenSGRPrice();

        var isInWhiteList = await _levelProvider.CheckAddressIsInWhiteListAsync(input.Address);
        _logger.LogInformation("calculate rank info for user: {address}", input.Address);
        foreach (var schrodingerDto in list.Where(schrodingerDto => (schrodingerDto.Generation == 9 && !schrodingerDto.Level.IsNullOrEmpty())))
        {
            //get levelInfo
            var levelInfoDto = await _levelProvider.GetItemLevelDicAsync(schrodingerDto.Rank, price);
            _logger.LogInformation("rank info: {info}", JsonConvert.SerializeObject(levelInfoDto));
            schrodingerDto.AwakenPrice = levelInfoDto?.AwakenPrice;
            schrodingerDto.Level = levelInfoDto?.Level;
            schrodingerDto.Token = levelInfoDto?.Token;
            // schrodingerDto.Total = levelInfoDto?.Token;
            schrodingerDto.Describe = levelInfoDto?.Describe;
        }
        
        if (!isInWhiteList)
        {
            _logger.LogInformation("user not in whitelist");
            foreach (var schrodingerDto in list.Where(schrodingerDto => schrodingerDto.Generation == 9))
            {
                schrodingerDto.Rank = 0;
                schrodingerDto.Level = "";
                schrodingerDto.Rarity = "";
                schrodingerDto.AwakenPrice = "";
                schrodingerDto.Token = "";
            }
        }
        
        list.ForEach(item =>
        {
            if(item.Generation == 9 && item.Describe.IsNullOrEmpty())
            {
                item.Describe = "Common,,";
            }
            
            var specialTag = TraitHelper.GetSpecialTraitOfElection(_traitsOptions.CurrentValue, item.Traits);
            item.SpecialTrait = specialTag;
        });
        
        result.Data = list;
        result.TotalCount = schrodingerIndexerListDto.TotalCount;
        return result;
    }
    
    private async Task<List<SchrodingerDto>> SetLevelInfoAsync(List<SchrodingerIndexerDto> indexerList, string address)
    {
        var list = _objectMapper.Map<List<SchrodingerIndexerDto>, List<SchrodingerDto>>(indexerList);

        var genNineList = list.Where(cat => cat.Generation == 9).ToList();
        var genOtherList = list.Where(cat => cat.Generation != 9).ToList();
        
        var symbolIds = genNineList.Select(cat => cat.Symbol).ToList();
        var itemLevelList = new List<RankData>();

        if (symbolIds.Count == 0)
        {
            return list;
        }
        
        var rankData = await _schrodingerCatProvider.GetRankDataAsync(symbolIds);
        
        var map = new Dictionary<string, RankData>();

        foreach (var rarity in rankData.RarityInfo)
        {
            _logger.LogInformation("rarity data: {a} {b}", address, rarity.Rank);
            var rarityData = await _levelProvider.GetRarityInfo(address, rarity.Rank, true);
            map[rarity.Symbol] = rarityData;
        }

        foreach (var adoptId in symbolIds)
        {
            var rarityData = map[adoptId];
            itemLevelList.Add(rarityData);
        }
        
        if (genNineList.Count != itemLevelList.Count)
        {
            _logger.LogWarning("get item level count not equals, count1: {count1}, count2:{count2}", genNineList.Count, itemLevelList.Count);
            return list;
        }

        var newGenNineList = genNineList.Zip(itemLevelList, (a, b) =>
        {
            a.AwakenPrice = b.LevelInfo?.AwakenPrice;
            a.Level = b.LevelInfo?.Level;
            a.Token = b.LevelInfo?.Token;
            a.Rank = b.Rank.Rank;
            a.Total = b.Rank?.Total;
            a.Describe = b.LevelInfo?.Describe;
            a.Rarity = LevelConsts.RarityDictionary.TryGetValue(a.Level ?? "",
                out var rarity)
                ? rarity
                : b.LevelInfo != null && b.LevelInfo.Describe!= null ? b.LevelInfo.Describe.Split(",")[0] : "";
            return a;
        }).ToList();
        genOtherList.AddRange(newGenNineList);
        return genOtherList;
    }

    private async Task<List<SchrodingerIndexerDto>> GetSchrodingerCatAllList(GetCatListInput input)
    {
        var res = new List<SchrodingerIndexerDto>();
        var skipCount = 0;
        var maxResultCount = 5000;
        List<SchrodingerIndexerDto> list;
        do
        {
            input.SkipCount = skipCount;
            input.MaxResultCount = maxResultCount;
            var result = await _schrodingerCatProvider.GetSchrodingerCatListAsync(input);
            list = result.Data;
            var count = list.Count;
            res.AddRange(list);
            if (list.IsNullOrEmpty() || count < maxResultCount)
            {
                break;
            }

            skipCount += count;
        } while (!list.IsNullOrEmpty());

        return res;
    }

    public async Task<HoldingRankDto> GetHoldingRankAsync()
    {
        var rankList = await  _schrodingerCatProvider.GetHoldingRankAsync();
        return new HoldingRankDto
        {
            Items = _objectMapper.Map<List<RankItem>, List<RankItemDto>>(rankList)
        };
    }
    
    public async Task<RarityRankDto> GetRarityRankAsync()
    {
        var rankList = await  _schrodingerCatProvider.GetRarityRankAsync();
        return new RarityRankDto()
        {
            Items =  _objectMapper.Map<List<RarityRankItem>, List<RarityRankItemDto>>(rankList)
        };
    }

    public async Task<SchrodingerListDto> GetSchrodingerCatListInBotAsync(GetCatListInput input)
    {
        _logger.LogInformation("GetSchrodingerCatListInBotAsync input:{input}", JsonConvert.SerializeObject(input));
        var list = await GetSchrodingerCatListAsync(input);
        if (list.TotalCount == 0 || list.Data.IsNullOrEmpty())
        {
            return new SchrodingerListDto();
        }

        var nftIdList = list.Data.Select(i => input.ChainId + "-" + i.Symbol).ToList();
        var nftInfoList = await _levelProvider.BatchGetForestNftInfoAsync(nftIdList, input.ChainId);
        if (nftInfoList.Count == 0)
        {
            return list;
        }
        
        var nftInfoDict = nftInfoList.ToDictionary(i => i.NftSymbol, i => i);
        list.Data.ForEach(item =>
        {
            if (nftInfoDict.TryGetValue(item.Symbol, out var value))
            {
                item.ForestPrice = value.ListingPrice;
            }
            
            var specialTag = TraitHelper.GetSpecialTraitOfElection(_traitsOptions.CurrentValue, item.Traits);
            item.SpecialTrait = specialTag;
            
        });

        return list;
    }

    public async Task<SchrodingerListDto> GetSchrodingerAllCatsListInBotAsync(GetCatListInput input)
    {
        _logger.LogInformation("GetSchrodingerAllCatsListInBotAsync input:{input}", JsonConvert.SerializeObject(input));
        input.MinAmount = "100000000";
        input.Generations = new List<int> { 9 };
        var list = await GetSchrodingerAllCatsListAsync(input);
        if (list.TotalCount == 0 || list.Data.IsNullOrEmpty())
        {
            return new SchrodingerListDto();
        }

        var nftIdList = list.Data.Select(i => input.ChainId + "-" + i.Symbol).ToList();
        var nftInfoList = await _levelProvider.BatchGetForestNftInfoAsync(nftIdList, input.ChainId);
        if (nftInfoList.Count == 0)
        {
            return list;
        }
        
        var nftInfoDict = nftInfoList.ToDictionary(i => i.NftSymbol, i => i);
        list.Data.ForEach(item =>
        {
            if (nftInfoDict.TryGetValue(item.Symbol, out var value))
            {
                item.ForestPrice = value.ListingPrice;
            }
            
            var specialTag = TraitHelper.GetSpecialTraitOfElection(_traitsOptions.CurrentValue, item.Traits);
            item.SpecialTrait = specialTag;
            
        });

        return list;
    }
    
    public async Task<SchrodingerBoxListDto> GetSchrodingerBoxListAsync(GetBlindBoxListInput input)
    {
        var address = await _userActionProvider.GetCurrentUserAddressAsync();
        if (!address.IsNullOrEmpty())
        {
            input.Address = address;
        }
        _logger.LogInformation("GetSchrodingerBoxListAsync address:{address}",input.Address);
        
        var resp = new SchrodingerBoxListDto();

        input.AdoptTime = _levelOptions.CurrentValue.AdoptTime;
        var schrodingerIndexerBoxListDto = await _schrodingerCatProvider.GetSchrodingerBoxListAsync(input);

        var data = schrodingerIndexerBoxListDto?.Data;
        if (data.IsNullOrEmpty())
        {
            return resp;
        }
        
        // data = data.OrderBy(x => x.Rank).ThenBy(x => x.AdoptTime).ToList();
        
        data.Sort((x, y) => {
            if (x.Rarity != y.Rarity) {
                if (x.Rarity.IsNullOrEmpty())
                {
                    return 1;
                }

                if (y.Rarity.IsNullOrEmpty())
                {
                    return -1;
                }

                var indexX = BoxRarityConst.RarityList.IndexOf(x.Rarity);
                var indexY = BoxRarityConst.RarityList.IndexOf(y.Rarity);

                if (indexX < indexY)
                {
                    return -1;
                }
                else
                {
                    return 1;
                }
            } 
            
            return x.AdoptTime.CompareTo(y.AdoptTime);
        });
        
        var boxList = _objectMapper.Map<List<SchrodingerIndexerBoxDto>, List<BlindBoxDto>>(data);
        
        var isInWhiteList = await _levelProvider.CheckAddressIsInWhiteListAsync(input.Address);
        _logger.LogInformation("GetSchrodingerBoxListAsync, isInWhiteList: {isInWhiteList}", isInWhiteList);
        var price = await _levelProvider.GetAwakenSGRPrice();
        foreach (var schrodingerDto in boxList)
        {
            var levelInfoDto = await _levelProvider.GetItemLevelDicAsync(schrodingerDto.Rank, price);
            _logger.LogInformation("rank info: {info}", JsonConvert.SerializeObject(levelInfoDto));
            schrodingerDto.Describe = levelInfoDto?.Describe;

            if (isInWhiteList)
            {
                schrodingerDto.Level = levelInfoDto?.Level;
            }
            else
            {
                schrodingerDto.Rank = 0;
            }
        }
        
        boxList.ForEach(x =>
        {
            if (x.Generation == 9 && x.Rarity.IsNullOrEmpty())
            {
                x.Describe = "Common,,";
            }
            
            x.InscriptionImageUri = BoxHelper.GetBoxImage(x.Generation == 9, x.Rarity);
            
            var specialTag = TraitHelper.GetSpecialTraitOfElection(_traitsOptions.CurrentValue, _objectMapper.Map<List<TraitDto>, List<TraitsInfo>>(x.Traits));
            x.SpecialTrait = specialTag;
            
        });
        
        resp.Data = boxList.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        resp.TotalCount = schrodingerIndexerBoxListDto.TotalCount;
        
        return resp;
    }
    
    public async Task<BlindBoxDetailDto> GetSchrodingerBoxDetailAsync(GetCatDetailInput input)
    {
        _logger.LogInformation("GetSchrodingerBoxDetailAsync symbol:{symbol}",input.Symbol);
        if (input.Symbol.IsNullOrEmpty())
        {
            return new BlindBoxDetailDto();
        }
        
        var boxDetail = await _schrodingerCatProvider.GetSchrodingerBoxDetailAsync(input);
        
        var resp = _objectMapper.Map<SchrodingerIndexerBoxDto, BlindBoxDetailDto>(boxDetail);
        resp.InscriptionImageUri = BoxHelper.GetBoxImage(resp.Generation == 9, resp.Rarity);
        
        var specialTag = TraitHelper.GetSpecialTraitOfElection(_traitsOptions.CurrentValue, _objectMapper.Map<List<TraitDto>, List<TraitsInfo>>(resp.Traits));
        resp.SpecialTrait = specialTag;

        return resp;
    }

    public async Task<StrayCatsListDto> GetStrayCatsAsync(StrayCatsInput input)
    {
        _logger.LogInformation("GetStrayCatsAsync adopter:{adopter}",input.Adopter);
        if (input.Adopter.IsNullOrEmpty())
        {
            return new StrayCatsListDto();
        }

        input.AdoptTime = _levelOptions.CurrentValue.AdoptTime;
        var strayCats = await _schrodingerCatProvider.GetStrayCatsListAsync(input);

        if (strayCats == null)
        {
            return  new StrayCatsListDto();
        }
        
        var resp = _objectMapper.Map<SchrodingerIndexerStrayCatsDto, StrayCatsListDto>(strayCats);
        return resp;
    }

    public async Task<RankData> GetRarityAsync(GetRarityAsync input)
    {
        _logger.LogInformation("GetRarityAsync symbol:{symbol}",input.Symbol);
        
        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();

        var rankData = await _schrodingerCatProvider.GetRankDataAsync(new List<string>
        {
            input.Symbol
        });

        if (rankData.RarityInfo.IsNullOrEmpty())
        {
            _logger.LogInformation("GetRarityAsync, rankData is null, {symbol}", input.Symbol);
            return  new RankData();
        }
        
        var rank = rankData.RarityInfo.FirstOrDefault();
        var res = await _levelProvider.GetRarityInfo(currentAddress, rank.Rank, rank.Generation == 9);
        
        return res;
    }

    public async Task<CombineOutput> CombineAsync(CombineInput input)
    {
        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();
        // var currentAddress = input.Address;
        if (currentAddress.IsNullOrEmpty())
        {
            _logger.LogError("CombineAsync Get current address failed");
            throw new UserFriendlyException("Invalid user");
        }

        if (input.Symbols.Count != 2)
        {
            _logger.LogError("CombineAsync, invalid input, {input}", JsonConvert.SerializeObject(input));
            throw new UserFriendlyException("Invalid input");
        }
        
        var rankData = await _schrodingerCatProvider.GetRankDataAsync(input.Symbols);
        
        // check holding amount and generation
        foreach (var symbol in input.Symbols)
        {
            // query as cat 
            var holderDetail = await _schrodingerCatProvider.GetSchrodingerCatDetailAsync(new GetCatDetailInput
            {
                Address = currentAddress,
                Symbol = symbol,
                ChainId = _levelOptions.CurrentValue.ChainIdForReal
            });

            if (holderDetail != null && !holderDetail.Symbol.IsNullOrEmpty())
            {
                if (holderDetail.Amount < 100000000)
                {
                    _logger.LogError("not enough cat for, address:{address}, symbol:{symbol}, holderAmount:{holderAmount}", currentAddress, symbol, holderDetail.Amount);
                    throw new UserFriendlyException("holding not enough cat");
                }
                
                if (holderDetail.Address != currentAddress)
                {
                    _logger.LogError("cat not owned by user, address:{address}, symbol:{symbol}, owner:{owner}", currentAddress, symbol, holderDetail.Address);
                    throw new UserFriendlyException("not holding cat");
                }

                if (holderDetail.Generation != 9)
                {
                    _logger.LogError("cat not gen 9, address:{address}, symbol:{symbol}, gen:{gen}", currentAddress, symbol, holderDetail.Generation);
                    throw new UserFriendlyException("must use gen 9 cat");
                }
            }
            else
            {
                var boxDetail = rankData.RarityInfo.FirstOrDefault(x => x.Symbol == symbol);
                var amount = boxDetail?.OutputAmount ?? 0;
                var adopter = boxDetail?.Adopter ?? "";

                if (adopter != currentAddress)
                {
                    _logger.LogError("box not owned by user, address:{address}, symbol:{symbol}, adopter:{adopter}", currentAddress, symbol, adopter);
                    throw new UserFriendlyException("box not owned by user");
                }
                
                if (amount < 100000000)
                {
                    _logger.LogError("not enough box for, address:{address}, symbol:{symbol}, holderAmount:{holderAmount}", currentAddress, symbol, amount);
                    throw new UserFriendlyException("holding not enough box");
                }
                
                var gen = boxDetail?.Generation ?? 0;
                if (gen != 9)
                {
                    _logger.LogError("box not gen 9, address:{address}, symbol:{symbol}, gen:{gen}", currentAddress, symbol, gen);
                    throw new UserFriendlyException("must use gen 9 box");
                }
            }
        }
        
        var rarityInfo1 = await _levelProvider.GetRarityInfo(currentAddress, rankData.RarityInfo[0].Rank, true, true);
        var rarityInfo2 = await _levelProvider.GetRarityInfo(currentAddress, rankData.RarityInfo[1].Rank, true, true);

        if (rarityInfo1.LevelInfo.Describe != rarityInfo2.LevelInfo.Describe)
        {
            _logger.LogError("not at same level, address:{address}, symbol1:{symbol1}, symbol2:{symbol2}, " +
                             "level: {level1} {level2}", currentAddress, input.Symbols[0], input.Symbols[1], 
                rarityInfo1.LevelInfo.Level, rarityInfo2.LevelInfo.Level);
            throw new UserFriendlyException("cat not same level");
        }

        var level = rarityInfo1.LevelInfo.Level.IsNullOrEmpty() ? 0 : long.Parse(rarityInfo1.LevelInfo.Level);
        var data = new MergeInput
        {
            Tick = "SGR",
            AdoptIdA = Hash.LoadFromHex(rankData.RarityInfo[0].AdoptId),
            AdoptIdB = Hash.LoadFromHex(rankData.RarityInfo[1].AdoptId),
            Level = level + 1
        };
        _logger.LogInformation(
            "combine param, address:{address}, adoptIdA:{adoptIdA}, adoptIdB:{adoptIdB}, level:{level}", currentAddress,
            rankData.RarityInfo[0].AdoptId, rankData.RarityInfo[1].AdoptId, level);
        
        var dataHash = HashHelper.ComputeFrom(data);
        var signature = await _secretProvider.GetSignatureFromHashAsync(_chainOptions.PublicKey, dataHash);
        
        return new CombineOutput
        {
            Tick = "SGR",
            AdoptIds = rankData.RarityInfo.Select(x => x.AdoptId).ToList(),
            Level = level + 1,
            Signature = ByteStringHelper.FromHexString(signature)
        };
    }

    public async Task<PoolOutput> GetPoolAsync()
    {
        var poolId = _poolOptionsMonitor.CurrentValue.PoolId;
        var poolData = await _schrodingerCatProvider.GetPoolDataAsync(poolId);
        if (poolData == null)
        {
            _logger.LogInformation("GetPoolAsync Error, no pool data");
            throw new UserFriendlyException("No pool data");
        }
        
        var key = "sgr-prize";
        var cache = await _distributedCache.GetAsync(key);
        double sgrPrice;
        if (cache == null)
        {
            var elfPrice = await _levelProvider.GetAwakenELFPrice();
            sgrPrice = await _levelProvider.GetAwakenSGRPrice() * elfPrice;
            await _distributedCache.SetAsync(key, sgrPrice.ToString(), new DistributedCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.UtcNow.AddMinutes(10)
            });

            _logger.LogInformation("get sgr price from api， prize: {prize}", sgrPrice);
        }
        else
        {
            sgrPrice = double.Parse(cache);
        }
        
        var res = new PoolOutput
        {
            Prize = poolData.Balance,
            UsdtValue = (long) (poolData.Balance * sgrPrice)
        };
        _logger.LogInformation("GetPoolAsync Balance {balance}, sgr price: {price}", poolData.Balance, sgrPrice);
        
        var now = DateTime.UtcNow.ToUtcSeconds();
        if (now >= _poolOptionsMonitor.CurrentValue.Deadline)
        {
            res.Countdown = 0;
            return res;
        }

        res.Countdown = _poolOptionsMonitor.CurrentValue.Deadline - now;
        return res;
    }

    public async Task<GetPoolWinnerOutput> GetPoolWinnerAsync()
    {
        var poolId = _poolOptionsMonitor.CurrentValue.PoolId;
        var poolData = await _schrodingerCatProvider.GetPoolDataAsync(poolId);
        if (poolData == null)
        {
            _logger.LogInformation("GetPoolAsync Error, no pool data");
            throw new UserFriendlyException("No pool data");
        }
        
        var res = new GetPoolWinnerOutput();
        if (!poolData.WinnerAddress.IsNullOrEmpty())
        {
            var rankData = await _levelProvider.GetRarityInfo(poolData.WinnerAddress, poolData.WinnerRank, true);
            var rarity = rankData.LevelInfo.Describe.Split(",").ToList()[0];
            res.WinnerImage = BoxHelper.GetBoxImage(true, rarity);
            res.WinnerAddress = poolData.WinnerAddress;
            res.WinnerSymbol = poolData.WinnerSymbol;
            res.WinnerDescribe = rankData.LevelInfo.Describe;
            res.IsOver = true;
            return res;
        }
        
        var now = DateTime.UtcNow.ToUtcSeconds();
        if (now >= _poolOptionsMonitor.CurrentValue.Deadline)
        {
            res.IsOver = true;
            return res;
        }
        
        var adoptRecords = await _schrodingerCatProvider.GetLatestRareAdoptionAsync(50, _poolOptionsMonitor.CurrentValue.BeginTs);
        var winningList = adoptRecords.Take(_poolOptionsMonitor.CurrentValue.RankNumber).ToList();
        
        var rankList = new List<PoolRankItem>();
        foreach (var item in winningList)
        {
            var rankData = await _levelProvider.GetRarityInfo(item.Adopter, item.Rank, true);
            var rarity = rankData.LevelInfo.Describe.Split(",").ToList()[0];
            
            rankList.Add(new PoolRankItem
            {
                Address = item.Adopter,
                Symbol = item.Symbol,
                Describe = rankData.LevelInfo.Describe,
                Image = BoxHelper.GetBoxImage(true, rarity)
            });
        }
        
        res.RankList = rankList;

        return res;
    }

    public async Task<RedeemOutput> RedeemAsync()
    {
        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();
        // var currentAddress = input.Address;
        if (currentAddress.IsNullOrEmpty())
        {
            _logger.LogError("CombineAsync Get current address failed");
            throw new UserFriendlyException("Invalid user");
        }
        _logger.LogDebug("RedeemAsync for address, address:{address}", currentAddress);
        
        var poolId = _poolOptionsMonitor.CurrentValue.PoolId;
        var poolData = await _schrodingerCatProvider.GetPoolDataAsync(poolId);
        if (poolData == null)
        {
            _logger.LogInformation("GetPoolAsync Error, no pool data");
            throw new UserFriendlyException("No pool data");
        }

        if (poolData.WinnerSymbol.IsNullOrEmpty())
        {
            _logger.LogInformation("GetPoolAsync Error, no winner");
            throw new UserFriendlyException("No winner");
        }
        
        var rankData = await _schrodingerCatProvider.GetRankDataAsync(
            new List<string> { poolData.WinnerSymbol });
        
        var rarityInfo = rankData.RarityInfo.First();
        var level = poolData.WinnerLevel;
        var data = new RedeemInput()
        {
            Tick = "SGR",
            AdoptId = Hash.LoadFromHex(rarityInfo.AdoptId),
            Level = level + 1
        };
        
        _logger.LogInformation(
            "redeem param, address:{address}, adoptId:{adoptId}, level:{level}", currentAddress,
            rarityInfo.AdoptId, level);
        
        var dataHash = HashHelper.ComputeFrom(data);
        var signature = await _secretProvider.GetSignatureFromHashAsync(_chainOptions.PublicKey, dataHash);
        
        return new RedeemOutput
        {
            Tick = "SGR",
            AdoptId = rarityInfo.AdoptId,
            Level = level + 1,
            Signature = ByteStringHelper.FromHexString(signature)
        };
    }
}