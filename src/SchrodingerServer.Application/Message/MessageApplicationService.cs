using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SchrodingerServer.Cat.Provider;
using SchrodingerServer.Common;
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.Message.Dtos;
using SchrodingerServer.Message.Provider;
using SchrodingerServer.Message.Provider.Dto;
using SchrodingerServer.Options;
using SchrodingerServer.Users;
using SchrodingerServer.Users.Index;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Auditing;
using Volo.Abp.ObjectMapping;


namespace SchrodingerServer.Message;

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class MessageApplicationService :  ApplicationService, IMessageApplicationService
{
    private readonly IMessageProvider _messageProvider;
    private readonly IUserActionProvider _userActionProvider;
    private readonly ILevelProvider _levelProvider;
    private readonly ISchrodingerCatProvider _schrodingerCatProvider;
    private readonly IOptionsMonitor<LevelOptions> _levelOptions;
    private readonly ILogger<MessageApplicationService> _logger;
    private readonly IObjectMapper _objectMapper;
    private const char NFTSymbolSeparator = '-';
    
    public MessageApplicationService(
        IMessageProvider messageProvider,
        IUserActionProvider userActionProvider,
        ILevelProvider levelProvider, 
        ISchrodingerCatProvider schrodingerCatProvider,
        IOptionsMonitor<LevelOptions> levelOptions,
        ILogger<MessageApplicationService> logger, 
        IObjectMapper objectMapper)
    {
        _messageProvider = messageProvider;
        _userActionProvider = userActionProvider;
        _levelProvider = levelProvider;
        _schrodingerCatProvider = schrodingerCatProvider;
        _levelOptions = levelOptions;
        _logger = logger;
        _objectMapper = objectMapper;
    }
    
    
    public async Task<UnreadMessageCountDto> GetUnreadCountAsync(GetUnreadMessageCountInput input)
    {
        _logger.LogDebug("GetUnreadCountAsync, input: {address}", JsonConvert.SerializeObject(input));
        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();
        if (currentAddress.IsNullOrEmpty())
        {
            currentAddress = input.Address;
        }
        
        var chainId = _levelOptions.CurrentValue.ChainIdForReal;
        
        currentAddress = FullAddressHelper.ToFullAddress(currentAddress, chainId);
        var getSoldListInput = new GetSchrodingerSoldListInput()
        {
            Address = currentAddress,
            MaxResultCount = 1000,
            SkipCount = 0,
            FilterSymbol = chainId == "tDVV" ? "SGR" : "SGRTEST",
            ChainId = chainId
        };
        
        var soldIdList = await  _messageProvider.GetAllSchrodingerSoldIdAsync(getSoldListInput);
        _logger.LogDebug("sold id list: {info}", JsonConvert.SerializeObject(soldIdList));
        
        var readIdList = await _messageProvider.GetAllReadMessagesAsync(currentAddress);
        _logger.LogDebug("read id list: {info}", JsonConvert.SerializeObject(readIdList));

        var unreadIds = soldIdList.Where(x => !readIdList.Contains(x)).ToList();
        
        return new UnreadMessageCountDto()
        {
            Count = unreadIds.Count
        };
    }

    public async Task<MessageListDto> GetMessageListAsync(GetMessageListInput input)
    {
        _logger.LogDebug("GetMessageListAsync, input: {address}", JsonConvert.SerializeObject(input));
        var currentAddress = await _userActionProvider.GetCurrentUserAddressAsync();
        _logger.LogDebug("GetMessageListAsync, GetCurrentUserAddressAsync, address: {address}", currentAddress);
        if (currentAddress.IsNullOrEmpty())
        {
            currentAddress = input.Address;
        }

        var chainId = _levelOptions.CurrentValue.ChainIdForReal;

        currentAddress = FullAddressHelper.ToFullAddress(currentAddress, chainId);
        _logger.LogDebug("GetMessageListAsync, full address, address: {address}", currentAddress);
      
        var response = new MessageListDto();
        var getSoldListInput = new GetSchrodingerSoldListInput()
        {
            Address = currentAddress,
            MaxResultCount = input.MaxResultCount,
            SkipCount = input.SkipCount,
            FilterSymbol = "SGR",
            ChainId = chainId
        };
        var schrodingerIndexerListDto = await _messageProvider.GetSchrodingerSoldListAsync(getSoldListInput);
        _logger.LogDebug("GetSchrodingerSoldList: {info}", JsonConvert.SerializeObject(schrodingerIndexerListDto));

        if (schrodingerIndexerListDto == null || schrodingerIndexerListDto.TotalRecordCount == 0)
        {
            return response;
        }

        var messageInfoList = new List<MessageInfo>();
        var price = await _levelProvider.GetAwakenSGRPrice();
        foreach (var soldDto in schrodingerIndexerListDto.Data)
        {
            _logger.LogDebug("sold info: {info}", JsonConvert.SerializeObject(soldDto));
            var messageInfo = _objectMapper.Map<NFTActivityIndexDto, MessageInfo>(soldDto);
            var symbol = RemovePrefix(soldDto.NftInfoId);
            var detail = await _schrodingerCatProvider.GetSchrodingerCatRankAsync(new GetCatRankInput()
            {
                ChainId = chainId,
                Symbol = symbol
            });

            if (detail == null)
            {
                _logger.LogError("query schrodinger detail failed, symbol:{symbol}", symbol);
                continue;
            }
            
            _logger.LogDebug("detail info: {info}", JsonConvert.SerializeObject(detail));
            
            messageInfo.TokenName = detail.TokenName;
            messageInfo.PreviewImage = detail.InscriptionImageUri;
            messageInfo.Generation = detail.Generation;
            messageInfo.Createtime = TimeHelper.GetTimeStampFromDateTime(soldDto.Timestamp);
            
            var isInWhiteList = await _levelProvider.CheckAddressIsInWhiteListAsync(currentAddress);
            if (!isInWhiteList)
            {
                _logger.LogInformation("not in whitelist: {address}", currentAddress);
                messageInfoList.Add(messageInfo);
                continue;
            }

            messageInfo.Rank = detail.Rank;
            messageInfo.Level = detail.Level;
            messageInfo.Rarity = detail.Rarity;
            messageInfo.Grade = detail.Grade;
            messageInfo.Star = detail.Star;
            
            if (price == 0.0)
            {
                _logger.LogError("query awaken price failed");
                messageInfoList.Add(messageInfo);
                continue;
            }

            if (detail.Level.IsNullOrEmpty())
            {
                _logger.LogInformation("not rare cat: {detail}", JsonConvert.SerializeObject(detail));
                messageInfoList.Add(messageInfo);
                continue;
            }
            
            var levelInfoDto = await _levelProvider.GetItemLevelDicAsync(detail.Rank, price);
            
            messageInfo.AwakenPrice = levelInfoDto?.AwakenPrice;
            messageInfo.Level = levelInfoDto?.Level;
            messageInfo.Describe = levelInfoDto?.Describe;
            
            messageInfoList.Add(messageInfo);
            _logger.LogDebug("message info: {info}", JsonConvert.SerializeObject(messageInfo));
        }

        await MarkReadAsync(schrodingerIndexerListDto.Data, currentAddress);
        response.Data = messageInfoList;
        response.TotalCount = schrodingerIndexerListDto.TotalRecordCount;
        return response;
    }
    
    private static string RemovePrefix(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        int index = input.IndexOf(NFTSymbolSeparator);
        if (index >= 0)
        {
            return input.Substring(index + 1);
        }
       
        return input;
    }

    private async Task MarkReadAsync(List<NFTActivityIndexDto> messageInfoList, string address)
    {
        var readMessageList = messageInfoList.Select(x => new ReadMessageIndex
        {
            Id = x.Id,
            MessageId = x.Id,
            CreateTime = DateTime.Now,
            Address = address
        }).ToList();    
        
        await  _messageProvider.MarkMessageReadAsync(readMessageList);
    }
}