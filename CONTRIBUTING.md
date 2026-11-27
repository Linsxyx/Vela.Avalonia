# Contributing to Vela.Avalonia

Thanks for helping improve Vela. Small reproductions, focused fixes, and improvements to the gallery and documentation are welcome.

## Development setup

Install the .NET 10 SDK. `global.json` selects a .NET 10 feature band; `Directory.Build.props` defines the shared target framework and Avalonia version.

```bash
git clone https://github.com/Linsxyx/Vela.Avalonia.git
cd Vela.Avalonia
dotnet restore Vela.Avalonia.slnx
dotnet build Vela.Avalonia.slnx -c Release --no-restore
dotnet test tests/Vela.Avalonia.Tests/Vela.Avalonia.Tests.csproj -c Release --no-build
dotnet run --project samples/Vela.Avalonia.Gallery
```

## Repository layout

| Path | Contents |
| --- | --- |
| `src/Vela.Avalonia` | Controls, themes, design tokens, icons, and motion |
| `src/Vela.Avalonia.Charts` | Optional chart controls |
| `samples/Vela.Avalonia.Gallery.Shared` | Shared gallery views, view models, and localization |
| `samples/Vela.Avalonia.Gallery` | Desktop entry point |
| `samples/Vela.Avalonia.Gallery.Browser` | WebAssembly entry point and web assets |
| `tests/Vela.Avalonia.Tests` | Unit and regression tests |
| `tools/sync-tokens.mjs` | Color-token generator |
| `assets/branding` | Project icon, source artwork, and generation notes |

## Making a change

Keep changes focused and include enough context to reproduce the problem. Add or update a gallery example for public behavior changes, and keep the English and Chinese strings in sync. Add regression tests where they exercise meaningful behavior.

For controls, check keyboard interaction, focus, disabled states, reduced motion, light/dark themes, and attach/detach behavior. For graphics or native integration, describe the OS and renderer you actually checked. Report measured performance with its workload and environment.

The README has [English](README.md) and [Chinese](README.zh-CN.md) versions. Update both when changing the public setup instructions or feature overview. Add new controls to the [catalog](docs/components.md).

To regenerate design tokens after changing the generator:

```bash
node tools/sync-tokens.mjs
```

Review the generated diff before committing it.

## Browser gallery

The browser project is separate from the default solution so desktop development does not require the WebAssembly workload.

```bash
dotnet workload install wasm-tools
dotnet publish samples/Vela.Avalonia.Gallery.Browser/Vela.Avalonia.Gallery.Browser.csproj -c Release
```

The static output is `samples/Vela.Avalonia.Gallery.Browser/bin/Release/net10.0-browser/publish/wwwroot`.

For GitHub Pages, select **Settings → Pages → Build and deployment → Source → GitHub Actions**. The [Pages workflow](.github/workflows/pages.yml) builds and deploys the gallery on pushes to `main` or `master`, or by manual dispatch.

## Local packages

```bash
dotnet pack src/Vela.Avalonia/Vela.Avalonia.csproj -c Release -o ./artifacts
dotnet pack src/Vela.Avalonia.Charts/Vela.Avalonia.Charts.csproj -c Release -o ./artifacts
```

Both packages include the README, MIT license, and project icon. README images use absolute GitHub raw URLs so they can also render on NuGet.org; new artwork must be present on the repository's `master` branch before publishing a package that references it.

## Maintainer release setup

The [release workflow](.github/workflows/release.yml) builds, tests, packs, and optionally publishes both packages. NuGet publishing uses [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) through GitHub OIDC.

1. In NuGet.org account settings, add a GitHub trusted publisher for this owner/repository and workflow file `release.yml`. The current workflow does not specify an environment.
2. Set the GitHub Actions repository variable `NUGET_USER` to the NuGet profile username. The workflow also accepts a repository secret with that name.
3. Push a new semantic-version tag such as `v0.2.0`, or manually dispatch the workflow with a version.

The workflow skips NuGet login and upload if `NUGET_USER` is absent. A tag-triggered run creates a GitHub Release with package artifacts; manual dispatch does not create that release. Verify the workflow result and package contents before announcing a version.

## Reporting an issue

Include the Vela, Avalonia, and .NET versions, your OS, expected and actual behavior, and a minimal reproduction. A short recording helps for motion problems; sample data helps for data views and charts. File reports in [GitHub Issues](https://github.com/Linsxyx/Vela.Avalonia/issues).
