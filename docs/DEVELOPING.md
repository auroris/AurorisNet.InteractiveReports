# Developing

How to build the repository, run the Workbench host, and produce the packages. [Testing](TESTING.md) describes the test suites.

## Set up and build

You need the .NET 10 SDK, Node.js 20 or later, and Playwright's Chromium, which takes the screenshots of the packaged help.

```sh
npm ci
npx playwright install chromium
npm run build
npm run start
```

On Linux, install Chromium with `npx playwright install --with-deps chromium` so its system libraries come with it.

| Command | What it does |
|---|---|
| `npm run build` | Runs the three build steps below, in order. |
| `npm run build:client` | Bundles `src/client` with esbuild into `src/InteractiveReport.Client.Json/Ui/dist`: `ir.js`, `ir-admin.js` and `ir-chart.js`, with source maps. |
| `npm run build:help` | Takes the documentation screenshots, then renders the user guide into `Ui/dist/help.en.html`. |
| `npm run build:dotnet` | Builds `InteractiveReport.slnx`. |
| `npm run build:watch` | Rebuilds the bundles when a client source file changes. `npm run start:dev` is the same command. |
| `npm run start` | Runs Workbench without building it. |

`Ui/dist` is not committed. The `Client.Json` project embeds its files, so a fresh checkout must run the client build before it runs the packaged UI. A Release build and `dotnet pack` fail with instructions when the bundles are missing.

## Workbench

`samples/Workbench` hosts every package over a seeded SQLite database at `http://localhost:5042`. Its start page shows a report and links the packaged viewer and administration pages, a French page (`fr.html`) and a page with an application-owned editor (`crud.html`). `embedding.html` shows two configured elements over one report. In Development, Workbench also serves Swagger UI at `/swagger` and GraphiQL at `/graphiql`.

Workbench has no sign-in. Every request is authenticated as `workbench-dev`, which `appsettings.json` lists as an administrator. A request with an `X-Workbench-User` header runs as that identity instead. The Playwright suite uses the header to test denial, ownership and non-administrator paths.

The database is `samples/Workbench/App_Data/sample.db`, which Git ignores.

## Packaged help and screenshots

The [User guide](USER-GUIDE.md) is also the help page behind the report's **?** button. `scripts/build-help.mjs` renders every `docs/USER-GUIDE*.md` into one HTML file with its images embedded: `USER-GUIDE.md` becomes `help.en.html`, and a `USER-GUIDE.fr-CA.md` would become `help.fr-CA.html`. A locale without a page shows the English one.

The guide must therefore stand alone: its links to headings work in the help window, and a link to another file does not. Each heading gets the id GitHub would give it.

`npm run build:screenshots` starts a temporary Workbench, drives the packaged viewer with Playwright, writes `docs/images/*.png`, and stops Workbench. `npm run build:help` and `npm run build` run it first, and so do the pack and release scripts.

- Stop a Workbench started with `npm run start` first: the capture needs port 5042.
- The images change on a machine with other fonts or display scaling. Do not commit such changes by accident.
- `IR_DOCS_BASE` changes the address, and `IR_HELP_IMAGE=avif` embeds AVIF in place of lossless WebP.

## Live demo

`npm run build:demo` builds the standalone demo into `demo/`: the browser bundle, the help page, and `src/browser-server`, a JavaScript implementation of the server routes over SQLite WebAssembly. `npm run start:demo` serves the folder locally. The demo is published at <https://interactive-report.pages.dev/>.

`src/browser-server` exists for the demo only. The .NET server defines the behavior.

## Package READMEs

Each library packs the `README.md` in its project folder as its nuget.org page. Keep these pages short: what the package adds, how to install it, and a link to the repository README. nuget.org needs Markdown without HTML and absolute links, and shows images only from [allowed domains](https://learn.microsoft.com/en-us/nuget/nuget-org/package-readme-on-nuget-org#allowed-domains-for-images). A change reaches nuget.org with the next release.

## Pack and release locally

The two scripts are PowerShell and use Windows paths.

`scripts/pack.ps1`, or `npm run pack:nuget`, runs the pipeline that CI runs:

1. `npm ci`, unless `-SkipInstall` is given.
2. `npm run build`.
3. The client unit tests and the Core and ASP.NET Core test projects, unless `-SkipTests` is given.
4. `dotnet pack` of the five projects in Release, into `artifacts/packages` or the folder given with `-Output`.
5. A check that each package and its symbol package exist.

The version is `-Version`, or `0.0.0-dev` from `Directory.Build.props`.

`scripts/release.ps1`, or `npm run release:nuget`, wraps `pack.ps1` to produce a complete release folder:

```powershell
pwsh scripts/release.ps1 -Version 1.2.3
```

It writes the five `.nupkg` files, the five `.snupkg` symbol packages and `SHA256SUMS.txt` to `releases/<version>/`, which Git ignores. It rejects a version that is not `MAJOR.MINOR.PATCH` with an optional prerelease suffix, and a folder that already holds packages unless `-Force` is given. `-SkipTests` and `-SkipInstall` pass through to `pack.ps1`.

Neither script publishes. `release.yml` runs `release.ps1` and then publishes the folder.

## Automatic dependency updates and releases

The [release guide](https://github.com/auroris/AurorisNet.Build/blob/main/docs/releasing.md) describes the workflows, version numbers, the inputs of `release.yml` and nuget.org trusted publishing. This section lists what is specific to this repository.

**Primary dependency.** The .NET platform packages: `Microsoft.Extensions.*`, which the libraries reference, and the packages that move with them in the monthly .NET servicing release.

**Dependabot** ([`dependabot.yml`](../.github/dependabot.yml)) checks NuGet, npm and workflow actions each week. It groups its pull requests:

| Ecosystem | Group | Packages |
|---|---|---|
| NuGet | `dotnet` | `Microsoft.Extensions.*`, `Microsoft.AspNetCore.*`, `Microsoft.Data.Sqlite`, `System.*`. Major updates are ignored: a new .NET major is a deliberate upgrade. |
| NuGet | `graphql` | `GraphQL*`: the GraphQL adapter and Workbench's GraphiQL page move together. |
| NuGet | `test-dependencies` | xunit, the test SDK, coverlet, and the database drivers the tests use. |
| NuGet | `dependencies` | Everything else, such as SqlKata. |
| npm | `client` | Packages bundled into the browser UI or used to build the help page: esbuild, chart.js, big.js, intl-messageformat, marked, sharp. |
| npm | `test-tooling` | Playwright and happy-dom. |
| npm | `demo` | sql.js. |

**CI** ([`ci.yml`](../.github/workflows/ci.yml)) runs on each pull request, on Windows: `scripts/pack.ps1`, then the browser-server tests and the documentation tests. The live-database tests skip there, because no `IR_TEST_*` variable is set.

**What starts a release.** After CI passes on a Dependabot pull request, the workflow merges it. Each week [`weekly-release.yml`](../.github/workflows/weekly-release.yml) starts `release.yml` once when an update merged since the last release ships:

- a NuGet package that a project under `src/` references. The project files are read at merge time, so a new reference needs no workflow change;
- any npm package except `@playwright/test`, `playwright`, `happy-dom` and `sql.js`, because the browser bundle and help page are built from npm packages and embedded in `AurorisNet.InteractiveReports`.

Updates of test packages, sample-only packages and workflow actions merge without a release.

**One release** ([`release.yml`](../.github/workflows/release.yml)) publishes all five packages with one version, their symbol packages and `SHA256SUMS.txt`.

Run the live-database tests on your machine before a release that changes the engine: see [Testing](TESTING.md#live-database-tests).
