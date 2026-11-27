using Accounts.Bll;
using Accounts.Bll.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace Accounts.Api.Controllers;

[ApiController]
[Route("api/donations")]
public sealed class DonationsController(IAccountsService accountsService) : ControllerBase
{
    [HttpGet("{donationId:long}")]
    [ProducesResponseType<DonationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DonationResponse>> Get(long donationId, CancellationToken cancellationToken)
    {
        var donation = await accountsService.GetDonationAsync(donationId, cancellationToken);
        return donation is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Resource not found", detail: $"Donation {donationId} was not found.")
            : Ok(donation);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateDonationRequest request, CancellationToken cancellationToken)
    {
        var donationId = await accountsService.CreateDonationAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { donationId }, new { donationId });
    }

    [HttpPut("{donationId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(long donationId, UpdateDonationRequest request, CancellationToken cancellationToken)
    {
        await accountsService.UpdateDonationAsync(donationId, request, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{donationId:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long donationId, CancellationToken cancellationToken)
    {
        await accountsService.DeleteDonationAsync(donationId, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{donationId:long}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeStatus(long donationId, ChangeDonationStatusRequest request, CancellationToken cancellationToken)
    {
        await accountsService.ChangeDonationStatusAsync(donationId, request, cancellationToken);
        return NoContent();
    }
}
