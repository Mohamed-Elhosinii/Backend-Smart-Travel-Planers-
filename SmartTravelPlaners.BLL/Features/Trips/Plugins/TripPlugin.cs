using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using SmartTravelPlaners.BLL.Features.Chat.DTOs;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.Interfaces;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.BLL.Features.Trips.Plugins
{
    public class TripPlugin
    {
        private readonly ITripCreationService _tripCreationService;
        private readonly ITripRepository _tripRepo;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServiceProvider _serviceProvider;

        // Context properties that will be set per-request by ChatService
        public string UserId { get; set; } = string.Empty;
        public Guid? TripId { get; set; }
        
        // Flags to communicate state changes back to ChatService
        public bool IsPlanUpdating { get; set; }
        public bool ShowPlanRequested { get; set; }

        public TripPlugin(
            ITripCreationService tripCreationService,
            ITripRepository tripRepo,
            IUnitOfWork unitOfWork,
            IServiceProvider serviceProvider)
        {
            _tripCreationService = tripCreationService;
            _tripRepo = tripRepo;
            _unitOfWork = unitOfWork;
            _serviceProvider = serviceProvider;
        }

        [KernelFunction("create_trip")]
        [Description("Create a completely new trip plan. Use this when the user has provided a destination, dates, budget, and travelers.")]
        public async Task<string> CreateTripAsync(
            [Description("Destination city name, e.g. Paris")] string destination,
            [Description("Start date of the trip in yyyy-MM-dd format")] string startDate,
            [Description("End date of the trip in yyyy-MM-dd format")] string endDate,
            [Description("Number of travelers")] int numTravelers,
            [Description("Total budget in the user's currency")] decimal budgetTotal,
            [Description("Origin city name for the flight. Null if no flight is needed. MUST be a specific city name, NEVER a country.")] string? originCity = null,
            [Description("Comma-separated list of preferences, e.g. museum,park")] string preferences = "")
        {
            if (DateOnly.TryParse(startDate, out var start) && start < DateOnly.FromDateTime(DateTime.UtcNow))
            {
                return JsonSerializer.Serialize(new { error = "عذراً، لا يمكن إنشاء رحلة بتاريخ في الماضي. من فضلك اختر تاريخ مستقبلي." });
            }

            var prefList = string.IsNullOrWhiteSpace(preferences) 
                ? new List<string>() 
                : new List<string>(preferences.Split(',', StringSplitOptions.RemoveEmptyEntries));

            var dto = new TripCreateDto
            {
                Destination = destination,
                OriginCity = originCity,
                StartDate = startDate,
                EndDate = endDate,
                NumTravelers = numTravelers,
                BudgetTotal = budgetTotal,
                Preferences = prefList
            };

            var creationResult = await _tripCreationService.CreateAndBuildAsync(dto, UserId);

            if (creationResult.LimitReached)
            {
                return JsonSerializer.Serialize(new { error = creationResult.Message });
            }

            // Sync the created trip id back to this plugin instance so ChatService can grab it
            TripId = creationResult.Trip!.Id;

            return JsonSerializer.Serialize(new { 
                success = true, 
                message = $"جاري تجهيز أفضل خطة لرحلتك إلى {destination} (من {startDate} إلى {endDate}). ثواني وهتكون جاهزة ✈️" 
            });
        }

        [KernelFunction("update_trip_field")]
        [Description("Update a specific field (destination, startdate, enddate, numtravelers, budgettotal, origincity) for the CURRENT trip and rebuild the affected parts of the plan.")]
        public async Task<string> UpdateTripFieldAsync(
            [Description("The field to update: destination, startdate, enddate, numtravelers, budgettotal, or origincity")] string field,
            [Description("The new value for the field")] string value)
        {
            if (TripId == null)
            {
                return JsonSerializer.Serialize(new { error = "مفيش رحلة موجودة عشان تتعدل. نبدأ نعمل رحلة جديدة؟" });
            }

            var tripId = TripId.Value;
            var trip = await _tripRepo.GetByIdAsync(tripId);
            if (trip == null) return JsonSerializer.Serialize(new { error = "الرحلة غير موجودة" });

            try
            {
                switch (field.ToLower())
                {
                    case "destination":
                        trip.Destination = value;
                        trip.Title = $"Trip to {value}";
                        break;
                    case "startdate":
                        var newStart = DateOnly.Parse(value);
                        if (newStart < DateOnly.FromDateTime(DateTime.UtcNow))
                            return JsonSerializer.Serialize(new { error = "لا يمكن تغيير تاريخ البداية لتاريخ في الماضي" });
                        if (newStart >= trip.EndDate)
                            return JsonSerializer.Serialize(new { error = "تاريخ البداية لازم يكون قبل تاريخ النهاية" });
                        trip.StartDate = newStart;
                        break;
                    case "enddate":
                        var newEnd = DateOnly.Parse(value);
                        if (newEnd < DateOnly.FromDateTime(DateTime.UtcNow))
                            return JsonSerializer.Serialize(new { error = "لا يمكن تغيير تاريخ النهاية لتاريخ في الماضي" });
                        if (newEnd <= trip.StartDate)
                            return JsonSerializer.Serialize(new { error = "تاريخ النهاية لازم يكون بعد تاريخ البداية" });
                        trip.EndDate = newEnd;
                        break;
                    case "numtravelers":
                        trip.NumTravelers = int.Parse(value);
                        break;
                    case "budgettotal":
                        trip.BudgetTotal = decimal.Parse(value);
                        break;
                    case "origincity":
                        trip.OriginCity = value;
                        break;
                    default:
                        return JsonSerializer.Serialize(new { error = $"الحقل {field} غير مدعوم للتعديل." });
                }

                _tripRepo.Update(trip);
                await _unitOfWork.CompleteAsync();

                IsPlanUpdating = true; // Signal ChatService to set Stage = Modifying and skip fetching stale plan

                // EXACT switch statement logic from ChatService
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var orchestrator = scope.ServiceProvider.GetRequiredService<ITripOrchestratorService>();
                        var scopedTripRepo = scope.ServiceProvider.GetRequiredService<ITripRepository>();

                        switch (field.ToLower())
                        {
                            case "destination":
                                await orchestrator.RegenerateHotelAsync(tripId);
                                await Task.Delay(500); // ← استني شوية
                                await orchestrator.RegenerateFlightAsync(tripId);
                                var tripAfterDest = await scopedTripRepo.GetByIdAsync(tripId);
                                var daysAfterDest = tripAfterDest != null
                                    ? Math.Max(tripAfterDest.EndDate.DayNumber - tripAfterDest.StartDate.DayNumber, 1) : 1;
                                for (int day = 1; day <= daysAfterDest; day++)
                                    await orchestrator.RegenerateDayActivitiesAsync(tripId, day);
                                break;
                               
                            case "startdate":
                            case "enddate":
                                await orchestrator.RegenerateHotelAsync(tripId);
                                await orchestrator.RegenerateFlightAsync(tripId);
                                await orchestrator.RegenerateWeatherAsync(tripId);
                                await orchestrator.SyncDayPlansAsync(tripId, field.ToLower());
                                break;

                            case "numtravelers":
                                await orchestrator.RegenerateHotelAsync(tripId);
                                break;

                            case "budgettotal":
                                await orchestrator.RegenerateHotelAsync(tripId);
                                var tripAfterBudget = await scopedTripRepo.GetByIdAsync(tripId);
                                var daysAfterBudget = tripAfterBudget != null
                                    ? Math.Max(tripAfterBudget.EndDate.DayNumber - tripAfterBudget.StartDate.DayNumber, 1) : 1;
                                for (int day = 1; day <= daysAfterBudget; day++)
                                    await orchestrator.RegenerateDayActivitiesAsync(tripId, day);
                                break;

                            case "origincity":
                                await orchestrator.RegenerateFlightAsync(tripId);
                                break;

                            default:
                                await orchestrator.BuildTripPlanAsync(tripId);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Field cascade update failed: {ex.Message}");
                    }
                });

                return JsonSerializer.Serialize(new { success = true, message = "تم استلام التعديلات وجاري تحديث تفاصيل الرحلة... ثواني وتكون جاهزة." });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { error = ex.Message });
            }
        }

        [KernelFunction("update_hotel")]
        [Description("Generate a different hotel for the current trip.")]
        public Task<string> UpdateHotelAsync()
        {
            if (TripId == null)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { error = "مفيش رحلة موجودة عشان نغيرلها الفندق. نبدأ نعمل رحلة جديدة؟" }));
            }

            var tripId = TripId.Value;
            IsPlanUpdating = true; // Signal ChatService
            
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var orchestrator = scope.ServiceProvider.GetRequiredService<ITripOrchestratorService>();
                    await orchestrator.RegenerateHotelAsync(tripId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RegenerateHotelAsync failed: {ex.Message}");
                }
            });

            return Task.FromResult(JsonSerializer.Serialize(new { success = true, message = "جاري البحث عن فندق بديل مناسب... ثواني وهنعرضهولك." }));
        }

        [KernelFunction("update_activities")]
        [Description("Generate different activities for a specific day of the current trip.")]
        public Task<string> UpdateActivitiesAsync([Description("The day number to regenerate activities for")] int dayNumber)
        {
            if (TripId == null)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { error = "مفيش رحلة موجودة عشان نغيرلها الأنشطة. نبدأ نعمل رحلة جديدة؟" }));
            }

            if (dayNumber <= 0)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { error = "ممكن تحدد رقم اليوم اللي عايز تغير أنشطته؟" }));
            }

            var tripId = TripId.Value;
            IsPlanUpdating = true; // Signal ChatService

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var orchestrator = scope.ServiceProvider.GetRequiredService<ITripOrchestratorService>();
                    await orchestrator.RegenerateDayActivitiesAsync(tripId, dayNumber);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RegenerateDayActivitiesAsync failed: {ex.Message}");
                }
            });

            return Task.FromResult(JsonSerializer.Serialize(new { success = true, message = $"جاري تغيير أنشطة يوم {dayNumber}... ثواني وهنعرضهالك." }));
        }

        [KernelFunction("update_flight")]
        [Description("Generate a different flight for the current trip.")]
        public Task<string> UpdateFlightAsync()
        {
            if (TripId == null)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { error = "مفيش رحلة موجودة عشان نغيرلها الطيران. نبدأ نعمل رحلة جديدة؟" }));
            }

            var tripId = TripId.Value;
            IsPlanUpdating = true; // Signal ChatService

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var orchestrator = scope.ServiceProvider.GetRequiredService<ITripOrchestratorService>();
                    await orchestrator.RegenerateFlightAsync(tripId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"RegenerateFlightAsync failed: {ex.Message}");
                }
            });

            return Task.FromResult(JsonSerializer.Serialize(new { success = true, message = "جاري البحث عن رحلة طيران بديلة... ثواني وهنعرضهالك." }));
        }

        [KernelFunction("show_trip")]
        [Description("Get the full details of the current active trip plan.")]
        public Task<string> ShowTripAsync()
        {
            if (TripId == null)
            {
                return Task.FromResult(JsonSerializer.Serialize(new { error = "مفيش رحلة موجودة حالياً." }));
            }

            ShowPlanRequested = true; // Signal ChatService to fetch the plan for the UI
            return Task.FromResult(JsonSerializer.Serialize(new { success = true, message = "جاري عرض تفاصيل الرحلة..." }));
        }
    }
}
