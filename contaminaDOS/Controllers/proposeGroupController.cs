using Microsoft.AspNetCore.Mvc;
using Core.Models.Types;
using System;
using System.Text.Json;

namespace contaminaDOS.Controllers
{
    [ApiController]
    [Route("api/games/{gameId}")]
    public class ProposeGroupController : ControllerBase
    {
        private readonly IGameService _gameService;

        public ProposeGroupController(IGameService gameService)
        {
            _gameService = gameService ?? throw new ArgumentNullException(nameof(gameService));
        }

        [HttpPatch("rounds/{roundId}")]
        public async Task<ActionResult<SRoundsResponse>> ProposeGroup(
            [FromRoute] string gameId,
            [FromRoute] string roundId,
            [FromBody] GroupRequest groupRequest,
            [FromHeader(Name = "password")] string password = null,
            [FromHeader(Name = "player")] string player = null)
        {
            // Llamada al servicio para manejar la lógica de proponer el grupo
            var response = await _gameService.ProposeGroupAsync(gameId, roundId, groupRequest, password, player);

            // Retornar la respuesta del servicio
            return StatusCode(response.status, response);
        }
    }
}
