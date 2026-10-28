using AutoMapper;
using ThorntonEnergy.Messages.Api.Dtos;
using ThorntonEnergy.Messages.Api.Models;

namespace ThorntonEnergy.Messages.Api.Mapping;

// AutoMapper copies matching property names, so we only list the pairs
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<ContactMessage, ContactMessageDto>();   // entity -> DTO (what goes out)
        CreateMap<CreateMessageRequest, ContactMessage>(); // request -> entity (SentAt and Status are set by the service)
    }
}
