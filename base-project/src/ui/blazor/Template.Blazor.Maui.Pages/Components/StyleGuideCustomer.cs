namespace Template.Blazor.Maui.Pages.Components;

/// <summary>
/// The fabricated entity the style guide's Page composition sections are built over.
/// </summary>
/// <remarks>
/// Deliberately not the real <c>Widget</c>: the guide's other sections already pose fictional
/// customers, and a specimen built on the sample resource reads as the sample screen rather than
/// as the pattern. Nothing outside the guide should use this.
/// </remarks>
public sealed record StyleGuideCustomer(
    string Id,
    string Name,
    string Region,
    int Orders,
    string LastOrder,
    string Status,
    string Owner);

/// <summary>
/// What the guide hands <c>StyleGuideDetailDialog</c>: the entity, plus which of the modal's
/// states to open in.
/// </summary>
/// <remarks>
/// The flags ride on the content because that is the only thing
/// <c>IDialogService.ShowDialogAsync</c> passes through to the content component - there is no
/// second parameter bag for them.
/// </remarks>
public sealed record StyleGuideDetailRequest(
    StyleGuideCustomer Customer,
    bool StartInEdit = false,
    bool ShowSaveFailure = false);
