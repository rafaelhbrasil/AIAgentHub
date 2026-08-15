using Microsoft.AspNetCore.Mvc;

namespace AIAgentHub.Web.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected NotFoundObjectResult NotFoundResponse(string code, string message) => NotFound(new { code, message });

    protected BadRequestObjectResult BadRequestResponse(string code, string message) => BadRequest(new { code, message });

    protected ObjectResult ErrorResponse(int statusCode, string code, string message) => StatusCode(statusCode, new { code, message });
}
