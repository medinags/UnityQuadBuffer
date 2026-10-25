# 0002. Keep plugin, package, test project and docs in one repository

- **Status:** Accepted
- **Date:** 2026-10-18

## Context

The C# bindings and the native C API must change together. The package also needs a Unity
project to test it in.

## Decision

Use one repository with four top-level folders:

| Folder | Contents |
|---|---|
| `NativePlugin/` | C++ source, CMake build. |
| `Packages/com.xra.quadbuffer/` | The UPM package, including the built DLL. |
| `TestProject/` | Unity project referencing the package with a `file:` path. |
| `Docs/` | Documentation site sources. |

The built `QuadBufferPlugin.dll` is committed inside the package.

## Consequences

- A change to the C API and its P/Invoke bindings lands in one commit.
- The package installs and works without a C++ toolchain.
- The committed DLL must be rebuilt and committed whenever native code changes.

## Alternatives considered

- **Separate repositories for plugin and package.** Requires coordinating versions across repos
  for every API change.
- **Building the DLL in CI only.** Better long term, but needs a CI pipeline first.
