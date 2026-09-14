using System.ComponentModel.DataAnnotations.Schema;

namespace template.data;

/// <summary>
/// The sample resource, as it is both stored and served.
/// </summary>
/// <remarks>
/// <para>This is the one place a scaffolded project is meant to start editing. Rename it, change
/// its properties, or delete it and add your own - <c>docs/ADDING-A-RESOURCE.md</c> is the ordered
/// recipe for the second one.</para>
/// One type doing both jobs is deliberate for a scaffold this size, but it has a visible
/// consequence: <see cref="Id"/> is the EF key and it is therefore also on the wire and in the
/// OpenAPI schema, which is where both front ends get their types from. A real service that wanted
/// the persistence key kept private would split this into an entity and a DTO here.
/// </remarks>
public class Widget
{
    /// <summary>Database-generated, so it is deliberately not <c>required</c>: a caller creating
    /// one does not supply it, and the OpenAPI schema marks it optional to match.</summary>
    public int Id { get; set; }

    // `required` is doing real work here rather than documenting intent. It reaches the OpenAPI
    // schema as a required property, which is what makes these non-optional in the generated
    // TypeScript instead of `string | undefined` - see docs/ADDING-A-RESOURCE.md.
    public required string Name { get; set; }

    public required int Quantity { get; set; }

    /// <summary>When this row last changed. The seeder treats it as recency - see WidgetSeeder.</summary>
    public required DateOnly UpdatedOn { get; set; }

    /// <remarks>
    /// Computed, never stored. EF's convention already skips a getter-only expression-bodied
    /// property, so [NotMapped] is belt-and-braces - it is here to stop someone adding a setter
    /// and silently creating a column that then disagrees with <see cref="Quantity"/>.
    /// </remarks>
    [NotMapped]
    public bool IsLowStock => Quantity < LowStockThreshold;

    /// <summary>The line between low stock and not. Arbitrary; it exists to give the computed
    /// property above something to compute.</summary>
    public const int LowStockThreshold = 5;
}
