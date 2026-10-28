using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Api.Data;
using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Shared.Common;
using ThorntonEnergy.Shared.Data;

namespace ThorntonEnergy.Api.Services;

public interface IMessageService
{
    Task<Result<List<ContactMessageDto>>> GetAllAsync();
    Task<Result<ContactMessageDto>> GetByIdAsync(int id);
    Task<Result<ContactMessageDto>> CreateAsync(CreateMessageRequest request);
    Task<Result<ContactMessageDto>> UpdateAsync(int id, MessagePatch patch);
}

public class MessageService(
    IRepository<ContactMessage> messages,
    IUtilityClock clock,
    IMapper mapper,
    ILogger<MessageService> logger) : IMessageService
{
    // Newest first
    public async Task<Result<List<ContactMessageDto>>> GetAllAsync()
    {
        var list = await messages.Query().AsNoTracking().OrderByDescending(m => m.SentAt).ToListAsync();
        return Result<List<ContactMessageDto>>.Success(mapper.Map<List<ContactMessageDto>>(list));
    }

    public async Task<Result<ContactMessageDto>> GetByIdAsync(int id)
    {
        var message = await messages.GetByIdAsync(id);
        return message is null
            ? Result<ContactMessageDto>.NotFound()
            : Result<ContactMessageDto>.Success(mapper.Map<ContactMessageDto>(message));
    }

    public async Task<Result<ContactMessageDto>> CreateAsync(CreateMessageRequest request)
    {
        var message = mapper.Map<ContactMessage>(request);
        message.SentAt = clock.Now;           // the server decides when it was sent
        message.Status = MessageStatus.New;   // new messages always start unread
        messages.Add(message);
        await messages.SaveChangesAsync();

        logger.LogInformation("Contact message {MessageId} received about {Topic}", message.Id, message.Topic);
        return Result<ContactMessageDto>.Success(mapper.Map<ContactMessageDto>(message));
    }

    public async Task<Result<ContactMessageDto>> UpdateAsync(int id, MessagePatch patch)
    {
        var message = await messages.GetByIdAsync(id);
        if (message is null) return Result<ContactMessageDto>.NotFound();

        message.Status = patch.Status;
        await messages.SaveChangesAsync();

        logger.LogInformation("Contact message {MessageId} marked {Status}", id, patch.Status);
        return Result<ContactMessageDto>.Success(mapper.Map<ContactMessageDto>(message));
    }
}
