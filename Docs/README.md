# Docs

Sources for the documentation site. Build it with:

```powershell
./Docs/build-docs.ps1          # output in Docs/_site
./Docs/build-docs.ps1 -Serve   # build and serve at http://localhost:8080
```

Needs the .NET SDK (DocFX is restored as a local tool) and Doxygen.

| Path | Contents |
|---|---|
| `docfx.json`, `toc.yml`, `index.md` | Site configuration, top navigation, home page. |
| `architecture/` | How the package and native plugin fit together. |
| `development/` | Building, testing, documentation conventions, releasing. |
| `adr/` | Architecture decision records (`template.md` for new ones). |
| `doxygen/` | Doxygen configuration and main page for the native API. |
| `api/` | C# API landing page; the rest is generated. |
| `api-project/` | Project that compiles the package code outside Unity so DocFX can read its API. |

The user manual is not here: it lives in `Packages/com.xra.quadbuffer/Documentation~/`
so it ships with the package. See `development/documentation.md` for conventions.
