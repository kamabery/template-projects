# Follow-ups found while verifying the deploy path

Found by the verification work and deliberately left out of it, because they do not affect the deploy
path. Delete an entry when it is done.

## An LF checkout staged three CRLF files

Found 2026-11-07 at `5d366d1`, cause not traced. Staging from an `autocrlf=false` checkout, where the sources
are LF, gives uniform CRLF in `aspire-base/template.slnx`, `aspire-base/.aspire/apphost.cs` and
`aspire-base/.aspire/appsettings.json` under `artifacts/templates/`. They reach every scaffold that way, so an
LF scaffold has those three as CRLF until they are normalized. No file is mixed and nothing fails.

```powershell
# Reproduces it. Run in an LF checkout, then look at the line endings of those three staged files.
pwsh templating/stage.ps1
```
