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
        public ActionResult<SRoundsResponse> SubmitAction(
            [FromRoute] string gameId,
            [FromRoute] string roundId,
            [FromHeader(Name = "password")] string? password = null,
            [FromHeader(Name = "player")] string player = null,
            [FromBody] ActionRequest requestAction = null)
        {
            // Validar que el cuerpo de la solicitud no esté vacío
            if (requestAction == null)
            {
                return BadRequest(new ErrorResponse { status = 400, msg = "The body cannot be empty" });
            }
            if (!bool.TryParse(requestAction.action.ToString(), out bool action))
            {
                return BadRequest(new ErrorResponse { status = 400, msg = "Invalid or missing action" });
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
            var result = _gameService.SubmitAction(gameId, roundId, player, password, action);

            switch (result.status)
            {
                case 401:
                    return StatusCode(401, new ErrorResponse
                    {
                        status = 401,
                        msg = result.msg
                    });
                case 403:
                    return StatusCode(403, new ErrorResponse
                    {
                        status = 403,
                        msg = result.msg
                    });
                case 404:
                    return StatusCode(404, new ErrorResponse
                    {
                        status = 404,
                        msg = result.msg
                    });
                case 409:
                    return StatusCode(409, new ErrorResponse
                    {
                        status = 409,
                        msg = result.msg
                    });
                case 428:
                    return StatusCode(428, new ErrorResponse
                    {
                        status = 428,
                        msg = result.msg
                    });
            }

            // 200 okk
            return Ok(new SRoundsResponse
            {
                status = 200,
                msg = "Action registered",
                data = result.data
            });
        }
    }


}