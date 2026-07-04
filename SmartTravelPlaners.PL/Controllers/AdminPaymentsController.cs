using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Admin.Interfaces;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/admin/payments")]
    [Authorize(Roles = "Admin")]
    public class AdminPaymentsController : ControllerBase
    {
        private readonly IAdminDashboardService _adminService;
        private readonly ILogger<AdminPaymentsController> _logger;

        public AdminPaymentsController(IAdminDashboardService adminService, ILogger<AdminPaymentsController> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetPayments([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                _logger.LogInformation("Fetching payments history: Page: {Page}, PageSize: {PageSize}", page, pageSize);
                var (transactions, totalCount) = await _adminService.GetPaymentsHistoryAsync(page, pageSize);
                _logger.LogInformation("Payments history retrieved successfully: TotalCount: {TotalCount}, Page: {Page}", totalCount, page);
                return Ok(new { transactions, totalCount, page, pageSize });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching payments history. Page: {Page}, PageSize: {PageSize}. Error: {ErrorMessage}", page, pageSize, ex.Message);
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
