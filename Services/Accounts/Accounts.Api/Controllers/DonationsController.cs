using Accounts.Bll;
using Accounts.Bll.Contracts;
using Microsoft.AspNetCore.Mvc;
using Platform.ServiceDefaults;

namespace Accounts.Api.Controllers;

[ApiController]
[Route("api/donations")]
public sealed class DonationsController(IAccountsService accountsService, BusinessMetrics metrics) : ControllerBase
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
        using var operation = metrics.Start("donation.create");
        var donationId = await accountsService.CreateDonationAsync(request, cancellationToken);
        operation.Succeed();
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
        using var operation = metrics.Start("donation.status.change");
        await accountsService.ChangeDonationStatusAsync(donationId, request, cancellationToken);
        operation.Succeed();
        return NoContent();
    }
}
