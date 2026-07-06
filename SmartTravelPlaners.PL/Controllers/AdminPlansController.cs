using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Admin.Interfaces;
using SmartTravelPlaners.BLL.Features.Subscription.DTOs;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/admin/plans")]
    [Authorize(Roles = "Admin")]
    public class AdminPlansController : ControllerBase
    {
        private readonly IAdminDashboardService _adminService;
        private readonly ILogger<AdminPlansController> _logger;

        public AdminPlansController(IAdminDashboardService adminService, ILogger<AdminPlansController> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                _logger.LogInformation("Fetching all plans");
                var plans = await _adminService.GetAllPlansAsync();
                _logger.LogInformation("Plans retrieved successfully. Count: {PlanCount}", plans?.Count() ?? 0);
                return Ok(plans);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all plans. Error: {ErrorMessage}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PlanDto planDto)
        {
            try
            {
                _logger.LogInformation("Creating new plan: {PlanName}", planDto.Name);
                var created = await _adminService.CreatePlanAsync(planDto);
                _logger.LogInformation("Plan created successfully. PlanId: {PlanId}", created.Id);
                return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating plan. Error: {ErrorMessage}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] PlanDto planDto)
        {
            try
            {
                _logger.LogInformation("Updating plan: {PlanId}", id);
                await _adminService.UpdatePlanAsync(id, planDto);
                _logger.LogInformation("Plan updated successfully. PlanId: {PlanId}", id);
                return Ok(new { message = "Plan updated successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating plan: {PlanId}. Error: {ErrorMessage}", id, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                _logger.LogInformation("Deleting plan: {PlanId}", id);
                await _adminService.DeletePlanAsync(id);
                _logger.LogInformation("Plan deleted successfully. PlanId: {PlanId}", id);
                return Ok(new { message = "Plan deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting plan: {PlanId}. Error: {ErrorMessage}", id, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
