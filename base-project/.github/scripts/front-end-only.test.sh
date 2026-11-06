#!/usr/bin/env bash
# Proves front-end-only.sh against a throwaway git repo. One line per case; exits 1 if any fails.
#
set -u

here=$(cd "$(dirname "$0")" && pwd)
script="$here/front-end-only.sh"
real_git=$(command -v git)
work=$(mktemp -d)
trap 'cd /; rm -rf "$work"' EXIT
failures=0

# check <case> <expected output> <since>: runs the script from the current directory.
check() {
  actual=$(bash "$script" "$3" 2>&1)
  code=$?
  if [ "$actual" = "$2" ] && [ "$code" -eq 0 ]; then
    echo "ok   $1"
  else
    echo "FAIL $1: expected '$2' and exit 0, got '$actual' and exit $code"
    failures=$((failures + 1))
  fi
}

# commit_paths <path>...: writes each path with fresh content and commits them.
commit_paths() {
  for path in "$@"; do
    mkdir -p "$(dirname "$path")"
    echo "$path $(git rev-parse --verify --quiet HEAD)" >> "$path"
  done
  git add -A
  git commit -q -m "change $*"
}

# check_change <case> <expected output> <path>...: commits the paths, then diffs just that commit.
check_change() {
  name=$1
  expected=$2
  shift 2
  before=$(git rev-parse HEAD)
  commit_paths "$@"
  check "$name" "$expected" "$before"
}

git init -q "$work/repo"
cd "$work/repo" || exit 1
git config user.name test
git config user.email test@example.invalid
git config commit.gpgsign false
git config core.autocrlf false
commit_paths src/app.api/keep.cs src/ui/react/App.tsx

# AC1: only src/ui/ changed.
check_change 'react files only'             true  src/ui/react/App.tsx src/ui/react/src/routes/index.tsx
check_change 'blazor files only'            true  src/ui/blazor/Web/Page.razor

# AC2: anything outside src/ui/ changed.
check_change 'api file'                     false src/app.api/x.cs
check_change 'IaC file'                     false IaC/main.tf
check_change 'workflow file'                false .github/workflows/deploy.yml
check_change 'src/ui/ and an api file'      false src/ui/react/App.tsx src/app.api/x.cs
check_change 'src/ui/ and an IaC file'      false src/ui/react/App.tsx IaC/main.tf
check_change 'a sibling of src/ui/'         false src/ui-admin/x.tsx
check_change 'src/ui/ deeper in the tree'   false docs/src/ui/x.md

# Git's rename detection would list only the new name, so a file moved in from the API would pass.
before=$(git rev-parse HEAD)
git mv src/app.api/keep.cs src/ui/react/keep.cs
git commit -q -m 'move a file into src/ui/'
check 'file renamed into src/ui/' false "$before"

before=$(git rev-parse HEAD)
git rm -q IaC/main.tf
git commit -q -m 'delete a file outside src/ui/'
check 'file deleted outside src/ui/' false "$before"

# AC3: no usable since.
blob=$(git rev-parse HEAD:src/ui/react/App.tsx)
check 'empty since'                         false ''
check 'since is not a revision'             false not-a-commit
check 'since is an unknown commit id'       false 0123456789012345678901234567890123456789
check 'since is a blob, not a commit'       false "$blob"
check 'since looks like an option'          false --help

# AC4: nothing changed.
check 'since equals HEAD (id)'              true  "$(git rev-parse HEAD)"
check 'since equals HEAD (name)'            true  HEAD

# git diff itself fails. The same since prints true with the real git, so only the failure can
# make this one false.
before=$(git rev-parse HEAD)
commit_paths src/ui/react/App.tsx
check 'real git, for the case below'        true  "$before"
mkdir -p "$work/shim"
cat > "$work/shim/git" <<EOF
#!/bin/sh
for arg in "\$@"; do [ "\$arg" = diff ] && exit 128; done
exec "$real_git" "\$@"
EOF
chmod +x "$work/shim/git"
PATH="$work/shim:$PATH" check 'git diff fails' false "$before"

# The workflow runs it from IaC/, and the paths must still be measured from the repo root.
mkdir -p "$work/repo/src/ui/sub"
cd "$work/repo/src/ui/sub" || exit 1
check 'run from a subdirectory, src/ui/ only' true "$before"
cd "$work/repo" || exit 1

[ "$failures" -eq 0 ] || exit 1
