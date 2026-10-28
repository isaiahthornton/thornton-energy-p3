using AutoMapper;
using ThorntonEnergy.Billing.Api.Dtos;
using ThorntonEnergy.Billing.Api.Models;

namespace ThorntonEnergy.Billing.Api.Mapping;

// AutoMapper copies matching property names, so we only list the pairs
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Bill, BillDto>();             // entity -> DTO (what goes out)
        CreateMap<CreateBillRequest, Bill>();   // request -> entity (totals, due date and status are set by the service)
    }
}
