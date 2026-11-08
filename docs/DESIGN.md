# DESIGN.md — the design system, and the rules that keep it one system

This is the owner of the design doctrine for this workspace and for every project scaffolded from it.
`CLAUDE.md` links here rather than restating it. If a rule below is wrong, fix it **here** — a second
copy is the drift this file exists to prevent.

The system is not a document. It is a **token vocabulary**, a **set of approved components**, and a
**living style guide per front end** at `/styleguide` that renders every one of them. This file is the
part a person or an agent reads before touching a screen.

---

## 1. Where it lives

| Piece | React | Blazor |
|---|---|---|
| Tokens | `src/ui/react/src/index.css` (Tailwind v4 `@theme`; there is no `tailwind.config.js`) | `src/ui/blazor/Template.Blazor.Maui.Pages/wwwroot/app.css` (aliases onto FluentUI's variables) |
| Components | `src/ui/react/src/components/ui/` | FluentUI, plus `Template.Blazor.Maui.Pages/Components/AppField.razor` |
| Style guide | `src/ui/react/src/routes/styleguide.tsx` | `Template.Blazor.Maui.Pages/Pages/StyleGuide.razor` |
| Guide chrome | `src/components/styleguide/parts.tsx` | `Components/StyleGuideSection.razor`, `StyleGuideRow.razor`, `Swatch.razor`, `StyleGuideDetailDialog.razor` |
| Guard test | none — that direction is on you | `tests/Template.Blazor.Maui.Tests/Pages/StyleGuidePageTests.cs` |

Both guides ship in **every** configuration, not gated on Development. A `@page` cannot be gated as
cheaply as a React route, and gating only the React one would let the two drift.

The Blazor token stylesheet is served from the RCL as a static web asset
(`_content/Template.Blazor.Maui.Pages/app.css`), which is what keeps the MAUI and WebAssembly hosts
visually identical rather than merely similar.

## 2. The token vocabulary

One set of names, two implementations, no build step between them.

| Group | Names |
|---|---|
| Surface and content | `surface`, `surface-raised`, `surface-sunken`, `border-subtle`, `border-strong`, `content`, `content-muted`, `content-disabled` |
| Brand | `brand`, `brand-subtle`, `brand-contrast` |
| Intents | `intent-{info,success,warning,danger}`, each with `-subtle` and `-contrast` |
| Focus | `focus-ring` — its own token, because the focus ring is not the brand, it only looked like it |
| Shape | `radius-control`, `radius-surface`, `radius-pill` |
| Elevation | `shadow-raised`, `shadow-overlay` |
| Motion | `--motion-fast`, `--motion-slow` |

An intent is a **triple**, not a colour: the solid fill, a tinted background for banners and badges,
and the text colour that sits on the solid fill. Reaching for the fill where you needed `-subtle` is
the commonest way a banner ends up unreadable.

**React** carries both palettes in one declaration through `light-dark()`, resolved against the
`color-scheme` on `:root`. That is why there is no second dark block to keep in sync, and why `@theme`
not being nestable in a media query does not matter. `src/store/preferences.ts` writes
`<html data-theme>`, and the attribute selectors outrank the bare `:root` rule so the override wins in
both directions.

**Blazor** aliases the same names onto FluentUI's own variables (`--neutral-layer-1`,
`--accent-fill-rest`, `--error`, …), each with a literal fallback. `FluentDesignTheme` rewrites those
when the theme flips, so the alias layer follows for free — the same property the React side gets from
`light-dark()`. The `-subtle` backgrounds have no Fluent equivalent and are `color-mix`ed from the fill
and the current surface.

## 3. The rules

**Never a raw value.** Not `bg-slate-800`, and not `rounded-lg` or `shadow-sm` either. A raw colour has
exactly one value, so it breaks whichever theme it was not picked for; a literal radius or shadow drifts
the moment one component is restyled. This includes an inline `style=` attribute carrying a colour a
server handed you — pass it through a token or a `color-mix`, or it will not follow the theme and
nothing will tell you. On the React side `npm run lint` catches raw palette colours, radii and
shadows in class names (`scripts/check-design-tokens.mjs`; the style guide and a dialog's
`backdrop:bg-black/NN` scrim are exempt). It cannot see an inline `style=` or a value built in code.

