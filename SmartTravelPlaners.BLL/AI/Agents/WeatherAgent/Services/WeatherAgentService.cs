using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SmartTravelPlaners.BLL.AI.Agents.WeatherAgent.Plugins;
using SmartTravelPlaners.BLL.ExternalApis.WeatherAPI.Interfaces;

namespace SmartTravelPlaners.BLL.AI.Agents.WeatherAgent.Services
{
    public class WeatherAgentService
    {
        private readonly Kernel _kernel;
        private readonly IWeatherApiService _weatherApiService;

        public WeatherAgentService(IWeatherApiService weatherApiService, IConfiguration configuration)
        {
            _weatherApiService = weatherApiService;

            var builder = Kernel.CreateBuilder();

            builder.AddOpenAIChatCompletion(
                modelId: configuration["OpenAI:ModelId"] ?? "gpt-4o",
                apiKey: configuration["OpenAI:ApiKey"] ?? string.Empty
            );

            builder.Plugins.AddFromObject(new WeatherPlugin(_weatherApiService), "WeatherPlugin");

            _kernel = builder.Build();
        }

        public async Task<string> ProcessWeatherQueryAsync(string userMessage)
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            var chatHistory = new ChatHistory(
                "You are the specialized Weather Agent for the Smart Travel Planner platform. " +
                "Your absolute goal is to provide clear, friendly, and structured weather overviews for trips. " +
                "You MUST invoke the WeatherPlugin whenever a user mentions a city and trip dates. " +
                "In your final textual response, always summarize the daily temperature ranges, general conditions (e.g., Clear, Rain), " +
                "and explicitly include the exact 'iconUrl' string for each day in a structured way (like a bulleted list or a brief markdown summary). " +
                "If the user query is missing a city name, start date, or end date, politely ask them to provide the missing details."
            );

            chatHistory.AddUserMessage(userMessage);

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            var response = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, _kernel);

            return response.ToString();
        }
    }
}