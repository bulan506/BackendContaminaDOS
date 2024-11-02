using Microsoft.AspNetCore.Mvc;
using Core.Models.Types;

namespace contaminaDOS.controllers
{
    [ApiController]
    [Route("api/games/{gameId}/rounds/{roundId}")]
    public class VoteController : ControllerBase
    {
        private readonly IGameService _gameService;

        public VoteController(IGameService gameService)
        {
            _gameService = gameService;
        }

        [HttpPost]
        public async Task<ActionResult<SRoundsResponse>> Vote(
            [FromRoute] string gameId,
            [FromRoute] string roundId,
            [FromHeader(Name = "password")] string password = null,
            [FromHeader(Name = "player")] string player = null,
            [FromBody] RequestVote requestVote = null)
        {
            if (requestVote == null || requestVote.vote == null)
            {
                return BadRequest(new ErrorResponse { status = 400, msg = "The field vote cannot be empty" });
            }

            if (!bool.TryParse(requestVote.vote.ToString(), out bool vote))
            {
                return BadRequest(new ErrorResponse { status = 400, msg = "Invalid or missing vote" });
            }

            if (string.IsNullOrEmpty(player) || string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(roundId))
            {
                return BadRequest(new ErrorResponse
                {
                    status = 400,
                    msg = "Player, gameId, and roundId are required",
                    data = { }
                });
            }
            var result = await _gameService.VoteAsync(gameId, roundId, player, password, vote);
            switch (result.status)
            {
                case 401:
                    return StatusCode(401, new ErrorResponse
                    {
                        status = 401,
                        msg = "Invalid credentials",
                        data = { }
                    });
                case 403:
                    return StatusCode(403, new ErrorResponse
                    {
                        status = 403,
                        msg = "Not part of the game",
                        data = { }
                    });
                case 404:
                    return StatusCode(404, new ErrorResponse
                    {
                        status = 404,
                        msg = "The specified resource was not found",
                        data = { }
                    });
                case 409:
                    return StatusCode(409, new ErrorResponse
                    {
                        status = 404,
                        msg = result.msg,
                        data = { }
                    });
                case 428:
                    return StatusCode(result.status, new ErrorResponse
                    {
                        status = 404,
                        msg = "This action is not allowed at this time",
                        data = { }
                    });
            }

            return Ok(result);
        }
    }

}