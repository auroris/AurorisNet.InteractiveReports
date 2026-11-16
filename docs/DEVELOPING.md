# Developing

The client-side source is in `src/client`; generated browser assets are written to
`src/InteractiveReport.Client.Json/Ui/dist` for embedding by the server project. With
Node.js 20 or later and the .NET 10 SDK installed, install the dependencies and
Playwright's Chromium browser, build the browser assets, packaged help, and .NET
solution, then start Workbench with:

```sh
npm ci
npx playwright install chromium
npm run build
npm run start
```

Workbench runs at `http://127.0.0.1:5042`; stop it with Ctrl+C.

Workbench has no login: every request is authenticated as `workbench-dev`, which
`appsettings.json` lists as an administrator, so the packaged pages and the
policy-protected sample report open without credentials. A request that carries an
`X-Workbench-User` header runs as that identity instead; the Playwright suite uses it to
exercise denial, ownership, and non-administrator paths, and it is handy for trying them
by hand.

On Linux, replace the Playwright installation command with
`npx playwright install --with-deps chromium` so Chromium's system libraries are
installed as well. Windows and Linux both support the normal build, Workbench, and
test workflows.

`npm run start:dev` (or `npm run build:watch`) rebuilds on changes. `npm test` builds
the client and runs the fast DOM unit tests, browser-server tests, and documentation
integrity tests. Browser automation is configured with Playwright; run
`npm run test:e2e` for that layer. `npm run test:all` combines those JavaScript and
browser layers. Run `dotnet test` separately for the Core, ASP.NET Core, and live-test
projects; live database cases skip unless their connection variables are configured.

The generated `src/InteractiveReport.Client.Json/Ui/dist/ir.js`, `ir-admin.js`, and
`ir-chart.js` files are embedded in the ASP.NET Core assembly and are deliberately
not committed. A source checkout must run the client build before a Release build,
a pack, or a run of the packaged UI — `dotnet pack` and `dotnet build -c Release`
fail with instructions when the bundles are missing, so a UI-less package cannot
ship silently. NuGet packaging is a Windows workflow. `scripts/pack.ps1` (also
`npm run pack:nuget`) runs the complete build, the client unit tests, the Core and
ASP.NET Core test projects, and `dotnet pack` for the five distributable projects into
`artifacts/packages`. It does not run the browser-server, Playwright, or live-database
suites. Use the manual GitHub Actions workflow below to publish a release, or
`dotnet nuget push` to publish locally built packages yourself.
Package consumers never need Node.js. `ir-chart.js` (the Chart.js-based chart
renderer) is fetched on demand the first time a report enters chart view; pages
that never chart never load it.

