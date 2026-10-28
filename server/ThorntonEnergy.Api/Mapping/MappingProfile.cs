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
        CreateMap<Bill, BillDto>();
        CreateMap<Customer, CustomerDto>();
        CreateMap<Meter, MeterDto>();
        CreateMap<MeterReading, MeterReadingDto>();
        CreateMap<Outage, OutageDto>();
        CreateMap<RatePlan, RatePlanDto>();

        // request -> entity (what comes in). Server-controlled fields are filled by the services, not by the client.
        CreateMap<CreateBillRequest, Bill>();
        CreateMap<CreateMeterReadingRequest, MeterReading>();
        CreateMap<CreateOutageRequest, Outage>();
        CreateMap<RatePlanRequest, RatePlan>();
    }
}
