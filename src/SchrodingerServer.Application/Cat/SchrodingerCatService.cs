using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SchrodingerServer.Cat.Provider;
using SchrodingerServer.Cat.Provider.Dtos;
using SchrodingerServer.Common;
using SchrodingerServer.Dto;
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.Options;
using SchrodingerServer.Users;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
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

    private static readonly List<string> GenOneTraitTypes = new() { "Background", "Clothes", "Breed" };

    public SchrodingerCatService(ISchrodingerCatProvider schrodingerCatProvider, ILevelProvider levelProvider,
        IObjectMapper objectMapper, ILogger<SchrodingerCatService> logger, IOptionsMonitor<LevelOptions> levelOptions,
        IUserInformationProvider userInformationProvider,IUserActionProvider userActionProvider)
    {
        _schrodingerCatProvider = schrodingerCatProvider;
        _levelProvider = levelProvider;
        _objectMapper = objectMapper;
        _logger = logger;
        _levelOptions = levelOptions;
        _userInformationProvider = userInformationProvider;
        _userActionProvider = userActionProvider;
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
            var data = await SetLevelInfoAsync(indexerList, input.Address, input.ChainId);
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
            var data = await SetLevelInfoAsync(indexerList, input.Address, input.ChainId);
            result.Data = data;
            result.TotalCount = schrodingerIndexerListDto.TotalCount;
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

    private async Task<SchrodingerListDto> GetSchrodingerCatPageList(GetCatListInput input)
    {
        var result = new SchrodingerListDto();
        input.FilterSgr = true;
        var schrodingerIndexerListDto = await _schrodingerCatProvider.GetSchrodingerCatListAsync(input);
        var data = await SetLevelInfoAsync(schrodingerIndexerListDto.Data, input.Address, input.ChainId, input.SearchAddress);
        if (input.Rarities.IsNullOrEmpty())
        {
            result.Data = data;
            result.TotalCount = schrodingerIndexerListDto.TotalCount;
            return result;
        }

        var pageData = data
            .Where(cat => input.Rarities.Contains(cat.Rarity))
            .OrderByDescending(cat => cat.AdoptTime)
            .ToList();
        result.Data = pageData;
        result.TotalCount = schrodingerIndexerListDto.TotalCount;
        return result;
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
        
        result.Data = list;
        result.TotalCount = schrodingerIndexerListDto.TotalCount;
        return result;
    }
    
    private async Task<List<SchrodingerDto>> SetLevelInfoAsync(List<SchrodingerIndexerDto> indexerList, string address,
        string chainId, string searchAddress = "")
    {
        var list = _objectMapper.Map<List<SchrodingerIndexerDto>, List<SchrodingerDto>>(indexerList);

        var genNineList = list.Where(cat => cat.Generation == 9).ToList();
        var genOtherList = list.Where(cat => cat.Generation != 9).ToList();

        var traitInfos = genNineList.Select(cat => cat.Traits).ToList();

        var getLevelInfoInputDto = BuildParams(traitInfos, address, chainId, searchAddress);

        var itemLevelList = await GetItemLevelInfoAsync(getLevelInfoInputDto);
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

    private async Task<List<RankData>> GetItemLevelInfoAsync(GetLevelInfoInputDto input)
    {
        var result = new List<RankData>();

        var batchSize = _levelOptions.CurrentValue.BatchQuerySize;
        var totalItems = input.CatsTraits.Count;

        for (var i = 0; i < totalItems; i += batchSize)
        {
            var remainingItems = totalItems - i;
            var batchItems = input.CatsTraits.Skip(i).Take(Math.Min(batchSize, remainingItems));
            var batchInput = new GetLevelInfoInputDto
            {
                Address = input.Address,
                SearchAddress = input.SearchAddress,
                CatsTraits = new LinkedList<LinkedList<LinkedList<LinkedList<string>>>>(batchItems)
            };

            var batchResult = await _levelProvider.GetItemLevelAsync(batchInput);
            result.AddRange(batchResult);
        }
        return result;
    }

    private static GetLevelInfoInputDto BuildParams(List<List<TraitsInfo>> traitInfosList, string address,
        string chainId, string searchAddress = "")
    {
        var catsTraits = new LinkedList<LinkedList<LinkedList<LinkedList<string>>>>();
        foreach (var traitsInfos in traitInfosList)
        {
            var catTraits = new LinkedList<LinkedList<LinkedList<string>>>();
            var genOneTraits = new LinkedList<LinkedList<string>>();
            var genTwoToNineTraits = new LinkedList<LinkedList<string>>();
            var genOneTraitType = new LinkedList<string>();
            var genOneTraitValue = new LinkedList<string>();

            var genTwoToNineTraitType = new LinkedList<string>();
            var genTwoToNineTraitValue = new LinkedList<string>();
            foreach (var traitsInfo in traitsInfos)
            {
                if (GenOneTraitTypes.Contains(traitsInfo.TraitType))
                {
                    genOneTraitType.AddLast(traitsInfo.TraitType);
                    genOneTraitValue.AddLast(traitsInfo.Value);
                }
                else
                {
                    var traitType = traitsInfo.TraitType;
                    var traitValue = traitsInfo.Value;
                        
                    if (traitType == "Face" && traitValue == "WUKONG Face Paint")
                    {
                        traitValue = "Monkey King Face Paint";
                    }
                    
                    genTwoToNineTraitType.AddLast(traitType);
                    genTwoToNineTraitValue.AddLast(traitValue);
                }
            }

            genOneTraits.AddLast(genOneTraitType);
            genOneTraits.AddLast(genOneTraitValue);

            genTwoToNineTraits.AddLast(genTwoToNineTraitType);
            genTwoToNineTraits.AddLast(genTwoToNineTraitValue);

            catTraits.AddLast(genOneTraits);
            catTraits.AddLast(genTwoToNineTraits);
            catsTraits.AddLast(catTraits);
        }

        return new GetLevelInfoInputDto
        {
            Address = !string.IsNullOrEmpty(address) ? "ELF_" + address + "_" + chainId : "",
            CatsTraits = catsTraits,
            SearchAddress = !string.IsNullOrEmpty(searchAddress) ? "ELF_" + searchAddress + "_" + chainId : ""
        };
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
        list.Data.ForEach(i =>
        {
            if (nftInfoDict.TryGetValue(i.Symbol, out var value))
            {
                i.ForestPrice = value.ListingPrice;
            }
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
        list.Data.ForEach(i =>
        {
            if (nftInfoDict.TryGetValue(i.Symbol, out var value))
            {
                i.ForestPrice = value.ListingPrice;
            }
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

        var data = schrodingerIndexerBoxListDto.Data;
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
        
        boxList.ForEach(x =>
        {
            if (x.Rarity.NotNullOrEmpty())
            {
                x.InscriptionImageUri = BoxImageConst.RareBox;
                x.Describe = x.Rarity + ",,";
            }
            else if (x.Generation == 9)
            {
                x.InscriptionImageUri = BoxImageConst.NormalBox;
                x.Describe = "Common,,";
            }
            else
            {
                x.InscriptionImageUri = BoxImageConst.NonGen9Box;
            }
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
        if (resp.Rarity.NotNullOrEmpty())
        {
            resp.InscriptionImageUri = BoxImageConst.RareBox;
        }
        else if (resp.Generation == 9)
        {
            resp.InscriptionImageUri = BoxImageConst.NormalBox;
        }
        else
        {
            resp.InscriptionImageUri = BoxImageConst.NonGen9Box;
        }

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
        var boxDetail = await _schrodingerCatProvider.GetStrayCatsListAsync(input);
        
        var resp = _objectMapper.Map<SchrodingerIndexerStrayCatsDto, StrayCatsListDto>(boxDetail);
        return resp;
    }
}