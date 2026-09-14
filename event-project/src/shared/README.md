# `src/shared/` — linked source, not a project

These files are **compiled into each event-store host** via a `<Compile Include="..\shared\**\*.cs">`
glob, rather than living in a class library the hosts reference. There is no `.csproj` here on
purpose.

## Why linked source rather than a shared project

The obvious arrangement — a `template.events.domain` assembly that all three hosts reference — does
not work, and fails at run time rather than at build time.

`JasperFx.Events` dispatches an aggregate's conventional `Apply` / `Create` methods through a
**compile-time source generator**; there is no runtime fallback. The dispatcher it has to emit is for
the store's own closed generic — `Polecat.Projections.SingleStreamProjection<Widget, Guid>`,
`Fisher.Projections.SingleStreamProjection<Widget, Guid>`, and so on. A generator only ever sees the
compilation it is referenced from, so emitting that type requires **the aggregate and the event store
to be in the same compilation**. Put `Widget` in its own assembly and the generator has the aggregate
but no store; put it in the host and every host needs its own copy.

Linking the source is what satisfies both. `Widget` is written once, here, and compiled separately
into each host alongside that host's store — so each compilation has everything the generator needs,
and there is still exactly one copy of the code to maintain.

The symptom of getting this wrong, for whoever hits it next: a clean build, a green test run, and
then `InvalidProjectionException: No source-generated dispatcher found for
…SingleStreamProjection<Widget, Guid>` on the first command that calls `FetchForWriting`.

## Why the controller is here too

`WidgetsController` needs exactly one thing from an event store — a document load by id — so it takes
`IWidgetSummaries` and each host supplies the ten-line implementation in its own `Storage/` folder.
That keeps the routes, the status codes and the eventual-consistency behaviour in one place.

## What is here, and what is not

Here: the aggregate and its invariant, the events and commands, the decision logic every host's
handlers wrap, the versioned contract, the read model, the integration-event consumer, the
controller, and the one abstraction it needs.

Not here: anything that names an event store. `Program.cs`, both command handlers, the projection
type and the `IWidgetSummaries` implementation live in each host, because each of them spells a
store-specific type — `IDocumentSession`, `[AggregateHandler]`, `Events`,
`SingleStreamProjection<,>`, `IQuerySession`. They are thin adapters over what is in this folder.
