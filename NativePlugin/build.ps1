# Configures and builds the native plugin with Visual Studio 2022, then copies
# QuadBufferPlugin.dll into the Unity package (see CMakeLists.txt POST_BUILD).
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$buildDir = Join-Path $PSScriptRoot "build"

# Use cmake from PATH, otherwise the copy bundled with a Visual Studio install.
$cmake = (Get-Command cmake -ErrorAction SilentlyContinue).Source
if (-not $cmake) {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    foreach ($vs in & $vswhere -all -products * -property installationPath) {
        $candidate = Join-Path $vs "Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
        if (Test-Path $candidate) { $cmake = $candidate; break }
    }
}
if (-not $cmake) { throw "CMake not found. Install it or the VS 'C++ CMake tools' component." }

& $cmake -S $PSScriptRoot -B $buildDir -G "Visual Studio 17 2022" -A x64
& $cmake --build $buildDir --config $Configuration
