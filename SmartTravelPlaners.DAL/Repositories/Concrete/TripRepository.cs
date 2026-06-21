using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.DAL.Repositories.Concrete
{
    public class TripRepository : GenericRepository<Trip>, ITripRepository
    {
        public TripRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Trip?> GetTripWithDetailsAsync(Guid tripId)
        {
            return await _context.Trips
                .Include(t => t.Days)
                    .ThenInclude(d => d.Activities)
                .Include(t => t.Hotels)
                 .Include(t => t.Flights)
                .Include(t => t.WeatherDays)
                .FirstOrDefaultAsync(t => t.Id == tripId);
        }
    }
}
