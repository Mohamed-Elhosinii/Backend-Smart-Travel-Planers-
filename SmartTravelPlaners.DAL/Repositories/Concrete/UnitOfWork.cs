using System;
using System.Threading.Tasks;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.DAL.Repositories.Concrete
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> CompleteAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