**A class name is not a style.** Markup that emits `.kanban-board` and a stylesheet that never defines
it both look finished, and every test still passes. If you emit a class, something must define it — a
token-built utility, or a rule in the stylesheet. This is the single defect that made a previous build's
UI unusable: 43 of its 49 emitted class names had no rule anywhere in the repo, so a board that was
meant to be four columns stacked vertically instead.

**Adding a token means adding it to both stylesheets.** They declare the same names by hand and nothing
catches a drift. The whole point of the vocabulary is that "the danger button" means one thing on both
sides.

**A retuned fill needs its pair re-measured.** Every fill/foreground pair was checked against WCAG AA in
both palettes; the worst is 5.02:1 light and 6.31:1 dark against a 4.5 threshold. The trap is real, not
theoretical: `--color-brand` at the 58% lightness that read better measured 4.2:1 — just under — so it
sits at 51%. On the Blazor side, white on Fluent's accent measures 1.87:1 in the dark theme, which is
why the `-contrast` tokens there are *derived* from their own fill's lightness with `oklch(from …)`
rather than written as literals, each with a literal fallback declared first for an engine without
relative colour syntax. **Re-measure the pair; do not eyeball it.**

**Motion is not in `@theme`.** Tailwind v4 has no duration namespace, so `--motion-fast` and
`--motion-slow` sit on `:root` and are read as `duration-[var(--motion-fast)]`. They collapse to `1ms`
under `prefers-reduced-motion`, so no component opts in individually.

**Colour never carries meaning alone.** Every intent is paired with a word or a glyph wherever it is
used. A status swatch with no text beside it is a defect, not a design.

**SVG has no useful defaults.** A `<rect>` with no `fill` is a black slab; a `<polyline fill="none">`
with no `stroke` is invisible. Neither is an error to any compiler, linter or test. Every mark you draw
declares its `fill` and its `stroke`, from the token palette.

**Nothing is fixed-width.** A chart sized `width="640"` in code, with no `width="100%"` and no
`preserveAspectRatio`, is unusable on the phone this app also ships to.

## 4. Page composition - the three archetypes

Sections 2 and 3 say what a *control* may look like. This one says what a *screen* is made of, and it
is the half a component library cannot enforce: every screen below can be built entirely from
approved components, out of tokens, with no raw value anywhere, and still be the wrong screen.

There are **three** archetypes, and every route in an application of this shape is one of them:

| Archetype | Route | What it answers |
|---|---|---|
| **Dashboard** | `/` - the home route, and only it | "What is the state of my data right now?" |
| **List** | one per entity collection (`/widgets`) | "Show me all of them, filtered and sorted my way." |
| **Detail** | **a modal over the list**, addressed by the URL | "Tell me everything about this one." |

And one anti-archetype, which is what gets reached for unprompted and is wrong here: a **full-page
detail route you navigate away to**, abandoning the list and everything the user set up on it.

### 4.1 The home route is a dashboard

**Home is a complete view of the entities, not a description of the application.** A home page that
explains the stack, welcomes the user, or lists the libraries in use is a README that got rendered.
The sample home page in this workspace is currently exactly that, and it is the thing this section
exists to stop being copied - see *4.5 What this workspace does and does not yet show*.

The rules:

- **Every entity the application owns appears on it.** Its count, the one number that matters about
  it, and a short panel of its most recent or most urgent rows. "Complete view of the entities" is
  the acceptance test: name every entity type, then find each one on the dashboard. An entity with
  nowhere to appear is either not a first-class entity or the dashboard is unfinished.
- **Composition, in this order**: a row of stat tiles, then panels - one per entity, or one per
  question the user opens the application to ask. Each panel has a heading, **at most five to seven
  rows**, and a link to the full list. A panel that wants pagination is a list route wearing a
  panel's clothes.
