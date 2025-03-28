using Microsoft.AspNetCore.Mvc;
using Core.Models.Types;
using Core.Models.Business;
using MongoDB.Driver;

namespace contaminaDOS.controllers
{
    [ApiController]
    [Route("api/games")]
    public class GamesController : ControllerBase
    {
        private readonly IGameCreationService _gameCreationService;

        public GamesController(IGameCreationService gameCreationService)
        {
            _gameCreationService = gameCreationService;
        }

        [HttpPost]
        public async Task<ActionResult<ResponseCreate>> CreateGame([FromBody] RequestGame requestGame)
        {
            if (requestGame == null)
            {
                return BadRequest(new ErrorResponse { status = 400, msg = "The body cannot be empty" });
            }

            if (string.IsNullOrEmpty(requestGame.name) || string.IsNullOrWhiteSpace(requestGame.name))
            {
                return BadRequest(new ErrorResponse { status = 400, msg = "The fields name or owner cannot be empty" });
            }

            if (string.IsNullOrEmpty(requestGame.owner) || string.IsNullOrWhiteSpace(requestGame.owner))
            {
                return BadRequest(new ErrorResponse { status = 400, msg = "The fields name or owner cannot be empty" });
            }

            if (requestGame.name.Length < 3 || requestGame.name.Length > 20)
            {
                return BadRequest(new ErrorResponse { status = 400, msg = "Invalid or missing game name" });
            }

            if (!string.IsNullOrEmpty(requestGame.owner))
            {
                if (requestGame.owner.Length < 3 || requestGame.owner.Length > 20)
                {
                    return BadRequest(new ErrorResponse { status = 400, msg = "Invalid owner" });
                }
            }

            if (requestGame.password != null && string.IsNullOrWhiteSpace(requestGame.password))
            {
                return BadRequest(new ErrorResponse { status = 400, msg = "Invalid password" });
            }

            if (requestGame.password != null && (requestGame.password.Length < 3 || requestGame.password.Length > 20))
            {
                return BadRequest(new ErrorResponse { status = 400, msg = "Invalid password" });
            }

            var result = await _gameCreationService.CreateGameAsync(requestGame);

            if (result.status == 409)
            {
                return Conflict(new ErrorResponse { status = 409, msg = "Asset already exists" });
            }

            return CreatedAtAction(nameof(GetGame), new { gameId = result.data.id }, result);
        }

        [HttpGet("{gameId}")]
        public async Task<ActionResult<Game>> GetGame(
         [FromRoute] string gameId,
         [FromHeader(Name = "player")] string player = null,
         [FromHeader] string password = null)
        {
            var errorResponse = new ErrorResponse
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
            };

            if (string.IsNullOrEmpty(player))
            {
                return BadRequest(errorResponse);
            }

            if (player.Length < 3 || player.Length > 20)
            {
                return BadRequest(errorResponse);
            }

            if (!string.IsNullOrEmpty(password))
            {
                if (password.Length < 3 || password.Length > 20)
                {
                    return BadRequest(new ErrorResponse { status = 400, msg = "Invalid password" });
                }
            }

            var gameResponse = await _gameCreationService.GetGameAsync(gameId, player, password);

            if (gameResponse.status == 404)
            {
                return NotFound(new ErrorResponse { status = 404, msg = gameResponse.msg, data = null, others = new List<ErrorDetail> { new ErrorDetail { status = 404, msg = gameResponse.msg } } });
            }
            else if (gameResponse.status == 401)
            {
                return Unauthorized(new ErrorResponse { status = 401, msg = gameResponse.msg, data = null, others = new List<ErrorDetail> { new ErrorDetail { status = 401, msg = gameResponse.msg } } });
            }
            else if (gameResponse.status == 403)
            {
                return StatusCode(403, new ErrorResponse { status = 403, msg = gameResponse.msg, data = gameResponse.data, others = new List<ErrorDetail> { new ErrorDetail { status = 403, msg = gameResponse.msg } } });
            }
            return Ok(gameResponse);
        }



        [HttpGet]
        public async Task<ActionResult<IEnumerable<Game>>> SearchGames(
     [FromQuery] string name = null,
     [FromQuery] string? status = null,
     [FromQuery] string page = null,
     [FromQuery] string limit = null)
        {
            if (!string.IsNullOrEmpty(page) && !int.TryParse(page, out _))
            {
                var errorResponse = new ErrorResponse
                {
                    status = 400,
                    msg = "Invalid page number",
                    others = new List<ErrorDetail>
            {
                new ErrorDetail { status = 400, msg = "Invalid page number" }
            }
                };
                return BadRequest(errorResponse);
            }

            if (!string.IsNullOrEmpty(limit) && !int.TryParse(limit, out _))
            {
                var errorResponse = new ErrorResponse
                {
                    status = 400,
                    msg = "Invalid limit number",
                    others = new List<ErrorDetail>
            {
                new ErrorDetail { status = 400, msg = "Invalid limit number" }
            }
                };
                return BadRequest(errorResponse);
            }
            int parsedPage = string.IsNullOrEmpty(page) ? 0 : int.Parse(page);
            int parsedLimit = string.IsNullOrEmpty(limit) ? 50 : int.Parse(limit);
            if (!string.IsNullOrEmpty(name))
            {
                if (name.Length < 3 || name.Length > 20)
                {
                    return BadRequest(new ErrorResponse { status = 400, msg = "Invalid game name" });
                }
            }

            var result = await _gameCreationService.SearchGamesAsync(name, status, parsedPage, parsedLimit);

            return Ok(result);
        }
    }
}