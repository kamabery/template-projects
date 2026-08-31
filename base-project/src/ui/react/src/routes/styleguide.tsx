import { useState } from 'react'
import type { FormEvent } from 'react'
import { createFileRoute } from '@tanstack/react-router'
import { Group, Row, Section, Swatch, SwatchGrid } from '@/components/styleguide/parts'
import { Alert } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Breadcrumb, BreadcrumbItem } from '@/components/ui/breadcrumb'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { ConfirmDialog, Dialog } from '@/components/ui/dialog'
import {
  Field,
  FieldDescription,
  FieldError,
  FieldLabel,
} from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { NavBar, NavItem } from '@/components/ui/nav'
import { Radio, RadioGroup } from '@/components/ui/radio'
import { Select } from '@/components/ui/select'
import { Skeleton } from '@/components/ui/skeleton'
import { Spinner } from '@/components/ui/spinner'
import { Switch } from '@/components/ui/switch'
import { Tab, TabList, TabPanel, Tabs } from '@/components/ui/tabs'
import { Table, Tbody, Td, Th, Thead, Tr } from '@/components/ui/table'
import { Textarea } from '@/components/ui/textarea'
import { useToast } from '@/components/ui/toast'

export const Route = createFileRoute('/styleguide')({ component: StyleGuidePage })

/**
 * Living style guide. Every approved component, every variant, every state.
 *
 * It ships in production builds rather than being gated on DEV: it is the
 * documentation for a starter template, and the Blazor guide at the same path
 * cannot be gated as cheaply, so gating this one would only make the two drift.
 */
function StyleGuidePage() {
  return (
    <div className="space-y-12">
      <header className="space-y-3">
        <h1 className="text-3xl font-semibold tracking-tight">Style guide</h1>
        <p className="max-w-2xl text-content-muted">
          The approved components, the tokens they are built from, and the
          behaviour each one is contracted to have. Prefer these over new markup:
          a variant that is missing belongs here first, not in one screen.
        </p>
        <p className="max-w-2xl text-sm text-content-muted">
          The Blazor front end publishes the same sections in the same order at
          its own <code>/styleguide</code>, so the two can be read side by side.
        </p>
      </header>

      <Group title="Foundations">
        <ColorTokens />
        <ShapeElevationMotion />
      </Group>

      <Group title="Controls">
        <ButtonSection />
        <TextInputSection />
        <TextareaSection />
        <SelectSection />
        <CheckboxSection />
        <SwitchSection />
        <RadioSection />
      </Group>

      <Group title="Feedback">
        <AlertSection />
        <BadgeSection />
        <LoadingSection />
        <SkeletonSection />
        <ToastSection />
      </Group>

      <Group title="Navigation and structure">
        <TabsSection />
        <BreadcrumbSection />
        <NavigationSection />
        <TableSection />
      </Group>

      <Group title="Overlays">
        <DialogSection />
      </Group>

      <Group title="Patterns">
        <ValidationSection />
        <SearchFormPattern />
        <DestructivePattern />
      </Group>
    </div>
  )
}

/* -------------------------------------------------------------- Foundations */

const surfaceTokens = [
  'surface',
  'surface-raised',
  'surface-sunken',
  'border-subtle',
  'border-strong',
  'content',
  'content-muted',
  'content-disabled',
]

const brandTokens = ['brand', 'brand-subtle', 'brand-contrast', 'focus-ring']

const intentTokens = [
  'intent-info',
  'intent-info-subtle',
  'intent-success',
  'intent-success-subtle',
  'intent-warning',
  'intent-warning-subtle',
  'intent-danger',
  'intent-danger-subtle',
]

