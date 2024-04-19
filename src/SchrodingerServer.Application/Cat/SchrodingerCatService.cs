using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        
        return await GetSchrodingerAllCatsPageList(input);
    }

    public async Task<SchrodingerDetailDto> GetSchrodingerCatDeailAsync(GetCatDetailInput input)
    {
        var detail = new SchrodingerDetailDto();
        var address = await _userActionProvider.GetCurrentUserAddressAsync();
        if (!address.IsNullOrEmpty())
        {
            input.Address = address;
        }
        //query symbolIndex
        var querySymbolInput = new GetCatListInput
        {
            ChainId = input.ChainId,
            Keyword = input.Symbol,
            SkipCount = 0,
            MaxResultCount = 1
        };
        var symbolIndexerListDto =  await GetSchrodingerAllCatsPageList(querySymbolInput);

        if (symbolIndexerListDto == null && symbolIndexerListDto.TotalCount == 0)
        {
            return new SchrodingerDetailDto();
        }
        var amount = symbolIndexerListDto.Data[0].Amount;
   
        if (address.IsNullOrEmpty())
        {
            detail = _objectMapper.Map<SchrodingerDto, SchrodingerDetailDto>(symbolIndexerListDto.Data[0]);
            return detail;
        }

        var holderDetail = await _schrodingerCatProvider.GetSchrodingerCatDetailAsync(input);
        if (holderDetail == null)
        {
            detail.Amount = amount;
            detail.HolderAmount = 0;
            return detail;
        }

        detail = holderDetail;

        //query total amount
        detail.HolderAmount = detail.Amount;
        detail.Amount = amount;
        return detail;
    }

    private async Task<SchrodingerListDto> GetSchrodingerCatPageList(GetCatListInput input)
    {
        var result = new SchrodingerListDto();
        input.FilterSgr = true;
        var schrodingerIndexerListDto = await _schrodingerCatProvider.GetSchrodingerCatListAsync(input);
        var data = await SetLevelInfoAsync(schrodingerIndexerListDto.Data, input.Address, input.ChainId, input.SearchAddress);
        result.Data = data;
        result.TotalCount = schrodingerIndexerListDto.TotalCount;
        return result;
    }
    private async Task<SchrodingerListDto> GetSchrodingerAllCatsPageList(GetCatListInput input)
    {
        var result = new SchrodingerListDto();
        input.FilterSgr = true;
        var schrodingerIndexerListDto = await _schrodingerCatProvider.GetSchrodingerAllCatsListAsync(input);
        var list = _objectMapper.Map<List<SchrodingerSymbolIndexerDto>, List<SchrodingerDto>>(schrodingerIndexerListDto.Data);
        //get awaken price
        var price = await _levelProvider.GetAwakenSGRPrice();
        foreach (var schrodingerDto in list.Where(schrodingerDto => schrodingerDto.Generation == 9))
        {
            //get levelInfo
            var levelInfoDto = await _levelProvider.GetItemLevelDicAsync(schrodingerDto.Rank, price);
            schrodingerDto.AwakenPrice = levelInfoDto?.AwakenPrice;
            schrodingerDto.Level = levelInfoDto?.Level;
            schrodingerDto.Token = levelInfoDto?.Token;
            schrodingerDto.Total = levelInfoDto?.Token;
            schrodingerDto.Describe = levelInfoDto?.Describe;
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
            _logger.LogWarning("get item level count not equals");
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
                    genTwoToNineTraitType.AddLast(traitsInfo.TraitType);
                    genTwoToNineTraitValue.AddLast(traitsInfo.Value);
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
}