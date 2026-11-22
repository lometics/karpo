<#
.SYNOPSIS
Packs a published Karpo as the Microsoft Store's package: artifacts\package\Karpo_<version>_x64.msix.

.DESCRIPTION
What Partner Center's Packages page takes (store\README.md, "Before the first submission"):

    Karpo.exe                 from scripts\publish.ps1 (run here unless -Published names its folder)
    AppxManifest.xml          package\AppxManifest.xml, with this version
    Images\*.png              package\Images, the logos of `node store/compose.mjs --package`
    resources.pri             made here: which logo file is which size, scale and form

The package is NOT signed: the Store signs what it publishes, and Partner Center takes an unsigned
upload. An unsigned package cannot be installed by a double click; to try the packaged Karpo on this
PC, register the folder it was packed from instead (Developer Mode on):

    Add-AppxPackage -Register artifacts\package\layout\AppxManifest.xml

Nothing running is touched.

.PARAMETER Version
The package's version, three numbers ("1.0.0"); the Store wants the fourth to be 0 and every
submission's higher than the last. Also the version Karpo.exe says it is (About Karpo).

.PARAMETER Published
A folder scripts\publish.ps1 already made (its Karpo.exe is packed as it is, whatever version it
says, and whomever it was built for: only one published with -Channel store is the Store's); by
default Karpo is published first, with -Version and -Channel store.

.PARAMETER Output
Where the package goes; artifacts\package in the repository by default.

.PARAMETER WithGoogle
Packs a Karpo that offers a Google account, which the build must have been given Google's client
secret for. Without this switch the Store's Karpo is built and packed without Google sign-in, and one
that has it is refused: Google has not verified Karpo yet (docs\widgets.md section 716), and until it
has only the accounts named as test users could sign in.

.EXAMPLE
./scripts/package.ps1 -Version 1.0.0
#>
param(
    [Parameter(Mandatory)] [ValidatePattern('^\d+\.\d+\.\d+$')] [string] $Version,
    [string] $Published,
    [string] $Output,
    [switch] $WithGoogle
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $Output) { $Output = Join-Path $repo 'artifacts\package' }
$Output = [IO.Path]::GetFullPath($Output)
$layout = Join-Path $Output 'layout'
$package = Join-Path $Output "Karpo_${Version}_x64.msix"

# makeappx and makepri come with the Windows SDK; the newest one installed is taken.
$kits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$sdk = Get-ChildItem $kits -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^10\.' -and (Test-Path (Join-Path $_.FullName 'x64\makeappx.exe')) } |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if (-not $sdk) { throw "The Windows SDK's makeappx.exe was not found under $kits. Install the Windows 10/11 SDK." }
$makeappx = Join-Path $sdk.FullName 'x64\makeappx.exe'
$makepri = Join-Path $sdk.FullName 'x64\makepri.exe'

function Run([string] $name, [string] $tool, [string[]] $arguments) {
    Write-Host "== $name" -ForegroundColor Cyan
    $said = & $tool @arguments 2>&1 | ForEach-Object { "$_" }
    if ($LASTEXITCODE -ne 0) {
        $said | ForEach-Object { Write-Host $_ }
        throw "$name failed (exit code $LASTEXITCODE)"
    }
}

if (-not $Published) {
    $Published = Join-Path $Output 'published'
    & (Join-Path $PSScriptRoot 'publish.ps1') -Output $Published -Version $Version -Channel store -WithoutGoogle:(-not $WithGoogle)
}
$Published = [IO.Path]::GetFullPath($Published)
if (-not (Test-Path (Join-Path $Published 'Karpo.exe'))) { throw "No Karpo.exe in $Published." }

# The exe must say it was built for the Store: one that doesn't is a developer build wherever it runs
# without its package, which nothing limits (Karpo.Core\Licensing.cs, LicenseBasis.StoreMissing).
$said = & { $ErrorActionPreference = 'Continue'; & (Join-Path $Published 'Karpo.exe') --smoke 2>&1 | ForEach-Object { "$_" } }
if (-not ($said | Where-Object { $_ -match 'license: Free \(StoreMissing\), built for store' })) {
    $said | Where-Object { $_ -match 'license|FAILED' } | ForEach-Object { Write-Host $_ }
    throw "The Karpo.exe in $Published was not built for the Store. Publish it with: scripts\publish.ps1 -Channel store"
}
# And it says whether it offers a Google account as this package was asked for (docs\widgets.md §713, §716):
# without one unless -WithGoogle, since Google has not verified Karpo yet; with one where it was asked for, which
# takes Google's client secret (Local.Build.props, or KARPO_GOOGLE_CLIENT_SECRET in the environment).
$google = [bool]($said | Where-Object { $_ -match 'accounts: Google sign-in built in' })
if ($WithGoogle -and -not $google) {
    throw "The Karpo.exe in $Published has no Google client secret, so it offers no Google account. Give the build one (Local.Build.props, or the environment's KARPO_GOOGLE_CLIENT_SECRET), or pack it without -WithGoogle."
}
if ($google -and -not $WithGoogle) {
    throw "The Karpo.exe in $Published offers a Google account, and this package was not asked for one. Publish it with scripts\publish.ps1 -WithoutGoogle, or pack it with -WithGoogle once Google has verified Karpo."
}

$images = Join-Path $repo 'package\Images'
if (@(Get-ChildItem $images -Filter *.png -ErrorAction SilentlyContinue).Count -eq 0) {
    throw "No logos in $images. Make them with: node store/compose.mjs --package, then copy artifacts\store\package\Images there."
}

# The folder the package is made of, new each time.
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $layout 'Images') | Out-Null
Get-ChildItem $Published -File | Copy-Item -Destination $layout
Copy-Item (Join-Path $images '*.png') (Join-Path $layout 'Images')

