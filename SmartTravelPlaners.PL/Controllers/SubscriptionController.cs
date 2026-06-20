using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public SubscriptionController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        /// <summary>Returns all available subscription plans.</summary>
        [HttpGet("plans")]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _subscriptionService.GetPlansAsync();
            return Ok(plans);
        }

        /// <summary>Returns the current user's subscription details + usage.</summary>
        [HttpGet("my-subscription")]
        public async Task<IActionResult> GetMySubscription()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var subscription = await _subscriptionService.GetMySubscriptionAsync(userId);
            return Ok(subscription);
        }

        /// <summary>
        /// Subscribe to a plan. For free plans, activates immediately.
        /// For paid plans, returns a Paymob iframe URL for payment.
        /// </summary>
        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] CreateSubscriptionRequestDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var iframeUrl = await _subscriptionService.CreateSubscriptionAsync(userId, dto.PlanId);

            return Ok(new { iframeUrl });
        }

        /// <summary>Cancels the current user's active subscription.</summary>
        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            await _subscriptionService.CancelSubscriptionAsync(userId);
            return Ok(new { message = "Subscription cancelled successfully." });
        }
    }
}
