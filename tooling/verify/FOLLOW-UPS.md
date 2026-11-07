# Follow-ups found while verifying the deploy path

Found by the verification work and deliberately left out of it, because they do not affect the deploy
path. Delete an entry when it is done.

## CLI tests fail on a CRLF checkout

Found 2026-11-07 at `5c4fa6e`. With `core.autocrlf=true`, the setting on Kurt's machine, a clean checkout
fails 9 of the 97 CLI tests. They pass in his main folder only because those files happen to be LF on disk,
so a fresh Windows clone is red.

```powershell
# Reproduces it. Run from the workspace root, and delete the worktree afterwards.
git -c core.autocrlf=true worktree add --detach $env:TEMP\crlf HEAD
dotnet test $env:TEMP\crlf\tooling\tooling.slnx
```

The failing tests:

- `PackagesEditorTests.Inserts_BeforeTheClosingProjectTag_SkippingIdsAlreadyPinned`
- `JsonMemberEditorTests.AfterTheLastMember_AddsTheSeparatingComma`
- `JsonMemberEditorTests.MissingAnchorMember_AppendsAsTheLastMember`
- `JsonMemberEditorTests.AfterAMiddleMember_KeepsTheCommaChainValid`
- `AnchorEditorTests.Below_InsertsUnderTheMarkerAfterABlankLine`
- `AnchorEditorTests.MissingPackagesMarker_FallsBackToAfterTheLastDirective`
- `AnchorEditorTests.Above_InsertsDirectlyAboveTheMarker`
- `AnchorEditorTests.MissingSlnxFolder_IsCreated`
- `AnchorEditorTests.MissingSlnxMarker_FallsBackToTheEndOfTheMatchingFolder`

**Likely cause, not confirmed:** the tests' raw string literals take the source file's line endings, so
their input is CRLF on such a checkout, while the editors insert LF.

**The question to answer first:** is this only the tests, or does the product have the same problem? If the
editors insert LF into a CRLF file, then `aspire-addon` run on a Windows scaffold may be writing files with
mixed line endings. That decides whether the fix belongs in the tests, in `.gitattributes`, or in the
editors.

`tooling/verify/mutations.ps1` works around this by forcing LF in its worktree. Remove that if the fix makes
it unnecessary.
