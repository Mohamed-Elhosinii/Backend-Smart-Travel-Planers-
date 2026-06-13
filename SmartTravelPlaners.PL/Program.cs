using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartTravelPlaners.BLL.DTOs.Auth;
using SmartTravelPlaners.BLL.ExternalApis.FourSquare.Services;
using SmartTravelPlaners.BLL.ExternalApis.FourSquare.Interfaces;
using SmartTravelPlaners.BLL.ExternalApis.Settings.Places;

using SmartTravelPlaners.BLL.Services.Abstract;
using SmartTravelPlaners.BLL.Services.Concrete;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using SmartTravelPlaners.DAL.Repositories.Concrete;
using System.Text;
using SmartTravelPlaners.BLL.ExternalApis.HotelsAPI.Settings;
using SmartTravelPlaners.BLL.ExternalApis.HotelsAPI.Interfaces;
using SmartTravelPlaners.BLL.ExternalApis.HotelsAPI.Services;
using SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.Settings;
using SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.Interfaces;
using SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.Services;

namespace SmartTravelPlaners.PL
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // =======================================================
            // 1. CONTROLLERS & SWAGGER
            // =======================================================
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
                {
                    Title = "SmartTravelPlaners API",
                    Version = "v1"
                });

                c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = Microsoft.OpenApi.SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = Microsoft.OpenApi.ParameterLocation.Header,
                    Description = "Enter your JWT token"
                });

                c.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document, null),
                        new List<string>()
                    }
                });
            });

            // =======================================================
            // 2. DATABASE
            // =======================================================
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // =======================================================
            // 3. IDENTITY
            // =======================================================
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // =======================================================
            // 4. JWT + OAUTH
            // =======================================================
            var jwtSettings = builder.Configuration.GetSection("JwtSettings");

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!))
                };
            })
            .AddGoogle(options =>
            {
                options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
                options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
            });

            // =======================================================
            // 5. APPLICATION SERVICES
            // =======================================================
            builder.Services.Configure<EmailSettings>(
                builder.Configuration.GetSection("EmailSettings"));
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddScoped<IAuthService, AuthService>();

            // Repositories & Unit of Work
            builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            builder.Services.AddScoped<ITripRepository, TripRepository>();
            builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Flight Service
            builder.Services.AddHttpClient();
            builder.Services.AddScoped<
                SmartTravelPlaners.BLL.ExternalApis.Interfaces.IFlightService,
                SmartTravelPlaners.BLL.ExternalApis.Services.FlightService>();

            // TODO: Register Semantic Kernel & OpenAI Agents


            //External APis

            //Hotel API
            builder.Services.Configure<HotelApiSettings>(builder.Configuration.GetSection("HotelApiSettings"));
            builder.Services.AddHttpClient<IHotelApiService, HotelApiService>();

            //Places API

            builder.Services.Configure<FoursquareSettings>(
           builder.Configuration.GetSection("FoursquareSettings"));

            builder.Services.Configure<SerperSettings>(
                builder.Configuration.GetSection("SerperSettings"));

            builder.Services.AddHttpClient("Foursquare", client =>
            {
                client.BaseAddress = new Uri("https://places-api.foursquare.com");
            });

            builder.Services.AddHttpClient("Serper", client =>
            {
                client.BaseAddress = new Uri("https://google.serper.dev");
            });

            builder.Services.AddScoped<IPlacesApiService, PlacesApiService>();
            // Weather API
            builder.Services.Configure<WeatherApiSettings>(
                builder.Configuration.GetSection("WeatherApiSettings")
            );

            builder.Services.AddHttpClient<IWeatherApiService, WeatherApiService>();
            // =======================================================
            // 6. BUILD APP
            // =======================================================
            var app = builder.Build();

            // =======================================================
            // 7. MIDDLEWARE PIPELINE
            // =======================================================
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartTravelPlaners API V1");
                c.RoutePrefix = string.Empty;
            });

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}