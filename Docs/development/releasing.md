# Releasing

The package follows [Semantic Versioning](https://semver.org/). While the version is `0.x`,
any release may contain breaking changes.

## Checklist

1. Build the native plugin in Release: `./NativePlugin/build.ps1`.
2. Run the package tests in the test project.
3. Build the test player and check stereo output on stereo hardware.
4. Bump `version` in `Packages/com.xra.quadbuffer/package.json`.
5. Move the `Unreleased` entries in `CHANGELOG.md` under the new version and date.
6. Build the docs (`./Docs/build-docs.ps1`) and check for warnings.
7. Commit, then tag the commit `v<version>` (for example `v0.2.0`).
