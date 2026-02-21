#!/bin/bash
set -e

export SIDECAR_OUTPUT_DIR="0$/src/VoucherProvider.Web/Types"

NAME=$1
dotnet ef migrations add $NAME