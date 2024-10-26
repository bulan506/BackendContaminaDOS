using Microsoft.AspNetCore.Mvc;
using Core.Models.Types;
using Core.Models.Business;

namespace contaminaDOS.Controllers
{
    [ApiController]
    [Route("api/games/{gameId}/rounds/{roundId}")]
    public class ActionController : ControllerBase
    {
        private readonly IGameService _gameService;

        public ActionController(IGameService gameService)
        {
            _gameService = gameService;
        }

        // PUT /api/games/{gameId}/rounds/{roundId}
        [HttpPut]
        public ActionResult<ActionResponse> SubmitAction(
            [FromRoute] string gameId,
            [FromRoute] string roundId,
            [FromHeader(Name = "password")] string? password = null,
            [FromHeader(Name = "player")] string player = null,
            [FromBody] RequestAction requestAction = null)
        {
            // Validar que el cuerpo de la solicitud no esté vacío
            if (requestAction == null)
            {
                return BadRequest(new ErrorResponse { status = 400, msg = "The body cannot be empty" });
            }

            // Validar que el campo 'action' esté presente
           if (requestAction == null || !(requestAction.action is bool))
            {
                return BadRequest(new ErrorResponse { status = 400, msg = "The field 'action' cannot be empty" });
            }

            // Validar que los parámetros obligatorios estén presentes
            if (string.IsNullOrEmpty(player) || string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(roundId))
            {
                return BadRequest(new ErrorResponse
                {
                    status = 400,
                    msg = "Player, gameId, and roundId are required"
                });
            }

            // Llamada al servicio para procesar la acción del jugador
            var result = _gameService.SubmitAction(gameId, roundId, player, password, requestAction.action.Value);

            switch (result.status)
            {
                case 401:
                    return StatusCode(401, new ErrorResponse
                    {
                        status = 401,
                        msg = "Invalid credentials"
                    });
                case 403:
                    return StatusCode(403, new ErrorResponse
                    {
                        status = 403,
                        msg = "Not part of the game"
                    });
                case 404:
                    return StatusCode(404, new ErrorResponse
                    {
                        status = 404,
                        msg = "The specified resource was not found"
                    });
                case 409:
                    return StatusCode(409, new ErrorResponse
                    {
                        status = 409,
                        msg = "Action already registered"
                    });
                case 428:
                    return StatusCode(428, new ErrorResponse
                    {
                        status = 428,
                        msg = "This action is not allowed at this time"
                    });
            }

            // 200 okk
            return Ok(new ActionResponse
            {
                status = 200,
                msg = "Action registered",
                data = result.data
            });
        }
    }

   
}