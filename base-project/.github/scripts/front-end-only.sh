#!/usr/bin/env bash
# Usage: front-end-only.sh <since>
#
# Prints true when every file changed between <since> and HEAD is under src/ui/, so a redeploy needs
# only to publish the front end. Prints false when <since> is empty or not a commit in this checkout,
# when git diff fails, or when any changed path is outside src/ui/. Always exits 0: any doubt means
# false, and false means the full deploy.

answer_false() {
  echo false
  exit 0
}

since="${1:-}"
[ -n "$since" ] || answer_false

base=$(git rev-parse --verify --quiet "${since}^{commit}" 2>/dev/null) || answer_false

# --no-renames lists both ends of a rename. Otherwise a file moved from the API into src/ui/ shows
# only its new name and passes. A path git still quotes starts with " and so counts as outside.
changed=$(git -c core.quotepath=off diff --name-only --no-renames "$base" HEAD 2>/dev/null) || answer_false

while IFS= read -r path; do
  case "$path" in
    '' | src/ui/*) ;;
    *) answer_false ;;
  esac
done <<< "$changed"

echo true
