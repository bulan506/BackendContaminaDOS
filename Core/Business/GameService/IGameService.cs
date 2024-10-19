using Core.Models.Types;
using Microsoft.AspNetCore.Mvc;

public interface IGameService
{
    ResponseJoin JoinGame(string gameId, string player, string password);
    ResponseStart StartGame(string gameId, string player, string password);
    RoundsResponse GetRounds(string gameId, string player, string password);
    SRoundsResponse GetRoundDetail(string gameId, string roundId, string player, string password);

}