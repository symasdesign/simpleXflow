#!/usr/bin/env bash
set -euo pipefail

: "${AZURE_RESOURCE_GROUP:?AZURE_RESOURCE_GROUP is required}"
: "${AZURE_CONTAINER_APP:?AZURE_CONTAINER_APP is required}"
: "${AZURE_SUBSCRIPTION_ID:?AZURE_SUBSCRIPTION_ID is required}"

# A stop begins with CanNotDelete and ends with ReadOnly. Either lock blocks CI.
if ! locks=$(az lock list \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --resource-name "${AZURE_CONTAINER_APP}" \
  --resource-type Microsoft.App/containerApps \
  --subscription "${AZURE_SUBSCRIPTION_ID}" \
  --query "length([?name=='monthly-budget-stop'])" --output tsv); then
  echo "::error::Cannot verify the budget stop lock. Refusing to change Azure."
  exit 1
fi
if [[ "${locks}" != "0" ]]; then
  echo "::error::Budget stop lock present or lock state unknown. Only an operator may resume the app."
  exit 1
fi

if ! status=$(az containerapp show \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --name "${AZURE_CONTAINER_APP}" \
  --subscription "${AZURE_SUBSCRIPTION_ID}" \
  --query properties.runningStatus --output tsv); then
  echo "::error::Cannot verify the app status. Refusing to change Azure."
  exit 1
fi
if [[ "${status}" != "Running" ]]; then
  echo "::error::App is not Running. Only an operator may resume it."
  exit 1
fi
