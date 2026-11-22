# Karpo on the Microsoft Store

Everything the Partner Center submission needs: the listing in 40 languages, the pictures, the
add-on, and the answers to every page of the submission. Every screenshot is a capture of the real
app on the stage (`scripts/stage`), never a drawing of it; only the headline around it is added.

## What's here

| File | What it is |
| --- | --- |
| `listing/<lang>.json` | The listing of one language: name, descriptions, features, keywords, screenshot headlines and captions, the Pro add-on's words. English is the source; the others are translations of it. |
| `shots.txt` | The stage scenes the screenshots are captured from (`Stage.cs -- store shots`). |
| `config.json` | The stage's Karpo for the Store: the site's bar (`scripts/stage/config.json`) made the one bar of every page (`work`: the apps, the mail and the next meeting; this PC, the weather, two dashboards, quick settings and the date, each a folder composed for its page) and a folder of the widgets no other shows, for the wall and the accounts (`variety`); no media widget, so nothing the PC plays shows. The palettes are the stage's `palette` step, not layouts. |
| `catalog.json` | The same for the folder of nearly every widget (`Stage.cs -- store catalog`: the stocks in the widgets bento): the site's catalogue without the media widget. |
| `compose.mjs` | Makes the Store's pictures from the captures and the listings: screenshots per language, hero art, tile icon, add-on icon, and the MSIX package's logos. |
| `fill-csv.mjs` | Fills a listing CSV exported from Partner Center with every language and lays out the folder to import. |
| `capture.ps1` | A capture of the real screen, for anything the stage can't show. |
| `*.test.mjs` | `node --test store/listing.test.mjs store/csv.test.mjs store/panorama.test.mjs`: every listing within Partner Center's limits and shaped like English; the CSV reader; the panorama's frames (monitors whole on their pages, a bubble for every callout, pieces inside their captures). |

## Making the pictures

```powershell
./scripts/publish.ps1 -Output "$PWD\artifacts\stage\dock-aot"   # the dock the stage runs, as it ships
node store/wallpapers.mjs                                       # the stage's wallpapers, once (store/wallpapers.json)
dotnet run scripts/stage/Stage.cs -- store shots                # every scene, to artifacts/store/shots
dotnet run scripts/stage/Stage.cs -- store shots edit           # or one scene
dotnet run scripts/stage/Stage.cs -- store catalog              # the folder of nearly every widget, at 160 %
dotnet run scripts/stage/Stage.cs -- store site                 # the website's own scenes (site.txt on site.json), to artifacts/store/shots/site
node store/compose.mjs                                          # every language, to artifacts/store/images
node store/compose.mjs --package                                # the package's logos
```

The desktop behind the bar is an illustration, never Windows' own wallpaper: one of `wallpapers.json`
(other people's work, free to use under the licence each entry names; the files are fetched, not kept in
the repository), the same Themia's pictures use. A scene of `shots.txt` names its own with
`wallpaper NAME`; one that names none shows the stage's usual one. Beside the captures the stage says which
wallpaper stands behind each scene (`wallpapers.json`) and keeps each as it drew it (`wallpaper-NAME.png`):
what a capture shows wherever nothing of Karpo's stands, for whoever cuts the captures (karpo.app's `npm run shots`).

Every language's screenshots show Karpo in that language once it has captures of its own
(`artifacts/store/shots/<lang>`); until then they show the English ones. All of them, about an hour
on an idle PC:

```powershell
foreach ($lang in (Get-ChildItem store/listing/*.json).BaseName | Where-Object { $_ -ne 'en' }) {
    $env:STAGE_LANGUAGE = $lang
    dotnet run scripts/stage/Stage.cs -- store shots
    dotnet run scripts/stage/Stage.cs -- store catalog
}
Remove-Item Env:STAGE_LANGUAGE
node store/compose.mjs
```

Look through `artifacts/store/images` afterwards: a scene that lost its flyout shows the bare desktop,
and `edit` clicks the add list's "+" where it is in English.

The stage opens its own desktop on the left display, so run it while nobody uses the PC: Karpo's own
edit mode (its dimmed screen) or a click elsewhere spoils a scene. The hour doesn't matter: the stage's
Karpo tells ten past one in the afternoon whenever the pictures are taken (`KARPO_NOW`, Karpo.Dock's
`Clock`), a day's sample meetings behind it and ahead; `STAGE_TIME=09:30` sets another time of day,
`STAGE_TIME=real` the PC's own.

