using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public AdminPlansController(IAdminDashboardService adminService)
        {
            _adminService = adminService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var plans = await _adminService.GetAllPlansAsync();
                return Ok(plans);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PlanDto planDto)
        {
            try
            {
                var created = await _adminService.CreatePlanAsync(planDto);
                return CreatedAtAction(nameof(GetAll), new { id = created.Id }, created);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] PlanDto planDto)
        {
            try
            {
                await _adminService.UpdatePlanAsync(id, planDto);
                return Ok(new { message = "Plan updated successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _adminService.DeletePlanAsync(id);
                return Ok(new { message = "Plan deleted successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
