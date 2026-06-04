namespace SmartTravelPlaners.PL
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // =======================================================
            // 1. ADD SERVICES TO THE CONTAINER (Dependency Injection)
            // =======================================================
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // TODO: Register your DbContext (DAL)
            // builder.Services.AddDbContext<ApplicationDbContext>(options => ...);

            // TODO: Register Unit of Work & Repositories
            // builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

            // TODO: Register Semantic Kernel & OpenAI Agents

            var app = builder.Build();

            // =======================================================
            // 2. CONFIGURE THE HTTP REQUEST PIPELINE (Middleware)
            // =======================================================
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}