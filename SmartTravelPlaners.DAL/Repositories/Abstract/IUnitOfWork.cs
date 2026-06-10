using System;
using System.Threading.Tasks;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Repositories.Abstract
{
    public interface IUnitOfWork : IDisposable
    {
        ITripRepository Trips { get; }
        IUserProfileRepository UserProfiles { get; }

        IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class;
        Task<int> CompleteAsync();
    }
}
