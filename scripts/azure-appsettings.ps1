<#
.SYNOPSIS
  Apply JobLens Azure App Service settings from local user-secrets plus freshly generated production secrets.
.DESCRIPTION
  Reads AI / EDGAR / USAJOBS values from `dotnet user-secrets` (memory only), generates new production
  passwords and an MCP access key, and applies everything to the Web App with the Azure CLI.
  Nothing is written to disk or committed. Only the three newly generated values are printed once at the end.
  ASCII only on purpose (Windows PowerShell 5.1 reads BOM-less files as ANSI).
.EXAMPLE
  az login
  powershell -ExecutionPolicy Bypass -File scripts/azure-appsettings.ps1 -AppName joblens-ism6225
#>
[CmdletBinding()]
param(
    [string] $ResourceGroup = "rg-joblens",
    [Parameter(Mandatory = $true)]
    [string] $AppName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Run az with an explicit argument array (no PowerShell parameter binding, no splatting surprises).
function Invoke-Az {
    param([string[]] $Arguments)
    & az @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ("Azure CLI command failed (exit {0}): az {1} {2} ..." -f $LASTEXITCODE, $Arguments[0], $Arguments[1])
    }
}

function Get-UserSecretsMap {
    param([string] $ProjectPath)
    $map = @{}
    $previous = $ErrorActionPreference; $ErrorActionPreference = "Continue"
    $raw = & dotnet user-secrets list --project $ProjectPath 2>$null
    $ErrorActionPreference = $previous
    if ($LASTEXITCODE -ne 0 -or $null -eq $raw) { return $map }
    foreach ($line in @($raw)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $idx = $line.IndexOf(" = ")
        if ($idx -lt 1) { continue }
        $map[$line.Substring(0, $idx).Trim()] = $line.Substring($idx + 3).Trim()
    }
    return $map
}

function ConvertFrom-SecureStringPlain {
    param([System.Security.SecureString] $Secure)
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

function Read-RequiredValue {
    param([hashtable] $Secrets, [string] $Key, [switch] $Secret)
    if ($Secrets.ContainsKey($Key) -and -not [string]::IsNullOrWhiteSpace([string]$Secrets[$Key])) {
        return [string]$Secrets[$Key]
    }
    Write-Host ("Missing user-secret '{0}'. Enter the value:" -f $Key) -ForegroundColor Yellow
    if ($Secret) {
        $value = ConvertFrom-SecureStringPlain -Secure (Read-Host -AsSecureString)
    } else {
        $value = Read-Host
    }
    if ([string]::IsNullOrWhiteSpace($value)) { throw ("A value is required for {0}." -f $Key) }
    return $value.Trim()
}

function Get-RandomBytes {
    param([int] $Count)
    $bytes = New-Object byte[] $Count
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return ,$bytes
}

# Symbols limited to "-" and "_" so cmd.exe (az.cmd) never rewrites the value (no % ! ^ & | < > ").
function New-CryptoPassword {
    param([int] $Length = 20)
    $upper = "ABCDEFGHJKLMNPQRSTUVWXYZ"
    $lower = "abcdefghijkmnopqrstuvwxyz"
    $digit = "23456789"
    $symbol = "-_"
    $all = $upper + $lower + $digit + $symbol
    $bytes = Get-RandomBytes -Count ($Length * 2)
    $chars = New-Object char[] $Length
    for ($i = 0; $i -lt $Length; $i++) { $chars[$i] = $all[$bytes[$i] % $all.Length] }
    $sets = @($upper, $lower, $digit, $symbol)
    for ($i = 0; $i -lt 4; $i++) { $chars[$i] = $sets[$i][$bytes[$Length + $i] % $sets[$i].Length] }
    for ($i = $Length - 1; $i -gt 0; $i--) {
        $j = $bytes[$Length + 4 + ($i % ($Length - 4))] % ($i + 1)
        $tmp = $chars[$i]; $chars[$i] = $chars[$j]; $chars[$j] = $tmp
    }
    return -join $chars
}

function New-CryptoAccessKey {
    param([int] $Length = 48)
    $alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"
    $bytes = Get-RandomBytes -Count $Length
    $chars = New-Object char[] $Length
    for ($i = 0; $i -lt $Length; $i++) { $chars[$i] = $alphabet[$bytes[$i] % $alphabet.Length] }
    return -join $chars
}

# --- Preconditions -----------------------------------------------------------------------------------
$repoRoot = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $repoRoot "src\JobLens.Web"
if (-not (Test-Path -LiteralPath $webProject)) {
    throw ("Could not find src\JobLens.Web under {0}. Run from the repo: scripts\azure-appsettings.ps1" -f $repoRoot)
}
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) is not installed. Run: winget install Microsoft.AzureCLI  (then reopen the terminal and run az login)"
}
# Windows PowerShell 5.1 turns redirected native stderr into errors under "Stop", so relax it for these checks.
$ErrorActionPreference = "Continue"
& az account show --output none 2>$null
$loggedIn = ($LASTEXITCODE -eq 0)
$domain = ""
if ($loggedIn) {
    $domain = [string](& az webapp show -g $ResourceGroup -n $AppName --query defaultHostName -o tsv 2>$null)
}
$showExit = $LASTEXITCODE
$ErrorActionPreference = "Stop"
if (-not $loggedIn) { throw "Azure CLI is not logged in. Run: az login" }
$domain = $domain.Trim()
if ($showExit -ne 0 -or [string]::IsNullOrWhiteSpace($domain)) {
    throw ("Web app '{0}' was not found in resource group '{1}'. Check -AppName (the app name, not the full domain)." -f $AppName, $ResourceGroup)
}
Write-Host ("Web app found: https://{0}" -f $domain) -ForegroundColor Green

