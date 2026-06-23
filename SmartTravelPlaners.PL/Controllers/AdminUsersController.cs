using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.Features.Admin.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : ControllerBase
    {
        private readonly IAdminDashboardService _adminService;

        public AdminUsersController(IAdminDashboardService adminService)
        {
            _adminService = adminService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var (users, totalCount) = await _adminService.GetUsersListAsync(search, page, pageSize);
                return Ok(new { users, totalCount, page, pageSize });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{userId}/plan")]
        public async Task<IActionResult> ForceUpdatePlan(string userId, [FromBody] Guid planId)
        {
            try
            {
                await _adminService.UpdateUserSubscriptionPlanAsync(userId, planId);
                return Ok(new { message = "User plan updated successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{userId}/toggle-status")]
        public async Task<IActionResult> ToggleUserStatus(string userId)
        {
            try
            {
                await _adminService.ToggleUserStatusAsync(userId);
                return Ok(new { message = "User active status toggled successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
