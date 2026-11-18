#!/usr/bin/env bash
# Proves wait-for-outputs.sh against a fake terraform on PATH. One line per case; exits 1 if any fails.
#
set -u

here=$(cd "$(dirname "$0")" && pwd)
script="$here/wait-for-outputs.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

# A terraform whose `output -json` fails until it has been called $FAKE_FAILS times, as HCP does while it
# processes a new state's outputs.
mkdir -p "$work/bin"
cat > "$work/bin/terraform" <<'EOF'
#!/usr/bin/env bash
n=$(( $(cat "$FAKE_COUNT" 2>/dev/null || echo 0) + 1 ))
echo "$n" > "$FAKE_COUNT"
if [ "$n" -le "$FAKE_FAILS" ]; then
  echo "Error: current outputs were not ready to be read within the deadline. Please try again" >&2
  exit 1
fi
echo '{"acr_name":{"value":"cr1"}}'
EOF
chmod +x "$work/bin/terraform"

# check <case> <fails before success> <deadline seconds> <expected exit> <least calls>: a success stops at
# exactly its call; a deadline is wall time, so a slow machine fits fewer tries into it.
check() {
  rm -f "$work/count"
  actual=$(PATH="$work/bin:$PATH" FAKE_COUNT="$work/count" FAKE_FAILS="$2" \
    WAIT_OUTPUTS_SECONDS="$3" WAIT_OUTPUTS_INTERVAL=1 bash "$script" 2>&1)
  code=$?
  calls=$(cat "$work/count" 2>/dev/null || echo 0)
  if [ "$code" -eq "$4" ] && [ "$calls" -ge "$5" ]; then
    echo "ok   $1"
  else
    echo "FAIL $1: expected exit $4 after at least $5 calls, got exit $code after $calls calls: $actual"
    failures=$((failures + 1))
  fi
}

check "readable at once" 0 30 0 1
check "readable on the third try" 2 30 0 3
check "gives up after its deadline" 1000 2 1 2

[ "$failures" -eq 0 ]
