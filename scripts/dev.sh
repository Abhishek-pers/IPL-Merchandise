#!/usr/bin/env bash
# Developer commands (macOS / Linux). Usage: ./scripts/dev.sh db|api|web|test|unit|full|down|reset
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
case "${1:-}" in
  db)    docker compose -f "$ROOT/docker-compose.yml" up -d postgres ;;
  api)   dotnet run --project "$ROOT/backend/src/IplStore.Api" --launch-profile IplStore.Api ;;
  web)   cd "$ROOT/frontend"; [ -d node_modules ] || npm install; npm run dev ;;
  unit)  dotnet test "$ROOT/backend/tests/IplStore.UnitTests" ;;
  test)  dotnet test "$ROOT/backend/IplStore.sln"; cd "$ROOT/frontend"; [ -d node_modules ] || npm install; npm test ;;
  full)  docker compose -f "$ROOT/docker-compose.yml" --profile full up -d --build ;;
  down)  docker compose -f "$ROOT/docker-compose.yml" --profile full down ;;
  reset) docker compose -f "$ROOT/docker-compose.yml" --profile full down -v ;;
  *) echo "usage: $0 db|api|web|test|unit|full|down|reset"; exit 1 ;;
esac
