using MassTransit;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace WebApplication2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NotifyController : ControllerBase
    {
        private readonly IBus _publishEndpoint;

        public NotifyController(IBus publishEndpoint)
        {
            _publishEndpoint = publishEndpoint;
        }

        [HttpPost]
        public async Task<IActionResult> Post(Testes testes) 
        {
            await _publishEndpoint.Publish(testes);

            return Ok(testes);
        }
    }

    public class Testes {

        public string Message { get; set; }
    }
}
