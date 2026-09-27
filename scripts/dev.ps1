<#
.SYNOPSIS
  One-stop developer commands for the IPL Store (Windows / PowerShell).

.EXAMPLE
  ./scripts/dev.ps1 db        # start PostgreSQL in Docker
  ./scripts/dev.ps1 api       # run the API on http://localhost:5080 (Swagger at /swagger)
  ./scripts/dev.ps1 web       # run the React app on http://localhost:5173
  ./scripts/dev.ps1 test      # unit tests (+ integration tests if Docker is running)
  ./scripts/dev.ps1 full      # everything in containers (web on http://localhost:3000)
  ./scripts/dev.ps1 down      # stop containers
#>
param(
  [Parameter(Mandatory = $true)]
  [ValidateSet('db', 'api', 'web', 'test', 'unit', 'full', 'down', 'reset')]
  [string]$Command
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

switch ($Command) {
  'db'    { docker compose -f "$root/docker-compose.yml" up -d postgres }
  'api'   { dotnet run --project "$root/backend/src/IplStore.Api" --launch-profile IplStore.Api }
  'web'   {
            Push-Location "$root/frontend"
            if (-not (Test-Path node_modules)) { npm install }
            npm run dev
            Pop-Location
          }
  'unit'  { dotnet test "$root/backend/tests/IplStore.UnitTests" }
  'test'  {
            dotnet test "$root/backend/IplStore.sln"
            Push-Location "$root/frontend"; if (-not (Test-Path node_modules)) { npm install }; npm test; Pop-Location
          }
  'full'  { docker compose -f "$root/docker-compose.yml" --profile full up -d --build }
  'down'  { docker compose -f "$root/docker-compose.yml" --profile full down }
  'reset' { docker compose -f "$root/docker-compose.yml" --profile full down -v }  # also deletes the DB volume
}
