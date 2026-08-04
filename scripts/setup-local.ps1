[CmdletBinding()]
param(
    [switch]$SkipStart
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentPath = Join-Path $repositoryRoot '.env'
$frontendEnvironmentPath = Join-Path $repositoryRoot 'frontend\.env.local'
$certificateDirectory = Join-Path $repositoryRoot '.local\https'
$certificatePath = Join-Path $certificateDirectory 'legaria-local.pfx'

function New-RandomBase64([int]$byteCount) {
    $bytes = New-Object byte[] $byteCount
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }

    return [Convert]::ToBase64String($bytes)
}

function Read-DotEnv([string]$path) {
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $path) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) {
            continue
        }

        $separatorIndex = $trimmed.IndexOf('=')
        if ($separatorIndex -gt 0) {
            $name = $trimmed.Substring(0, $separatorIndex)
            $value = $trimmed.Substring($separatorIndex + 1)
            $values[$name] = $value
        }
    }

    return $values
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK 8 no esta disponible en PATH.'
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker no esta disponible en PATH.'
}

if (-not (Test-Path -LiteralPath $environmentPath) -or
    (Get-Item -LiteralPath $environmentPath).Length -eq 0) {
    throw 'Copia .env.example a .env y completa POSTGRES_PASSWORD y BOOTSTRAP_OWNER_PASSWORD.'
}

$environment = Read-DotEnv $environmentPath
$generatedSecret = $false
foreach ($secretVariable in @('JWT_SIGNING_KEY', 'CERT_PASSWORD')) {
    if (-not $environment.ContainsKey($secretVariable) -or
        [string]::IsNullOrWhiteSpace($environment[$secretVariable]) -or
        $environment[$secretVariable] -eq '__GENERATE__') {
        $byteCount = if ($secretVariable -eq 'JWT_SIGNING_KEY') { 48 } else { 32 }
        $environment[$secretVariable] = New-RandomBase64 $byteCount
        $generatedSecret = $true
    }
}

if ($generatedSecret) {
    $existingLines = Get-Content -LiteralPath $environmentPath
    $updatedLines = foreach ($line in $existingLines) {
        $separatorIndex = $line.IndexOf('=')
        $name = if ($separatorIndex -gt 0) { $line.Substring(0, $separatorIndex) } else { $line }
        if ($name -in @('JWT_SIGNING_KEY', 'CERT_PASSWORD')) {
            "$name=$($environment[$name])"
        }
        else {
            $line
        }
    }
    $utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllLines($environmentPath, $updatedLines, $utf8WithoutBom)
    Write-Host 'Los secretos aleatorios de JWT y certificado se generaron dentro de .env y no se muestran.'
}

$requiredVariables = @(
    'POSTGRES_DB',
    'POSTGRES_USER',
    'POSTGRES_PASSWORD',
    'POSTGRES_PORT',
    'API_HTTPS_PORT',
    'JWT_ISSUER',
    'JWT_AUDIENCE',
    'JWT_SIGNING_KEY',
    'CERT_PASSWORD',
    'RESEND_API_KEY',
    'RESEND_FROM_EMAIL',
    'RESEND_FROM_NAME',
    'RESEND_REPLY_TO_EMAIL',
    'FRONTEND_BASE_URL',
    'BOOTSTRAP_OWNER_EMAIL',
    'BOOTSTRAP_OWNER_PASSWORD',
    'BOOTSTRAP_OWNER_FIRST_NAME',
    'BOOTSTRAP_OWNER_LAST_NAME'
)

foreach ($requiredVariable in $requiredVariables) {
    if (-not $environment.ContainsKey($requiredVariable) -or
        [string]::IsNullOrWhiteSpace($environment[$requiredVariable])) {
        throw "Falta $requiredVariable en .env."
    }
}

if ([Text.Encoding]::UTF8.GetByteCount($environment['JWT_SIGNING_KEY']) -lt 32) {
    throw 'JWT_SIGNING_KEY debe tener al menos 32 bytes.'
}

$apiHttpsPort = if ($environment.ContainsKey('API_HTTPS_PORT')) {
    $environment['API_HTTPS_PORT']
}
else {
    '7007'
}
$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines(
    $frontendEnvironmentPath,
    @("VITE_API_BASE_URL=https://localhost:$apiHttpsPort"),
    $utf8WithoutBom)

New-Item -ItemType Directory -Path $certificateDirectory -Force | Out-Null
if (-not (Test-Path -LiteralPath $certificatePath)) {
    & dotnet dev-certs https --export-path $certificatePath --password $environment['CERT_PASSWORD'] --trust
    if ($LASTEXITCODE -ne 0) {
        throw 'No fue posible generar y confiar el certificado HTTPS local.'
    }
    Write-Host 'Certificado HTTPS local generado y confiado.'
}

if (-not $SkipStart) {
    Push-Location $repositoryRoot
    try {
        & docker compose up --detach --build
        if ($LASTEXITCODE -ne 0) {
            throw 'Docker Compose no pudo iniciar Legaria.'
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host ''
Write-Host 'Legaria local preparada:'
Write-Host "  API: https://localhost:$apiHttpsPort"
Write-Host "  PostgreSQL: localhost:$($environment['POSTGRES_PORT']) / legaria / legaria_local"
Write-Host "  Usuario: $($environment['BOOTSTRAP_OWNER_EMAIL'])"
Write-Host 'La clave JWT y la contrasena del certificado permanecen solo en .env.'