- **Every number is a way in.** A tile the user cannot click through to the rows behind it is a
  poster. A tile navigates to its list route pre-filtered to exactly the rows it counted - a tile
  reading "3 low stock" that lands on an unfiltered list has lied about what it counted.
- **A panel row opens the same detail modal the list opens.** The dashboard owns no editing of its
  own; it is a set of shortcuts into the two archetypes below. A second edit form here is how two
  forms for one entity start to disagree.
- **A tile is a number, a label and a qualifier.** If it needs axes it is a panel, not a tile. Order
  the tiles by how often they are acted on, never alphabetically.
- **Panels load independently.** Each owns its own query and its own skeleton; the dashboard never
  blocks on the slowest one. A panel that fails shows an inline alert *in that panel*, with the rest
  of the page still usable - a whole-page error because one query timed out makes the dashboard less
  available than the sum of its parts.
- **First run is a designed state, not an empty one.** With no data, each panel says what to create
  and carries the control that creates it. "No widgets yet." on its own is the loading state with the
  spinner taken out.
- **Nothing on it is decoration.** No framework blurb, no stack list, no hero copy. That material
  belongs in `README.md`, which is where someone looking for it will look.

### 4.2 The list owns its rows; the detail is a modal over it

**Selecting a row opens the detail in front of the list, and the list stays exactly where it was.**
That is the whole justification, and it is worth stating plainly: a list carries state the user built
by hand - scroll position, filters, sort, page, density, a search they typed. A detail *page*
discards all of it and offers the back button as the apology. A modal keeps every bit of it, and
returning is Esc.

- **The row is the trigger.** Not a "View" link in the last column, and not a chevron. The whole row
  is one activation target, and its accessible name is the row's own identity - "Ada Lovelace", not
  "View". A trailing link makes the other 95% of the row dead pixels that look clickable. The way to
  get both - one real control *and* a row-sized hit target - is a `<button>` in the identity cell
  stretched over the row by an `::after` overlay on a `position: relative` row. Each guide's *List
  with modal detail* section is the worked example; `.row-trigger` in the Blazor stylesheet and the
  `after:absolute after:inset-0` utilities in the React one are the same technique twice.
- **Secondary actions in a row must stop propagation.** A row with an overflow menu or a delete
  button is fine; a row where clicking delete *also* opens the detail behind the confirmation is the
  defect this rule exists for.
- **The open detail is in the URL, not in component state.** Refresh, deep link, share and the back
  button all have to work, and they only work if the identity of the open row is addressable. See
  4.4 for how each front end spells it. A `selectedId` in a `useState` or a ViewModel property is the
  shortcut that breaks all four at once, and breaks none of them in a test.
- **Back closes the modal and returns to the list**, it does not leave the list route; forward
  reopens it. That falls out of putting the state in the URL, and is the reason to do so.
- **Focus returns to the row that opened it** when the modal closes by any route - Esc, the close
  button, the backdrop, or Back. Without it a keyboard user is dropped at the top of the document and
  has to travel the whole list again. This one is contractual; see 4.6.
- **A loading list is skeleton rows inside the table**, keeping the header and the column widths -
  not a centred spinner that collapses the layout and reflows it when the data lands.
- **One presentation per entity.** An entity that has a modal detail does not also have a detail
  page. Two answers to the same question drift.

**When the detail is a route instead - the honest exceptions.** Take one when it applies; do not take
one because it is easier:

1. The detail is itself a **workspace**, with its own sub-navigation - tabs, nested lists, an editor,
   a conversation. A modal that needs tabs is a page.
2. The detail is the **primary destination**, where the user spends minutes rather than seconds, and
   the list is only the way in.
3. The content genuinely **does not fit**: more than roughly one screen at 400px wide, or it needs
   two columns.

Then it is a route, with a breadcrumb back to the list. Note what does *not* change: it is still
addressed by the URL, and the list still has to restore its own state when the user returns. The
exception changes the container, not the contract.

