using Core.Models.Types;
using System.Collections.Generic;

namespace Core.Models.Business
{
    public interface IGameCreationService
    {
        Task<ResponseCreate> CreateGameAsync(RequestGame requestGame);
        Task<ResponseAllGames> GetAllGamesAsync();
        Task<ResponseCreate> GetGameAsync(string id, string player, string password = null);
        Task<ResponseAllGames> SearchGamesAsync(string name = null, string status = null, int page = 0, int limit = 50);
    }
}