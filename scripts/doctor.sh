#!/usr/bin/env bash
# Refuses to continue unless Docker and kubectl point at the local InvoiceFlow environment.
# In CI (CI=true), Colima is not required; Docker uses the runner's engine.
set -euo pipefail

expected_context="k3d-invoiceflow"
expected_docker_host="unix://${HOME}/.colima/invoiceflow/docker.sock"
expected_kubeconfig="$(cd "$(dirname "$0")/.." && pwd)/.kube/config"
ci_mode=false
if [[ "${CI:-}" == "true" || "${INVOICEFLOW_CI:-}" == "1" ]]; then
  ci_mode=true
fi
failed=0

ok() { printf '  ok    %s\n' "$1"; }
fail() { printf '  FAIL  %s\n' "$1"; failed=1; }

tools=(docker k3d kubectl)
if [[ "$ci_mode" == false ]]; then
  tools+=(helm colima)
else
  # Helm is only needed for observability; stack-up / e2e do not use it.
  tools+=(helm)
fi

for tool in "${tools[@]}"; do
  if command -v "$tool" >/dev/null; then ok "$tool installed"; else fail "$tool not installed"; fi
done

if [[ "$ci_mode" == true ]]; then
  if [[ -z "${DOCKER_HOST:-}" || "${DOCKER_HOST}" == "unix:///var/run/docker.sock" ]]; then
    ok "DOCKER_HOST uses the runner Docker engine"
  else
    fail "DOCKER_HOST is '${DOCKER_HOST}', expected unset or unix:///var/run/docker.sock in CI"
  fi
elif [[ "${DOCKER_HOST:-}" == "$expected_docker_host" ]]; then
  ok "DOCKER_HOST points to the invoiceflow Colima profile"
else
  fail "DOCKER_HOST is '${DOCKER_HOST:-unset}', expected '$expected_docker_host'"
fi

if docker info >/dev/null 2>&1; then
  ok "Docker engine reachable"
  if [[ "$ci_mode" == true ]]; then
    ok "skipping Colima br_netfilter check (CI)"
  elif colima ssh --profile invoiceflow -- test -d /proc/sys/net/bridge 2>/dev/null; then
    ok "br_netfilter loaded in the VM"
  else
    fail "br_netfilter not loaded in the VM (run: make up)"
  fi
else
  fail "Docker engine not reachable (run: make up)"
fi

if [[ "${KUBECONFIG:-}" == "$expected_kubeconfig" ]]; then
  ok "KUBECONFIG is the project file"
else
  fail "KUBECONFIG is '${KUBECONFIG:-unset}', expected '$expected_kubeconfig'"
fi

# The project kubeconfig must never contain any other cluster, e.g. after a manual merge
contexts="$(kubectl config get-contexts -o name 2>/dev/null || true)"
other_contexts="$(grep -vx "$expected_context" <<<"$contexts" || true)"
if [[ -n "$other_contexts" ]]; then
  fail "unexpected contexts in project kubeconfig: $(tr '\n' ' ' <<<"$other_contexts")"
elif [[ -z "$contexts" ]]; then
  ok "no cluster yet (run: make cluster-up)"
else
  current="$(kubectl config current-context 2>/dev/null || true)"
  if [[ "$current" == "$expected_context" ]]; then
    ok "current context is $expected_context"
  else
    fail "current context is '${current:-unset}', expected '$expected_context'"
  fi
fi

if [[ $failed -ne 0 ]]; then
  echo "doctor: environment is not safe to use, stopping" >&2
  exit 1
fi