The nine are one picture: a panorama 9 × 1920 px wide (`panorama.mjs`), cut into the Store's pages,
so side by side in the Store's row they are one scene. They tell Karpo in the order somebody meets it,
and every page shows the same bar (`config.json`'s `work`):

1. **What you see.** Your day on the taskbar (the week, the inbox and a to-do list open from the date);
   then the bar itself, its two ends enlarged over the screen, a callout on each button.
2. **What a click opens.** A fan of folders behind the buttons (the news, the sea and the week's
   weather, the next meetings, quick settings, stocks); then one of them whole, this PC: files and
   drives beside live graphs (callouts).
3. **What it reads.** The accounts, a lane for each thing they give (calendars, contacts, mail, To Do
   tasks, a meeting's Join): that as Karpo shows it, a piece of a capture, over the services it comes
   from.
4. **How it becomes yours.** A dashboard folder you fill yourself (callouts); a wall of widgets, more
   than the page holds; the bar down the screen's left edge, the weather open beside it; your look (one
   screen, the second dashboard, cut into four palettes by tilted cuts).

From page to page the widgets change: a kind is shown once where it can be (the clocks only in the
dashboard, the week only on the first page; `panorama.test.mjs` holds those two). A whole screen is
always shown whole, in a monitor with its stand, 16:9; a lens enlarges a piece of that screen beside
it, and only the fan, the lanes and the wall cut pieces out of captures. The wall runs over the cuts into
the pages beside it; words and monitors never do. The light and a line along the bottom run through all
nine. A frame's callouts are placed in `panorama.mjs` and worded in `listing/<lang>.json` (`callouts`,
in the same order); the accounts page's callouts head its lanes.

