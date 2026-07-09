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
                .Include(t => t.Preferences)
                .FirstOrDefaultAsync(t => t.Id == tripId);
        }

        public async Task<List<Trip>> GetUserTripsAsync(Guid profileId)
        {
            return await _context.Trips
                .AsNoTracking()
                .Include(t => t.Preferences)
                .Include(t => t.Days)
                .Where(t => t.UserId == profileId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<Trip?> GetTripWithDetailsNoTrackingAsync(Guid tripId)
        {
            return await _context.Trips
                .AsNoTracking()
                .Include(t => t.Days)
                    .ThenInclude(d => d.Activities)
                .Include(t => t.Hotels)
                .Include(t => t.Flights)
                .Include(t => t.Preferences)
                .FirstOrDefaultAsync(t => t.Id == tripId);
        }
       
    }

    }

