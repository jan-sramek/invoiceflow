#!/usr/bin/env bash
# Stops the simulated on-premises PostgreSQL container.
set -euo pipefail

name=invoiceflow-onprem-postgres
if docker rm -f "$name" >/dev/null 2>&1; then
  echo "On-prem PostgreSQL stopped"
else
  echo "On-prem PostgreSQL was not running"
fi