The folders on the bar are composed to the cell (20 DIP, `"height": { "fixed": true, "cells": n }`): every
list of a folder adds up to the same cells, so its lists end level (the week beside the inbox and the
to-do list, the files beside the graphs, each dashboard's four). The weather's
lines and the stocks are their forms' own heights, whatever cells they are given.

The accounts page's logos are Simple Icons' SVGs (`logos/`, CC0; each mark is its owner's trademark), drawn white on
the brand's colour; Microsoft's four squares are drawn in theirs (`compose.mjs`, `logos`).

Everything else the pages draw is Karpo's teal, the mark's (`assets/logo/karpo.svg`), from one palette at the top of
`compose.mjs` (`teal`): the night ground, its lights, the bubbles and check marks (`#08827D`, white words on it), the
lines, dots and lanes. The captures keep their own colours.

The note and the to-do list are made up (`notebook.json`, copied beside the stage's config). A full folder is opened
with one click and a wait (`move`, `click`, `wait`), not `open`: it takes a moment to draw, and a second click would
close it. A folder the stage opens wears an icon face: from a markets, sports or tides face it opened nothing.

Out of `compose.mjs`, all PNG:

| Picture | Size | Where it goes |
| --- | --- | --- |
| `images/<lang>/screenshot-NN-<shot>.png` | 3840 × 2160 | Store listing › Screenshots (Desktop), in this order, with the caption from `listing/<lang>.json` |
| `images/hero-3840x2160.png` | 3840 × 2160 | Store listing › Store logos › 16:9 Super hero art. No words and no UI, as the Store asks |
| `images/tile-300x300.png` | 300 × 300 | Store listing › Store logos › 1:1 App tile icon. The mark, a square, filling it to its edges |
| `images/pro-300x300.png` | 300 × 300 | The Karpo Pro add-on's Store listing icon: the tile with PRO in amber under its bar |
| `package/Images/*.png` | 72 logos | The MSIX package's `Images` folder: Square44x44 (with target sizes, unplated), Square150x150, Wide310x150, SmallTile, LargeTile and StoreLogo at 100, 125, 150, 200 and 400 %. Square44x44 and StoreLogo are the mark to their edges, as the exe's icon is (`assets/logo/karpo-<size>.png` where it has the size); the tiles keep room round it |

Poster art (2:3) and box art (1:1, 1080/2160) are only for games and Xbox; Karpo needs neither.

The screenshots, in order: your day (the pitch), the bar's buttons, every button opens more, files, your
PC, folders, more than 100 widgets, accounts, your look. The first two show in search results, so they carry
the pitch on their own; ten is the Store's most. Edit mode, privacy, layouts and the bar on
any edge are left to the description.

## Before the first submission

1. **The account.** Karpo is published from the person's own, verified developer account (publisher "Nathan Walser"), decided on 2026-11-22 while Lometics GmbH's company account waits for its verification. Microsoft documents no way to turn an individual account into a company one, and no way to move an app between accounts but a support request: ask them in writing before counting on a move. The account still needs its **tax and payout details** before the add-on can charge anything.
2. **The name "Karpo"** is reserved: Partner Center's product "Karpo", Store ID `9MXKV2VWR2CP`, identity `NathanWalser.Karpo`. (A product "Karpos" beside it was a slip of the name; it holds nothing.) The name must match the package's `DisplayName`.
3. **The package** is made by `scripts/package.ps1 -Version 1.0.0` (docs/widgets.md §710), or by the release workflow from a tag (`.github/workflows/release.yml`): `artifacts/package/Karpo_1.0.0_x64.msix`, unsigned, since the Store signs what it publishes. What it holds:
   - Partner Center's identity (`package/AppxManifest.xml`): Package/Identity/Name, Publisher and PublisherDisplayName.
   - The languages it supports, all 40, by their listings' tags. A listing language must be one the package declares.
   - `runFullTrust`, and `windows.startupTask` in place of the Run entry and the logon task, which a packaged copy no longer makes (a scheduled task outlives an uninstall, policy 10.2.7).
   - `unvirtualizedResources`, with the registry's and the files' redirection turned off: Karpo writes for Windows to read and starts every app the person clicks, neither of which works behind a package's own copies. A restricted capability the Store must grant (the words for it are under "Submission options").
   - The logos in `package/Images` (`node store/compose.mjs --package`, then copied there).
   - **Not tried in its package yet.** Before the first submission, with Developer Mode on: `Add-AppxPackage -Register artifacts\package\layout\AppxManifest.xml`, start it, start apps from it, turn Start with Windows off and on, end it from Task Manager (the guard starts it again), and uninstall it while it runs: Windows' taskbar must be back afterwards. An uninstall runs none of Karpo's code, so a taskbar left hidden would stay hidden.
4. **The Karpo Pro add-on** exists (below): `karpo-pro`, Store ID `9NT4RFR5R3BN`, which is `StoreIds.ProAddOn` (`Karpo.Core/Licensing.cs`). It can be submitted only once the app is published, so publish the app first as "available but not discoverable, direct link only", buy Pro once to see the whole purchase work, and then make it public.
5. **Take each language's own captures** (above) on an idle PC. The English set was captured on 2026-11-12.
6. **Have native speakers read the listings** where you can. They're careful translations, not reviewed ones.

## Pricing and availability

- **Base price:** Free. Free trial: none (the free version is the trial).
- **Markets:** all.
- **Visibility:** public, discoverable.
- **Release:** manual (publish once certified), so the site's `STORE_URL` and the launch email go out together.

## Properties

- **Category:** Productivity. Secondary category: Personalization.
- **Privacy policy URL:** https://lometics.com/en/legal/privacy/ (it covers Karpo: what stays on the PC, the services each widget asks, the Store purchase).
- **Website:** https://karpo.app
- **Support contact info:** support@lometics.com
- **Address and phone:** left empty: they are optional, and the account is a person's.
- **Product declarations:** leave "This product allows users to make purchases, but does not use the Microsoft Store commerce system" unchecked (Pro is a Store add-on). Leave "Includes generative AI features" unchecked. Check "Tested to meet accessibility guidelines" only after an accessibility pass. Keep the defaults for alternate drives and OneDrive backup.
- **System requirements:** Minimum: keyboard and mouse. The architecture comes from the package (x64).

## Age ratings (IARC)

Category: "App (not a game)". Every content question (violence, sex, language, drugs, gambling,
crude humour, fear) is **No**. The interactive ones:

| Question | Answer | Why |
| --- | --- | --- |
| Can users interact or exchange content with other users? | No | Karpo only reads mail and calendars; it sends nothing to anyone. |
| Does the app share the user's current physical location with other users? | No | The weather rounds the position to about 1 km and sends it only to the weather service. |
| Does the app allow purchase of digital goods? | Yes | Karpo Pro, a durable add-on. |
| Does the app provide unrestricted internet access (a browser, search)? | No | Feeds and cameras show only the addresses the person adds; there's no browser. |

Expected rating: Everyone / 3+, with "In-App Purchases".

## Store listing

One per language, from `listing/<lang>.json`. The fastest way in: fill English by hand once, then
Store listings › Export listings, `node store/fill-csv.mjs <the exported csv>`, and Import listings
with the folder it names (`artifacts/store/import/karpo-store`). It fills, per language:

| Field | From | Limit |
| --- | --- | --- |
| Product name | `title` (choose the reserved name) | |
| Description | `description` (lines joined) | 10,000 |
| Short description | `shortDescription` | 1,000; the first 270 show |
| What's new in this version | empty for the first release | 1,500 |
| Product features | `features` | 20 × 200 |
| Screenshots and captions | `screenshots` | 10; captions 200 |
| Keywords | `keywords` | 7 × 40, 21 words in all |
| Additional system requirements | `minimumHardware`, `recommendedHardware` | 11 × 200 each |
| Copyright and trademark info | `copyright` | 200 |
| Developed by | `developedBy` | 255 |
| Store logos | `tile-300x300.png`, `hero-3840x2160.png` | |

Fields it doesn't know are left as exported and listed. Check Partner Center's preview of one
language before submitting. Short, sort and voice titles are Xbox fields: leave them empty.

## The Karpo Pro add-on

- **Product type:** Durable. **Product ID:** `karpo-pro`. **Lifetime:** Forever.
- **Content type:** Electronic software download.
- **Price:** $4.99 (the person, 2026-11-22: "you should price it at 5usd"). The site (karpo.app) still says $15 in every language and should say the same.
- **Listing, per language:** `addOn.title` (100), `addOn.description` (200), icon `pro-300x300.png`.
- **Keywords:** `addOn.keywords` (10 × 30).

## Submission options

**Restricted capability `runFullTrust`, why it's needed:**

> Karpo is a desktop (Win32) app packaged with the Desktop Bridge. It runs as a full-trust desktop
> process to show its own taskbar: it registers an AppBar with SHAppBarMessage, lists top-level
> windows to show running apps, and shows the notification area's and the apps' icons. It changes
> nothing outside its own windows except, when the user turns on "Hide the Windows taskbar", hiding
> it through documented APIs; Windows' taskbar comes back when Karpo quits.

(Out of date in two places since it was written: Karpo hides Windows' taskbar whenever it runs, with no setting for it,
§465; say that, and that it comes back when Karpo ends.)

**Restricted capability `unvirtualizedResources`, why it's needed:**

> Karpo replaces the Windows taskbar, so it has to work with the user's real settings and apps rather than with a
> package's private copies of them. It starts every app the user clicks on its bar, and those apps must see their own
> real settings and files, not Karpo's package view of them. It writes a few Windows settings for Windows itself to read:
> which notification-area icons are shown, night light, colour filters and nearby sharing from its quick settings, and
> where the Windows taskbar stands. And its own settings file is one the user can open in File Explorer and back up.
> Registry and file system write virtualization are therefore turned off; Karpo writes nothing outside the current
> user's own profile and registry hive.

**Notes for certification:**

> No account or sign-in is needed. Karpo starts as a taskbar along the bottom of the screen. Click
> any widget (the weather, the clock, the processor) to open its details. Right-click the taskbar
> and choose Edit to add, move and remove widgets; the gear in the bar along the top holds every
> setting. Karpo Pro is bought in Edit › gear › Karpo Pro; the free version allows 20 widgets per
> layout and 2 layouts, and only refuses to add more. Mail and calendar accounts are optional (Edit
> › gear › Accounts) and only read. To quit, Edit › gear › Quit Karpo: the Windows taskbar comes
> back as it was.

(Out of date since §703: the gear has no Quit Karpo any more. Karpo ends with `karpo --quit` from Win+R, or with the
taskbar menu's Quit Karpo where the person added that row; say whichever the submitted build has.)

## Words for customers

Avoid in anything a customer sees: Win32, Direct2D, DWM, native AOT, DIP, "islands", "sections",
"registry". Say "email and calendar accounts", "on any edge of the screen", "floating or
full-width". Keep the resource promise plain and numberless: Karpo is light on memory and battery.

## Concepts

Pictures tried before any is chosen for the listing: `concepts.json` (each one's words and layout given in full) over the captures of `concepts.txt` (`dotnet run scripts/stage/Stage.cs -- store concepts [scene]`), in English alone. `node store/compose.mjs --concepts [id ...]` renders them to `artifacts/store/concepts`, numbered. One that is chosen moves to `listing/<lang>.json` and `pictures` in `compose.mjs`, and is translated. The `combo`, `slim` and `standard` layouts of `config.json` are built like the person's own bar: buttons wearing two faces (the weather and the air, Wi-Fi and the sound, the date and the bell), folders for the files and for the PC.
