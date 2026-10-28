using AutoMapper;
using ThorntonEnergy.Outages.Api.Dtos;
using ThorntonEnergy.Outages.Api.Models;

namespace ThorntonEnergy.Outages.Api.Mapping;

// AutoMapper copies matching property names, so we only list the pairs
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Outage, OutageDto>();               // entity -> DTO (what goes out)
        CreateMap<CreateOutageRequest, Outage>();     // request -> entity (customer, status and times are set by the service)
    }
}
