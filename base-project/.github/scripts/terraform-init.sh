#!/usr/bin/env bash
# terraform init, retried when the provider registry fails to answer (J-029: "Error while installing
# azure/azapi ... context deadline exceeded"). Any other failure, a bad config say, fails at once.
# Arguments go to terraform init. TF_INIT_ATTEMPTS (3) and TF_INIT_DELAY (seconds, 20) tune it.
set -u

attempts=${TF_INIT_ATTEMPTS:-3}
delay=${TF_INIT_DELAY:-20}
log=$(mktemp)
trap 'rm -f "$log"' EXIT

for ((i = 1; ; i++)); do
  terraform init "$@" 2>&1 | tee "$log"
  code=${PIPESTATUS[0]}
  [ "$code" -eq 0 ] && exit 0
  if [ "$i" -ge "$attempts" ] || ! grep -qE 'Error while installing|could not query provider registry|Failed to query available provider packages.*(timeout|deadline exceeded)' "$log"; then
    exit "$code"
  fi
  echo "::warning::terraform init: the provider registry did not answer (attempt $i of $attempts); retrying in ${delay}s"
  sleep "$delay"
done