function ColorTokens() {
  return (
    <Section
      title="Color tokens"
      intent="Every color in the product comes from one of these. A raw palette value has a single value and so breaks whichever theme it was not picked for."
      a11y="Color never carries meaning alone: each intent is paired with a word or a glyph wherever it is used."
      code={'<div className="bg-surface-raised text-content" />'}
    >
      <Row label="Surface + content">
        <SwatchGrid>
          {surfaceTokens.map((token) => (
            <Swatch key={token} token={token} />
          ))}
        </SwatchGrid>
      </Row>
      <Row label="Brand + focus">
        <SwatchGrid>
          {brandTokens.map((token) => (
            <Swatch key={token} token={token} />
          ))}
        </SwatchGrid>
      </Row>
      <Row label="Intents">
        <SwatchGrid>
          {intentTokens.map((token) => (
            <Swatch key={token} token={token} />
          ))}
        </SwatchGrid>
      </Row>
    </Section>
  )
}

function ShapeElevationMotion() {
  return (
    <Section
      title="Shape, elevation and motion"
      intent="Corner radius, shadow and duration are tokens for the same reason colors are: a literal rounded-lg drifts the moment one component is restyled."
      a11y="Motion tokens collapse to 1ms under prefers-reduced-motion, so no component has to opt in individually."
      code={'className="rounded-surface shadow-raised duration-[var(--motion-fast)]"'}
    >
      <Row label="Radius">
        <div className="size-16 rounded-control border border-border-subtle bg-surface-sunken p-1 text-[10px] text-content-muted">
          control
        </div>
        <div className="size-16 rounded-surface border border-border-subtle bg-surface-sunken p-1 text-[10px] text-content-muted">
          surface
        </div>
        <div className="size-16 rounded-pill border border-border-subtle bg-surface-sunken p-4 text-[10px] text-content-muted">
          pill
        </div>
      </Row>
      <Row label="Elevation">
        <div className="rounded-surface bg-surface-raised px-4 py-3 text-xs shadow-raised">
          shadow-raised
        </div>
        <div className="rounded-surface bg-surface-raised px-4 py-3 text-xs shadow-overlay">
          shadow-overlay
        </div>
      </Row>
      <Row label="Motion">
        <span className="rounded-control bg-surface-sunken px-3 py-2 text-xs transition-colors duration-[var(--motion-fast)] hover:bg-brand hover:text-brand-contrast">
          hover me - motion-fast
        </span>
        <span className="rounded-control bg-surface-sunken px-3 py-2 text-xs transition-colors duration-[var(--motion-slow)] hover:bg-brand hover:text-brand-contrast">
          hover me - motion-slow
        </span>
      </Row>
    </Section>
  )
}

/* ------------------------------------------------------------------ Controls */

function ButtonSection() {
  return (
    <Section
      title="Button"
      intent="Invokes an action on the current screen. Use a link, not a button, for anything that navigates."
      a11y="Focus is a two-pixel offset ring on the focus-ring token, never the brand. The loading state keeps the label and sets aria-busy, so the accessible name does not change under the user."
      code={'<Button variant="danger" size="sm" loading>Delete</Button>'}
    >
      <Row label="Variants">
        <Button>Primary</Button>
        <Button variant="secondary">Secondary</Button>
        <Button variant="ghost">Ghost</Button>
        <Button variant="danger">Danger</Button>
      </Row>
      <Row label="Sizes">
        <Button size="sm">Small</Button>
        <Button size="md">Medium</Button>
      </Row>
      <Row label="States">
        <Button disabled>Disabled</Button>
        <Button loading>Saving</Button>
        <Button variant="danger" disabled>
          Disabled danger
        </Button>
      </Row>
    </Section>
  )
}

function TextInputSection() {
  return (
    <Section
      title="Text input"
      intent="A single line of free text. Wrap it in a Field to get the label, description and validation wiring."
      a11y="Inside a Field it adopts that field's id, aria-invalid and aria-describedby. A placeholder is never the label."
      code={'<Field invalid={!!error}>\n  <FieldLabel required>Email</FieldLabel>\n  <Input type="email" />\n  <FieldError>{error}</FieldError>\n</Field>'}
    >
      <Row label="Default">
        <div className="w-64">
          <Field>
            <FieldLabel>Customer name</FieldLabel>
            <Input placeholder="Ada Lovelace" />
          </Field>
        </div>
      </Row>
      <Row label="With description">
        <div className="w-64">
          <Field>
            <FieldLabel required>Email</FieldLabel>
            <Input type="email" defaultValue="ada@example.com" />
            <FieldDescription>Used for order confirmations.</FieldDescription>
          </Field>
        </div>
      </Row>
      <Row label="Invalid">
        <div className="w-64">
          <Field invalid>
            <FieldLabel>Email</FieldLabel>
            <Input type="email" defaultValue="not-an-email" />
            <FieldError>Enter an email address.</FieldError>
          </Field>
        </div>
      </Row>
      <Row label="Disabled">
        <div className="w-64">
          <Field>
            <FieldLabel>Account id</FieldLabel>
            <Input disabled defaultValue="ACC-4021" />
          </Field>
        </div>
      </Row>
    </Section>
  )
}

