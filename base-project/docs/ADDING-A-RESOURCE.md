# Adding a resource

The scaffold ships with one sample resource — `Widget` — wired end to end. This is the ordered
recipe for the second one, and for anyone (or anything) replacing the first.

**The API is the contract.** `contracts/api.openapi.json` is committed, and both front ends
generate their types from it. Nothing is hand-written twice. Follow the order below and the
generated halves stay in agreement; skip step 4 and they drift silently.

```
src/template.data/Widget.cs                 the entity, and the wire shape
        │
        ├── DbSet on AppDbContext + a migration per provider
        │
src/template.api/Controllers/WidgetsController.cs   [ProducesResponseType<T>] is load-bearing
        │
        ↓  dotnet test  (UPDATE_CONTRACT=1 to rewrite)
contracts/api.openapi.json                  ← the single source of truth
        │
        ├── npm run api:types    → react/src/lib/api.generated.ts
        └── dotnet refitter      → blazor/…Pages/Providers/ApiClient.generated.cs
```

---

## 1. The entity

`src/template.data/Widget.cs` is the worked example. Copy it.

```csharp
public class Invoice
{
    public int Id { get; set; }                  // not `required`: the database assigns it
    public required string Reference { get; set; }
    public required decimal Amount { get; set; }

    [NotMapped]
    public bool IsOverdue => /* … */;
}
```

**`required` is not decoration.** It reaches the OpenAPI schema as a required property, which is
what makes the field non-optional in the generated TypeScript. Leave it off and every consumer
gets `string | undefined` and has to narrow a value that is never actually absent.

**`[NotMapped]` on a computed property** keeps it off the table while keeping it on the wire —
so the clients get the computed value rather than recomputing it and disagreeing.

Then add the `DbSet` and any index to `src/template.data/AppDbContext.cs`.

*Verify:* `dotnet build template.slnx`

## 2. The migration — all providers, or none

From `base-project/` (the scaffold root):

```bash
dotnet ef migrations add AddInvoice --project src/template.api.migrations.postgres  --startup-project src/template.api -- --provider postgres
dotnet ef migrations add AddInvoice --project src/template.api.migrations.sqlserver --startup-project src/template.api -- --provider sqlserver
dotnet ef migrations add AddInvoice --project src/template.api.migrations.sqlite    --startup-project src/template.api -- --provider sqlite
```

A scaffold has only the provider it asked for, so run the one that exists. In this workspace run
all three: nothing fails until someone scaffolds a provider whose migrations were never written.

## 3. The endpoint

`src/template.api/Controllers/WidgetsController.cs` is the worked example. Three attributes decide
what the generated clients look like:

| Attribute | What it produces |
| --- | --- |
| `[Route("[controller]")]` | the path — `/Invoices` |
| `[HttpGet(Name = "GetInvoices")]` | the OpenAPI `operationId`, and so the generated method name |
| `[ProducesResponseType<T>(200)]` | the **schema**. Without it the response is a bare 200 and the generated client is untyped |

No DI registration is needed: `IRepository<T>` is registered open-generic, so `IRepository<Invoice>`
resolves on its own.

`ContractTests.ServedDocument_GivesEverySuccessResponseASchema` fails the build if you forget the
third one. That is deliberate — the failure it prevents is otherwise invisible until someone tries
to use the generated member.

*Verify:* `dotnet test tests/template.api.tests`

## 4. Regenerate the contract, then the clients

```bash
# 1. rewrite contracts/api.openapi.json from what the API actually serves
UPDATE_CONTRACT=1 dotnet test tests/template.api.tests --filter "Category=ContractTest"

# 2. React types
cd src/ui/react && npm run api:types

# 3. Blazor Refit client + DTOs
cd src/ui/blazor/Template.Blazor.Maui.Pages && dotnet tool restore && dotnet refitter --settings-file .refitter
```

Commit all three generated files. They are committed rather than built so that neither front end
needs a running API, a database or the other toolchain in order to build — the same bargain
`routeTree.gen.ts` already makes.

`ContractTests.CommittedContract_MatchesTheServedDocument` fails if the committed document and the
API disagree, so a forgotten step 4.1 is caught by `dotnet test`. **Nothing catches a forgotten
4.2 or 4.3** except the compiler noticing the member is missing — which it will, as soon as you use
it in step 5.

## 5. The UI

Only one front end is present in a scaffold. Do the one you have.

### React

| Step | File |
| --- | --- |
| Re-export the generated type and add a fetch function | `src/ui/react/src/lib/api.ts` |
| A live collection, if several components share the data | `src/ui/react/src/db/collections.ts` |
| The page — the path *is* the URL | `src/ui/react/src/routes/invoices.tsx` |
| A nav entry | `src/ui/react/src/routes/__root.tsx` |

`src/lib/api.ts` re-exports rather than redeclares:

```ts
export type Invoice = components['schemas']['Invoice']
```

Keep the `/api` prefix — Vite proxies it to the API, which is what keeps the browser same-origin
and `traceparent` flowing into the API's spans.

After adding a route file, run `npx vite build` (or `npm run dev`) once before `npm run typecheck`:
`routeTree.gen.ts` is written by the router plugin and `tsc` fails until it has run.

*Verify:* `npm run typecheck && npm run lint && npm run build`

### Blazor

| Step | File |
| --- | --- |
| *(nothing — `IApi` and the DTO are generated)* | `Providers/ApiClient.generated.cs` |
| A ViewModel | `ViewModels/InvoicesViewModel.cs` |
| Register it | `Mvvm/MvvmServiceCollectionExtensions.AddSharedViewModels` |
| The page, `@inherits ViewModelComponentBase<T>` | `Pages/Invoices.razor` |
| A nav entry | `Layout/NavMenu.razor` |

There is no `StateHasChanged` anywhere and there must not be: `[ObservableProperty]` raises
`PropertyChanged` and `ViewModelBinder` re-renders. **Swap collections, never mutate them** —
nothing listens to `CollectionChanged`.

A page with no state of its own needs neither a ViewModel nor a registration.

*Verify:* `dotnet test tests/Template.Blazor.Maui.Tests`

## 6. The design system is not optional

New UI composes from the approved components — `src/ui/react/src/components/ui/` on one side,
FluentUI on the other — and uses the token vocabulary. No raw colours, radii or shadows.
[`docs/DESIGN.md`](DESIGN.md) is the contract, including the rule that a new variant belongs in
the design system and in **both** `/styleguide` pages before it belongs in a screen. (That file
arrives with a front end, so a scaffold that has no UI yet has no copy of it and no need for one.)

---

## Renaming the sample instead of adding to it

A scaffold is created with `--resource <Name>`, so the sample already carries your noun. If you
want a different one afterwards it is an ordinary rename — the name appears in the entity,
the controller, the seeder, the page, the tests and the generated files, and nothing depends on
it being what it is.

Two notes:

- **The plural is mechanically the singular plus `s`.** `--resource Person` scaffolds `Persons`.
  Fix it with a rename; there is deliberately no second parameter for it.
- **Regenerate after renaming** (step 4). The committed contract still describes the old name
  until you do, and `dotnet test` will say so.
