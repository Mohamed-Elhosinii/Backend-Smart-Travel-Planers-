using System;
using System.Threading.Tasks;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Repositories.Abstract
{
    public interface IUserProfileRepository : IGenericRepository<UserProfile>
    {
        Task<UserProfile?> GetUserProfileWithPreferencesAsync(string userId);
    }
}
