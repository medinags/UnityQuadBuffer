# Architecture decision records

Each record captures one significant decision: the context, what was decided, and the
consequences. Records are numbered and never deleted; a changed decision gets a new record
that supersedes the old one.

| # | Decision | Status |
|---|---|---|
| [0001](0001-native-dxgi-stereo-swap-chain.md) | Present stereo through a native DXGI swap chain | Accepted |
| [0002](0002-repository-layout.md) | Keep plugin, package, test project and docs in one repository | Accepted |
| [0003](0003-documentation-toolchain.md) | Use DocFX and Doxygen for documentation | Accepted |
| [0004](0004-share-unity-window-blit-model.md) | Share Unity's window by turning off the flip model swap chain | Accepted (unverified) |

To add one, copy `Docs/adr/template.md`, use the next number, and add it to this table
and to `toc.yml`.
