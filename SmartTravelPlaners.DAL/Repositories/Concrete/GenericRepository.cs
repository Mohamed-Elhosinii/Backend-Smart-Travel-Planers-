using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Repositories.Abstract;

namespace SmartTravelPlaners.DAL.Repositories.Concrete
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _context;

        public GenericRepository(ApplicationDbContext context)
        {
            _context = context;
        }
    }
}