function TextareaSection() {
  return (
    <Section
      title="Textarea"
      intent="Several lines of free text. Anything longer than a sentence or two."
      a11y="Resizable vertically only, so it cannot be dragged out of the layout and force the page to scroll sideways."
      code={'<Textarea rows={4} placeholder="Notes" />'}
    >
      <Row label="Default">
        <div className="w-72">
          <Field>
            <FieldLabel>Notes</FieldLabel>
            <Textarea placeholder="What happened?" />
          </Field>
        </div>
      </Row>
      <Row label="Invalid">
        <div className="w-72">
          <Field invalid>
            <FieldLabel>Notes</FieldLabel>
            <Textarea defaultValue="x" />
            <FieldError>Give at least 20 characters.</FieldError>
          </Field>
        </div>
      </Row>
    </Section>
  )
}

const regions = ['Europe', 'North America', 'Asia Pacific']

function SelectSection() {
  return (
    <Section
      title="Select"
      intent="One value from a short, known list. Above roughly a dozen options, use a search field instead."
      a11y="The native element on purpose: it gets the platform's own picker on touch devices, which no custom listbox reproduces."
      code={'<Select defaultValue="Europe">\n  {regions.map((r) => <option key={r}>{r}</option>)}\n</Select>'}
    >
      <Row label="Default">
        <div className="w-64">
          <Field>
            <FieldLabel>Region</FieldLabel>
            <Select defaultValue="Europe">
              {regions.map((region) => (
                <option key={region}>{region}</option>
              ))}
            </Select>
          </Field>
        </div>
      </Row>
      <Row label="Invalid">
        <div className="w-64">
          <Field invalid>
            <FieldLabel required>Region</FieldLabel>
            <Select defaultValue="">
              <option value="">Choose a region</option>
              {regions.map((region) => (
                <option key={region}>{region}</option>
              ))}
            </Select>
            <FieldError>Choose a region.</FieldError>
          </Field>
        </div>
      </Row>
      <Row label="Disabled">
        <div className="w-64">
          <Select disabled defaultValue="Europe">
            <option>Europe</option>
          </Select>
        </div>
      </Row>
    </Section>
  )
}

function CheckboxSection() {
  return (
    <Section
      title="Checkbox"
      intent="An independent on/off choice, or several of them. For one setting that takes effect immediately, use a Switch."
      a11y="The label wraps the input, so the text is both the accessible name and part of the hit target."
      code={'<Checkbox label="Email me about order changes" />'}
    >
      <Row label="Default">
        <Checkbox label="Email me about order changes" />
      </Row>
      <Row label="Checked">
        <Checkbox label="Include archived customers" defaultChecked />
      </Row>
      <Row label="Disabled">
        <Checkbox label="Managed by your administrator" disabled defaultChecked />
      </Row>
    </Section>
  )
}

function SwitchSection() {
  return (
    <Section
      title="Switch"
      intent="A setting that takes effect the moment it is flipped. If the change only applies on save, use a Checkbox."
      a11y='role="switch" on a native checkbox, so a screen reader says "on"/"off" and space still toggles it.'
      code={'<Switch label="Dark mode" defaultChecked />'}
    >
      <Row label="Default">
        <Switch label="Show retired products" />
      </Row>
      <Row label="Checked">
        <Switch label="Email notifications" defaultChecked />
      </Row>
      <Row label="Disabled">
        <Switch label="Enforced by policy" disabled defaultChecked />
      </Row>
    </Section>
  )
}

