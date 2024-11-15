using Core.Models.Types;
using Microsoft.AspNetCore.Mvc;

public interface IGameService
{
  Task<ResponseJoin> JoinGameAsync(string gameId, string player, string password);
  Task<ResponseStart> StartGameAsync(string gameId, string player, string password);
  Task<RoundsResponse> GetRoundsAsync(string gameId, string player, string password);
  Task<SRoundsResponse> GetRoundDetailAsync(string gameId, string roundId, string player, string password);
  Task<SRoundsResponse> ProposeGroupAsync(string gameId, string roundId, GroupRequest groupRequest, string password, string player);
  Task<SRoundsResponse> VoteAsync(string gameId, string roundId, string player, string password, bool vote);
  Task<SRoundsResponse> SubmitActionAsync(string gameId, string roundId, string player, string password, bool action);
}