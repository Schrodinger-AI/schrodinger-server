using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using Microsoft.Extensions.Logging;
using SchrodingerServer.Common.GraphQL;
using SchrodingerServer.Dto;
using Volo.Abp.DependencyInjection;
using Attribute = SchrodingerServer.Dtos.Adopts.Attribute;

namespace SchrodingerServer.Adopts.provider;

public interface IAdoptGraphQLProvider
{
    Task<AdoptInfo> QueryAdoptInfoAsync(string adoptId);
    Task<List<AdpotInfoDto>> GetAdoptInfoByTime(long beginTime, long endTime);
    Task<List<string>> GetAdoptAddressByTime(long beginTime, long endTime);
}

public class AdoptGraphQLProvider : IAdoptGraphQLProvider, ISingletonDependency
{
    private readonly IGraphQlHelper _graphQlHelper;
    private readonly ILogger<AdoptGraphQLProvider> _logger;

    public AdoptGraphQLProvider(IGraphQlHelper graphQlHelper, ILogger<AdoptGraphQLProvider> logger)
    {
        _graphQlHelper = graphQlHelper;
        _logger = logger;
    }

    public async Task<AdoptInfo> QueryAdoptInfoAsync(string adoptId)
    {
        var adpotInfoDto = await _graphQlHelper.QueryAsync<AdoptInfoQuery>(new GraphQLRequest
        {
            Query =
                @"query($adoptId:String){
                    getAdoptInfo(input: {adoptId:$adoptId}){
                          symbol,
                          tokenName,
                          attributes{
                            traitType,
                            value,
                            percent,
                            isRare
                          }
                          adopter,
                          imageCount,
                          gen
                }
            }",
            Variables = new
            {
                adoptId = adoptId
            }
        });
        if (adpotInfoDto == null || adpotInfoDto.GetAdoptInfo == null)
        {
            _logger.LogError("query adopt info failed, adoptId = {AdoptId}", adoptId);
            return null;
        }

        return new AdoptInfo()
        {
            Symbol = adpotInfoDto.GetAdoptInfo.Symbol,
            TokenName = adpotInfoDto.GetAdoptInfo.TokenName,
            Attributes = adpotInfoDto.GetAdoptInfo.Attributes.Select(a => new Attribute()
            {
                TraitType = a.TraitType,
                Value = a.Value,
                Percent = a.Percent
            }).ToList(),
            Adopter = adpotInfoDto.GetAdoptInfo.Adopter,
            ImageCount = adpotInfoDto.GetAdoptInfo.ImageCount,
            Generation = adpotInfoDto.GetAdoptInfo.Gen
        };
    }
    
    
    public async Task<List<AdpotInfoDto>> GetAdoptInfoByTime(long beginTime, long endTime)
    {
        var adpotInfoDto = await _graphQlHelper.QueryAsync<AdoptInfoByTimeQuery>(new GraphQLRequest
        {
            Query =
                @"query($beginTime:Long!, 
                        $endTime:Long!
                ){
                       getAdoptInfoByTime(input: {
                          beginTime:$beginTime, 
                          endTime:$endTime})
                   {
                       inputAmount,
        		       outputAmount,
                       adoptId,
                       adopter,,
                       adoptTime
                   }
              }",
            Variables = new
            {
                beginTime = beginTime,
                endTime = endTime
            }
        });
        if (adpotInfoDto == null || adpotInfoDto.GetAdoptInfoByTime == null)
        {
            _logger.LogError("query adopt info by time failed");
            return null;
        }

        return adpotInfoDto.GetAdoptInfoByTime;
    }
    
    
    public async Task<List<string>> GetAdoptAddressByTime(long beginTime, long endTime)
    {
        var adpotInfoDto = await _graphQlHelper.QueryAsync<AdoptInfoByTimeQuery>(new GraphQLRequest
        {
            Query =
                @"query($beginTime:Long!, 
                        $endTime:Long!
                ){
                    getAdoptInfoByTime(input: {
                           beginTime:$beginTime, 
                           endTime:$endTime})
                   {
                       adopter
                   }
            }",
            Variables = new
            {
                beginTime = beginTime,
                endTime = endTime
            }
        });
        if (adpotInfoDto == null || adpotInfoDto.GetAdoptInfoByTime == null)
        {
            _logger.LogError("query adopt address by time failed");
            return null;
        }

        return adpotInfoDto.GetAdoptInfoByTime.Select(x => x.Adopter).Where(x => !x.IsNullOrEmpty()).ToList();
    }
}





