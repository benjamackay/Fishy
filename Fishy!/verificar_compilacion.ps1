<#
.SYNOPSIS
    Compila todos los scripts C# del juego sin abrir Unity.

.DESCRIPTION
    Usa el compilador Roslyn que viene con Unity y la lista de referencias del
    .csproj que Unity ya generó (Assembly-CSharp.csproj), así que es la misma
    compilación que hace el editor: mismos ensamblados, mismos símbolos de
    compilación, misma versión del lenguaje.

    Sirve para descartar errores de compilación en segundos antes de abrir Unity,
    que en este proyecto tarda minutos en importar. NO reemplaza probar el juego:
    no valida referencias de escena, prefabs, ni nada que pase en runtime.

    Compila cada assembly definition (Fishy.Mision y compañía) antes que
    Assembly-CSharp y lo referencia recién compilado, no el .dll viejo de
    Library\ScriptAssemblies: si no, un cambio en MissionManager no se vería y
    todo lo que use ese namespace daría error de "no existe" sin motivo.

    También compila los scripts que están en Assets pero que Unity todavía no
    agregó al .csproj, asignándolos al assembly de su carpeta. Así un archivo
    recién creado se verifica sin tener que abrir el editor primero.

.EXAMPLE
    .\verificar_compilacion.ps1

.NOTES
    Requiere que exista Assembly-CSharp.csproj, que Unity regenera al abrir el
    proyecto. Si agregaste scripts nuevos y Unity no lo ha reabierto, el script
    avisa que el .csproj quedó desactualizado.
#>

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

function Escribir-Rojo  { param($t) Write-Host $t -ForegroundColor Red }
function Escribir-Verde { param($t) Write-Host $t -ForegroundColor Green }
function Escribir-Ambar { param($t) Write-Host $t -ForegroundColor Yellow }
function Escribir-Gris  { param($t) Write-Host $t -ForegroundColor DarkGray }

