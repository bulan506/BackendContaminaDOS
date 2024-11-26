using Microsoft.AspNetCore.Mvc;
using Core.Models.Types;
using Core.Models.Business;

namespace contaminaDOS.Controllers
{
    [ApiController]
    [Route("api/games")]
    public class GameStartController : ControllerBase
    {
        private readonly IGameService _gameService;

        public GameStartController(IGameService gameService)
        {
            _gameService = gameService;
        }

        [HttpHead("{gameId}/start")]
        public async Task<IActionResult> StartGame(
            [FromRoute] string gameId,
            [FromHeader(Name = "player")] string player,
            [FromHeader(Name = "password")] string password = null)
        {
            if (string.IsNullOrEmpty(gameId))
            {
                return CreateErrorResponse("GameId is required", 400);
            }

            if (!string.IsNullOrEmpty(player))
            {
                if (player.Length < 3 || player.Length > 20)
                {
                    return CreateErrorResponse("Invalid player name", 400);
                }
            }
            else
            {
                return CreateErrorResponse("Player is required", 400);
            }

            if (!string.IsNullOrEmpty(password))
            {
                if (password.Length < 3 || password.Length > 20)
                {
                    return CreateErrorResponse("Invalid password", 400);
                }
            }

            var result = await _gameService.StartGameAsync(gameId, player, password);

            return result.status switch
            {
                200 => Ok(),
                404 => CreateErrorResponse("Game not found", 404),
                403 => CreateErrorResponse("Forbidden", 403),
                401 => CreateErrorResponse("Unauthorized", 401),
                409 => CreateErrorResponse("Game already started", 409),
                428 => CreateErrorResponse("Need 5 players to start", 428),
                _ => CreateErrorResponse("An unexpected error occurred", 500)
            };
        }

        private IActionResult CreateErrorResponse(string message, int statusCode)
        {
            Response.Headers.Add("x-msg", message);
            return StatusCode(statusCode);
        }
    }
}