# The manifest with this version: the one attribute of the Identity element that changes between releases.
$manifest = [xml](Get-Content (Join-Path $repo 'package\AppxManifest.xml') -Raw -Encoding UTF8)
$manifest.Package.Identity.Version = "$Version.0"
$settings = [Xml.XmlWriterSettings]::new()
$settings.Indent = $true
$settings.Encoding = [Text.UTF8Encoding]::new($false)
$writer = [Xml.XmlWriter]::Create((Join-Path $layout 'AppxManifest.xml'), $settings)
try { $manifest.Save($writer) } finally { $writer.Dispose() }

# resources.pri: the index that tells Windows which of Images\Square44x44Logo.scale-200.png,
# .targetsize-24_altform-unplated.png, … to show for "Images\Square44x44Logo.png". One index for the
# whole package (no split by language or scale: that is for bundles), so the packaging part of the
# configuration makepri writes is taken out again.
$config = Join-Path $Output 'priconfig.xml'
Run 'makepri createconfig' $makepri @('createconfig', '/cf', $config, '/dq', 'en-US', '/pv', '10.0.0', '/o')
$pri = [xml](Get-Content $config -Raw)
foreach ($node in @($pri.resources.SelectNodes('packaging'))) { [void]$pri.resources.RemoveChild($node) }
$pri.Save($config)
Run 'makepri new' $makepri @('new', '/pr', $layout, '/cf', $config, '/mn', (Join-Path $layout 'AppxManifest.xml'), '/of', (Join-Path $layout 'resources.pri'), '/o')

if (Test-Path $package) { Remove-Item $package -Force }
Run 'makeappx pack' $makeappx @('pack', '/d', $layout, '/p', $package, '/o')

Write-Host ''
Write-Host "Packed $package" -ForegroundColor Green
Write-Host ('  {0:N1} MB, version {1}.0, unsigned (the Store signs it)' -f ((Get-Item $package).Length / 1MB), $Version)
Write-Host "  Upload it in Partner Center: Karpo > Packages."
