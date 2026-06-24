using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

        public AdminDashboardService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<AdminStatsDto> GetOverviewStatsAsync()
        {
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

        public async Task<(IEnumerable<AdminUserListItemDto> Users, int TotalCount)> GetUsersListAsync(string? search, int page, int pageSize)
        {
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

            return (list, totalCount);
        }

        public async Task UpdateUserSubscriptionPlanAsync(string userId, Guid planId)
        {
            var userProfile = await _context.UserProfiles
                .Include(p => p.Subscriptions)
                .FirstOrDefaultAsync(p => p.AspNetUserId == userId);

            if (userProfile == null)
                throw new Exception("User profile not found");

            var plan = await _context.Plans.FindAsync(planId);
            if (plan == null)
                throw new Exception("Plan not found");

            // Cancel active subscriptions
            var activeSubs = userProfile.Subscriptions.Where(s => s.Status == SubscriptionStatus.Active).ToList();
            foreach (var sub in activeSubs)
            {
                sub.Status = SubscriptionStatus.Cancelled;
                _context.Subscriptions.Update(sub);
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
        }

        public async Task ToggleUserStatusAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                throw new Exception("User not found");

            var isCurrentlyBanned = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;

            if (isCurrentlyBanned)
            {
                user.LockoutEnd = null;
            }
            else
            {
                user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        public async Task<(IEnumerable<AdminPaymentTransactionDto> Transactions, int TotalCount)> GetPaymentsHistoryAsync(int page, int pageSize)
        {
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

            return (list, totalCount);
        }

        public async Task<IEnumerable<PlanDto>> GetAllPlansAsync()
        {
            var plans = await _context.Plans.ToListAsync();
            return plans.Select(p => new PlanDto
            {
                Id = p.Id,
                Name = p.Name,
                PriceMonthly = p.PriceMonthly,
                MaxTripsPerMonth = p.MaxTripsPerMonth,
                MaxMessagesPerMonth = p.MaxMessagesPerMonth
            });
        }

        public async Task<PlanDto> CreatePlanAsync(PlanDto planDto)
        {
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
            return planDto;
        }

        public async Task UpdatePlanAsync(Guid id, PlanDto planDto)
        {
            var plan = await _context.Plans.FindAsync(id);
            if (plan == null)
                throw new Exception("Plan not found");

            plan.Name = planDto.Name;
            plan.PriceMonthly = planDto.PriceMonthly;
            plan.MaxTripsPerMonth = planDto.MaxTripsPerMonth;
            plan.MaxMessagesPerMonth = planDto.MaxMessagesPerMonth;

            _context.Plans.Update(plan);
            await _context.SaveChangesAsync();
        }

        public async Task DeletePlanAsync(Guid id)
        {
            var plan = await _context.Plans.FindAsync(id);
            if (plan == null)
                throw new Exception("Plan not found");

            var hasActiveSubs = await _context.Subscriptions.AnyAsync(s => s.PlanId == id && s.Status == SubscriptionStatus.Active);
            if (hasActiveSubs)
                throw new Exception("Cannot delete plan as there are active subscriptions using it.");

            _context.Plans.Remove(plan);
            await _context.SaveChangesAsync();
        }
    }
}
