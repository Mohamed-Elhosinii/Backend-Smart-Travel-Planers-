using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.DAL.Repositories.Concrete
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly Dictionary<string, object> _repositories = new();

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public ITripRepository Trips => 
            (ITripRepository)GetOrAddRepository<Trip>(() => new TripRepository(_context));

        public IUserProfileRepository UserProfiles => 
            (IUserProfileRepository)GetOrAddRepository<UserProfile>(() => new UserProfileRepository(_context));

        public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class
        {
            var type = typeof(TEntity);
            if (type == typeof(Trip))
            {
                return (IGenericRepository<TEntity>)Trips;
            }
            if (type == typeof(UserProfile))
            {
                return (IGenericRepository<TEntity>)UserProfiles;
            }

            return (IGenericRepository<TEntity>)GetOrAddRepository<TEntity>(() => new GenericRepository<TEntity>(_context));
        }

        private object GetOrAddRepository<TEntity>(Func<object> factory) where TEntity : class
        {
            var typeName = typeof(TEntity).FullName ?? typeof(TEntity).Name;
            if (!_repositories.TryGetValue(typeName, out var repo))
            {
                repo = factory();
                _repositories[typeName] = repo;
            }
            return repo;
        }

        public async Task<int> CompleteAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
