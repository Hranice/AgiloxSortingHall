using AgiloxSortingHall.Models;
using AgiloxSortingHall.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgiloxSortingHall.Controllers
{
    [ApiController]
    [Route("agilox")]
    public class AgiloxCallbackController : ControllerBase
    {
        private readonly AgiloxService _service;

        public AgiloxCallbackController(AgiloxService service)
        {
            _service = service;
        }

        [HttpPost("callback")]
        public async Task<IActionResult> Callback([FromBody] AgiloxCallbackDto dto)
        {
            await _service.ProcessCallbackAsync(dto);
            return Ok();
        }
    }
}
