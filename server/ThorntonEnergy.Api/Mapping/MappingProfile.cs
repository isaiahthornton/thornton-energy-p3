using AutoMapper;
using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Models;

namespace ThorntonEnergy.Api.Mapping;

// AutoMapper copies matching property names, so we only list the pairs (and the few fields it must skip)
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // entity -> DTO (what goes out)
        CreateMap<Customer, CustomerDto>();
        CreateMap<RatePlan, RatePlanDto>();

        // request -> entity (what comes in). Server-controlled fields are filled by the services, not by the client.
        CreateMap<RatePlanRequest, RatePlan>();
    }
}
