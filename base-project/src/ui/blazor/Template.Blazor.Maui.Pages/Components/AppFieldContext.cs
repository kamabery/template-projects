namespace Template.Blazor.Maui.Pages.Components;

/// <summary>
/// What <c>AppField</c> hands to the control it wraps: the generated id the label points at, the
/// ids the control is described by, and whether it should render as invalid.
/// </summary>
/// <remarks>
/// The control spreads these itself rather than AppField reaching into it, because FluentUI inputs
/// are components and not elements - there is nothing to decorate from the outside.
/// </remarks>
/// <param name="ControlId">Value for the control's <c>Id</c>. The label's <c>for</c> names it.</param>
/// <param name="DescribedBy">
/// Value for <c>aria-describedby</c>, naming the description and - while invalid - the error.
/// Empty when the field has neither, so the attribute is omitted rather than pointing at nothing.
/// </param>
/// <param name="Invalid">Whether the control should render its invalid state.</param>
public sealed record AppFieldContext(string ControlId, string DescribedBy, bool Invalid);
