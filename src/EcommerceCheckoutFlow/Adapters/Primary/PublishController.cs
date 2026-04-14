using DotNetCore.CAP;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceCheckoutFlow.Adapters.Primary;

[ApiController]
public sealed class PublishController : ControllerBase
{
    [HttpGet("~/send")]
    public IActionResult SendMessage([FromServices] ICapPublisher capBus)
    {
        capBus.Publish("test.show.time", DateTime.Now);
        return Ok();
    }

    [HttpGet("~/send/delay")]
    public IActionResult SendDelayMessage([FromServices] ICapPublisher capBus)
    {
        capBus.PublishDelay(TimeSpan.FromSeconds(100), "test.show.time", DateTime.Now);
        return Ok();
    }
}
