using Microsoft.AspNetCore.Mvc;
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
        public OrchestratorTestController(ITripOrchestratorService orchestrator , IUnitOfWork unitOfWork)
        {
            _orchestrator = orchestrator;
            _unitOfWork= unitOfWork;
        }

        [HttpPost("build/{tripId}")]
        public async Task<IActionResult> Build(Guid tripId)
        {
            try
            {
                var plan = await _orchestrator.BuildTripPlanAsync(tripId);
                return Ok(plan);
            }
            catch (Exception ex)
            {
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

            return Ok(new { tripId = trip.Id });
        }
    }
}