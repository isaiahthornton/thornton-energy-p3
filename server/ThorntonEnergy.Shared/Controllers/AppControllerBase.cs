using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ThorntonEnergy.Shared.Common;

namespace ThorntonEnergy.Shared.Controllers;

// Controllers only deal with HTTP. The business rules live in the services; this turns a service
// Result<T> into the matching HTTP response.
public abstract class AppControllerBase : ControllerBase
{
    // Success -> 200 with the value, otherwise the matching error response
    protected ActionResult<T> FromResult<T>(Result<T> result)
    {
        if (result.IsSuccess) return result.Value!;
        return Failure(result);
    }

    protected ActionResult Failure<T>(Result<T> result)
    {
        var body = new { message = result.Error };

        return result.Status switch
        {
            ResultStatus.NotFound => result.Error is null ? NotFound() : NotFound(body),
            ResultStatus.Unauthorized => Unauthorized(body),
            // No message -> plain Forbid(); with a message -> 403 and { message }
            ResultStatus.Forbidden => result.Error is null ? Forbid() : StatusCode(StatusCodes.Status403Forbidden, body),
            ResultStatus.Invalid => BadRequest(body),
            ResultStatus.Conflict => Conflict(body),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
