# UnityQuadBuffer

Quad-buffered (active stereo) output for Unity Windows standalone players.

## Layout

| Folder | Contents |
|---|---|
| `NativePlugin/` | C++ D3D11 native rendering plugin (CMake). Builds `QuadBufferPlugin.dll`. |
| `Packages/com.xra.quadbuffer/` | The Unity package (UPM): C# bindings, `QuadBufferCamera`, editor build checks, tests. |
| `TestProject/` | Unity 6000.6.2f1 project that references the package locally to test it. |
| `Docs/` | Documentation site (DocFX + Doxygen): architecture, development guides, ADRs, API reference. |

```
NativePlugin/
  CMakeLists.txt, build.ps1
  external/UnityPluginAPI/     Unity plugin headers (from Editor/Data/PluginAPI)
  src/                         QuadBufferPlugin.cpp, D3D11StereoPresenter.*
Packages/com.xra.quadbuffer/
  package.json
  Runtime/                     QuadBufferCamera.cs, QuadBufferNative.cs
  Runtime/Plugins/x86_64/      QuadBufferPlugin.dll (build output)
  Editor/                      QuadBufferBuildValidator.cs
  Tests/Runtime/
  Samples~/BasicStereo/
TestProject/
  Assets/Editor/TestProjectSetup.cs   "QuadBuffer" menu: configure + build player
  Packages/manifest.json              references the package via file:
```

## Workflow

1. Build the native plugin (copies the DLL into the package):
   ```powershell
   ./NativePlugin/build.ps1
   ```
2. Open `TestProject/` in Unity 6000.6.2f1.
3. Run **QuadBuffer > Configure Project** (sets Direct3D11, creates the test scene).
4. Run **QuadBuffer > Build Windows Player** and launch `TestProject/Builds/Windows/QuadBufferTest.exe`
   on a machine with stereo 3D enabled in the GPU driver.

Unity locks loaded native DLLs, so close the Editor before rebuilding the plugin.

## Documentation

The user manual is in [Packages/com.xra.quadbuffer/Documentation~](Packages/com.xra.quadbuffer/Documentation~/index.md).
Build the full site (manual, architecture, C# and native API reference) with:

```powershell
./Docs/build-docs.ps1
```

See [Docs/README.md](Docs/README.md).
