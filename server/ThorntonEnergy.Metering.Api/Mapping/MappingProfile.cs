using AutoMapper;
using ThorntonEnergy.Metering.Api.Dtos;
using ThorntonEnergy.Metering.Api.Models;

namespace ThorntonEnergy.Metering.Api.Mapping;

// AutoMapper copies matching property names, so we only list the pairs
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // entity -> DTO (what goes out)
        CreateMap<Meter, MeterDto>();
        CreateMap<MeterReading, MeterReadingDto>();

        // request -> entity (what comes in)
        CreateMap<CreateMeterReadingRequest, MeterReading>();
    }
}
