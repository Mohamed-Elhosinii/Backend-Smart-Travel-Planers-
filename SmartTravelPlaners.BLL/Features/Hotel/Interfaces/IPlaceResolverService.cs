using System.Threading.Tasks;
using SmartTravelPlaners.BLL.Features.Hotel.DTOs;

namespace SmartTravelPlaners.BLL.Features.Hotel.Interfaces
{
    public interface IPlaceResolverService
    {
        Task<PlaceResolutionResult> ResolveAsync(string userInput);
        Task<PlaceResolutionResult> ConfirmAsync(string destId, string resolvedName);
    }
}
