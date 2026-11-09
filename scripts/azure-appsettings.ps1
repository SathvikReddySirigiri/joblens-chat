#Requires -Version 5.1
<#
.SYNOPSIS
  Apply JobLens Azure App Service settings from local user-secrets + freshly generated production secrets.

.DESCRIPTION
  Does not print secret values except the three newly generated production secrets at the end.
  Never commits or writes secrets to disk.

.PARAMETER ResourceGroup
  Azure resource group (default: rg-joblens).

.PARAMETER AppName
  Azure Web App name (required).
#>
[CmdletBinding()]
param(
    [string] $ResourceGroup = "rg-joblens",
    [Parameter(Mandatory = $true)]
    [string] $AppName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-AzCliReady {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Azure CLI (az) is not installed. Install it (e.g. winget install Microsoft.AzureCLI), then run az login."
    }

    & az account show --output none 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI is not logged in. Run: az login"
    }
}

function Get-UserSecretsMap {
    param([string] $ProjectPath)

    $map = @{}
    $raw = & dotnet user-secrets list --project $ProjectPath 2>$null
    if ($LASTEXITCODE -ne 0 -or $null -eq $raw) {
        return $map
    }

    foreach ($line in @($raw)) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $idx = $line.IndexOf(" = ")
        if ($idx -lt 1) { continue }
        $key = $line.Substring(0, $idx).Trim()
        $value = $line.Substring($idx + 3)
        if ($key.Length -gt 0) {
            $map[$key] = $value
        }
    }

    return $map
}

function ConvertFrom-SecureStringPlain {
    param([System.Security.SecureString] $Secure)

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        if ($bstr -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    }
}

function Read-RequiredValue {
    param(
        [string] $Label,
        [hashtable] $Secrets,
        [string] $Key,
        [switch] $AsSecureString
    )

    if ($Secrets.ContainsKey($Key) -and -not [string]::IsNullOrWhiteSpace([string]$Secrets[$Key])) {
        return [string]$Secrets[$Key]
    }

    Write-Host "Missing user-secret '$Key'. Enter $Label:" -ForegroundColor Yellow
    if ($AsSecureString) {
        $secure = Read-Host -AsSecureString
        return (ConvertFrom-SecureStringPlain -Secure $secure)
    }

    $plain = Read-Host
    if ([string]::IsNullOrWhiteSpace($plain)) {
        throw "A value is required for $Key."
    }
    return $plain.Trim()
}

function Fill-RandomBytes {
    param([byte[]] $Buffer)

    # RandomNumberGenerator.Fill is not available in Windows PowerShell 5.1.
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($Buffer)
    }
    finally {
        $rng.Dispose()
    }
}

function New-CryptoPassword {
    param(
        [int] $Length = 20,
        [string] $Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!#%*-_"
    )

    $upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    $lower = "abcdefghijklmnopqrstuvwxyz"
    $digit = "0123456789"
    $symbol = "!#%*-_"

    $bytes = New-Object byte[] $Length
    Fill-RandomBytes -Buffer $bytes

    $chars = New-Object char[] $Length
    for ($i = 0; $i -lt $Length; $i++) {
        $chars[$i] = $Alphabet[$bytes[$i] % $Alphabet.Length]
    }

    # Guarantee at least one of each required class (overwrite first four slots with fresh RNG picks).
    $classes = @($upper, $lower, $digit, $symbol)
    $pickBytes = New-Object byte[] 4
    Fill-RandomBytes -Buffer $pickBytes
    for ($i = 0; $i -lt 4; $i++) {
        $set = $classes[$i]
        $chars[$i] = $set[$pickBytes[$i] % $set.Length]
    }

    # Shuffle so required chars are not always at the front.
    for ($i = $Length - 1; $i -gt 0; $i--) {
        $swapByte = New-Object byte[] 1
        Fill-RandomBytes -Buffer $swapByte
        $j = $swapByte[0] % ($i + 1)
        $tmp = $chars[$i]
        $chars[$i] = $chars[$j]
        $chars[$j] = $tmp
    }

    return -join $chars
}

function New-CryptoAccessKey {
    param([int] $Length = 48)

    $alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"
    $bytes = New-Object byte[] $Length
    Fill-RandomBytes -Buffer $bytes
    $chars = New-Object char[] $Length
    for ($i = 0; $i -lt $Length; $i++) {
        $chars[$i] = $alphabet[$bytes[$i] % $alphabet.Length]
    }
    return -join $chars
}

function Invoke-AzQuiet {
    param([Parameter(ValueFromRemainingArguments = $true)][object[]] $AzArgs)

    & az @AzArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI command failed (exit $LASTEXITCODE)."
    }
}

# --- main ---
$repoRoot = Split-Path -Parent $PSScriptRoot
$webProject = Join-Path $repoRoot "src\JobLens.Web"
if (-not (Test-Path -LiteralPath $webProject)) {
    throw "Could not find JobLens.Web at $webProject. Run this script from the repo (./scripts/azure-appsettings.ps1)."
}

Test-AzCliReady

$domain = (& az webapp show -g $ResourceGroup -n $AppName --query defaultHostName -o tsv).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($domain)) {
    throw "Could not read defaultHostName for web app '$AppName' in resource group '$ResourceGroup'."
}

$localSecrets = Get-UserSecretsMap -ProjectPath $webProject

