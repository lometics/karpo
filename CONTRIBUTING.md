# Working on Karpo

Karpo follows **GitHub flow**: `master` is always what could be released, every change comes to it
through a short-lived branch and a pull request, and a release is a tag on `master`.

## A change

1. **Branch from `master`**, named for what it does: `fix/full-screen-video`, `feat/tides-widget`,
   `docs/store-readme`.

   ```powershell
   git switch master; git pull
   git switch -c fix/full-screen-video
   ```

2. **Make the change**, with its tests and its words:
   - `dotnet build Karpo.slnx -c Release` and `dotnet test Karpo.Tests` (README, "Build and run").
     Tests go with logic that has edges and with every real bug that is fixed; plain wiring needs
     none.
   - Every text a person reads is a key in `Karpo.Core\Strings\en.json` **and in every other
     language file**: the build refuses a language that lacks one (`docs\widgets.md`, "Adding a
     string").
   - A change in what Karpo does gets its entry at the end of `docs\widgets.md`: the next number,
     `## §NNN Title (date)`, saying what was asked, what changed and how it is tested. The commit's
     subject ends with that number.

3. **Commit** in the form the history has: `type(scope): what is true now (§NNN)`, with `feat`,
   `fix`, `perf`, `chore`, `docs`, `ci` or `test` as the type.

   ```
   fix(bar): a maximized browser's video a pixel row short keeps its display (§709)
   ```

4. **Open a pull request** against `master` and let CI run (`.github\workflows\ci.yml`: build and
   tests). Say what the change does and how you tried it. Merge it **squashed**, so `master` keeps one
   commit for one change; the branch is deleted on merge.

Nobody force-pushes to `master` or rewrites what is on it. That is an agreement, not a lock: GitHub
offers branch protection for a private repository only on a paid plan, and the organisation is on the
free one (checked 2026-11-22). The maintainers may push small changes to `master` directly; everything
else comes through a pull request.

## Releases

A release is a tag on `master`, `v` and three numbers:

```powershell
git switch master; git pull
git tag v1.0.1
git push origin v1.0.1
```

The tag starts `.github\workflows\release.yml`, which

1. builds `Karpo.exe` for the Microsoft Store (native AOT) and runs its smoke check,
2. packs `Karpo_1.0.1_x64.msix` (`scripts\package.ps1`) and keeps it on a GitHub release named for
   the tag,
3. hands the package to the Microsoft Store as an update and submits it for certification, where the
   repository is set up for that (below). Otherwise upload the `.msix` from the GitHub release in
   Partner Center › Karpo › Packages, and submit.

Nothing is signed by us: the Store signs what it publishes.

**Choosing the number.** The Store takes only a version higher than the one it has. Raise the third
number for fixes, the second for something new, the first for a new major version (Karpo Pro's
"free updates for this major version" is about that first number). Tag only a commit CI has passed.

**Trying the pipeline without releasing**: Actions › Release › Run workflow, with a version typed in.
The package is kept on the run and goes nowhere else.

**The same package on your own PC**: `./scripts/package.ps1 -Version 1.0.1` (it needs Visual Studio's
C++ build tools and the Windows SDK). To run it, with Developer Mode on:
`Add-AppxPackage -Register artifacts\package\layout\AppxManifest.xml`.

### Letting a tag submit to the Store

Once, by an owner of the Partner Center account. It works only after Karpo's first submission has
been made and published by hand in Partner Center; the Store's tools update an app, they don't
create one.

1. Partner Center › Account settings › Tenants: associate a Microsoft Entra ID tenant with the
   account.
2. Account settings › User management › Microsoft Entra applications: add an application and give it
   the **Manager** role. Note its tenant ID and client ID, and make a key (the client secret, shown
   once).
3. In this repository, Settings › Secrets and variables › Actions:
   - secrets `STORE_TENANT_ID`, `STORE_SELLER_ID` (Partner Center › Account settings › Legal info or
     Developer settings), `STORE_CLIENT_ID`, `STORE_CLIENT_SECRET`;
   - variable `STORE_SUBMIT` = `true`.

Until then, and whenever `STORE_SUBMIT` is not `true`, a tag stops at the GitHub release.

What a tag does **not** change: the Store listing's words and pictures (`store\README.md`), the price
and the age rating. Those are edited in Partner Center, and the next update carries them on.

## What the source does not hold

Nothing secret is committed, ever: the repository is public, and so is everything that was once in it.

Google's client secret is the one thing a full build needs from outside (`docs\widgets.md` §713). A
build without it works and simply offers no Google account, so a fork and CI need nothing. The
maintainers' PCs have it in `Local.Build.props` beside `Directory.Build.props`, a file `.gitignore`
keeps out:

```xml
<Project>
  <PropertyGroup>
    <KarpoGoogleClientSecret>…</KarpoGoogleClientSecret>
  </PropertyGroup>
</Project>
```

The release workflow takes it from the repository's secret `KARPO_GOOGLE_CLIENT_SECRET`, and
`scripts\package.ps1` refuses to pack the Store's Karpo without it.

## Where things are

| | |
| --- | --- |
| `README.md` | what Karpo is, how to build and run it |
| `docs\widgets.md` | every decision, numbered: why Karpo does what it does |
| `store\README.md` | the Microsoft Store: the listing, the pictures, the submission's answers |
| `package\` | the Store package's manifest and logos |
| `scripts\publish.ps1`, `scripts\package.ps1` | a Karpo to run; the Store's package |
