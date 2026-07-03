using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Admin.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : ControllerBase
    {
        private readonly IAdminDashboardService _adminService;
        private readonly ILogger<AdminUsersController> _logger;

        public AdminUsersController(IAdminDashboardService adminService, ILogger<AdminUsersController> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                _logger.LogInformation("Fetching users list: Page: {Page}, PageSize: {PageSize}, Search: {Search}", page, pageSize, search ?? "none");
                var (users, totalCount) = await _adminService.GetUsersListAsync(search, page, pageSize);
                _logger.LogInformation("Users list retrieved successfully: TotalCount: {TotalCount}, Page: {Page}", totalCount, page);
                return Ok(new { users, totalCount, page, pageSize });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users list. Page: {Page}, PageSize: {PageSize}. Error: {ErrorMessage}", page, pageSize, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{userId}/plan")]
        public async Task<IActionResult> ForceUpdatePlan(string userId, [FromBody] Guid planId)
        {
            try
            {
                _logger.LogInformation("Updating user subscription plan: {UserId}, PlanId: {PlanId}", userId, planId);
                await _adminService.UpdateUserSubscriptionPlanAsync(userId, planId);
                _logger.LogInformation("User subscription plan updated successfully: {UserId}, PlanId: {PlanId}", userId, planId);
                return Ok(new { message = "User plan updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user subscription plan for {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{userId}/toggle-status")]
        public async Task<IActionResult> ToggleUserStatus(string userId)
        {
            try
            {
                _logger.LogInformation("Toggling user status: {UserId}", userId);
                await _adminService.ToggleUserStatusAsync(userId);
                _logger.LogInformation("User status toggled successfully: {UserId}", userId);
                return Ok(new { message = "User active status toggled successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user status for {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