$aiProvider = "AzureOpenAI"
$aiModel = if ($localSecrets.ContainsKey("Ai:Model") -and $localSecrets["Ai:Model"]) { [string]$localSecrets["Ai:Model"] } else { "gpt-4.1-mini-1" }
$aiDisplayName = if ($localSecrets.ContainsKey("Ai:DisplayName") -and $localSecrets["Ai:DisplayName"]) { [string]$localSecrets["Ai:DisplayName"] } else { "Azure OpenAI gpt-4.1-mini" }
$aiEndpoint = Read-RequiredValue -Label "Ai:Endpoint" -Secrets $localSecrets -Key "Ai:Endpoint"
$aiApiKey = Read-RequiredValue -Label "Ai:ApiKey" -Secrets $localSecrets -Key "Ai:ApiKey" -AsSecureString
$edgarUserAgent = Read-RequiredValue -Label "Edgar:UserAgent" -Secrets $localSecrets -Key "Edgar:UserAgent"
$usaJobsApiKey = Read-RequiredValue -Label "Jobs:UsaJobs:ApiKey" -Secrets $localSecrets -Key "Jobs:UsaJobs:ApiKey" -AsSecureString
$usaJobsUserAgent = Read-RequiredValue -Label "Jobs:UsaJobs:UserAgent" -Secrets $localSecrets -Key "Jobs:UsaJobs:UserAgent"

$seedModerator = New-CryptoPassword -Length 20
$seedStudent = New-CryptoPassword -Length 20
$mcpAccessKey = New-CryptoAccessKey -Length 48

$mcpEndpoint = "https://$domain/mcp"

$settingPairs = @(
    "ASPNETCORE_ENVIRONMENT=Production",
    "AllowedHosts=$domain",
    "WEBSITES_ENABLE_APP_SERVICE_STORAGE=true",
    "Seed__ModeratorPassword=$seedModerator",
    "Seed__StudentPassword=$seedStudent",
    "Mcp__AccessKey=$mcpAccessKey",
    "Mcp__Endpoint=$mcpEndpoint",
    "Ai__Provider=$aiProvider",
    "Ai__Endpoint=$aiEndpoint",
    "Ai__Model=$aiModel",
    "Ai__ApiKey=$aiApiKey",
    "Ai__DisplayName=$aiDisplayName",
    "Edgar__UserAgent=$edgarUserAgent",
    "Jobs__UsaJobs__ApiKey=$usaJobsApiKey",
    "Jobs__UsaJobs__UserAgent=$usaJobsUserAgent"
)

$settingNames = @(
    "ASPNETCORE_ENVIRONMENT",
    "AllowedHosts",
    "WEBSITES_ENABLE_APP_SERVICE_STORAGE",
    "Seed__ModeratorPassword",
    "Seed__StudentPassword",
    "Mcp__AccessKey",
    "Mcp__Endpoint",
    "Ai__Provider",
    "Ai__Endpoint",
    "Ai__Model",
    "Ai__ApiKey",
    "Ai__DisplayName",
    "Edgar__UserAgent",
    "Jobs__UsaJobs__ApiKey",
    "Jobs__UsaJobs__UserAgent"
)

# One call; do not Write-Host the argument list (contains secrets).
Invoke-AzQuiet webapp config appsettings set -g $ResourceGroup -n $AppName --settings @settingPairs --output none

Invoke-AzQuiet webapp config set -g $ResourceGroup -n $AppName `
    --startup-file "dotnet JobLens.Web.dll" `
    --min-tls-version "1.2" `
    --ftps-state Disabled `
    --generic-configurations '{"healthCheckPath":"/healthz"}' `
    --output none

Invoke-AzQuiet webapp update -g $ResourceGroup -n $AppName --https-only true --output none

try {
    Invoke-AzQuiet resource update -g $ResourceGroup `
        --namespace Microsoft.Web `
        --resource-type basicPublishingCredentialsPolicies `
        --parent "sites/$AppName" `
        --name scm `
        --set properties.allow=false `
        --output none
}
catch {
    Write-Warning "Could not disable SCM basic auth: $($_.Exception.Message)"
}

try {
    Invoke-AzQuiet resource update -g $ResourceGroup `
        --namespace Microsoft.Web `
        --resource-type basicPublishingCredentialsPolicies `
        --parent "sites/$AppName" `
        --name ftp `
        --set properties.allow=false `
        --output none
}
catch {
    Write-Warning "Could not disable FTP basic auth: $($_.Exception.Message)"
}

$appUrl = "https://$domain"

Write-Host ""
Write-Host "App URL: $appUrl"
Write-Host "Settings applied (names only):"
foreach ($name in $settingNames) {
    Write-Host "  - $name"
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "SAVE THESE NOW in a password manager — they are not stored anywhere else" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Seed__ModeratorPassword: $seedModerator"
Write-Host "Seed__StudentPassword:   $seedStudent"
Write-Host "Mcp__AccessKey:          $mcpAccessKey"
Write-Host "============================================================" -ForegroundColor Cyan

# Clear sensitive in-memory values.
$aiApiKey = $null
$usaJobsApiKey = $null
$seedModerator = $null
$seedStudent = $null
$mcpAccessKey = $null
$settingPairs = $null
$localSecrets = $null
[GC]::Collect()

Write-Host ""
Write-Host "Next: Deployment Center -> GitHub -> main, then open https://$domain/healthz"
