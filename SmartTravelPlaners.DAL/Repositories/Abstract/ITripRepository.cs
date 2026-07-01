using System;
using System.Threading.Tasks;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Repositories.Abstract
{
    public interface ITripRepository : IGenericRepository<Trip>
    {
        Task<Trip?> GetTripWithDetailsAsync(Guid tripId);
        Task<Trip?> GetTripWithDetailsNoTrackingAsync(Guid tripId);
        Task<List<Trip>> GetUserTripsAsync(Guid profileId);
    }
}
