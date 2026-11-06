#!/usr/bin/env bash
# SPDX-License-Identifier: AGPL-3.0-or-later
# Sourced from a script in the repository root.
set -euo pipefail
for task_dir in .cache vendor bin obj dist; do
  if [[ "$(realpath -m -- "$task_dir")" != "$PWD/$task_dir" ]]; then
    echo "Build directories must stay inside the repository: $task_dir" >&2
    exit 1
  fi
done
export DOTNET_CLI_HOME="$PWD/.cache/home" NUGET_PACKAGES="$PWD/.cache/nuget"
export TMPDIR="$PWD/.cache/tmp" XDG_CACHE_HOME="$PWD/.cache/xdg"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_EnableDiagnostics=0
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES" "$TMPDIR" "$XDG_CACHE_HOME"
if [[ -z "${DOTNET_EXECUTABLE:-}" ]]; then
  if command -v dotnet >/dev/null; then DOTNET_EXECUTABLE="$(command -v dotnet)"
  elif [[ -x "$PWD/.cache/dotnet/dotnet" ]]; then DOTNET_EXECUTABLE="$PWD/.cache/dotnet/dotnet"
  else echo 'Install the .NET 10 SDK, or set DOTNET_EXECUTABLE to its dotnet executable.' >&2; exit 1
  fi
fi
export DOTNET_EXECUTABLE