# ── 1. Ubicar el editor de Unity que corresponde al proyecto ──────────────────
$versionFile = ".\ProjectSettings\ProjectVersion.txt"
if (-not (Test-Path $versionFile)) {
    Escribir-Rojo "No encuentro ProjectSettings\ProjectVersion.txt"
    Escribir-Gris "¿Estás corriendo esto dentro de la carpeta del proyecto Unity?"
    exit 1
}
$version = (Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(.+)$').Matches[0].Groups[1].Value.Trim()

$editor = Join-Path "C:\Program Files\Unity\Hub\Editor" $version
if (-not (Test-Path $editor)) {
    # El editor exacto no está: cualquier 6000.x sirve para compilar C#.
    $alternativa = Get-ChildItem "C:\Program Files\Unity\Hub\Editor" -Directory -ErrorAction SilentlyContinue |
                   Sort-Object Name -Descending | Select-Object -First 1
    if (-not $alternativa) {
        Escribir-Rojo "No encontré ninguna instalación de Unity en C:\Program Files\Unity\Hub\Editor"
        exit 1
    }
    Escribir-Ambar "El proyecto pide Unity $version, uso $($alternativa.Name) para compilar."
    $editor = $alternativa.FullName
}

$dotnet = Join-Path $editor "Editor\Data\NetCoreRuntime\dotnet.exe"
$csc    = Join-Path $editor "Editor\Data\DotNetSdkRoslyn\csc.dll"
foreach ($archivo in @($dotnet, $csc)) {
    if (-not (Test-Path $archivo)) {
        Escribir-Rojo "Falta $archivo"
        Escribir-Gris "Esa instalación de Unity no trae el compilador de línea de comandos."
        exit 1
    }
}

# ── 2. Leer el .csproj ────────────────────────────────────────────────────────
$csproj = ".\Assembly-CSharp.csproj"
if (-not (Test-Path $csproj)) {
    Escribir-Rojo "No existe $csproj"
    Escribir-Gris "Lo genera Unity al abrir el proyecto. Ábrelo una vez y vuelve a intentar."
    exit 1
}
$xml = [xml](Get-Content $csproj -Raw)

$fuentes = @($xml.Project.ItemGroup.Compile | Where-Object { $_ } | ForEach-Object { $_.Include })
$projRefs = @($xml.Project.ItemGroup.ProjectReference | Where-Object { $_ } | ForEach-Object { $_.Include })
$refs    = @($xml.Project.ItemGroup.Reference | Where-Object { $_ } | ForEach-Object { $_.HintPath })
$props   = $xml.Project.PropertyGroup | Where-Object { $_.DefineConstants }
$defines = ($props | Select-Object -First 1).DefineConstants
$lang    = ($xml.Project.PropertyGroup | Where-Object { $_.LangVersion } | Select-Object -First 1).LangVersion
if (-not $lang) { $lang = "9.0" }

# Referencias que el .csproj declara pero no existen en disco. En este proyecto
# suele pasar con rutas de Library\PackageCache que superan los 260 caracteres de
# Windows; se omiten para que la compilación pueda seguir.
$refsOk       = @($refs | Where-Object { Test-Path -LiteralPath $_ })
$refsFaltantes = @($refs | Where-Object { -not (Test-Path -LiteralPath $_) })

# ── 3. Avisar si el .csproj quedó viejo ───────────────────────────────────────
$enDisco = Get-ChildItem ".\Assets" -Filter *.cs -Recurse -File |
           ForEach-Object { $_.FullName.Substring($PSScriptRoot.Length + 1) }
# Lo que ya esta listado en CUALQUIER .csproj: el de Assembly-CSharp, el de
# Editor y el de cada assembly definition. Mirar solo el principal marcaba como
# "sin listar" a los scripts de Fishy.Mision, que si estan en el suyo, y los
# terminaba compilando dos veces.
$conocidos = @()
foreach ($proj in (Get-ChildItem "." -Filter *.csproj -File)) {
    try {
        $xmlProj = [xml](Get-Content $proj.FullName -Raw)
        $conocidos += @($xmlProj.Project.ItemGroup.Compile | Where-Object { $_ } | ForEach-Object { $_.Include })
    } catch { }
}
$sinListar = @($enDisco | Where-Object { $conocidos -notcontains $_ })

# Carpeta de cada assembly definition: un .cs pertenece al asmdef mas profundo
# que lo contenga, igual que decide Unity.
$asmdefs = @(Get-ChildItem ".\Assets" -Filter *.asmdef -Recurse -File | ForEach-Object {
    $nombreAsm = ($_.BaseName)
    try {
        $json = Get-Content $_.FullName -Raw | ConvertFrom-Json
        if ($json.name) { $nombreAsm = $json.name }
    } catch { }
    [pscustomobject]@{
        Nombre  = $nombreAsm
        Carpeta = $_.DirectoryName.Substring($PSScriptRoot.Length + 1) + "\"
    }
})

function Assembly-De-Script {
    param($rutaRelativa)
    $mejor = $null
    foreach ($a in $asmdefs) {
        if ($rutaRelativa.StartsWith($a.Carpeta, [StringComparison]::OrdinalIgnoreCase)) {
            if ($null -eq $mejor -or $a.Carpeta.Length -gt $mejor.Carpeta.Length) { $mejor = $a }
        }
    }
    if ($mejor) { return $mejor.Nombre }
    return "Assembly-CSharp"
}

# ── 4. Compilar ───────────────────────────────────────────────────────────────
Write-Host ""
Write-Host ("=" * 70)
Write-Host "VERIFICACION DE COMPILACION  ·  Unity $version"
Write-Host ("=" * 70)
Escribir-Gris "  $($fuentes.Count) scripts, $($refsOk.Count) referencias"
if ($refsFaltantes.Count -gt 0) {
    Escribir-Gris "  $($refsFaltantes.Count) referencia(s) omitida(s) por no existir en disco (ruta larga)"
}

function Compilar {
    param($nombre, $fuentesAsm, $referencias)

    $salidaAsm = Join-Path $env:TEMP "fishy_$nombre.dll"
    $rspAsm    = Join-Path $env:TEMP "fishy_$nombre.rsp"

    $lineas = New-Object System.Collections.Generic.List[string]
    $lineas.Add("-target:library")
    $lineas.Add("-langversion:$lang")
    $lineas.Add("-nostdlib+")
    $lineas.Add("-out:`"$salidaAsm`"")
    $lineas.Add("-define:$defines")
    foreach ($r in $referencias) { $lineas.Add("-r:`"$r`"") }
    foreach ($f in $fuentesAsm)  { $lineas.Add("`"$f`"") }
    [System.IO.File]::WriteAllLines($rspAsm, $lineas, (New-Object System.Text.UTF8Encoding $false))

    $salidaTexto = & $dotnet $csc "@$rspAsm"
    return [pscustomobject]@{
        Codigo = $LASTEXITCODE
        Texto  = $salidaTexto
        Dll    = $salidaAsm
    }
}

# Cada assembly definition primero: Assembly-CSharp los referencia recien
# compilados, para que un cambio en ellos se vea en la misma corrida.
$dllsPropias = @()
$resultado   = @()
$codigo      = 0

foreach ($pref in $projRefs) {
    $nombreAsm = [System.IO.Path]::GetFileNameWithoutExtension($pref)
    if (-not (Test-Path ".\$pref")) {
        Escribir-Ambar "  No existe $pref; se referencia el .dll ya compilado si lo hay."
        $dllViejo = ".\Library\ScriptAssemblies\$nombreAsm.dll"
        if (Test-Path $dllViejo) { $dllsPropias += (Resolve-Path $dllViejo).Path }
        continue
    }

    $xmlAsm = [xml](Get-Content ".\$pref" -Raw)
    $fuentesAsm = @($xmlAsm.Project.ItemGroup.Compile | Where-Object { $_ } | ForEach-Object { $_.Include })
    $fuentesAsm += @($sinListar | Where-Object { (Assembly-De-Script $_) -eq $nombreAsm })
    $refsAsm = @($xmlAsm.Project.ItemGroup.Reference | Where-Object { $_ } | ForEach-Object { $_.HintPath })
    $refsAsm = @($refsAsm | Where-Object { Test-Path -LiteralPath $_ })

    Escribir-Gris "  $nombreAsm : $($fuentesAsm.Count) scripts"
    $r = Compilar $nombreAsm $fuentesAsm ($refsAsm + $dllsPropias)
    $resultado += $r.Texto
    if ($r.Codigo -ne 0) { $codigo = $r.Codigo }
    if (Test-Path $r.Dll) { $dllsPropias += $r.Dll }
}

# Los que no caen en ningun asmdef son de Assembly-CSharp.
$propios = @($sinListar | Where-Object { (Assembly-De-Script $_) -eq "Assembly-CSharp" })
if ($propios.Count -gt 0) {
    Escribir-Gris "  + $($propios.Count) script(s) que el .csproj todavia no lista"
    $fuentes += $propios
}

$r = Compilar "AssemblyCSharp" $fuentes ($refsOk + $dllsPropias)
$resultado += $r.Texto
if ($r.Codigo -ne 0) { $codigo = $r.Codigo }
$salida = $r.Dll

$errores  = @($resultado | Where-Object { $_ -match ': error ' })
$avisos   = @($resultado | Where-Object { $_ -match ': warning ' -and $_ -notmatch 'CS2023' })

Write-Host ""
if ($errores.Count -gt 0) {
    Escribir-Rojo "  $($errores.Count) ERROR(ES) DE COMPILACION:"
    Write-Host ""
    $errores | ForEach-Object { Escribir-Rojo "    $_" }
}
if ($avisos.Count -gt 0) {
    Escribir-Gris "  $($avisos.Count) advertencia(s) (no impiden compilar):"
    $avisos | Select-Object -First 5 | ForEach-Object { Escribir-Gris "    $_" }
    if ($avisos.Count -gt 5) { Escribir-Gris "    ... y $($avisos.Count - 5) mas" }
}
if ($sinListar.Count -gt 0) {
    Write-Host ""
    Escribir-Ambar "  OJO: $($sinListar.Count) script(s) existen en Assets pero no estan en el .csproj."
    Escribir-Ambar "  Se compilaron igual, asignados al assembly de su carpeta, pero abre Unity"
    Escribir-Ambar "  una vez para que regenere los proyectos:"
    $sinListar | ForEach-Object { Escribir-Ambar "    $_  ->  $(Assembly-De-Script $_)" }
    Escribir-Gris  "  (los de un assembly de tests no se compilan: Unity los corre aparte)"
}

Write-Host ""
Write-Host ("=" * 70)
if ($codigo -eq 0 -and $errores.Count -eq 0) {
    Escribir-Verde "RESULTADO: compila sin errores"
    Write-Host ("=" * 70)
    exit 0
}
Escribir-Rojo "RESULTADO: NO compila"
Write-Host ("=" * 70)
exit 1
