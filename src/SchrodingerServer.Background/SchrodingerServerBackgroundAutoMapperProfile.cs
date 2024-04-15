using AutoMapper;
using SchrodingerServer.ContractInvoke.Eto;
using SchrodingerServer.ContractInvoke.Index;
using SchrodingerServer.Grains.Grain.ContractInvoke;
using SchrodingerServer.Grains.Grain.ZealyScore.Dtos;
using SchrodingerServer.Zealy.Eto;

namespace SchrodingerServer.Background;

public class SchrodingerServerBackgroundAutoMapperProfile : Profile
{
    public SchrodingerServerBackgroundAutoMapperProfile()
    {
        CreateMap<ContractInvokeIndex, ContractInvokeEto>();
        CreateMap<ContractInvokeEto, ContractInvokeGrainDto>().ReverseMap();
        CreateMap<XpRecordGrainDto, XpRecordEto>().ReverseMap();
        CreateMap<XpRecordGrainDto, AddXpRecordEto>().ReverseMap();
    }
}