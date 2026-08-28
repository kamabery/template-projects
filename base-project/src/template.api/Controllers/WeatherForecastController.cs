using Microsoft.AspNetCore.Mvc;
using template.data;
using template.data.Repositories;
using template.api.Services;

namespace template.api.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController(
    IRepository<WeatherForecast> forecasts,
    HarnessFaultInjector faults,
    ILogger<WeatherForecastController> logger) : ControllerBase
{
    [HttpGet(Name = "GetWeatherForecast")]
    [EndpointSummary("Get a five-day forecast")]
    [ProducesResponseType<IEnumerable<WeatherForecast>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<WeatherForecast>>> Get(CancellationToken cancellationToken)
    {
        // The injected-fault check is how both front ends' error banners and spinners get
        // exercised without unplugging the network. Inert unless /harness/* has armed it. It runs
        // before the read on purpose: an armed fault should not depend on the database being up.
        if (await faults.ShouldFailAsync(cancellationToken))
        {
            logger.LogWarning("Failing GET /WeatherForecast because a fault was injected.");

            return Problem(
                detail: "Injected failure from the test harness.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        // Read, not generate. The rows are seeded by WeatherSeeder at startup, so unlike the stock
        // WebAPI sample this returns the same forecast every time until the seed goes stale.
        return Ok(await forecasts.GetAllAsync(cancellationToken));
    }
}
