using Core.Models.Types;
using Microsoft.AspNetCore.Mvc;

public interface IGameService
{
    ResponseJoin JoinGame(string gameId, string player, string password);
    ResponseStart StartGame(string gameId, string player, string password);
    RoundsResponse GetRounds(string gameId, string player, string password);
    SRoundsResponse GetRoundDetail(string gameId, string roundId, string player, string password);
    ResponseGameId GetGameById(string gameId, string player, string password);
    SRoundsResponse GetRoundById(string gameId, string roundId);
  //  bool HasGroupAlreadyProposed(string roundId, string phase);
    SRoundsResponse AddGroupRound(GroupRound groupRound);
    SRoundsResponse UpdateRound(DataRounds round);
    SRoundsResponse ProposeGroup(string gameId, string roundId, GroupRequest groupRequest, string password, string player);

}