### 4.3 The detail modal's anatomy

Header, body, footer - and each has a rule that is usually got wrong.

- **The header is the entity's identity**, plus its status as a badge. Never the word "Details", and
  never "Edit widget" while the body is read-only. The dialog's accessible name is that identity,
  which is what a screen reader announces on open; "Details" announces nothing.
- **The body is read-first.** Label/value pairs, most-identifying first, grouped once there are
  enough to group. It is not a form until the user asks for one. A modal that opens straight into
  inputs turns "I only wanted to look at it" into something that can be saved by accident.
- **Editing happens in place, in the same modal.** The body swaps from read to form and the footer's
  primary action becomes Save. **A second modal to edit what the first modal is showing is banned.**
- **At most two layers, and the second is only ever a confirmation.** A destructive action inside a
  detail modal opens the confirmation from the *Destructive confirmation* pattern on top of it.
  Nothing else stacks.
- **The footer holds at most one primary action.** Secondary beside it, destructive separated from
  both. Close is always available and is never one of the three - closing is not an action, and must
  never read as the way to discard.
- **A dirty close asks.** If the body is in edit state with unsaved changes, Esc and the backdrop
  prompt before discarding. This is the single place where Esc does not simply close, and the only
  exception the dialog contract makes.
- **Open immediately with what the row already knows.** The identity comes from the row, so the
  header is right from the first frame; anything still being fetched is a skeleton in the body. Never
  delay the open until a fetch returns, and never open a modal that is nothing but a spinner - both
  read as a broken click.
- **An error in the body keeps the modal open**, with the list behind it untouched: an inline alert,
  and the action retryable. A detail that closes itself on failure leaves the user nothing to retry.
- **One column, one screen.** The dialog is `min(32rem, 100vw - 2rem)` wide; that is already in the
  component. The *body* scrolls, never the page behind it. If you are reaching for two columns, 4.2's
  exception 3 applies and this was never a modal.

### 4.4 Where each front end spells it

One pattern, expressed with what each router gives it.

| | React | Blazor |
|---|---|---|
| List route | `src/routes/widgets.tsx` | `Pages/Widgets.razor` |
| Detail address | a child route, `src/routes/widgets.$widgetId.tsx`, rendered over the list | a query parameter, `?id=`, on the same `@page` |
| Opening | `navigate({ to: '/widgets/$widgetId', params })` from the row | `NavigationManager.GetUriWithQueryParameter("id", id)` |
| Reading it | `Route.useParams()`; the modal is open when the param is there | `[SupplyParameterFromQuery] public Guid? Id { get; set; }` |
| Closing | `navigate({ to: '/widgets' })` - never `setOpen(false)` | `GetUriWithQueryParameter("id", (string?)null)` |
| The modal | `components/ui/dialog.tsx`, driven by the route | `IDialogService.ShowDialogAsync<TDialog>(content, parameters)` with a `.razor` implementing `IDialogContentComponent<T>` |

Two things to notice. Closing is a **navigation** on both sides, which is what makes Back and the
close button do the same thing rather than two things that agree by accident. And on the Blazor side
the dialog component is per-entity and lives beside the page it serves - not a general-purpose
wrapper, because a wrapper over a single use is `CLAUDE.md` §2 broken to save four lines.

### 4.5 What this workspace does and does not yet show

Be clear about the gap, rather than letting the next reader take the sample pages for the model:

- **Both `/styleguide` pages demonstrate all three archetypes**, in the *Page composition* group, as
  specimens over fabricated data. The specimens hold the open row in local state and say so in their
  intent text - the guide is one page and cannot route - while the copyable snippet under each shows
  the routed form a product screen must use.
- **The sample `/` and `/widgets` pages do not follow this yet.** Home is the stack blurb 4.1
  describes as the thing not to copy, and the widgets list has no detail affordance at all: the API
  serves the collection only, with no `GET /widgets/{id}` behind it. Bringing them into line is
  known, unstarted work. Until it is done, **read the guide, not the sample pages.**

