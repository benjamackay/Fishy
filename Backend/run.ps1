<#
.SYNOPSIS
    Levanta el backend de Fishy! cargando el .env automáticamente.

.EXAMPLE
    .\run.ps1                  # servidor en 127.0.0.1:8000
    .\run.ps1 0.0.0.0:8000     # escuchando en todas las interfaces
    .\run.ps1 --smoke          # smoke test end-to-end
    .\run.ps1 --check          # verifica config y drift de migraciones

.NOTES
    Equivalente en PowerShell de run.sh. Compatible con Windows PowerShell 5.1.
#>

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot   # siempre trabajar desde Backend/, pase lo que pase

function Escribir-Rojo  { param($t) Write-Host $t -ForegroundColor Red }
function Escribir-Verde { param($t) Write-Host $t -ForegroundColor Green }
function Escribir-Gris  { param($t) Write-Host $t -ForegroundColor DarkGray }

# ── 1. El .env ────────────────────────────────────────────────────────────────
if (-not (Test-Path ".\.env")) {
    Escribir-Rojo "No existe Backend\.env"
    Escribir-Gris "Cópialo de la plantilla y pon la contraseña de Supabase:"
    Escribir-Gris "    Copy-Item .env.example .env"
    exit 1
}

# Se parsea con regex en vez de evaluar: un valor con `$`, paréntesis o espacios
# (típico en DJANGO_SECRET_KEY) no debe interpretarse como código.
Get-Content .\.env | ForEach-Object {
    $linea = $_.TrimEnd("`r")                      # el .env puede venir con CRLF
    if ($linea -match '^\s*#') { return }          # comentario
    if ($linea -match '^\s*([^=]+?)\s*=(.*)$') {
        Set-Item -Path ("env:" + $matches[1]) -Value $matches[2].Trim()
    }
}

# ── 2. Qué Python usar ────────────────────────────────────────────────────────
$py = $null
foreach ($candidato in @(".\.venv\Scripts\python.exe", ".\.venv\bin\python")) {
    if (Test-Path $candidato) { $py = $candidato; break }
}
if (-not $py) {
    Escribir-Rojo "No encontré el entorno virtual en Backend\.venv"
    Escribir-Gris "Créalo con:"
    Escribir-Gris "    python -m venv .venv"
    Escribir-Gris "    .\.venv\Scripts\python -m pip install -r backend\requirements.txt"
    exit 1
}

Escribir-Gris "Usando venv de Windows · BD en $($env:DB_HOST):$($env:DB_PORT)"

# ── 3. Qué hacer ──────────────────────────────────────────────────────────────
$modo = ""
if ($args.Count -gt 0) { $modo = [string]$args[0] }
$resto = @($args | Select-Object -Skip 1)

switch ($modo) {
    "--smoke" {
        Escribir-Verde "Smoke test end-to-end (necesita el servidor corriendo en otra terminal)"
        & $py .\scripts\smoke_test.py @resto
        exit $LASTEXITCODE
    }
    "--check" {
        & $py .\backend\manage.py check
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        & $py .\backend\manage.py makemigrations --check --dry-run
        exit $LASTEXITCODE
    }
    default {
        $dir = "127.0.0.1:8000"
        if ($modo) { $dir = $modo }
        Escribir-Verde "Servidor en http://$dir/api/  ·  Ctrl+C para detener"
        & $py .\backend\manage.py runserver $dir
        exit $LASTEXITCODE
    }
}