function RadioSection() {
  const [shipping, setShipping] = useState('standard')

  return (
    <Section
      title="Radio group"
      intent="Exactly one value from two to five visible options. More than that belongs in a Select."
      a11y="A fieldset and legend, not role=radiogroup: the native pair already groups the controls and names the group, with no ARIA to keep correct."
      code={'<RadioGroup label="Shipping" value={value} onValueChange={setValue}>\n  <Radio value="standard" label="Standard" />\n</RadioGroup>'}
    >
      <Row label="Default">
        <RadioGroup label="Shipping" value={shipping} onValueChange={setShipping}>
          <Radio value="standard" label="Standard - 3 to 5 days" />
          <Radio value="express" label="Express - next day" />
          <Radio value="pickup" label="Collect in store" disabled />
        </RadioGroup>
      </Row>
      <Row label="Selected">
        <span className="text-sm text-content-muted">{shipping}</span>
      </Row>
    </Section>
  )
}

/* ------------------------------------------------------------------ Feedback */

function AlertSection() {
  const [dismissed, setDismissed] = useState(false)

  return (
    <Section
      title="Alert"
      intent="A message about the screen the user is on. For something transient and non-blocking, use a Toast."
      a11y="Warning and danger use role=alert and interrupt the screen reader; info and success use role=status and wait their turn."
      code={'<Alert intent="danger" title="Could not save">\n  The API returned 500.\n</Alert>'}
    >
      <Row label="Intents">
        <div className="w-full space-y-3">
          <Alert intent="info" title="Scheduled maintenance">
            The API is read-only on Sunday between 02:00 and 04:00 UTC.
          </Alert>
          <Alert intent="success" title="Customer saved">
            Changes are live.
          </Alert>
          <Alert intent="warning" title="Approaching your quota">
            You have used 92% of this month's requests.
          </Alert>
          <Alert intent="danger" title="Could not save">
            The API returned 500. Nothing was changed.
          </Alert>
        </div>
      </Row>
      <Row label="Dismissible">
        <div className="w-full">
          {dismissed ? (
            <Button variant="secondary" size="sm" onClick={() => setDismissed(false)}>
              Bring it back
            </Button>
          ) : (
            <Alert intent="info" onDismiss={() => setDismissed(true)}>
              Dismissible - only for messages the user can afford to lose.
            </Alert>
          )}
        </div>
      </Row>
    </Section>
  )
}

function BadgeSection() {
  return (
    <Section
      title="Badge"
      intent="A short status word attached to something else. Not a button, and never the only thing that carries the status."
      a11y="The status word is inside the badge, so the meaning survives without color."
      code={'<Badge intent="success" variant="solid">Active</Badge>'}
    >
      <Row label="Subtle">
        <Badge>Draft</Badge>
        <Badge intent="info">In review</Badge>
        <Badge intent="success">Active</Badge>
        <Badge intent="warning">Past due</Badge>
        <Badge intent="danger">Suspended</Badge>
      </Row>
      <Row label="Solid">
        <Badge variant="solid">Draft</Badge>
        <Badge variant="solid" intent="info">
          In review
        </Badge>
        <Badge variant="solid" intent="success">
          Active
        </Badge>
        <Badge variant="solid" intent="warning">
          Past due
        </Badge>
        <Badge variant="solid" intent="danger">
          Suspended
        </Badge>
      </Row>
    </Section>
  )
}

function LoadingSection() {
  return (
    <Section
      title="Loading indicator"
      intent="Work is in progress and the user must wait. If the shape of what is coming is known, a Skeleton says more."
      a11y="role=status with a label that is always present - visually hidden by default, so the wait is announced either way."
      code={'<Spinner size="lg" label="Loading the forecast" showLabel />'}
    >
      <Row label="Sizes">
        <Spinner size="sm" />
        <Spinner size="md" />
        <Spinner size="lg" />
      </Row>
      <Row label="With label">
        <Spinner label="Loading the forecast..." showLabel />
      </Row>
    </Section>
  )
}

