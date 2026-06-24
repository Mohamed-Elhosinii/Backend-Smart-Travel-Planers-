using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartTravelPlaners.BLL.Features.Admin.DTOs;
using SmartTravelPlaners.BLL.Features.Subscription.DTOs;

namespace SmartTravelPlaners.BLL.Features.Admin.Interfaces
{
    public interface IAdminDashboardService
    {
        Task<AdminStatsDto> GetOverviewStatsAsync();
        Task<(IEnumerable<AdminUserListItemDto> Users, int TotalCount)> GetUsersListAsync(string? search, int page, int pageSize);
        Task UpdateUserSubscriptionPlanAsync(string userId, Guid planId);
        Task ToggleUserStatusAsync(string userId);
        Task<(IEnumerable<AdminPaymentTransactionDto> Transactions, int TotalCount)> GetPaymentsHistoryAsync(int page, int pageSize);
        
        // Plan CRUD
        Task<IEnumerable<PlanDto>> GetAllPlansAsync();
        Task<PlanDto> CreatePlanAsync(PlanDto planDto);
        Task UpdatePlanAsync(Guid id, PlanDto planDto);
        Task DeletePlanAsync(Guid id);
    }
}
