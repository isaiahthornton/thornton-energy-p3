using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ThorntonEnergy.Messages.Api.Dtos;
using ThorntonEnergy.Messages.Api.Services;
using ThorntonEnergy.Shared.Auth;
using ThorntonEnergy.Shared.Controllers;

namespace ThorntonEnergy.Messages.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin)] // the inbox is admin-only...
public class MessagesController(IMessageService messages) : AppControllerBase
{
    // GET api/messages (newest first)
    [HttpGet]
    public async Task<ActionResult<List<ContactMessageDto>>> GetAll() =>
        FromResult(await messages.GetAllAsync());

    // GET api/messages/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ContactMessageDto>> GetById(int id) =>
        FromResult(await messages.GetByIdAsync(id));

    // POST api/messages (...but anyone can send one from the contact form)
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicForms)] // slows down spam
    [HttpPost]
    public async Task<ActionResult<ContactMessageDto>> Create(CreateMessageRequest request)
    {
        var result = await messages.CreateAsync(request);
        if (!result.IsSuccess) return Failure(result);
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    // PATCH api/messages/5   body: { "status": "read" }
    [HttpPatch("{id:int}")]
    public async Task<ActionResult<ContactMessageDto>> Update(int id, MessagePatch patch) =>
        FromResult(await messages.UpdateAsync(id, patch));
}
