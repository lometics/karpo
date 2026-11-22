<#
.SYNOPSIS
Publishes Karpo for running without the SDK: the bar as one native AOT
executable. Its settings are the edit bar's menus, inside it.

.DESCRIPTION
The result is a folder that runs on any Windows 10 2004 or later x64 PC, with
no .NET runtime installed:

    artifacts\publish\Karpo\Karpo.exe                      the bar (native AOT)

Nothing running is touched: a Karpo that is running keeps running from
wherever it was started.

The native AOT link needs Visual Studio's C++ build tools (the "Desktop
development with C++" workload, or the Build Tools with the C++ tools).

.PARAMETER Output
Where the folder goes; artifacts\publish\Karpo in the repository by default.

.PARAMETER Work
Where the build's intermediate files go; by default a folder in %TEMP% of this checkout's
own (karpo-publish-<a hash of its path>), so two checkouts publishing at once never build
into the same files (a CS2012 "cannot open ... for writing" was one taking the other's
obj apart). Two publishes of the same checkout take turns.

.PARAMETER Version
The version Karpo.exe says it is ("1.2.0"): About Karpo's, and the file's own in Explorer. Without
it the build's default, 1.0.0. scripts\package.ps1 gives the Store package's.

.PARAMETER Channel
Whom the copy is for. None, the default: a build of the source, which nothing limits (the person's
own Karpo). "store": the Microsoft Store's package, which scripts\package.ps1 asks for; that exe run
outside its package is the free version (Karpo.Core\Licensing.cs, LicenseBasis.StoreMissing).

.PARAMETER WithoutGoogle
Leaves Google sign-in out of this copy even where the build has Google's client secret
(Local.Build.props): no "Add a Google account" in it. scripts\package.ps1 asks for this unless told
-WithGoogle (docs\widgets.md section 716).

.EXAMPLE
./scripts/publish.ps1
#>
param(
    [string] $Output,
    [string] $Work,
    [ValidatePattern('^\d+\.\d+\.\d+$')] [string] $Version,
    [ValidateSet('', 'store')] [string] $Channel = '',
    [switch] $WithoutGoogle
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $Output) { $Output = Join-Path $repo 'artifacts\publish\Karpo' }
$checkout = [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($repo.ToLowerInvariant()))).Replace('-', '').Substring(0, 12)
$work = if ($Work) { $Work } else { Join-Path ([IO.Path]::GetTempPath()) "karpo-publish-$checkout" }
$work = [IO.Path]::GetFullPath($work)
# One publish into a work folder at a time: a second waits for the first rather than building into its files.
$turn = [Threading.Mutex]::new($false, 'Local\karpo-publish-' + [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($work.ToLowerInvariant()))).Replace('-', '').Substring(0, 12))
$Output = [IO.Path]::GetFullPath($Output)

# A Karpo running from the output folder holds its files: it is left alone, and so is the folder.
$running = @(Get-Process -Name Karpo -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and ([IO.Path]::GetFullPath($_.Path)).StartsWith($Output, [StringComparison]::OrdinalIgnoreCase) })
if ($running.Count -gt 0) {
    throw "Karpo is running from $Output. Quit it first (Karpo.exe --quit, or the taskbar menu's Quit Karpo where it is added), or publish somewhere else with -Output."
}

function Step([string] $name, [string] $project, [string[]] $properties) {
    Write-Host "== $name" -ForegroundColor Cyan
    $arguments = @('publish', (Join-Path $repo $project), '-c', 'Release', '-r', 'win-x64',
        '--artifacts-path', $work, '--nologo', '-v', 'minimal') + ($properties | ForEach-Object { "-p:$_" })
    $output = & dotnet @arguments 2>&1 | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
    $warnings = @($output | Where-Object { $_ -match ': warning ' } | Sort-Object -Unique)
    $errors = @($output | Where-Object { $_ -match ': error ' } | Sort-Object -Unique)
    $warnings | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
    $errors | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    if ($code -ne 0) {
        $output | Select-Object -Last 30 | ForEach-Object { Write-Host $_ }
        throw "$name failed (exit code $code)"
    }
    Write-Host ("   {0} warning(s)" -f $warnings.Count)
}

function Size([string] $folder) {
    $files = @(Get-ChildItem $folder -Recurse -File)
    '{0:N1} MB in {1} files' -f (($files | Measure-Object Length -Sum).Sum / 1MB), $files.Count
}

# The C++ tools are found through vswhere, which Visual Studio's own scripts
# run by name from the Installer folder; a shell that doesn't search the
# current folder for programs (NoDefaultCurrentDirectoryInExePath) breaks that.
$searchHere = $env:NoDefaultCurrentDirectoryInExePath
$env:NoDefaultCurrentDirectoryInExePath = $null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere) -or -not (& $vswhere -latest -prerelease -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath)) {
    throw 'Native AOT needs the Visual Studio C++ build tools (x64). Install "Desktop development with C++" or the Build Tools with the C++ tools.'
}

try {
    try { [void]$turn.WaitOne() } catch [Threading.AbandonedMutexException] { }
    if (Test-Path $work) { Remove-Item $work -Recurse -Force }
    Step 'Karpo.exe (native AOT)' 'Karpo.Dock\Karpo.Dock.csproj' @($(if ($Version) { "Version=$Version" }), $(if ($Channel) { "KarpoChannel=$Channel" }), $(if ($WithoutGoogle) { 'KarpoGoogleSignIn=false' }) | Where-Object { $_ })

    if (Test-Path $Output) { Remove-Item $Output -Recurse -Force }
    New-Item -ItemType Directory -Force $Output | Out-Null
    $dock = Join-Path $work 'publish\Karpo.Dock\release_win-x64'
    # The bar's symbols (.pdb) stay behind: they are for debugging a crash dump, not for running.
    Get-ChildItem $dock -File | Where-Object Extension -ne '.pdb' | Copy-Item -Destination $Output

    # The native build checked offscreen: drawing, every flyout, the stores (see Karpo.Dock\Smoke.cs).
    Write-Host '== Karpo.exe --smoke' -ForegroundColor Cyan
    # Its exit code says whether it passed: a line it writes to stderr (a weather service down, say) is its output,
    # not a failure of the script, so the stop-on-error preference is off for this one call.
    $smoke = & { $ErrorActionPreference = 'Continue'; & (Join-Path $Output 'Karpo.exe') --smoke 2>&1 | ForEach-Object { "$_" } }
    $smoke | Where-Object { $_ -match 'first bar|opening, again|after one of each|sound sources|fence|license|accounts|FAILED' } | ForEach-Object { Write-Host "   $_" }
    if ($LASTEXITCODE -ne 0) {
        $smoke | ForEach-Object { Write-Host $_ }
        throw 'Karpo.exe --smoke failed'
    }

    Write-Host ''
    Write-Host "Published to $Output" -ForegroundColor Green
    Write-Host ('  Karpo.exe                   {0:N1} MB' -f ((Get-Item (Join-Path $Output 'Karpo.exe')).Length / 1MB))
    Write-Host ('  everything                  {0}' -f (Size $Output))
}
finally {
    $env:NoDefaultCurrentDirectoryInExePath = $searchHere
    $turn.ReleaseMutex()
}
