using Microsoft.AspNetCore.Mvc;
using Core.Models.Types;
using Core.Models.Business;

namespace contaminaDOS.Controllers
{
    [ApiController]
    [Route("api/games")]
    public class GameJoinController : ControllerBase
    {
        private readonly IGameService _gameService;

        public GameJoinController(IGameService gameService)
        {
            _gameService = gameService;
        }

        [HttpPut("{gameId}")]
        public async Task<ActionResult<ResponseJoin>> JoinGame(
            [FromRoute] string gameId,
            [FromHeader(Name = "player")] string playerFromHeader = null,
            [FromHeader(Name = "password")] string passwordFromHeader = null,
            [FromBody] JoinRequest requestBody = null)
        {
            // Validar si se proporcionó el jugador desde el header o el cuerpo de la solicitud
            var player = playerFromHeader ?? requestBody?.Player;
            var password = passwordFromHeader;

            // Validación: Si el jugador no está especificado, se requiere
            if (string.IsNullOrEmpty(player))
            {
                return BadRequest(new ErrorResponse
                {
                    status = 400,
                    msg = "Player is required",
                    data = { }
                });
            }

            // Lógica de unión al juego usando el servicio
            var result = await _gameService.JoinGameAsync(gameId, player, password);

            // Manejar los códigos de estado según el resultado de la unión al juego
            switch (result.status)
            {
                case 404:
                    return NotFound(new ErrorResponse
                    {
                        status = 404,
                        msg = "Game not found",
                        data = { }
                    });
                case 401:
                    return Unauthorized(new ErrorResponse
                    {
                        status = 401,
                        msg = "Invalid credentials",
                        data = { }
                    });
                case 409:
                    return Conflict(new ErrorResponse
                    {
                        status = 409,
                        msg = result.msg,
                        data = { }
                    });
                case 428:
                    return StatusCode(428, new ErrorResponse
                    {
                        status = 428,
                        msg = "Precondition Required",
                        data = { }
                    });
            }

            // Si el estado es exitoso (200), devolver la respuesta de unión al juego
            return Ok(new ResponseJoin
            {
                status = 200,
                msg = "Joinned successfuly",
                data = result.data
            });
        }
    }
}
