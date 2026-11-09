# Runs scripts/db-counts.sql against the local JobLens SQLite file.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sqlPath = Join-Path $PSScriptRoot "db-counts.sql"
$dbPath = Join-Path $root "src\JobLens.Web\App_Data\joblens.db"

$sqlite = Get-Command sqlite3 -ErrorAction SilentlyContinue
if (-not $sqlite) {
    Write-Host "sqlite3 was not found on PATH."
    Write-Host "Install SQLite CLI, then re-run this script:"
    Write-Host "  winget install SQLite.SQLite"
    exit 1
}

if (-not (Test-Path $dbPath)) {
    Write-Host "Database not found at $dbPath"
    Write-Host "Start the app once (or seed) so App_Data/joblens.db exists."
    exit 1
}

Write-Host "Database: $dbPath"
& sqlite3 $dbPath ".read $sqlPath"
exit $LASTEXITCODE
