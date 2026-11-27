using Microsoft.AspNetCore.Mvc;
using Payments.Api.Authorization;

namespace Payments.Api.Controllers;

[ApiController]
[Route("payments")]
public sealed class PaymentsController : ControllerBase
{
    [HttpGet]
    [RequirePermission("payments:read")]
    public IActionResult List() => Ok(Array.Empty<object>());

    [HttpPost("process")]
    [RequirePermission("payments:process")]
    public IActionResult Process() => Ok(new { message = "Payment processing demonstration." });

    [HttpPost("refund")]
    [RequirePermission("payments:refund")]
    public IActionResult Refund() => Ok(new { message = "Payment refund demonstration." });
}
