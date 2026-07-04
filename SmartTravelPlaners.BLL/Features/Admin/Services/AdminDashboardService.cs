using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartTravelPlaners.BLL.Features.Admin.DTOs;
using SmartTravelPlaners.BLL.Features.Admin.Interfaces;
using SmartTravelPlaners.BLL.Features.Subscription.DTOs;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Enums;

namespace SmartTravelPlaners.BLL.Features.Admin.Services
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AdminDashboardService> _logger;

        public AdminDashboardService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<AdminDashboardService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<AdminStatsDto> GetOverviewStatsAsync()
        {
            try
            {
                _logger.LogInformation("Fetching admin overview statistics");

                var totalUsers = await _context.Users.CountAsync();
                var totalTrips = await _context.Trips.CountAsync();
                var activeSubs = await _context.Subscriptions.CountAsync(s => s.Status == SubscriptionStatus.Active);
                var totalRevenue = await _context.PaymentTransactions
                    .Where(t => t.Status == "paid")
                    .SumAsync(t => t.Amount);

                // Last 6 months revenue history
                var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
                var transactions = await _context.PaymentTransactions
                    .Where(t => t.Status == "paid" && t.CreatedAt >= sixMonthsAgo)
                    .ToListAsync();

                var revenueHistory = transactions
                    .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .Select(g => new MonthlyRevenueDto
                    {
                        Month = $"{CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(g.Key.Month)} {g.Key.Year}",
                        Revenue = g.Sum(t => t.Amount)
                    })
                    .ToList();

                // Daily user registrations for the last 7 days
                var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-7);
                var profiles = await _context.UserProfiles
                    .Where(p => p.CreatedAt >= sevenDaysAgo)
                    .ToListAsync();

                var userRegistrations = new List<DailyUserRegistrationDto>();
                for (int i = 6; i >= 0; i--)
                {
                    var date = DateTime.UtcNow.Date.AddDays(-i);
                    var count = profiles.Count(p => p.CreatedAt.Date == date);
                    userRegistrations.Add(new DailyUserRegistrationDto
                    {
                        Date = date.ToString("yyyy-MM-dd"),
                        Count = count
                    });
                }

                _logger.LogInformation("Admin statistics retrieved: {TotalUsers} users, {TotalTrips} trips, {ActiveSubs} active subscriptions, {TotalRevenue} revenue", 
                    totalUsers, totalTrips, activeSubs, totalRevenue);

                return new AdminStatsDto
                {
                    TotalUsers = totalUsers,
                    TotalTrips = totalTrips,
                    ActiveSubscriptions = activeSubs,
                    TotalRevenue = totalRevenue,
                    RevenueHistory = revenueHistory,
                    UserRegistrations = userRegistrations
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch admin overview statistics. Error: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<(IEnumerable<AdminUserListItemDto> Users, int TotalCount)> GetUsersListAsync(string? search, int page, int pageSize)
        {
            try
            {
                _logger.LogInformation("Fetching users list. Page: {Page}, PageSize: {PageSize}, Search: {Search}", page, pageSize, search ?? "none");

                var query = _context.Users
                    .Include(u => u.Profile)
                        .ThenInclude(p => p.Subscriptions)
                            .ThenInclude(s => s.Plan)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    var cleanSearch = search.Trim().ToLower();
                    query = query.Where(u => u.FullName.ToLower().Contains(cleanSearch) || (u.Email != null && u.Email.ToLower().Contains(cleanSearch)));
                }

                var totalCount = await query.CountAsync();

                var users = await query
                    .OrderByDescending(u => u.Profile != null ? u.Profile.CreatedAt : DateTime.MinValue)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var list = users.Select(u => {
                    var activeSub = u.Profile?.Subscriptions.FirstOrDefault(s => s.Status == SubscriptionStatus.Active);
                    var plan = activeSub?.Plan;
                    var isBanned = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow;

                    return new AdminUserListItemDto
                    {
                        UserId = u.Id,
                        FullName = u.FullName,
                        Email = u.Email ?? string.Empty,
                        CreatedAt = u.Profile?.CreatedAt ?? DateTime.MinValue,
                        PlanName = plan?.Name ?? "Free",
                        PlanId = plan?.Id ?? Guid.Empty,
                        SubscriptionStatus = activeSub?.Status.ToString() ?? "Active",
                        IsBanned = isBanned
                    };
                }).ToList();

                _logger.LogInformation("Users list retrieved: {UserCount} users out of {TotalCount} total", list.Count, totalCount);

                return (list, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch users list. Error: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task UpdateUserSubscriptionPlanAsync(string userId, Guid planId)
        {
            try
            {
                _logger.LogInformation("Updating user subscription plan. UserId: {UserId}, PlanId: {PlanId}", userId, planId);

                var userProfile = await _context.UserProfiles
                    .Include(p => p.Subscriptions)
                    .FirstOrDefaultAsync(p => p.AspNetUserId == userId);

                if (userProfile == null)
                {
                    _logger.LogWarning("User profile not found for UserId: {UserId}", userId);
                    throw new Exception("User profile not found");
                }

                var plan = await _context.Plans.FindAsync(planId);
                if (plan == null)
                {
                    _logger.LogWarning("Plan not found. PlanId: {PlanId}", planId);
                    throw new Exception("Plan not found");
                }

                // Cancel active subscriptions
                var activeSubs = userProfile.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active).ToList();
                foreach (var sub in activeSubs)
                {
                    sub.Status = SubscriptionStatus.Cancelled;
                    _context.Subscriptions.Update(sub);
                    _logger.LogInformation("Cancelled subscription. SubscriptionId: {SubscriptionId}", sub.Id);
                }

                // Create new active subscription
                var now = DateTime.UtcNow;
                var newSubscription = new SmartTravelPlaners.DAL.Entities.Subscription
                {
                    Id = Guid.NewGuid(),
                    UserProfileId = userProfile.Id,
                    PlanId = planId,
                    Status = SubscriptionStatus.Active,
                    CurrentPeriodStart = now,
                    CurrentPeriodEnd = plan.PriceMonthly == 0 ? now.AddYears(100) : now.AddMonths(1)
                };

                await _context.Subscriptions.AddAsync(newSubscription);
                await _context.SaveChangesAsync();

                _logger.LogInformation("User subscription plan updated successfully. UserId: {UserId}, NewSubscriptionId: {SubscriptionId}, Plan: {PlanName}", 
                    userId, newSubscription.Id, plan.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update user subscription plan. UserId: {UserId}, PlanId: {PlanId}. Error: {ErrorMessage}", 
                    userId, planId, ex.Message);
                throw;
            }
        }

        public async Task ToggleUserStatusAsync(string userId)
        {
            try
            {
                _logger.LogInformation("Toggling user status. UserId: {UserId}", userId);

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found for status toggle. UserId: {UserId}", userId);
                    throw new Exception("User not found");
                }

                var isCurrentlyBanned = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;

                if (isCurrentlyBanned)
                {
                    user.LockoutEnd = null;
                    _logger.LogInformation("User unbanned. UserId: {UserId}", userId);
                }
                else
                {
                    user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
                    _logger.LogInformation("User banned. UserId: {UserId}", userId);
                }

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    _logger.LogError("Failed to update user status. UserId: {UserId}, Errors: {Errors}", userId, 
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                    throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user status. UserId: {UserId}. Error: {ErrorMessage}", userId, ex.Message);
                throw;
            }
        }

        public async Task<(IEnumerable<AdminPaymentTransactionDto> Transactions, int TotalCount)> GetPaymentsHistoryAsync(int page, int pageSize)
        {
            try
            {
                _logger.LogInformation("Fetching payment history. Page: {Page}, PageSize: {PageSize}", page, pageSize);

                var query = _context.PaymentTransactions
                    .Include(t => t.Subscription)
                        .ThenInclude(s => s.UserProfile)
                            .ThenInclude(p => p.AspNetUser)
                    .Include(t => t.Subscription)
                        .ThenInclude(s => s.Plan)
                    .AsQueryable();

                var totalCount = await query.CountAsync();

                var transactionsList = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var list = transactionsList.Select(t => new AdminPaymentTransactionDto
                {
                    Id = t.Id,
                    UserEmail = t.Subscription?.UserProfile?.AspNetUser?.Email ?? "Unknown",
                    PlanName = t.Subscription?.Plan?.Name ?? "Unknown",
                    Amount = t.Amount,
                    Status = t.Status,
                    CreatedAt = t.CreatedAt,
                    PaymobOrderId = t.PaymobOrderId,
                    PaymobTransactionId = t.PaymobTransactionId
                }).ToList();

                _logger.LogInformation("Payment history retrieved: {TransactionCount} transactions out of {TotalCount} total", list.Count, totalCount);

                return (list, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch payment history. Error: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<IEnumerable<PlanDto>> GetAllPlansAsync()
        {
            try
            {
                _logger.LogInformation("Fetching all plans");

                var plans = await _context.Plans.ToListAsync();
                var result = plans.Select(p => new PlanDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    PriceMonthly = p.PriceMonthly,
                    MaxTripsPerMonth = p.MaxTripsPerMonth,
                    MaxMessagesPerMonth = p.MaxMessagesPerMonth
                }).ToList();

                _logger.LogInformation("Retrieved {PlanCount} plans", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch plans. Error: {ErrorMessage}", ex.Message);
                throw;
            }
        }

        public async Task<PlanDto> CreatePlanAsync(PlanDto planDto)
        {
            try
            {
                _logger.LogInformation("Creating new plan: {PlanName}, Price: {Price}", planDto.Name, planDto.PriceMonthly);

                var plan = new Plan
                {
                    Id = Guid.NewGuid(),
                    Name = planDto.Name,
                    PriceMonthly = planDto.PriceMonthly,
                    MaxTripsPerMonth = planDto.MaxTripsPerMonth,
                    MaxMessagesPerMonth = planDto.MaxMessagesPerMonth
                };

                await _context.Plans.AddAsync(plan);
                await _context.SaveChangesAsync();

                planDto.Id = plan.Id;

                _logger.LogInformation("Plan created successfully. PlanId: {PlanId}, Name: {PlanName}", plan.Id, plan.Name);
                return planDto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create plan. PlanName: {PlanName}. Error: {ErrorMessage}", planDto.Name, ex.Message);
                throw;
            }
        }

        public async Task UpdatePlanAsync(Guid id, PlanDto planDto)
        {
            try
            {
                _logger.LogInformation("Updating plan. PlanId: {PlanId}, NewName: {NewName}", id, planDto.Name);

                var plan = await _context.Plans.FindAsync(id);
                if (plan == null)
                {
                    _logger.LogWarning("Plan not found for update. PlanId: {PlanId}", id);
                    throw new Exception("Plan not found");
                }

                plan.Name = planDto.Name;
                plan.PriceMonthly = planDto.PriceMonthly;
                plan.MaxTripsPerMonth = planDto.MaxTripsPerMonth;
                plan.MaxMessagesPerMonth = planDto.MaxMessagesPerMonth;

                _context.Plans.Update(plan);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Plan updated successfully. PlanId: {PlanId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update plan. PlanId: {PlanId}. Error: {ErrorMessage}", id, ex.Message);
                throw;
            }
        }

        public async Task DeletePlanAsync(Guid id)
        {
            try
            {
                _logger.LogInformation("Deleting plan. PlanId: {PlanId}", id);

                var plan = await _context.Plans.FindAsync(id);
                if (plan == null)
                {
                    _logger.LogWarning("Plan not found for deletion. PlanId: {PlanId}", id);
                    throw new Exception("Plan not found");
                }

                var hasActiveSubs = await _context.Subscriptions.AnyAsync(s => s.PlanId == id && s.Status == SubscriptionStatus.Active);
                if (hasActiveSubs)
                {
                    _logger.LogWarning("Cannot delete plan as there are active subscriptions. PlanId: {PlanId}", id);
                    throw new Exception("Cannot delete plan as there are active subscriptions using it.");
                }

                _context.Plans.Remove(plan);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Plan deleted successfully. PlanId: {PlanId}", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete plan. PlanId: {PlanId}. Error: {ErrorMessage}", id, ex.Message);
                throw;
            }
        }
    }
}