Each distributable project embeds its own `src/<project>/README.md` as the NuGet
README. Keep these introductions focused on the package's role, dependencies,
and which package a consumer should install; link to the full guides for detail.
Use Markdown rather than raw HTML, absolute URLs for links, and images hosted on
a [NuGet-supported domain](https://learn.microsoft.com/en-us/nuget/nuget-org/package-readme-on-nuget-org#allowed-domains-for-images).
The repository's root README is separate and can retain its GitHub presentation.
README changes reach nuget.org when a new package version is published.

## Publishing a release from GitHub

The [release workflow](../.github/workflows/release.yml) runs only when it is started:
by hand from the Actions tab, or by the CI workflow after it merges a Dependabot update
(see [Automatic dependency updates and releases](#automatic-dependency-updates-and-releases)).
Pushes, tags, and pull requests never trigger it directly. Once the workflow is on the
repository's default branch, open **Actions → Release NuGet packages → Run workflow**:

1. Select the branch or tag to release.
2. Enter a semantic version such as `1.0.3` or `1.0.3-rc.1`, or leave it blank and pick a
   **bump** (`patch` or `minor`) to increment the latest stable `v*` tag. The version
   applies to every package and its internal dependencies without editing the repository.
3. Leave **Also publish the packages to nuget.org** unchecked to create just the
   GitHub Release, or check it after configuring Trusted Publishing below.
4. Run the workflow. A first job validates the version and rejects an existing tag. The
   build job then installs .NET 10, Node.js 24, and Chromium on Windows and runs
   `scripts/release.ps1` with the build and test stages enabled, plus the documentation
   checks.

An optional `v` prefix is accepted. Versions use `MAJOR.MINOR.PATCH`, optionally followed
by a prerelease suffix; build metadata (`+...`) is not supported. Numeric version components
must be between 0 and 65534 to fit .NET assembly versions. Existing tags are rejected. A blank
version bumps the highest `vMAJOR.MINOR.PATCH` tag; prerelease tags are ignored when choosing
it. `Directory.Build.props` no longer supplies release versions: its `Version` is the
development placeholder `0.0.0-dev`, which local builds and packs use unless a version is
passed explicitly.

Every successful build uploads an Actions artifact containing the five `.nupkg`
files, five `.snupkg` symbol packages, and `SHA256SUMS.txt`. The next job verifies
those checksums and publishes a GitHub Release tagged `v<version>` at the exact
commit that was built, with generated release notes and all eleven files attached.
Versions containing a prerelease suffix produce GitHub prereleases. Published releases
and tags are never overwritten by this workflow; use a new version for a new release.

The optional NuGet job runs after the GitHub Release and publishes the same packages
and their symbols. If NuGet publishing fails, the GitHub Release and Actions artifact
remain available; correct the setup and use **Re-run failed jobs**. Already published
package versions are skipped. The workflow does not change the version in Git or
create a version-bump commit.

### One-time nuget.org setup

This workflow uses [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing),
so no long-lived NuGet API key is stored in GitHub:

1. In nuget.org, sign in as `AurorisNet` and create a
   Trusted Publishing policy with repository owner `auroris`, repository
   `Interactive-Reports`, and workflow file **`release.yml`** (just the filename).
   Leave the environment field empty; this workflow does not use an environment.
2. Scope the policy to `AurorisNet.InteractiveReports*` and allow both publishing new
   packages and new versions. The first publication must be permitted to create all
   five package IDs listed in [Architecture](ARCHITECTURE.md#package-and-dependency-structure).
   Package IDs must be available or owned by your account; the workflow does not
   reserve the name in advance.

The workflow sets the NuGet login username to `AurorisNet` directly; no GitHub
repository variable is needed.

GitHub Releases use the workflow's built-in `GITHUB_TOKEN` with `contents: write`;
no personal access token is required. The NuGet job alone requests `id-token: write`
to exchange its GitHub identity for a short-lived publishing key.

## Cutting a release locally

On Windows, `scripts/release.ps1` (also `npm run release:nuget`) runs the pack pipeline and drops a
complete, versioned package set into `releases/<version>/`: the five `.nupkg` files,
their `.snupkg` symbol packages, and a `SHA256SUMS.txt` manifest. Without `-Version`,
the packages carry the development placeholder `0.0.0-dev` from `Directory.Build.props`;
pass `-Version 1.0.3` or `-Version 1.0.0-rc.1` for a real one, `-SkipTests` to pack
without the test layers, and `-Force` to replace a folder that already holds packages.
The `releases/` folder is git-ignored. Nothing is pushed: publishing is a manual
`dotnet nuget push releases/<version>/*.nupkg`.

## Automatic dependency updates and releases

[Dependabot](../.github/dependabot.yml) checks NuGet packages daily, so a new .NET
servicing release is picked up within a day, and npm packages and GitHub Actions weekly.
It opens grouped pull requests. For NuGet: one for the .NET platform packages
(`Microsoft.Extensions.*`, `Microsoft.AspNetCore.*`, `Microsoft.Data.Sqlite`, `System.*`,
the primary dependency), one for GraphQL.NET, one for test-only packages, and one for
everything else. For npm: one for the packages bundled into the browser client or used
to build the packaged help (esbuild, chart.js, big.js, intl-messageformat, marked, sharp),
one for test tooling (Playwright, happy-dom), and one for the demo's sql.js. Workflow
actions get one pull request. The platform packages stay on the current .NET major;
Dependabot never proposes a major update of them.

The [CI workflow](../.github/workflows/ci.yml) runs the release pipeline without a
version on Windows for every pull request and for pushes to `main`: the client build,
the client unit tests, the Core and ASP.NET Core test projects, `dotnet pack`, and the
browser-server and documentation tests. The live-database suite skips itself because no
`IR_TEST_*` connection string is set. For a Dependabot pull request that passes, the
final job:

1. Squash-merges the pull request into `main` and deletes the branch.
2. Starts the Release NuGet packages workflow on `main` with a blank version and
   nuget.org publishing enabled. A minor update of a NuGet package referenced by one of
   the five packable projects bumps the minor version; patch updates, and every npm
   update, bump the patch version. The release then runs exactly as a manual one.

No release is started when nothing shipped changes: packages that only the samples or
tests reference, npm test tooling, the demo's sql.js, and workflow actions. Which NuGet
packages ship is read from the project files under `src/` at merge time, so adding a
package to a library needs no workflow change. Major updates of packages other than the
.NET platform still open pull requests and run CI, but are left for manual review with a
comment explaining why; after merging one by hand, start the release workflow yourself
if it should ship. Major updates of workflow actions merge automatically: the CI run that
approves them already used the updated actions.

Dependabot workflows run with a read-only token, so the merge job requests `contents`,
`pull-requests` and `actions` write permissions explicitly. Starting the release workflow
through `workflow_dispatch` is the one way a workflow can trigger another with the built-in
token, which is why CI does not rely on a push trigger (a merge made with that token also
starts no CI run on `main`). Several updates merged close together each queue a release;
GitHub keeps one pending release run and cancels the ones between, and the last run still
ships everything that was merged.

To pause automation, remove or edit `.github/dependabot.yml`; to keep the updates but review
them yourself, delete the `dependabot` job from `ci.yml`.

## Documentation assets

The [User Guide](USER-GUIDE.md) is also the in-app help: `npm run build:help`
(part of `npm run build`) renders it to `Ui/dist/help.<locale>.html` with the
screenshots inlined as lossless WebP, and the report's **?** button opens that page in
a window. Regenerate the screenshots from a temporary Workbench instance with:

```sh
npm run build:screenshots
```

The command starts Workbench, waits for it to accept requests, drives the packaged
viewer with Playwright, and stops Workbench even when capture fails. The capture widens
the window until the toolbar fits on one row, adds the numbered callouts, and writes
`docs/images/*.png`. `npm run build:help` performs the same capture before rebuilding
the packaged help pages, and so does `npm run build`, which `pack.ps1` and
`release.ps1` run: stop a Workbench you started with `npm run start` first, because the
capture needs port 5042, and expect the screenshot files to change on a machine with
different fonts or display scaling.
