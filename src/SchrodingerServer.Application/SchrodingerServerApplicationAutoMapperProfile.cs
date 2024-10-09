using System.Linq;
using AutoMapper;
using Microsoft.IdentityModel.Tokens;
using SchrodingerServer.AddressRelationship.Dto;
using SchrodingerServer.Cat.Provider.Dtos;
using SchrodingerServer.Common.Options;
using SchrodingerServer.ContractInvoke.Eto;
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.Dtos.Faucets;
using SchrodingerServer.Dtos.TraitsDto;
using SchrodingerServer.Dtos.Uniswap;
using SchrodingerServer.Grains.Grain.ContractInvoke;
using SchrodingerServer.Grains.Grain.Faucets;
using SchrodingerServer.Grains.Grain.Points;
using SchrodingerServer.Grains.Grain.ZealyScore.Dtos;
using SchrodingerServer.Grains.State.ZealyScore;
using SchrodingerServer.Message.Dtos;
using SchrodingerServer.Message.Provider.Dto;
using SchrodingerServer.ScoreRepair.Dtos;
using SchrodingerServer.Tasks.Dtos;
using SchrodingerServer.Uniswap.Index;
using SchrodingerServer.Users;
using SchrodingerServer.Users.Dto;
using SchrodingerServer.Users.Eto;
using SchrodingerServer.Users.Index;
using SchrodingerServer.Zealy;
using RankItem = SchrodingerServer.Dtos.Cat.RankItem;

namespace SchrodingerServer;

public class SchrodingerServerApplicationAutoMapperProfile : Profile
{
    public SchrodingerServerApplicationAutoMapperProfile()
    {
        CreateMap<UserSourceInput, UserGrainDto>().ReverseMap();
        CreateMap<UserGrainDto, UserInformationEto>().ReverseMap();
        CreateMap<FaucetsGrainDto, FaucetsTransferResultDto>();
        CreateMap<ContractInvokeGrainDto, ContractInvokeEto>().ReverseMap();

        CreateMap<UpdateXpScoreRepairDataDto, ZealyXpScoreIndex>()
            .ForMember(t => t.Id, m => m.MapFrom(f => f.UserId));

        CreateMap<ZealyXpScoreIndex, XpScoreRepairDataDto>()
            .ForMember(t => t.UserId, m => m.MapFrom(f => f.Id));
        
        CreateMap<PointDailyRecordGrainDto, PointDailyRecordEto>().ReverseMap();
        
        CreateMap<RecordInfo, RecordInfoDto>();
        CreateMap<ZealyUserXpGrainDto, UserXpInfoDto>();
        CreateMap<ZealyUserXpRecordIndex, XpRecordDto>();
        CreateMap<SchrodingerIndexerDto, SchrodingerDto>();
        CreateMap<SchrodingerSymbolIndexerDto, SchrodingerDto>();
        CreateMap<SchrodingerDto, SchrodingerDetailDto>()
            .ForMember(des => des.Traits, opt
                => opt.MapFrom(source => source.Traits.IsNullOrEmpty()?null:source.Traits.Select(item => new TraitDto { TraitType = item.TraitType, Value = item.Value }).ToList()));
        CreateMap<NFTActivityIndexDto, MessageInfo>();
        CreateMap<UniswapPositionSnapshotIndex, UniswapLiquidityDto>();
        CreateMap<ActivityInfo, ActivityDto>();
        CreateMap<RankItem, RankItemDto>();
        CreateMap<RarityRankItem, RarityRankItemDto>();
        CreateMap<SchrodingerIndexerBoxDto, BlindBoxDto>()
            .ForMember(t => t.Generation, m => m.MapFrom(f => f.Gen));
        CreateMap<SchrodingerIndexerBoxDto, BlindBoxDetailDto>()
            .ForMember(t => t.Generation, m => m.MapFrom(f => f.Gen))
            .ForMember(t => t.HolderAmount, m => m.MapFrom(f => f.Amount));
        CreateMap<SchrodingerIndexerStrayCatsDto, StrayCatsListDto>();
        CreateMap<TraitDto, TraitsInfo>();
        CreateMap<TasksIndex, TasksDto>();
        CreateMap<TaskConfig, TasksDto>();
        CreateMap<TasksDto, TaskData>();
        CreateMap<TasksDto, ClaimOutput>();
    }
}