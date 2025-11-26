using AgiloxSortingHall.Models;
using AgiloxSortingHall.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgiloxSortingHall.Controllers
{
    /// <summary>
    /// API controller přijímající callbacky od systému Agilox
    /// a předávající je dále do aplikační logiky.
    /// </summary>
    [ApiController]
    [Route("agilox")]
    public class AgiloxCallbackController : ControllerBase
    {
        private readonly AgiloxService _service;

        /// <summary>
        /// Inicializuje instanci controlleru s referencí na AgiloxService.
        /// </summary>
        public AgiloxCallbackController(AgiloxService service)
        {
            _service = service;
        }

        /// <summary>
        /// Přijme callback JSON payload z Agiloxu a předá ho ke zpracování.
        /// </summary>
        /// <param name="dto">Callback data o doručení palety.</param>
        /// <returns>HTTP 200 OK po úspěšném zpracování.</returns>
        [HttpPost("callback")]
        public async Task<IActionResult> Callback([FromBody] AgiloxCallbackDto dto)
        {
            await _service.ProcessCallbackAsync(dto);
            return Ok();
        }
    }
}