### 4.6 The composition contracts

Acceptance criteria, in the same sense as the accessibility contracts in section 6 - and the modal
ones are repeated there, because they are where this pattern is usually broken:

- A dashboard tile and a panel row are **links**, not divs with click handlers: middle-click and
  "open in new tab" have to work, and a link is reachable from the keyboard for free.
- A clickable table row is a **`<button>` carrying the row's identity**, stretched over the row with
  an `::after` overlay - never a bare `onClick` on a `<tr>`, which is unreachable from the keyboard
  and announces nothing. A secondary action in the same row sits *above* that overlay
  (`position: relative; z-index: 1`, or `relative z-10`), or it cannot be clicked at all.
- Opening the modal **moves focus into it**; closing it **returns focus to the row that opened it**.
- The modal's accessible name is the entity's identity.
- A dirty edit state confirms before Esc or the backdrop discards it.
- Every panel announces its own loading and its own failure, inside its own region.

## 5. Adding to the system

**A variant that is missing belongs in the design system first, not in one screen.** That sentence is in
the header of both guides, and it is the rule the rest of this file exists to support. A one-off style in
a page is how a system stops being one.

**Add it to both guides, in the same place.** The two `/styleguide` pages list the same **25 sections in
the same order**, in seven groups - Foundations, Controls, Feedback, Navigation and structure, Overlays,
Patterns, Page composition - and that side-by-side reading is the reason this repository carries two
front ends. `StyleGuidePageTests` asserts the Blazor section count
(`FindAll(".sg-section").Count.ShouldBe(25)`), so adding one there without the other fails a test. The
React side has no such guard, so that direction is on you.

Every section has the same four parts, and all four are required:

1. **Title.**
2. **Intent** — what the component is for, *and when to reach for something else*. "Invokes an action on
   the current screen. Use a link, not a button, for anything that navigates."
3. **Rows** — one per variant and one per state, each labelled with which it is. Including the states
   nobody remembers: loading, disabled, error, empty.
4. **Accessibility** — the keyboard, focus, naming or announcement behaviour that is *contractual*, not
   aspirational.

A **Page composition** section carries one more thing, because its specimen cannot be honest on its own:
the guide is a single page and cannot route, so each of those specimens holds its open row in local
state while the copyable snippet shows the routed form. Say which is which in the Intent, or the next
reader copies the specimen.

## 6. The accessibility contracts

These are the commitments the guide's Accessibility notes make. They are the acceptance criteria for
anything new in the same group.

- **Focus** is a two-pixel offset ring on the `focus-ring` token, never the brand.
- **A loading control keeps its label** and sets `aria-busy`, so the accessible name does not change
  under the user mid-interaction.
- **A placeholder is never the label.** A control inside a `Field` adopts that field's `id`,
  `aria-invalid` and `aria-describedby`.
- **Prefer the native element.** A native `<select>` gets the platform's own picker on touch, which no
  custom listbox reproduces. A `fieldset`/`legend` already groups and names a radio group with no ARIA to
  keep correct. A `<dialog>`'s `showModal()` brings the focus trap, Esc, the inert background and the top
  layer for free.
- **A label that wraps its input** makes the text both the accessible name and part of the hit target.
- **Interrupt only when it matters.** `warning` and `danger` use `role="alert"`; `info` and `success` use
  `role="status"` and wait their turn.
- **A live region is mounted for the life of the app.** A region added at the same moment as its message
  is usually not announced.
- **Decoration is `aria-hidden`.** A spinner is decoration; the region around it announces the wait.
- **Active state is never colour alone** — `aria-current="page"` on the active nav link and on the last
  breadcrumb, which is text rather than a link to where you already are.
- **Tabs follow the WAI-ARIA pattern**: arrows move and select, Home and End jump to the ends, and the
  tablist is one tab stop.
- **Tables**: `scope="col"` on header cells, tabular figures on numeric columns, and the *wrapper*
  scrolls rather than the page.
