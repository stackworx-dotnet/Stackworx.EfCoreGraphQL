#!/bin/bash
set -e

cd "$(dirname "$0")"
export STACKWORX_EFCOREGRAPHQL_SIDECAR_OUTPUT_DIR="$PWD/Migrations"

NAME=$1
dotnet ef migrations add "$NAME"
