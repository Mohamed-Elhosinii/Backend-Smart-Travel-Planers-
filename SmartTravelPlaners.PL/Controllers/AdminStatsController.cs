using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Admin.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/admin/stats")]
    [Authorize(Roles = "Admin")]
    public class AdminStatsController : ControllerBase
    {
        private readonly IAdminDashboardService _adminService;
        private readonly ILogger<AdminStatsController> _logger;

        public AdminStatsController(IAdminDashboardService adminService, ILogger<AdminStatsController> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview()
        {
            try
            {
                _logger.LogInformation("Fetching overview statistics");
                var stats = await _adminService.GetOverviewStatsAsync();
                _logger.LogInformation("Overview statistics retrieved successfully");
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching overview statistics. Error: {ErrorMessage}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