function SkeletonSection() {
  return (
    <Section
      title="Skeleton"
      intent="A placeholder in the shape of the content that is coming. Use it where the layout is known, so nothing jumps when the data lands."
      a11y="aria-hidden: it is decoration. The surrounding region is what announces that loading is happening."
      code={'<Skeleton className="h-4 w-40" />'}
    >
      <Row label="Card">
        <div className="w-64 space-y-2 rounded-surface border border-border-subtle p-4">
          <Skeleton className="h-3 w-24" />
          <Skeleton className="h-7 w-16" />
          <Skeleton className="h-3 w-32" />
        </div>
      </Row>
    </Section>
  )
}

function ToastSection() {
  const toast = useToast()

  return (
    <Section
      title="Toast"
      intent="Confirmation of something that already happened, which the user does not have to act on. Anything they must read belongs in an Alert."
      a11y="The live region is mounted for the life of the app, because a region added at the same moment as its message is usually not announced."
      code={"const toast = useToast()\ntoast.show('Customer saved.', 'success')"}
    >
      <Row label="Intents">
        <Button
          variant="secondary"
          size="sm"
          onClick={() => toast.show('Nothing to report.')}
        >
          Info
        </Button>
        <Button
          variant="secondary"
          size="sm"
          onClick={() => toast.show('Customer saved.', 'success')}
        >
          Success
        </Button>
        <Button
          variant="secondary"
          size="sm"
          onClick={() => toast.show('Quota is nearly spent.', 'warning')}
        >
          Warning
        </Button>
        <Button
          variant="secondary"
          size="sm"
          onClick={() => toast.show('The request failed.', 'danger')}
        >
          Danger
        </Button>
      </Row>
    </Section>
  )
}

/* ------------------------------------------------ Navigation and structure */

function TabsSection() {
  return (
    <Section
      title="Tabs"
      intent="Alternative views of one subject, where only one is needed at a time. Not a substitute for navigation between pages."
      a11y="Arrow keys move and select, Home and End jump to the ends, and the tablist is one tab stop - the WAI-ARIA tabs pattern."
      code={'<Tabs defaultValue="details">\n  <TabList aria-label="Customer">\n    <Tab value="details">Details</Tab>\n  </TabList>\n  <TabPanel value="details">...</TabPanel>\n</Tabs>'}
    >
      <Row label="Default">
        <div className="w-full">
          <Tabs defaultValue="details">
            <TabList aria-label="Customer">
              <Tab value="details">Details</Tab>
              <Tab value="orders">Orders</Tab>
              <Tab value="activity">Activity</Tab>
            </TabList>
            <TabPanel value="details">
              <p className="text-sm text-content-muted">
                Name, contact details and billing address.
              </p>
            </TabPanel>
            <TabPanel value="orders">
              <p className="text-sm text-content-muted">Twelve orders, four open.</p>
            </TabPanel>
            <TabPanel value="activity">
              <p className="text-sm text-content-muted">Last seen three days ago.</p>
            </TabPanel>
          </Tabs>
        </div>
      </Row>
    </Section>
  )
}

function BreadcrumbSection() {
  return (
    <Section
      title="Breadcrumb"
      intent="Where this screen sits in a hierarchy, and a way back up it. Not a history trail."
      a11y="The nav landmark is labelled Breadcrumb, and the last crumb is text with aria-current=page rather than a link to where you already are."
      code={'<Breadcrumb>\n  <BreadcrumbItem href="/">Home</BreadcrumbItem>\n  <BreadcrumbItem current>Ada Lovelace</BreadcrumbItem>\n</Breadcrumb>'}
    >
      <Row label="Default">
        <Breadcrumb>
          <BreadcrumbItem href="/">Home</BreadcrumbItem>
          <BreadcrumbItem href="/weather">Customers</BreadcrumbItem>
          <BreadcrumbItem current>Ada Lovelace</BreadcrumbItem>
        </Breadcrumb>
      </Row>
    </Section>
  )
}

