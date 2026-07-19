#!/usr/bin/env bash

set -euo pipefail

# ============================================================
# Configuration
# ============================================================

PROJECT_ID="your-google-cloud-project-id"
PROJECT_NAME="Your Application"
BILLING_ACCOUNT_ID="" # Optional: 000000-000000-000000

# Enable APIs your application will use.
APIS=(
  "identitytoolkit.googleapis.com"
  "securetoken.googleapis.com"
  "iam.googleapis.com"
  "iamcredentials.googleapis.com"
  "serviceusage.googleapis.com"
)

# ============================================================
# Authenticate gcloud
# ============================================================

gcloud auth login

# ============================================================
# Create or select the project
# ============================================================

if gcloud projects describe "${PROJECT_ID}" >/dev/null 2>&1; then
  echo "Project already exists: ${PROJECT_ID}"
else
  gcloud projects create "${PROJECT_ID}" \
    --name="${PROJECT_NAME}"
fi

gcloud config set project "${PROJECT_ID}"

# ============================================================
# Optionally connect billing
# ============================================================

if [[ -n "${BILLING_ACCOUNT_ID}" ]]; then
  gcloud billing projects link "${PROJECT_ID}" \
    --billing-account="${BILLING_ACCOUNT_ID}"
fi

# ============================================================
# Enable APIs
# ============================================================

gcloud services enable "${APIS[@]}" \
  --project="${PROJECT_ID}"

# ============================================================
# Show project details
# ============================================================

PROJECT_NUMBER="$(
  gcloud projects describe "${PROJECT_ID}" \
    --format="value(projectNumber)"
)"

echo
echo "Google Cloud project configured."
echo "Project ID:     ${PROJECT_ID}"
echo "Project number: ${PROJECT_NUMBER}"
echo
echo "Complete the OAuth client configuration here:"
echo "https://console.cloud.google.com/auth/overview?project=${PROJECT_ID}"
