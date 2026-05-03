#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

rebuild=0
if [[ "${1:-}" == "--rebuild" || "${1:-}" == "--build" ]]; then
  rebuild=1
  shift
fi

if [[ "${TERM:-}" == "" || "${TERM:-}" == "dumb" ]]; then
  export TERM=xterm-256color
fi

restore_stty=""
if [[ -t 0 ]] && command -v stty >/dev/null 2>&1; then
  restore_stty="$(stty -g || true)"
  stty -ixon || true
fi

cleanup() {
  if [[ -n "$restore_stty" ]]; then
    stty "$restore_stty" || true
  fi
}
trap cleanup EXIT

project="src/StatisticsAnalysisTool.Tui/StatisticsAnalysisTool.Tui.csproj"
app_dll="src/StatisticsAnalysisTool.Tui/bin/Debug/net10.0/StatisticsAnalysisTool.Tui.dll"

if [[ "$rebuild" -eq 1 || ! -f "$app_dll" ]]; then
  echo "Restoring Linux TUI dependencies..."
  dotnet restore "$project" \
    --packages "${NUGET_PACKAGES:-$HOME/.nuget/packages}" \
    -m:1 \
    -v:minimal \
    --tl:off

  echo "Building Linux TUI..."
  dotnet build "$project" \
    -c Debug \
    -m:1 \
    --no-restore \
    /p:UseSharedCompilation=false \
    --tl:off
fi

echo "Starting Linux TUI..."
dotnet "$app_dll" "$@"
