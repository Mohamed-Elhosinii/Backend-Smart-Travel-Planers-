

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using OpenAI;
using SmartTravelPlaners.BLL.Features.Flight.Services;
using SmartTravelPlaners.BLL.Features.Flight.Interfaces;
using SmartTravelPlaners.BLL.Features.Flight.Plugins;
using SmartTravelPlaners.BLL.DTOs.Auth;
using SmartTravelPlaners.BLL.Features.Chat.Interfaces;
using SmartTravelPlaners.BLL.Features.Chat.Services;
using SmartTravelPlaners.BLL.Features.Hotel.Interfaces;
using SmartTravelPlaners.BLL.Features.Hotel.Plugins;
using SmartTravelPlaners.BLL.Features.Hotel.Services;
using SmartTravelPlaners.BLL.Features.Hotel.Settings;
using SmartTravelPlaners.BLL.Features.Orchestrator.Interfaces;
using SmartTravelPlaners.BLL.Features.Orchestrator.Services;
using SmartTravelPlaners.BLL.Features.Place.Interfaces;
using SmartTravelPlaners.BLL.Features.Place.Plugins;
using SmartTravelPlaners.BLL.Features.Place.Services;
using SmartTravelPlaners.BLL.Features.Place.Settings;
using SmartTravelPlaners.BLL.Features.Weather.Interfaces;
using SmartTravelPlaners.BLL.Features.Weather.Plugins;
using SmartTravelPlaners.BLL.Features.Weather.Services;
using SmartTravelPlaners.BLL.Features.Weather.Settings;
using SmartTravelPlaners.BLL.Services.Abstract;
using SmartTravelPlaners.BLL.Services.Concrete;
using SmartTravelPlaners.BLL.Features.Subscription.Interfaces;
using SmartTravelPlaners.BLL.Features.Subscription.Services;
using SmartTravelPlaners.BLL.Features.Subscription.Settings;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using SmartTravelPlaners.DAL.Repositories.Concrete;
using System.ClientModel;
using System.Text;

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
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAngular", policy =>
                {
                    policy.WithOrigins("http://localhost:4200")
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });
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
            // 5. APPLICATION SERVICES (Auth / Email / Repos)
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

            // =======================================================
            // 6. SEMANTIC KERNEL + GITHUB MODELS
            // =======================================================
            // GitHub Models is an OpenAI-compatible endpoint, authenticated
            // with a GitHub Personal Access Token instead of an OpenAI API key.
            var githubModelsConfig = builder.Configuration.GetSection("GitHubModels");
            var githubEndpoint = githubModelsConfig["Endpoint"]!;   // e.g. https://models.inference.ai.azure.com
            var githubToken = githubModelsConfig["Token"]!;         // GitHub PAT
            var githubModelId = githubModelsConfig["ModelId"]!;     // e.g. gpt-4o-mini

            builder.Services.AddKernel();

            // Build an OpenAIClient pointed at the GitHub Models endpoint,
            // then register it as the chat completion service for the Kernel.
            builder.Services.AddOpenAIChatCompletion(
                modelId: githubModelId,
                openAIClient: new OpenAIClient(
                    new ApiKeyCredential(githubToken),
                    new OpenAIClientOptions { Endpoint = new Uri(githubEndpoint) }
                ));

            // =======================================================
            // 7. EXTERNAL APIS (Hotel / Flight / Places / Weather)
            // =======================================================

            // ---- Hotel API (StayAPI) ----
            builder.Services.Configure<HotelApiSettings>(
                builder.Configuration.GetSection("HotelApiSettings"));
            builder.Services.AddHttpClient<IHotelApiService, HotelApiService>();

            // ---- Flight API ----
            builder.Services.AddHttpClient();
            builder.Services.AddHttpClient<IFlightService, FlightService>();

            builder.Services.AddScoped<IFlightService, FlightService>();

            builder.Services.AddScoped<IWeatherApiService, WeatherApiService>();
            builder.Services.AddScoped<WeatherPlugin>();
            builder.Services.AddScoped<PlacesPlugin>();
            builder.Services.AddScoped<HotelPlugin>();
            builder.Services.AddScoped<FlightPlugin>();

            // ---- Places API (Foursquare + Serper) ----
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

            // ---- Weather API ----
            builder.Services.Configure<WeatherApiSettings>(
                builder.Configuration.GetSection("WeatherApiSettings"));
            builder.Services.AddHttpClient<IWeatherApiService, WeatherApiService>();
            builder.Services.AddScoped<WeatherApiService>();

            // =======================================================
            // 8. CHAT SERVICE
            // =======================================================
            builder.Services.AddScoped<IChatService, ChatService>();   // <-- بدل AddScoped<ChatService>()
            builder.Services.AddScoped<IChatRepository, ChatRepository>();



            //orchestrator 
            builder.Services.AddScoped<ITripOrchestratorService, TripOrchestratorService>();

            // =======================================================
            // 10. SUBSCRIPTION & PAYMENTS (Paymob)
            // =======================================================
            builder.Services.Configure<PaymobSettings>(
                builder.Configuration.GetSection("Paymob"));
            builder.Services.AddHttpClient<IPaymobService, PaymobService>();
            builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
            builder.Services.AddScoped<IUsageLimitService, UsageLimitService>();
            builder.Services.AddHostedService<SubscriptionExpiryJob>();

            // =======================================================
            // 11. BUILD APP
            // =======================================================
            var app = builder.Build();

            // =======================================================
            // 10. MIDDLEWARE PIPELINE
            // =======================================================
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartTravelPlaners API V1");
                c.RoutePrefix = string.Empty;
            });

            app.UseHttpsRedirection();
            app.UseCors("AllowAngular");

            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}