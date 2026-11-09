#!/usr/bin/env bash
# Proves terraform-init.sh against a fake terraform on PATH. One line per case; exits 1 if any fails.
#
set -u

here=$(cd "$(dirname "$0")" && pwd)
script="$here/terraform-init.sh"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
failures=0

# The fake fails its first $FAIL_TIMES calls with $FAIL_TEXT, then succeeds. Each call is counted.
mkdir -p "$work/bin"
cat > "$work/bin/terraform" <<'FAKE'
#!/usr/bin/env bash
n=$(( $(cat "$CALLS" 2>/dev/null || echo 0) + 1 ))
echo "$n" > "$CALLS"
echo "args: $*"
if [ "$n" -le "$FAIL_TIMES" ]; then echo "$FAIL_TEXT" >&2; exit 1; fi
echo 'Terraform has been successfully initialized!'
FAKE
chmod +x "$work/bin/terraform"

timeout_text='Error while installing azure/azapi v2.12.0: could not query provider
registry for registry.terraform.io/azure/azapi: failed to retrieve
authentication checksums for provider: the request failed after 2 attempts,
please try again later: Get
context deadline exceeded (Client.Timeout exceeded while awaiting headers)'
config_text='Error: Unsupported argument'

# check <case> <fail times> <fail text> <expected exit> <expected calls>
check() {
  rm -f "$work/calls"
  CALLS="$work/calls" FAIL_TIMES=$2 FAIL_TEXT=$3 TF_INIT_DELAY=0 PATH="$work/bin:$PATH" \
    bash "$script" -input=false > "$work/out" 2>&1
  code=$?
  calls=$(cat "$work/calls" 2>/dev/null || echo 0)
  if [ "$code" -eq "$4" ] && [ "$calls" -eq "$5" ]; then
    echo "ok   $1"
  else
    echo "FAIL $1: expected exit $4 after $5 call(s), got exit $code after $calls"
    sed 's/^/     /' "$work/out"
    failures=$((failures + 1))
  fi
}

check 'succeeds first time'                   0 "$timeout_text" 0 1
check 'registry timeout, then succeeds'        2 "$timeout_text" 0 3
check 'registry timeout every time gives up'   9 "$timeout_text" 1 3
check 'config error is not retried'            9 "$config_text"  1 1

# The arguments reach terraform init.
rm -f "$work/calls"
CALLS="$work/calls" FAIL_TIMES=0 FAIL_TEXT=x TF_INIT_DELAY=0 PATH="$work/bin:$PATH" bash "$script" -input=false > "$work/out" 2>&1
if grep -q '^args: init -input=false$' "$work/out"; then echo 'ok   passes its arguments to terraform init'
else echo 'FAIL passes its arguments to terraform init'; sed 's/^/     /' "$work/out"; failures=$((failures + 1)); fi

[ "$failures" -eq 0 ] || exit 1
