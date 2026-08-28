using System.ComponentModel.DataAnnotations.Schema;

namespace template.data;

/// <summary>
/// The forecast as it is both stored and served.
/// </summary>
/// <remarks>
/// One type doing both jobs is deliberate for a scaffold this size, but it has a visible
/// consequence: <see cref="Id"/> is the EF key and it is therefore also on the wire and in the
/// OpenAPI schema. Neither front end reads it - the React interface mirrors it for accuracy and the
/// Blazor record drops it - but a real service that wanted the persistence key kept private would
/// split this into an entity and a DTO here.
/// </remarks>
public class WeatherForecast
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public int TemperatureC { get; set; }

    /// <remarks>
    /// Computed, never stored. EF's convention already skips a getter-only expression-bodied
    /// property, so [NotMapped] is belt-and-braces - it is here to stop someone adding a setter
    /// and silently creating a column that then disagrees with TemperatureC.
    /// <para>The arithmetic is wrong on purpose, carried over verbatim from the stock WebAPI
    /// sample: dividing by 0.5556 rather than multiplying by 9/5 truncates, so 25 C renders as
    /// 76 F, not 77. The Blazor client recomputes it identically and three tests assert it. Fix it
    /// on both sides or not at all.</para>
    /// </remarks>
    [NotMapped]
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    public string? Summary { get; set; }
}
