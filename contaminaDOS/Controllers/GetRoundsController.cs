using Microsoft.AspNetCore.Mvc;
using Core.Models.Types;
using Core.Models.Business;
using MongoDB.Driver;
using Microsoft.AspNetCore.Http.HttpResults;

namespace contaminaDOS.controllers
{
    [ApiController]
    [Route("api/games/{gameId}")]
    public class RoundsController : ControllerBase
    {
        private readonly IGameService _gameService;

        public RoundsController(IGameService gameService)
        {
            _gameService = gameService;
        }

        [HttpGet("rounds")]
        public async Task<ActionResult<RoundsResponse>> GetRoundsController(
       [FromRoute] string gameId,
       [FromHeader(Name = "player")] string player = null,
       [FromHeader] string password = null)
        {
            if(string.IsNullOrEmpty(gameId)){
                return BadRequest(new ErrorResponse { status = 400, msg = "The field gameId cannot be empty" });
            }
            
            // Validación del jugador
            if (string.IsNullOrEmpty(player) || player.Length < 3 || player.Length > 20)
            {
                return BadRequest(new ErrorResponse
                {
                    status = 400,
                    msg = "Invalid or missing player name",
                    data = null,
                    others = new List<ErrorDetail>
            {
                new ErrorDetail
                {
                    status = 400,
                    msg = "Invalid or missing player name"
                }
            }
                });
            }

            if(!string.IsNullOrEmpty(password)){
                if(password.Length < 3 || password.Length > 20){
                    return BadRequest(new ErrorResponse { status = 400, msg = "Invalid password" });
                }
            }

            var roundsResponse = await _gameService.GetRoundsAsync(gameId, player, password);

            if (roundsResponse.status != 200)
            {
                return StatusCode(roundsResponse.status, roundsResponse);
            }
            return Ok(roundsResponse);
        }



        [HttpGet("rounds/{roundId}")]
        public async Task<ActionResult<SRoundsResponse>> GetRoundsDetailsController(
    [FromRoute] string gameId = null,
    [FromRoute] string roundId = null,
    [FromHeader(Name = "player")] string player = null,
    [FromHeader] string password = null)
        {
            if(string.IsNullOrEmpty(gameId)){
                return BadRequest(new ErrorResponse { status = 400, msg = "The field gameId cannot be empty" });
            }

            if(string.IsNullOrEmpty(roundId)){
                return BadRequest(new ErrorResponse { status = 400, msg = "The field roundId cannot be empty" });
            }

            if (string.IsNullOrEmpty(player) || player.Length < 3 || player.Length > 20)
            {
                return BadRequest(new ErrorResponse
                {
                    status = 400,
                    msg = "Invalid or missing player name",
                    data = null
                });
            }

            if(!string.IsNullOrEmpty(password)){
                if(password.Length < 3 || password.Length > 20){
                    return BadRequest(new ErrorResponse { status = 400, msg = "Invalid password" });
                }
            }

            var roundDetailResponse = await _gameService.GetRoundDetailAsync(gameId, roundId, player, password);

            return StatusCode(roundDetailResponse.status, roundDetailResponse);
        }

    }
}