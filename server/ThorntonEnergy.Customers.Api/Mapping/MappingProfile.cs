using AutoMapper;
using ThorntonEnergy.Customers.Api.Dtos;
using ThorntonEnergy.Customers.Api.Models;

namespace ThorntonEnergy.Customers.Api.Mapping;

// AutoMapper copies matching property names, so we only list the pairs
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // entity -> DTO (what goes out)
        CreateMap<Customer, CustomerDto>();
        CreateMap<RatePlan, RatePlanDto>();

        // request -> entity (what comes in)
        CreateMap<RatePlanRequest, RatePlan>();
    }
}