- **Validation runs on submit**, not on every keystroke, so a message never appears while the user is
  still typing the value that would clear it. The message carries `role="alert"`.
- **A destructive confirmation gives Cancel initial focus**, so Enter on a dialog the user has not read
  cancels rather than destroys.
- **Nothing scrolls sideways.** A textarea resizes vertically only, so it cannot be dragged out of the
  layout.
- **A modal detail returns focus to the row that opened it**, on every dismissal - Esc, the close
  button, the backdrop and the back button. A modal opened from a list and closed back to the top of
  the document is the commonest keyboard failure in this pattern; see section 4.
- **A clickable row is a real control.** A `<button>` filling the cell, or a row that takes `tabIndex`
  and handles Enter and Space. A bare click handler on a `<tr>` is unreachable from the keyboard and
  announces nothing.
- **A dashboard tile is a link**, so middle-click and "open in new tab" work, and its accessible name
  says what it counts - "3 widgets low on stock", not "3".

## 7. Blazor: the FluentUI JS-module constraint

bUnit runs with `JSRuntimeMode.Loose`, under which an imported JS module resolves to `null`. What throws
is therefore not a component that *ships* a module but one that then *calls into* it. Plenty ship one and
never do: `FluentButton`, `FluentTextField`, `FluentCheckbox`, `FluentTab`, `FluentDivider` and
`FluentDialogProvider` all render fine — which is exactly what `StyleGuidePageTests` proves, by rendering
every approved component in one place, so a FluentUI upgrade that breaks one fails there.
`FluentNavMenu` and `FluentDataGrid` are the two that genuinely throw and stay out.

**This is a test-harness limit, not licence to leave the design system.** If a component throws, the fix
is a `JSInterop.SetupModule` in `FluentUiTestContext` — not deleting the usage, and *emphatically* not
hand-rolling the component in raw HTML. A previous build did exactly that: every chart and board carried
the comment *"Plain HTML, no FluentUI: the board must render under bUnit's loose JSInterop"*. A
testability constraint became the design strategy, the components dropped out of the design system, and
nothing replaced them. The result rendered as black rectangles and every test passed.

A neighbouring trap that is *not* the JS-module one: `FluentDialogHeader`, `FluentDialogBody` and
`FluentDialogFooter` throw `ArgumentNullException` - "must be used inside FluentDialog" - unless the
cascading `FluentDialog` that `FluentDialogProvider` supplies at run time is present. So a dialog
content component cannot be rendered standalone in bUnit at all, however the JSInterop is set up.
That is why the Page composition guard tests assert on the list and dashboard specimens rather than
on `StyleGuideDetailDialog`, and it is recorded here so the next session does not spend the
afternoon rediscovering it.

Related: **do not remove `FluentDialogProvider` or `FluentToastProvider` from `MainLayout`.**
`IDialogService` and `IToastService` resolve whether or not their provider is mounted, so removing one
breaks the guide's Dialog and Toast sections at run time with nothing failing at build time.
`MainLayoutTests` exists for exactly that.

## 8. How to tell whether any of this held

A design system is only as real as the check that notices you left it. In rough order of cost:

- **Run the guides side by side.** `aspire run`, then `/styleguide` on both front ends, in light and
  dark. That comparison is what the two front ends are for.
- **Assert on computed styles, not on file existence.** A screenshot test whose assertion is
  `File.Exists(shot)` is a file, not an observation — it will pass over a black-on-black label forever.
  Assert that the computed colour differs from the computed background, that every SVG mark has an
  explicit fill or stroke, that the document does not overflow horizontally at the narrowest viewport,
  and that every interactive control has an accessible name.
- **Run an accessibility engine over each rendered screen** and compare the violation count against a
  recorded baseline, number to number.
- **Compare screenshots against a committed baseline**, in both themes. A baseline updated to make the
  comparison pass is silencing, not fixing.

Note what does *not* work: **bUnit and jsdom render a DOM but compute neither layout nor colour**, so a
suite built only on them cannot see any of the failures above. Seeing them needs a real browser.
