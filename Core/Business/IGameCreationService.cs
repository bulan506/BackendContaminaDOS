using Core.Models.Types;
using System.Collections.Generic;

namespace Core.Models.Business
{
    public interface IGameCreationService
    {
        ResponseCreate CreateGame(RequestGame requestGame);
        ResponseAllGames GetAllGames();
        ResponseCreate GetGame(string id, string player, string password = null);
        ResponseAllGames SearchGames(string name = null, string status = null, int page = 0, int limit = 50);

    }
}