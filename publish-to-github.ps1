param(
    [string]$RepositoryName = "gestion-personal",
    [ValidateSet("private", "public")]
    [string]$Visibility = "private"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI no está instalado. Instálalo desde https://cli.github.com/ y ejecuta gh auth login."
}

gh auth status

if (-not (Test-Path ".git")) {
    git init -b main
}

git add .
if (-not (git diff --cached --quiet)) {
    git commit -m "Initialize project documentation and structure"
}

$owner = gh api user --jq .login
$fullName = "$owner/$RepositoryName"

$exists = $false
try {
    gh repo view $fullName *> $null
    $exists = $true
} catch {
    $exists = $false
}

if (-not $exists) {
    gh repo create $fullName --$Visibility --source . --remote origin --push
} else {
    if (-not (git remote get-url origin 2>$null)) {
        git remote add origin "https://github.com/$fullName.git"
    }
    git push -u origin main
}

Write-Host "Repositorio publicado: https://github.com/$fullName"
Write-Host "Para clonarlo: git clone https://github.com/$fullName.git"
