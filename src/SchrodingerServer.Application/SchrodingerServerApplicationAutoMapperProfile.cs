using AutoMapper;
using SchrodingerServer.Cat.Provider.Dtos;
using SchrodingerServer.ContractInvoke.Eto;
using SchrodingerServer.Dtos.Cat;
using SchrodingerServer.Dtos.Faucets;
using SchrodingerServer.Grains.Grain.ContractInvoke;
using SchrodingerServer.Grains.Grain.Faucets;
using SchrodingerServer.Grains.Grain.Points;
using SchrodingerServer.Grains.Grain.ZealyScore.Dtos;
using SchrodingerServer.Grains.State.ZealyScore;
using SchrodingerServer.ScoreRepair.Dtos;
using SchrodingerServer.Users;
using SchrodingerServer.Users.Dto;
using SchrodingerServer.Users.Eto;
using SchrodingerServer.Zealy;

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

    }
}