function NavigationSection() {
  return (
    <Section
      title="Navigation"
      intent="Movement between the screens of the application. The header uses exactly this component - what you see below is the same code."
      a11y="One labelled nav landmark. The router marks the active link with aria-current=page, so the active state is not carried by color alone."
      code={'<NavBar>\n  <NavItem to="/" exact>Home</NavItem>\n  <NavItem to="/weather">Weather</NavItem>\n</NavBar>'}
    >
      <Row label="Default">
        <NavBar label="Style guide example">
          <NavItem to="/" exact>
            Home
          </NavItem>
          <NavItem to="/counter">Counter</NavItem>
          <NavItem to="/weather">Weather</NavItem>
        </NavBar>
      </Row>
    </Section>
  )
}

const tableRows = [
  { region: 'Europe', orders: 128, status: 'Active' },
  { region: 'North America', orders: 94, status: 'Active' },
  { region: 'Asia Pacific', orders: 12, status: 'Suspended' },
]

function TableSection() {
  return (
    <Section
      title="Table"
      intent="Rows that are compared against each other along shared columns. For a handful of unrelated values, a description list reads better."
      a11y="Header cells carry scope=col. Numeric columns get tabular figures so digits line up, and the wrapper scrolls rather than the page."
      code={'<Table caption="Orders by region">\n  <Thead><Tr><Th>Region</Th><Th numeric>Orders</Th></Tr></Thead>\n</Table>'}
    >
      <Row label="Default">
        <div className="w-full">
          <Table caption="Orders by region">
            <Thead>
              <Tr>
                <Th>Region</Th>
                <Th numeric>Orders</Th>
                <Th>Status</Th>
              </Tr>
            </Thead>
            <Tbody>
              {tableRows.map((row) => (
                <Tr key={row.region}>
                  <Td>{row.region}</Td>
                  <Td numeric>{row.orders}</Td>
                  <Td>
                    <Badge intent={row.status === 'Active' ? 'success' : 'danger'}>
                      {row.status}
                    </Badge>
                  </Td>
                </Tr>
              ))}
            </Tbody>
          </Table>
        </div>
      </Row>
    </Section>
  )
}

/* ------------------------------------------------------------------ Overlays */

function DialogSection() {
  const [open, setOpen] = useState(false)

  return (
    <Section
      title="Dialog"
      intent="A task that must be finished or abandoned before the screen behind it is usable again. Anything the user can ignore is not a dialog."
      a11y="The browser's own dialog element: the focus trap, Esc, the inert background and the top layer come from showModal(), not from code here."
      code={'<Dialog open={open} onClose={close} title="Edit customer">...</Dialog>'}
    >
      <Row label="Default">
        <Button variant="secondary" onClick={() => setOpen(true)}>
          Open dialog
        </Button>
        <Dialog
          open={open}
          onClose={() => setOpen(false)}
          title="Edit customer"
          description="Changes take effect immediately."
          footer={
            <>
              <Button variant="secondary" onClick={() => setOpen(false)}>
                Cancel
              </Button>
              <Button onClick={() => setOpen(false)}>Save</Button>
            </>
          }
        >
          <Field>
            <FieldLabel>Display name</FieldLabel>
            <Input defaultValue="Ada Lovelace" />
          </Field>
        </Dialog>
      </Row>
    </Section>
  )
}

/* ------------------------------------------------------------------ Patterns */

function ValidationSection() {
  const [value, setValue] = useState('')

  // Empty is not an error: an untouched field has nothing wrong with it yet. The
  // Blazor guide's Live example does the same, so the two demonstrate one rule.
  const error = value === '' || value.includes('@') ? '' : 'Enter an email address.'

  return (
    <Section
      title="Form validation message"
      intent="How a control says what is wrong with it. Never a message that floats free of the control it describes."
      a11y="Field generates the ids; the control gets aria-invalid and aria-describedby, and the message carries role=alert so it is announced when it appears."
      code={'<Field invalid={!!error}>\n  <FieldLabel required>Email</FieldLabel>\n  <Input value={value} onChange={onChange} />\n  <FieldError>{error}</FieldError>\n</Field>'}
    >
      <Row label="Live">
        <div className="w-72">
          <Field invalid={!!error}>
            <FieldLabel required>Email</FieldLabel>
            <Input
              type="email"
              value={value}
              onChange={(event) => setValue(event.target.value)}
              placeholder="ada@example.com"
            />
            <FieldDescription>Type something without an @ to see the message.</FieldDescription>
            <FieldError>{error}</FieldError>
          </Field>
        </div>
      </Row>
    </Section>
  )
}

