#!/usr/bin/env bash
# Waits until `terraform output` can read the state an apply just wrote. HCP Terraform makes a new state's
# outputs readable some time after the apply returns: on 2026-11-18 over a minute, and `terraform output`
# gave up ("current outputs were not ready to be read within the deadline") or, behind `|| true`, read as
# empty. Run from IaC/ at the end of every apply step. Exits 1 if they still don't read after
# WAIT_OUTPUTS_SECONDS (default 300), trying every WAIT_OUTPUTS_INTERVAL seconds (default 10).
#
set -u

deadline=$((SECONDS + ${WAIT_OUTPUTS_SECONDS:-300}))
try=1
while true; do
  if err=$(terraform output -json 2>&1 >/dev/null); then
    exit 0
  fi
  if [ "$SECONDS" -ge "$deadline" ]; then
    echo "::error::terraform outputs still not readable after ${WAIT_OUTPUTS_SECONDS:-300} s: $err"
    exit 1
  fi
  echo "outputs not readable yet (try $try): $err"
  try=$((try + 1))
  sleep "${WAIT_OUTPUTS_INTERVAL:-10}"
done
