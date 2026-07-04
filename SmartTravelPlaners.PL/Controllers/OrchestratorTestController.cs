using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using SmartTravelPlaners.DAL.Repositories.Concrete;

namespace SmartTravelPlaners.PL.Controllers
{
    [ApiController]
    [Route("api/test/orchestrator")]
    public class OrchestratorTestController : ControllerBase
    {
        private readonly ITripOrchestratorService _orchestrator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<OrchestratorTestController> _logger;

        public OrchestratorTestController(ITripOrchestratorService orchestrator, IUnitOfWork unitOfWork, ILogger<OrchestratorTestController> logger)
        {
            _orchestrator = orchestrator;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        [HttpPost("build/{tripId}")]
        public async Task<IActionResult> Build(Guid tripId)
        {
            try
            {
                _logger.LogInformation("Building trip plan: {TripId}", tripId);
                var plan = await _orchestrator.BuildTripPlanAsync(tripId);
                _logger.LogInformation("Trip plan built successfully: {TripId}", tripId);
                return Ok(plan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building trip plan for {TripId}. Error: {ErrorMessage}", tripId, ex.Message);
                return BadRequest(new
                {
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }


        [HttpPost("seed-trip")]
        public async Task<IActionResult> SeedTrip()
        {
            try
            {
                _logger.LogInformation("Seeding test trip");
                // غيّر الـ UserId ده لـ UserProfile.Id حقيقي موجود في جدول UserProfiles عندك
                var userProfileId = Guid.Parse("d9a4730e-d248-4041-b809-5bc0b019cbd6");

                var trip = new SmartTravelPlaners.DAL.Entities.Trip
                {
                    Id = Guid.NewGuid(),
                    UserId = userProfileId,
                    Title = "Trip to Fayoum",
                    Destination = "Fayoum",
                    OriginCity = null, // سيب null الأول عشان تتجنب مشكلة IATA codes
                    StartDate = new DateOnly(2026, 6, 20),
                    EndDate = new DateOnly(2026, 6, 22),
                    NumTravelers = 2,
                    BudgetTotal = 300,
                    Status = SmartTravelPlaners.DAL.Enums.TripStatus.Draft
                };

                await _unitOfWork.Trips.AddAsync(trip);
                await _unitOfWork.CompleteAsync();

                _logger.LogInformation("Test trip seeded successfully: {TripId}", trip.Id);
                return Ok(new { tripId = trip.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding test trip. Error: {ErrorMessage}", ex.Message);
                throw;
            }
        }
    }
}