#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

rebuild=0
if [[ "${1:-}" == "--rebuild" || "${1:-}" == "--build" ]]; then
  rebuild=1
  shift
fi

project="src/StatisticsAnalysisTool.Web/StatisticsAnalysisTool.Web.csproj"
app_dll="src/StatisticsAnalysisTool.Web/bin/Debug/net10.0/StatisticsAnalysisTool.Web.dll"
listen_url="${SAT_WEB_URL:-http://127.0.0.1:5050}"

if [[ "$rebuild" -eq 1 || ! -f "$app_dll" ]]; then
  echo "Restoring Linux Web UI dependencies..."
  dotnet restore "$project" \
    --packages "${NUGET_PACKAGES:-$HOME/.nuget/packages}" \
    -m:1 \
    -v:minimal \
    --tl:off

  echo "Building Linux Web UI..."
  dotnet build "$project" \
    -c Debug \
    -m:1 \
    --no-restore \
    /p:UseSharedCompilation=false \
    --tl:off
fi

echo "Starting Linux Web UI on $listen_url ..."
echo "Set SAT_WEB_URL=http://127.0.0.1:5051 to use another port."
dotnet "$app_dll" "$@"
