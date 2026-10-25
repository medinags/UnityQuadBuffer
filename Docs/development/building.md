# Building and testing

## Prerequisites

| Tool | Used for |
|---|---|
| Visual Studio 2022 with **Desktop development with C++** and **C++ CMake tools** | Native plugin |
| Unity 6000.6.2f1 (via Unity Hub) | Test project |
| .NET SDK 8 or newer | DocFX (docs) |
| Doxygen 1.9 or newer | Native API docs |

## Native plugin

```powershell
./NativePlugin/build.ps1                 # Release
./NativePlugin/build.ps1 -Configuration Debug
```

The build copies `QuadBufferPlugin.dll` into `Packages/com.xra.quadbuffer/Runtime/Plugins/x86_64/`.
Unity locks a native DLL once it is loaded, so close the Editor before rebuilding.

## Test project

`TestProject/` references the package with a `file:` path, so package edits show up immediately.

1. Open `TestProject/` with Unity 6000.6.2f1.
2. **QuadBuffer > Configure Project** sets Direct3D11 and creates `Assets/Scenes/StereoTest.unity`.
3. **QuadBuffer > Build Windows Player** writes `TestProject/Builds/Windows/QuadBufferTest.exe`.

## Automated tests

Package tests live in `Packages/com.xra.quadbuffer/Tests/` and run through the Unity Test
Framework. The test project lists the package under `testables`, so they appear in
**Window > General > Test Runner**. From the command line:

```powershell
unity test TestProject --mode PlayMode --report-format junit --output test-results.xml
```

Stereo presentation itself needs stereo hardware and is tested manually with the built player.
