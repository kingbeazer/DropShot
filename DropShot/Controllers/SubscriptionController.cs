using DropShot.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace DropShot.Controllers;

[ApiController]
[Route("api/subscription")]
public class SubscriptionController(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration,
    ILogger<SubscriptionController> logger) : ControllerBase
{
    // ── Activate ────────────────────────────────────────────────────────────────
    [HttpPost("activate")]
    [Authorize]
    public async Task<IActionResult> Activate([FromBody] ActivateRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();

        // Verify the subscription with PayPal
        var sub = await GetPayPalSubscriptionAsync(request.SubscriptionId);
        if (sub is null)
            return BadRequest(new { message = "Could not verify PayPal subscription." });

        var status = sub.RootElement.GetProperty("status").GetString();
        if (status is not ("ACTIVE" or "APPROVED"))
            return BadRequest(new { message = $"Subscription status is '{status}', expected ACTIVE." });

        var planId = sub.RootElement.GetProperty("plan_id").GetString();
        var expectedPlanId = configuration["PayPal:PlanId"];
        if (planId != expectedPlanId)
            return BadRequest(new { message = "Subscription plan does not match." });

        user.IsSubscribed = true;
        user.SubscriptionTier = "Premium";
        user.SubscriptionStartDate = DateTime.UtcNow;
        user.SubscriptionEndDate = null; // ongoing until cancelled
        user.PaypalSubscriptionId = request.SubscriptionId;
        user.PaypalPayerId = request.PayerId;

        await userManager.UpdateAsync(user);

        logger.LogInformation("Subscription activated for user {UserId}, PayPal sub {SubId}",
            userId, request.SubscriptionId);

        return Ok(new { message = "Subscription activated successfully." });
    }

    // ── Webhook ─────────────────────────────────────────────────────────────────
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook()
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        JsonDocument payload;
        try { payload = JsonDocument.Parse(body); }
        catch { return BadRequest(); }

        var eventType = payload.RootElement.GetProperty("event_type").GetString();
        logger.LogInformation("PayPal webhook received: {EventType}", eventType);

        var resource = payload.RootElement.GetProperty("resource");
        var subId = resource.GetProperty("id").GetString();

        if (string.IsNullOrEmpty(subId)) return Ok();

        var user = userManager.Users.FirstOrDefault(u => u.PaypalSubscriptionId == subId);
        if (user is null)
        {
            logger.LogWarning("Webhook for unknown subscription {SubId}", subId);
            return Ok();
        }

        switch (eventType)
        {
            case "BILLING.SUBSCRIPTION.CANCELLED":
            case "BILLING.SUBSCRIPTION.EXPIRED":
            case "BILLING.SUBSCRIPTION.SUSPENDED":
                user.IsSubscribed = false;
                user.SubscriptionEndDate = DateTime.UtcNow;
                await userManager.UpdateAsync(user);
                logger.LogInformation("Subscription {Event} for user {UserId}", eventType, user.Id);
                break;

            case "BILLING.SUBSCRIPTION.ACTIVATED":
            case "BILLING.SUBSCRIPTION.RE-ACTIVATED":
                user.IsSubscribed = true;
                user.SubscriptionEndDate = null;
                await userManager.UpdateAsync(user);
                logger.LogInformation("Subscription reactivated for user {UserId}", user.Id);
                break;
        }

        return Ok();
    }

    // ── Status ──────────────────────────────────────────────────────────────────
    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> Status()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();

        return Ok(new
        {
            user.IsSubscribed,
            user.SubscriptionTier,
            user.SubscriptionStartDate,
            user.SubscriptionEndDate,
            user.PaypalSubscriptionId
        });
    }

    // ── PayPal API helpers ──────────────────────────────────────────────────────
    private async Task<JsonDocument?> GetPayPalSubscriptionAsync(string subscriptionId)
    {
        var token = await GetPayPalAccessTokenAsync();
        if (token is null) return null;

        var baseUrl = configuration["PayPal:Mode"] == "live"
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"{baseUrl}/v1/billing/subscriptions/{subscriptionId}");
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    private async Task<string?> GetPayPalAccessTokenAsync()
    {
        var clientId = configuration["PayPal:ClientId"];
        var secret = configuration["PayPal:ClientSecret"];
        var baseUrl = configuration["PayPal:Mode"] == "live"
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";

        using var client = new HttpClient();
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client.PostAsync($"{baseUrl}/v1/oauth2/token", content);

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString();
    }

    public record ActivateRequest(string SubscriptionId, string? PayerId);
}