# --- Values ------------------------------------------------------------------------------------------
$local = Get-UserSecretsMap -ProjectPath $webProject

$aiModel = "gpt-4.1-mini-1"
if ($local.ContainsKey("Ai:Model") -and $local["Ai:Model"]) { $aiModel = [string]$local["Ai:Model"] }
$aiDisplayName = "Azure OpenAI gpt-4.1-mini"
if ($local.ContainsKey("Ai:DisplayName") -and $local["Ai:DisplayName"]) { $aiDisplayName = [string]$local["Ai:DisplayName"] }

$aiEndpoint       = Read-RequiredValue -Secrets $local -Key "Ai:Endpoint"
$aiApiKey         = Read-RequiredValue -Secrets $local -Key "Ai:ApiKey" -Secret
$edgarUserAgent   = Read-RequiredValue -Secrets $local -Key "Edgar:UserAgent"
$usaJobsApiKey    = Read-RequiredValue -Secrets $local -Key "Jobs:UsaJobs:ApiKey" -Secret
$usaJobsUserAgent = Read-RequiredValue -Secrets $local -Key "Jobs:UsaJobs:UserAgent"

$seedModerator = New-CryptoPassword -Length 20
$seedStudent   = New-CryptoPassword -Length 20
$mcpAccessKey  = New-CryptoAccessKey -Length 48

$settings = [ordered]@{
    "ASPNETCORE_ENVIRONMENT"              = "Production"
    "AllowedHosts"                        = $domain
    "WEBSITES_ENABLE_APP_SERVICE_STORAGE" = "true"
    "Seed__ModeratorPassword"             = $seedModerator
    "Seed__StudentPassword"               = $seedStudent
    "Mcp__AccessKey"                      = $mcpAccessKey
    "Mcp__Endpoint"                       = ("https://{0}/mcp" -f $domain)
    "Ai__Provider"                        = "AzureOpenAI"
    "Ai__Endpoint"                        = $aiEndpoint
    "Ai__Model"                           = $aiModel
    "Ai__ApiKey"                          = $aiApiKey
    "Ai__DisplayName"                     = $aiDisplayName
    "Edgar__UserAgent"                    = $edgarUserAgent
    "Jobs__UsaJobs__ApiKey"               = $usaJobsApiKey
    "Jobs__UsaJobs__UserAgent"            = $usaJobsUserAgent
}

# --- Apply -------------------------------------------------------------------------------------------
Write-Host "Applying app settings..." -ForegroundColor Cyan
$appSettingsArgs = @("webapp", "config", "appsettings", "set", "-g", $ResourceGroup, "-n", $AppName, "--output", "none", "--settings")
foreach ($name in $settings.Keys) { $appSettingsArgs += ("{0}={1}" -f $name, $settings[$name]) }
Invoke-Az -Arguments $appSettingsArgs

Write-Host "Applying general settings (startup command, TLS 1.2, FTPS off, health check)..." -ForegroundColor Cyan
Invoke-Az -Arguments @("webapp", "config", "set", "-g", $ResourceGroup, "-n", $AppName,
    "--startup-file", "dotnet JobLens.Web.dll",
    "--min-tls-version", "1.2",
    "--ftps-state", "Disabled",
    "--generic-configurations", "healthCheckPath=/healthz",
    "--output", "none")
Invoke-Az -Arguments @("webapp", "update", "-g", $ResourceGroup, "-n", $AppName, "--https-only", "true", "--output", "none")

foreach ($kind in @("scm", "ftp")) {
    try {
        Invoke-Az -Arguments @("resource", "update", "-g", $ResourceGroup,
            "--namespace", "Microsoft.Web", "--resource-type", "basicPublishingCredentialsPolicies",
            "--parent", ("sites/{0}" -f $AppName), "--name", $kind,
            "--set", "properties.allow=false", "--output", "none")
    } catch {
        Write-Warning ("Could not disable {0} basic auth (you can do it in Configuration > General settings)." -f $kind)
    }
}

# --- Report ------------------------------------------------------------------------------------------
Write-Host ""
Write-Host ("App URL: https://{0}" -f $domain) -ForegroundColor Green
Write-Host "Settings applied (names only):"
foreach ($name in $settings.Keys) { Write-Host ("  - {0}" -f $name) }
Write-Host ""
Write-Host "============================================================" -ForegroundColor Yellow
Write-Host " SAVE THESE NOW in a password manager - not stored anywhere else" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Yellow
Write-Host ("Moderator login : moderator@joblens.demo / {0}" -f $seedModerator)
Write-Host ("Student login   : student1@joblens.demo  / {0}" -f $seedStudent)
Write-Host ("MCP access key  : {0}" -f $mcpAccessKey)
Write-Host "============================================================" -ForegroundColor Yellow

$aiApiKey = $null; $usaJobsApiKey = $null; $seedModerator = $null; $seedStudent = $null
$mcpAccessKey = $null; $settings = $null; $appSettingsArgs = $null; $local = $null
[GC]::Collect()

Write-Host ""
Write-Host ("Next: Deployment Center -> GitHub -> main, then open https://{0}/healthz" -f $domain)
