using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
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
using SmartTravelPlaners.BLL.Features.Trips.Interfaces;
using SmartTravelPlaners.BLL.Features.Trips.Services;
using SmartTravelPlaners.DAL.Context;
using SmartTravelPlaners.DAL.Entities;
using SmartTravelPlaners.DAL.Repositories.Abstract;
using SmartTravelPlaners.DAL.Repositories.Concrete;
using SmartTravelPlaners.BLL.Features.Admin.Interfaces;
using SmartTravelPlaners.BLL.Features.Admin.Services;
using System.ClientModel;
using System.Text;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using SmartTravelPlaners.BLL.DTOs.Common;
using SmartTravelPlaners.PL.Middlewares;
using SmartTravelPlaners.BLL.Validation.Auth;
using Serilog;
using Serilog.Events;
using System.IdentityModel.Tokens.Jwt;

namespace SmartTravelPlaners.PL
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Serilog as the logging provider
            builder.Host.UseSerilog((hostingContext, services, loggerConfiguration) =>
            {
                var isDev = hostingContext.HostingEnvironment.IsDevelopment();
                var minimumLevel = isDev ? LogEventLevel.Debug : LogEventLevel.Information;

                var outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

                loggerConfiguration
                    .MinimumLevel.Is(minimumLevel)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(outputTemplate: outputTemplate)
                    .WriteTo.File(
                        path: "logs/app-.txt",
                        rollingInterval: RollingInterval.Day,
                        outputTemplate: outputTemplate);
            });

            // =======================================================
            // 1. CONTROLLERS & SWAGGER
            // =======================================================
            builder.Services.AddControllers();
            builder.Services.AddFluentValidationAutoValidation();
            builder.Services.AddValidatorsFromAssemblyContaining<RegisterDtoValidator>();

            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = context =>
                {
                    var errors = context.ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    var response = ApiResponse<object>.Failure(errors, "Validation failed");
                    return new BadRequestObjectResult(response);
                };
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAngular", policy =>
                {
                    policy.AllowAnyOrigin()
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
            // 3. IDENTITY & RATE LIMITING
            // =======================================================
            
            // Add Rate Limiting
            builder.Services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("AuthPolicy", limiterOptions =>
                {
                    limiterOptions.PermitLimit = 5; // 5 requests per window
                    limiterOptions.Window = TimeSpan.FromMinutes(1);
                    limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
                    limiterOptions.QueueLimit = 2;
                });
            });

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Updated to match RegisterDtoValidator
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 6;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.SignIn.RequireConfirmedEmail = true;

                // Ensure 6-digit OTPs are generated instead of long tokens
                options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
                options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider;
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

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        var response = ApiResponse.Failure("You are not authorized to access this resource.");
                        var json = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                        await context.Response.WriteAsync(json);
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.ContentType = "application/json";
                        var response = ApiResponse.Failure("You do not have permission to access this resource.");
                        var json = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
                        await context.Response.WriteAsync(json);
                    }
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
            builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();

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

            var kernelBuilder = builder.Services.AddKernel();

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
            builder.Services.AddHttpClient<IPlaceResolverService, PlaceResolverService>();
            builder.Services.AddHttpClient<IHotelSearchService, HotelSearchService>();
            builder.Services.AddHttpClient<IBookingLinksService, BookingLinksService>();

            // ---- Flight API ----
            builder.Services.AddHttpClient();
            builder.Services.AddHttpClient<IFlightService, FlightService>();

            // Register TripPlugin as scoped so it can be resolved per-request in ChatService
            builder.Services.AddScoped<SmartTravelPlaners.BLL.Features.Trips.Plugins.TripPlugin>();

            // Plugins consumed directly by the orchestrator (registered as concrete types).
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

            // =======================================================
            // 8. CHAT SERVICE
            // =======================================================
            builder.Services.AddScoped<IChatService, ChatService>();   // <-- بدل AddScoped<ChatService>()
            builder.Services.AddScoped<IChatRepository, ChatRepository>();



            //orchestrator
            builder.Services.AddScoped<ITripOrchestratorService, TripOrchestratorService>();

            // Shared trip-creation pipeline — used by BOTH the chat TRIP_READY handler
            // and the form-driven POST /api/Trip/quick-plan endpoint.
            builder.Services.AddScoped<ITripCreationService, TripCreationService>();

            // =======================================================
            // 10. SUBSCRIPTION & PAYMENTS (Paymob)
            // =======================================================
            builder.Services.Configure<PaymobSettings>(
                builder.Configuration.GetSection("Paymob"));
            builder.Services.AddHttpClient<IPaymobService, PaymobService>();
            builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
            builder.Services.AddScoped<IUsageLimitService, UsageLimitService>();
            builder.Services.AddHostedService<SubscriptionExpiryJob>();
            
            // Background Jobs
            builder.Services.AddHostedService<SmartTravelPlaners.BLL.Features.Auth.Jobs.TokenCleanupJob>();

            // =======================================================
            // 11. BUILD APP
            // =======================================================
            var app = builder.Build();

            app.UseMiddleware<GlobalExceptionMiddleware>();

            // =======================================================
            // 12. MIDDLEWARE PIPELINE
            // =======================================================
            // Serilog request logging (HTTP request summary)
            app.UseSerilogRequestLogging();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartTravelPlaners API V1");
                c.RoutePrefix = string.Empty;
            });

            // IMPORTANT: UseCors must come BEFORE UseHttpsRedirection
            // to prevent CORS preflight OPTIONS requests from getting 307 redirected,
            // which browsers block and causes "Redirect is not allowed for a preflight" errors.
            app.UseCors("AllowAngular");

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
            
            // =======================================================
            // 13. SEED DATA (Roles)
            // =======================================================
            using (var scope = app.Services.CreateScope())
            {
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                if (!roleManager.RoleExistsAsync("Admin").GetAwaiter().GetResult())
                {
                    roleManager.CreateAsync(new IdentityRole("Admin")).GetAwaiter().GetResult();
                }
            }

            app.MapControllers();

            try
            {
                app.Run();
            }
            finally
            {
                // Ensure any buffered events are flushed on shutdown
                Log.CloseAndFlush();
            }
        }
    }
}