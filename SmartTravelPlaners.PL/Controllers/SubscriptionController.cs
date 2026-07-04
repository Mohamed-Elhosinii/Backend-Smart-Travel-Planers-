using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Subscription.DTOs;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using System.Security.Claims;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(ISubscriptionService subscriptionService, ILogger<SubscriptionController> logger)
        {
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        /// <summary>Returns all available subscription plans.</summary>
        [HttpGet("plans")]
        public async Task<IActionResult> GetPlans()
        {
            try
            {
                _logger.LogInformation("Subscription plans retrieval requested");
                var plans = await _subscriptionService.GetPlansAsync();
                _logger.LogInformation("Subscription plans retrieved successfully. Count: {PlansCount}", plans.Count());
                return Ok(plans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Subscription plans retrieval failed. Error: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        /// <summary>Returns the current user's subscription details + usage.</summary>
        [HttpGet("my-subscription")]
        public async Task<IActionResult> GetMySubscription()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                _logger.LogWarning("Unauthorized subscription retrieval attempt");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("User subscription retrieval requested for UserId: {UserId}", userId);
                var subscription = await _subscriptionService.GetMySubscriptionAsync(userId);
                _logger.LogInformation("User subscription retrieved successfully for UserId: {UserId}. Plan: {PlanName}", userId, subscription.PlanName);
                return Ok(subscription);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User subscription retrieval failed for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Subscribe to a plan. For free plans, activates immediately.
        /// For paid plans, returns a Paymob iframe URL for payment.
        /// </summary>
        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] CreateSubscriptionRequestDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                _logger.LogWarning("Unauthorized subscription creation attempt");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Subscription creation initiated for UserId: {UserId}, PlanId: {PlanId}", userId, dto.PlanId);
                var iframeUrl = await _subscriptionService.CreateSubscriptionAsync(userId, dto.PlanId);
                _logger.LogInformation("Subscription created successfully for UserId: {UserId}, PlanId: {PlanId}", userId, dto.PlanId);
                return Ok(new { iframeUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Subscription creation failed for UserId: {UserId}, PlanId: {PlanId}. Error: {ErrorMessage}", userId, dto.PlanId, ex.Message);
                throw;
            }
        }

        /// <summary>Cancels the current user's active subscription.</summary>
        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                _logger.LogWarning("Unauthorized subscription cancellation attempt");
                return Unauthorized();
            }

            try
            {
                _logger.LogInformation("Subscription cancellation initiated for UserId: {UserId}", userId);
                await _subscriptionService.CancelSubscriptionAsync(userId);
                _logger.LogInformation("Subscription cancelled successfully for UserId: {UserId}", userId);
                return Ok(new { message = "Subscription cancelled successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Subscription cancellation failed for UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }
    }
}