function SearchFormPattern() {
  const toast = useToast()
  const [name, setName] = useState('')
  const [region, setRegion] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const [searching, setSearching] = useState(false)

  const nameError = submitted && !name.trim() ? 'Enter a name to search for.' : ''
  const regionError = submitted && !region ? 'Choose a region.' : ''

  function onSubmit(event: FormEvent) {
    event.preventDefault()
    setSubmitted(true)

    if (!name.trim() || !region) {
      return
    }

    setSearching(true)
    setTimeout(() => {
      setSearching(false)
      toast.show('Searched ' + region + ' for ' + name + '.', 'success')
    }, 900)
  }

  return (
    <Section
      title="Customer search form"
      intent="The whole form contract in one place: labels, descriptions, required marks, validation on submit, and a single primary action."
      a11y="Validation runs on submit, not on every keystroke, so a message never appears while the user is still typing the value that would clear it."
      code={'<form onSubmit={onSubmit} noValidate>\n  <Field invalid={!!nameError}>...</Field>\n  <Button type="submit" loading={searching}>Search</Button>\n</form>'}
    >
      <form onSubmit={onSubmit} noValidate className="max-w-md space-y-4">
        <Field invalid={!!nameError}>
          <FieldLabel required>Customer name</FieldLabel>
          <Input
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="Ada Lovelace"
          />
          <FieldDescription>Partial names match.</FieldDescription>
          <FieldError>{nameError}</FieldError>
        </Field>

        <Field invalid={!!regionError}>
          <FieldLabel required>Region</FieldLabel>
          <Select value={region} onChange={(event) => setRegion(event.target.value)}>
            <option value="">Choose a region</option>
            {regions.map((option) => (
              <option key={option}>{option}</option>
            ))}
          </Select>
          <FieldError>{regionError}</FieldError>
        </Field>

        <Checkbox label="Include archived customers" />

        <div className="flex gap-2">
          <Button type="submit" loading={searching}>
            Search
          </Button>
          <Button
            type="button"
            variant="ghost"
            onClick={() => {
              setName('')
              setRegion('')
              setSubmitted(false)
            }}
          >
            Clear
          </Button>
        </div>
      </form>
    </Section>
  )
}

function DestructivePattern() {
  const toast = useToast()
  const [open, setOpen] = useState(false)
  const [deleted, setDeleted] = useState(false)

  return (
    <Section
      title="Destructive confirmation"
      intent="An action that cannot be undone, gated behind an explicit confirmation that names what will be lost."
      a11y="Cancel takes initial focus, so pressing Enter on a dialog the user has not read cancels rather than destroys."
      code={'<ConfirmDialog\n  destructive\n  title="Delete Ada Lovelace?"\n  confirmLabel="Delete"\n  onConfirm={remove}\n/>'}
    >
      <Row label="Default">
        {deleted ? (
          <Alert intent="success" title="Customer deleted">
            <Button variant="secondary" size="sm" onClick={() => setDeleted(false)}>
              Reset the demo
            </Button>
          </Alert>
        ) : (
          <Button variant="danger" onClick={() => setOpen(true)}>
            Delete customer
          </Button>
        )}

        <ConfirmDialog
          destructive
          open={open}
          onClose={() => setOpen(false)}
          onConfirm={() => {
            setOpen(false)
            setDeleted(true)
            toast.show('Customer deleted.', 'danger')
          }}
          title="Delete Ada Lovelace?"
          description="Their orders stay, but the customer record and its contact details are removed. This cannot be undone."
          confirmLabel="Delete customer"
        />
      </Row>
    </Section>
  )
}
