using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;
using SmartTravelPlaners.DAL.Entities;

namespace SmartTravelPlaners.DAL.Context
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public virtual DbSet<UserProfile> UserProfiles { get; set; }
        public virtual DbSet<Trip> Trips { get; set; }
        public virtual DbSet<TripDay> TripDays { get; set; }
        public virtual DbSet<Activity> Activities { get; set; }
        public virtual DbSet<Hotel> Hotels { get; set; }
        public virtual DbSet<Flight> Flights { get; set; }
        public virtual DbSet<WeatherDay> WeatherDays { get; set; }
        public virtual DbSet<TripPreference> TripPreferences { get; set; }
        public virtual DbSet<ChatSession> ChatSessions { get; set; }
        public virtual DbSet<ChatMessage> ChatMessages { get; set; }
        public virtual DbSet<PlaceCache> PlacesCache { get; set; }
        public virtual DbSet<RefreshToken> RefreshTokens { get; set; }

        // Subscription & Payments
        public virtual DbSet<Plan> Plans { get; set; }
        public virtual DbSet<Subscription> Subscriptions { get; set; }
        public virtual DbSet<UsageCounter> UsageCounters { get; set; }
        public virtual DbSet<PaymentTransaction> PaymentTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); 
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
