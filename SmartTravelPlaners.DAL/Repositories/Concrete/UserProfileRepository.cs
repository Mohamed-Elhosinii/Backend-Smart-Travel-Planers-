using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.DAL.Repositories.Concrete
{
    public class UserProfileRepository : GenericRepository<UserProfile>, IUserProfileRepository
    {
        public UserProfileRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<UserProfile?> GetUserProfileWithPreferencesAsync(string userId)
        {
            return await _context.UserProfiles
                .Include(up => up.Trips)
                    .ThenInclude(t => t.Preferences)
                .FirstOrDefaultAsync(up => up.AspNetUserId == userId);
        }
    }
}
