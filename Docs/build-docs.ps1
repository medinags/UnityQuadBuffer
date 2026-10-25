# Builds the documentation site into Docs/_site:
#   Doxygen -> native API (Docs/obj/doxygen), DocFX -> manual, C# API and the rest.
# See Docs/development/documentation.md.
param(
    # Unity version whose reference assemblies DocFX uses. Defaults to the test project's version.
    [string]$UnityVersion,
    # Unity Editor install folder (the one containing Editor/Unity.exe). Overrides $UnityVersion.
    [string]$UnityEditorPath,
    # Skip the Doxygen step (native API pages will be missing).
    [switch]$SkipNative,
    # Serve the site at http://localhost:8080 after building.
    [switch]$Serve
)

$ErrorActionPreference = "Stop"
$docs = $PSScriptRoot
$repo = Split-Path $docs -Parent
$obj = Join-Path $docs "obj"
Set-Location $docs

function Invoke-Checked([string]$exe, [string[]]$arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { throw "$exe exited with code $LASTEXITCODE" }
}

# 1. Package version, used as the Doxygen project number.
$package = Get-Content (Join-Path $repo "Packages/com.xra.quadbuffer/package.json") -Raw | ConvertFrom-Json
$env:QB_VERSION = $package.version
Write-Host "Documenting com.xra.quadbuffer $($package.version)"

# 2. Unity reference assemblies, so DocFX can resolve UnityEngine/UnityEditor types.
if (-not $UnityEditorPath) {
    if (-not $UnityVersion) {
        $versionFile = Get-Content (Join-Path $repo "TestProject/ProjectSettings/ProjectVersion.txt")
        $UnityVersion = ($versionFile | Select-String "m_EditorVersion: (.+)").Matches[0].Groups[1].Value.Trim()
    }
    $UnityEditorPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$UnityVersion"
}
$data = Join-Path $UnityEditorPath "Editor/Data"
if (-not (Test-Path $data)) { throw "Unity Editor not found at $UnityEditorPath. Pass -UnityVersion or -UnityEditorPath." }

# Not under obj/: DocFX ignores obj/ and bin/ folders when matching files.
$refs = Join-Path $docs "_unityrefs"
if (Test-Path $refs) { Remove-Item -Recurse -Force $refs }
New-Item -ItemType Directory -Force $refs | Out-Null
# All engine/editor modules (e.g. GUI lives in UnityEngine.IMGUIModule). The .NET Standard 2.1
# framework comes from api-project/QuadBuffer.Docs.csproj's target framework, not from here.
Copy-Item -Force (Join-Path $data "Managed/UnityEngine/*.dll") $refs

# 3. Native API with Doxygen.
# Not under obj/: DocFX ignores obj/ and bin/ folders when matching files.
$doxygenOut = Join-Path $docs "native"
if (Test-Path $doxygenOut) { Remove-Item -Recurse -Force $doxygenOut }
if ($SkipNative) {
    Write-Warning "Skipping native API docs (-SkipNative)."
} else {
    $doxygen = (Get-Command doxygen -ErrorAction SilentlyContinue).Source
    if (-not $doxygen -and (Test-Path "$env:ProgramFiles/doxygen/bin/doxygen.exe")) {
        $doxygen = "$env:ProgramFiles/doxygen/bin/doxygen.exe"
    }
    if (-not $doxygen) { throw "Doxygen not found. Install it (winget install DimitriVanHeesch.Doxygen) or pass -SkipNative." }
    Invoke-Checked $doxygen @("doxygen/Doxyfile")
}

# 4. User manual and changelog, copied next to each other inside Docs/: DocFX can't resolve
#    links to files outside its folder, and the manual's TOC links to CHANGELOG.md.
$packageDir = Join-Path $repo "Packages/com.xra.quadbuffer"
$manual = Join-Path $docs "manual"
if (Test-Path $manual) { Remove-Item -Recurse -Force $manual }
Copy-Item -Recurse (Join-Path $packageDir "Documentation~") $manual
Copy-Item (Join-Path $packageDir "CHANGELOG.md") $manual

# 5. DocFX, pinned in .config/dotnet-tools.json.
Push-Location $repo
try { Invoke-Checked dotnet @("tool", "restore") } finally { Pop-Location }

Invoke-Checked dotnet @("restore", "api-project/QuadBuffer.Docs.csproj", "--nologo", "-v", "q")
Invoke-Checked dotnet @("docfx", "metadata", "docfx.json")
Invoke-Checked dotnet @("docfx", "build", "docfx.json")
Write-Host "Site built: $(Join-Path $docs '_site/index.html')"

if ($Serve) {
    Invoke-Checked dotnet @("docfx", "serve", "_site", "--port", "8080")
}
