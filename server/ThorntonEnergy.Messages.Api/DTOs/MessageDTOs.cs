using System.ComponentModel.DataAnnotations;
using ThorntonEnergy.Messages.Api.Models;

namespace ThorntonEnergy.Messages.Api.Dtos;

// POST body from the public contact form. Status and SentAt are decided by the server, not the browser.
public record CreateMessageRequest(
    [Required, StringLength(100)] string Name,
    [Required, EmailAddress, StringLength(200)] string Email,
    MessageTopic Topic,
    [StringLength(20)] string? AccountNumber,
    [Required, StringLength(2000)] string Message);

// PATCH body: only the field Angular actually changes
public record MessagePatch(MessageStatus Status);

// What goes out (ContactMessage in Angular)
public record ContactMessageDto(int Id, string Name, string Email, MessageTopic Topic, string? AccountNumber,
    string Message, DateTime SentAt, MessageStatus Status);
