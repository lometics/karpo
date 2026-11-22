# Accounts

Karpo reads calendars, contacts and mail from the person's accounts itself. It
used to ask hearth, the separate sync hub, over a named pipe; that link is gone,
and Karpo holds no connection to hearth at all.

**Read-only, and little kept** — but for the tasks. Karpo writes nothing to an account's calendars, contacts
or mail: no event, no contact, no mark on a message. A Microsoft account's To Do tasks and a Google account's
Google Tasks are the one thing it writes: a task ticked, added, renamed or deleted on a to-do line set to one of
their lists (below, "To Do"). Every read is made on Karpo's own clock and
held in memory; what the calendar, mail and contacts widgets draw is
also kept on disk, encrypted for the Windows user, so they work offline
(below, "Offline"). It keeps the
sign-in too, because without it the bar would ask for a password at every boot.

## What there is

| Kind | Signs in with | Gives |
| --- | --- | --- |
| Microsoft (personal, work, school) | the browser: OAuth 2 with PKCE | calendars, contacts, mail, To Do's tasks |
| Google (Gmail, Google Workspace) | the browser: OAuth 2 with PKCE | calendars, contacts, mail, Google Tasks' tasks |
| Mail server (IMAP) | address and password (an app password for Gmail, iCloud) | mail |
| CalDAV and CardDAV | user name and password | calendars, contacts |

A Google account is its own kind since widgets.md §687 (below, "Google"), read through Google's own APIs. A
Gmail account can still be added as a mail server with an app password, and a Google calendar as CalDAV;
neither needs Karpo's client at Google.

The widgets:

- **Calendar** — the date badge, its card, and the calendar panel, with the
  events of every account's calendars, each in its calendar's colour (below, "Colours"). A country's
  public holidays can be one more calendar beside them, set on this page but kept in the config, not
  here: they have no account (docs\widgets.md §237).
- **Contacts** — a glyph button whose panel searches everyone in every address
  book; Enter copies the address or number. People of more than one book wear
  their book's colour on their glyph.
- **Mail** — an envelope with the unread count on its corner, whose panel lists
  the newest messages of every inbox; with more than one inbox, each message has
  its inbox's colour as a bar before it. An account syncs the other folders the person turns on on its page
  (below, "What is synced"; widgets.md §639), and a mail line may show them instead or besides (§455, "Show
  from"), each row then naming its folder. A row opens nothing: Windows has no way
  to ask "the default mail app" to show one message of one account, and Karpo
  opens nothing rather than the wrong thing.

## Where things are

`Karpo.Accounts` is the library the dock uses. It has no UI types and is
native-AOT clean: JSON goes through `JsonNode` or a generated context, MailKit
publishes without a trim warning, and the one HttpClient is Karpo's own.

| Piece | File |
| --- | --- |
| An account, and what its kind gives | `Account.cs` |
| accounts.json: patched under a mutex, never saved whole | `AccountBook.cs` |
| Secrets, DPAPI for this Windows user | `Secrets.cs` |
| What every kind answers (`IAccountReader`) | `Reading.cs` |
| A sign-in in the browser and its tokens, by hand: PKCE, the loopback port, refresh, the scopes granted (§687) | `OAuthClient.cs`, `Loopback.cs` |
| A provider's JSON API: a bearer per request, throttling waited out, its refusals in Karpo's words | `Rest.cs` |
| A kind the person never let Karpo at (`ConsentException`) | `Web.cs` |
| Microsoft's sign-in over it: its registration, To Do's token of its own | `Microsoft\MicrosoftAuth.cs` |
| Graph: calendarView, contacts, the inbox and the mail folders turned on (§639) | `Microsoft\Graph.cs`, `Microsoft\MicrosoftReader.cs` |
| What a widget's "Show from" offers (§455) | `SourceParts.cs` |
| Graph: To Do's lists and tasks, read and written | `Microsoft\MicrosoftTodo.cs` |
| Google's sign-in: its client, its scopes, a kind's token refused where its scope was not granted (§687) | `Google\GoogleAuth.cs` |
| Google's APIs: a kind's own door, Google's pages, its 403s for a rate and for a scope | `Google\GoogleApi.cs` |
| Calendar v3's events and People's contacts; Gmail's labels as folders; Google Tasks, read and written | `Google\GoogleReader.cs`, `Google\GoogleMail.cs`, `Google\GoogleTasks.cs` |
| Tasks, and the queue of changes not sent yet | `Tasks.cs` |
| IMAP through MailKit — the folders by special use (§639) —, and finding the server | `Mail\ImapReader.cs`, `Mail\MailDiscovery.cs` |
| CalDAV and CardDAV, and finding the homes | `Dav\DavClient.cs`, `Dav\DavReader.cs`, `Dav\DavDiscovery.cs`, `Dav\VCard.cs` |
| A CalDAV calendar synced against what the dock holds (`ICalendarSync`, §616) | `Dav\CalendarSync.cs` |
| What is kept of a CalDAV object: the rows' fields, the meeting's link | `Dav\KeptICalendar.cs` |
| SRV and TXT lookups | `Dns.cs` |
| What the dock synced, for the Accounts page | `SyncReport.cs` |
| The colour of each calendar, inbox and address book | `SourceColours.cs` |

The client code of the DAV, IMAP-discovery and loopback parts is ported from
hearth's providers; the sync passes are not, because Karpo keeps nothing to
sync into.

In the dock, `Karpo.Dock\Pim\` holds the stores and the widgets:
`AccountSet` (the readers, made again when accounts.json changes),
`AccountCalendars`, `ContactStore`, `MailStore`, and the islands and flyouts;
`CalendarMirror` keeps each CalDAV calendar whole on disk, and
`Karpo.Dock\Calendar\EventIndex.cs` holds its objects parsed, by the span each can have events in.
The edit bar's ⚙ Settings › **Accounts** (and each widget's own Accounts level) adds and removes
accounts, shows what is synced of each and turns parts of one off (below, "What is synced";
widgets.md §324, §338).

## The file

`%LOCALAPPDATA%\Karpo\accounts.json`, not beside the roaming config: its
secrets are DPAPI blobs that only this Windows user on this PC can read, so a
config carried to another PC must not carry them.

The dock changes it: its menus add and remove accounts, and it
writes a refresh token back whenever Microsoft (or, should it, Google) hands out a new one.
Every change is a patch on the file as it stands, under a named mutex, written
through a temporary file — so two apps never undo each other. A file that does
not read is no accounts, and is kept aside as `accounts.json.broken-<time>`
before anything writes over it.

The dock watches the file. A change in which accounts there are, or where they
read from, or a password typed again, or a part turned off or on, makes every
store read at once; a token written back does not (`AccountSet.Shape`). Signing in to an account again adds
it anew, with a new id, which is what tells the stores.

## What is synced

*"in the account view it should show which items are synced and how many of them. you should also be
able to not sync some of the emails inboxes, calendars, contact lists etc."* (2026-10-11)

**The menu.** Each account in an Accounts level is a row: its address, what it gives and where from,
its bin to remove it. Its submenu lists what is synced of it kind by kind
(`Karpo.Accounts\AccountSyncRows.cs`), in the order the line under the address names them.

*"in the accounts overview in the individual account it should be more obvious what the header is and
what actually and item is that can get synced."* (2026-10-12) A kind's heading and a part's row looked
alike — both cards with an icon, a name and a line under it. Now they are two things, as Windows 11's
Settings has section headers and cards:

- a **heading** per kind is no card: "Calendars" in Windows' section header style (`BodyStrong`), room
  above it, and under it, small and secondary, when it was synced — at the column the rows' icons stand
  at, on the expander's own ground, with no icon, colour or switch;
- every **part** is a card on the card's ground: its kind's glyph, its name, how many it holds under it,
  and at its end its colour's swatch and its switch.

The heading's words:

- per kind — Calendars, Contacts, Mail — saying when it was last synced ("Synced at 14:05",
  "Synced 10 October at 14:05"), or why it failed ("Couldn't sync: …"), or "Not synced yet". The
  calendars' row adds the span their counts are of ("events from September
  2026 to October 2027": the store's window, a month back to a year ahead, wider where a view looked);
- a row per part with a switch: the **inbox** ("1234 messages · 12 unread"), then each other **mail folder**
  (§639, below), each **calendar** with its colour's dot ("56 events" in the span), each **address
  book** ("312 contacts" — a Microsoft account's default folder is "Contacts", its other contact
  folders by their names; a CardDAV account's books by their display names). Every row has its kind's
  icon, so every name starts at one x (2026-10-12: "align the individual synced stuff as if all of them
  have an icon"): the inbox the envelope, a book the address book, a calendar the calendar. Its colour
  is the swatch at its end (below, "Colours"), no longer a dot in the icon's place.

Words change in place when a sync ends, a switch turns or a colour is picked: no card closes, no switch loses the keyboard;
a card's body is built again only when the parts it lists change (a calendar added at the account).

**Off.** A switch turned off writes the part to the account's `off` list in accounts.json
(`AccountBook.SetSynced`, a patch under the mutex like every other change), as `SyncKeys`: `mail`,
`calendar:<id>`, `contacts:<id>` — the ids the readers give, the same from run to run. The row says
"Not synced" at once. The dock hears the file (`AccountSet.Shape` holds the list) and reads again:

- a calendar off is still listed by its reader, so the page can name it, but its events are not asked
  for (`CalendarRead.Off`; Graph's calendarView, CalDAV's sync are skipped); the store shows
  it nowhere — not even what was read or kept before, and not when the account is offline — and the
  next cache write leaves it out; a CalDAV calendar's mirror is deleted, and turned on again it is
  downloaded again whole (§616);
- an inbox off is not read at all: the store drops that account's messages and unread count and the
  cache keeps nothing of it. With every inbox off the mail line says so ("Mail is turned off for every
  account…"), not "add an account";
- an address book off is listed and not fetched (`BookRead.Off`); nobody of it is in the contacts.

Turned on again, the part is read at once and shown when the read lands ("Not synced yet" meanwhile).
Nothing is off unless turned off: `off` is optional and left out when empty, so every accounts.json
written before has everything on, and turning everything back on leaves the file as it was.

**Mail folders** (§639). *"you should also be able to sync other folders than just the inbox for example. this
change should be put through all the way to the email widget as well."* (2026-11-09) Under the inbox, the Mail group
lists every other folder of the account as its last read listed them — Graph's `mailFolders` and each folder's
`childFolders` (to 4 deep), IMAP's one LIST, its folders of a special use (RFC 6154) first by their own names
("Sent Mail", not "[Gmail] / Sent Mail"), \All and \Flagged last —, a folder in a folder named after its parent
("Inbox / Projects"), each with the folder glyph and a switch. A folder is **off until turned on** — the one kind of
part that is: turned on, it is kept in the account's `on` list (`Account.On`, `"on": ["mail:<folder id>"]`, a patch
like `off`, left out when empty), read beside the inbox at every sync — its newest 30 and its counts, as the inbox is
— and offered to a mail widget's "Show from". Its row says its counts where it is read, and off, its unread where the
account says so with its list ("Not synced · 3 unread": Graph always; IMAP where the server has LIST-STATUS, never a
STATUS per folder). It has no swatch: on the bar it wears its inbox's colour. The inbox may be off and a folder on:
that folder is read; with every mail part off, the account's mail is not read at all and the heading says "Not
synced" (the folders come back in the list once a read lists them again). Before, a folder was read while a mail
widget picked it; that is gone — the Accounts page is the one place a folder is synced. Tests: `SyncChoiceTests`
(`AFolderIsOffUntilTurnedOnAndKeptInTheFileAsOn`, `AMicrosoftAccountListsItsFoldersAndReadsThoseTurnedOn`,
`AnImapServersFoldersAreListedBySpecialUse`), `AccountSyncRowsTests.AnAccountsMailFoldersAreRowsOffUntilTurnedOn`.

**Where the counts come from.** Never the network and never the page: the stores already hold what they
read, and after every read each puts its own kind into `sync-report.json` (`SyncReport.Put`), beside
accounts.json in the local folder — per account, when it was read, the problem, and each part with
its count, or off. The calendars count each occurrence in the window (a CalDAV series expanded), the
mail takes Graph's `totalItemCount` and `unreadItemCount` or the IMAP inbox's message and unread
counts it was already reading, the contacts the people of each book. A write that would change nothing
is not made. The file names calendars and books, so it is DPAPI-encrypted for this Windows user like
the caches (entropy `Karpo.SyncReport.v1`); one that does not read is no report. The page watches it
and accounts.json and reads them again a moment after either changes. An isolated run writes none.

A part turned off here leaves every widget. A widget may show fewer still: its own **Show from** picks among
what is synced here — calendars, address books, an inbox and the mail folders turned on — and filters
what the stores hold (docs\widgets.md §455, §639). A mail widget's Show from (§644) lists every account's every folder
under the account's line; one not synced says so, and picking it there turns it on here (the same `on` this page
writes), "Syncing…" until it is read. Unpicking it there leaves it on here: this page is where a part is turned off.

**Tests:** `Karpo.Tests\Accounts\SyncChoiceTests.cs` (on until turned off, a turn as a patch, a file of
before with everything on, a turn the dock hears, Graph asked nothing of a calendar or folder turned off,
the report's kinds and its encryption); `Pim\SyncChoiceStoreTests.cs` (a calendar off shown and kept
nowhere and back when on, hidden at once offline, an inbox off unread and dropped, every inbox off, a
book off); `Accounts\AccountSyncRowsTests.cs` (the groups, the counts, off before the dock heard, before
any report, a failure, words changed in place).

## Colours

*"in the account settings you should be able to set colors for different calendars, email inboxes, or
contact lists."* (2026-10-12)

**What a part wears** (`Karpo.Accounts\SourceColours.cs`): every calendar, inbox and address book has a
colour — the one the person picked, else the one the account gives it (Graph's `hexColor`, else
Outlook's named colour; CalDAV's `calendar-color`), else one of the palette's. The palette is Outlook's
calendar colours as Fluent's shared colours have them, twelve by hue (red, orange, gold, green, teal,
blue, cornflower, purple, lilac, pink, brown, grey), deep enough to read as a dot or a bar on a light bar
and a dark one. A part without a colour of its own takes the palette's next by its place among its kind
— the accounts in the order they were added, each one's calendars and books in the order its reader lists
them, shown or turned off — skipping what any account gives, starting blue, teal, purple, orange, so the
first few never look alike. A part keeps its place in that round when the person picks it a colour, so
picking one never recolours another (two may then wear one colour: the person's choice). Inboxes,
calendars and books are three rounds of their own.

Nothing is written for a colour nobody picked: the dock works it out, from the same accounts and the same
order — from what it read, and its `sync-report.json`, which lists every calendar and book with the
account's own colour.
The accent no longer stands in for a calendar without a colour: it was the first such calendar's, which
the page could not show as the bar did.

**Kept** in accounts.json beside `off`: `colours`, an object of `SyncKeys` to `#RRGGBB`
(`Account.Colours`, `AccountBook.SetColour`, a patch under the mutex like every other change), left out
when nothing is picked — every file before has nothing picked, and Automatic again leaves the file as it
was. Read tolerantly (`ColoursConverter`): an entry that is no colour is dropped, and anything but an
object is no colours, so a hand edit there never makes the file unreadable and the accounts gone.

**Picked** in an account's submenu, a swatch before each part's switch opening its colours in place (widgets.md
§452; §338 to §448 a **Colors ›** level apart; the settings app, where this started, is gone since §341). On the page it was: each part's row has a swatch before its switch (`Controls\PartColorButton.cs`),
a dropdown button with the colour as a round dot, as Outlook puts a calendar's colour beside it. Its
flyout is the Appearance page's colour flyout cut to a part: the part's name; **Automatic** with what it
would wear unpicked ("The account's own color" or "Picked by Karpo"), ticked while nothing is picked; the
palette, six a row, the picked one ticked; and **Custom colors → View colors**, WinUI's ColorPicker in a
dialog without the alpha, kept on Done. A tile is the colour at once and the flyout stays open; the full
picker writes only what Done keeps.

**Drawn** on the bar without a read: the dock hears accounts.json change, and a change only in the colours
(`AccountSet.Paint`) is `Recoloured`, not `Changed` — the readers are swapped for the file's, and the
stores draw again from what they hold:

- **calendars**: each `AccountCalendar` takes its colour at every rebuild (the same source object, its
  colour changed), so the events' bars and blocks in every line and view follow;
- **mail**: with more than one inbox shown, each message carries its inbox's colour (`MailItem.Colour`)
  and the mail line's taller rows draw it as a bar on the gutter before the sender and subject, as an
  event wears its calendar's (`EventsTile`), the words past it; one inbox marks nothing and keeps the room;
- **contacts**: with more than one book shown, each person carries their book's colour (`Person.Colour`)
  and the contacts line draws their glyph in it (high contrast: the text's); the store keeps the last
  read's books to recolour without reading.

**Tests:** `Accounts\PartColourTests.cs` (what a colour reads as, the palette's round, a pick moving no
other, a pick kept and Automatic leaving no trace, a file of before read intact and a pick as a patch, a
hand edit costing nothing, a pick alone drawn not read); `Pim\PartColourStoreTests.cs` (a calendar's pick,
the account's and the palette's, drawn without a read; several inboxes and books marked, one not);
`Accounts\AccountSyncRowsTests.cs` (every row's colour and Automatic, worked out across the accounts as the
dock does). Pictures: `Pim\PartColourPictures` (`part-colour-<mail|events|contacts>-lines-<dark|light>-<100|150>.png`); the
menus: `settings-account-<dark|light>-150.png` (Menus\AccountFormTests).

## Microsoft

Karpo's own app registration in Entra: a public client for personal and work
accounts, redirect `http://localhost` (the port is Windows' pick each time),
scopes `offline_access User.Read Calendars.Read Contacts.Read Mail.Read` —
read — and, since 2026-10-16, `Tasks.ReadWrite` for To Do (below). It is "Karpo" in its maker's Entra
directory, made 2026-09-27, client id
`09fc8db5-849d-4059-8d98-b71075df8cec` (`MicrosoftAuth.ClientId`). The scopes
are not listed on the registration: the v2 endpoint asks for them at sign-in,
and the person consents to exactly those. Nothing in it is a secret.

Not MSAL: its cache and broker lean on reflection native AOT keeps nothing for,
and all Karpo needs is two form posts — the sign-in Microsoft's and Google's share
(`OAuthClient.cs`, §687). The browser is the system's; the proof
is PKCE, because a desktop app cannot keep a secret. Microsoft hands out a new
refresh token with every refresh and the old one stays valid until it expires,
so whichever app refreshed last writes its token back and neither loses the
account. A refused refresh is "sign in again", said on the widget's panel.

Graph's `calendarView` expands a series into occurrences with their exceptions
applied, asked for in UTC (`Prefer: outlook.timezone="UTC"`). An all-day event
is its dates. A calendar's own colour is its `hexColor`, else Outlook's named
colour; the person may pick another ("Colours").

A work account whose organization does not let users consent to apps needs an
administrator's consent once; the panel shows Microsoft's words.

## Google

*"i want to integrate google oauth api into karpo as well. make a new project on google cloud for this and fill in all the
required information. use the company information. then also implement the sync on our side so that users can login with
google directly and give the application consent to access their data."* (2026-11-12, widgets.md §687)

**The client.** Karpo's own OAuth client in a Google Cloud project of Lometics GmbH, of the **Desktop app** type: its id is
`GoogleAuth.ClientId` (`Karpo.Accounts\Google\GoogleAuth.cs`), and it has a secret, `GoogleAuth.ClientSecret`. Made 2026-11-12:

- **Project** "Karpo" in Lometics GmbH's Google Cloud organization; its Calendar, People, Gmail and Tasks APIs on.
- **Client** "Karpo for Windows" (Desktop app). **Consent screen** (Google Auth Platform): app name Karpo, the Karpo logo,
  home page https://karpo.app, privacy https://lometics.com/en/legal/privacy/, terms https://lometics.com/en/legal/terms/,
  authorized domains karpo.app and lometics.com; user type **External**, publishing status **Testing** (only the accounts
  named there as test users can sign in until Google has verified the app). Data access lists the scopes below.
- **The secret** is not in the source (widgets.md §713): a build is given it, and one that was not offers no Google
  account, as below.

While the id is empty, or the build has no secret, no Google account is offered: Settings › Accounts' "Add a Google account" is greyed with "This Karpo has no Google
app registration to sign in with yet", as Microsoft's is without its registration, and a sign-in is refused before the
browser opens. **The secret is no secret.** Google's token endpoint wants a Desktop client's secret beside its id, and
Google says itself that a desktop app is a public client that cannot keep one: it ships in every copy of Karpo, and the
proof is PKCE, as with Microsoft. Not Google's library either: the same two form posts as Microsoft's (`OAuthClient`).

**The sign-in.** `https://accounts.google.com/o/oauth2/v2/auth` with PKCE (S256), a state, `access_type=offline` and
`prompt=consent select_account` — the consent screen every time, so a refresh token comes back every time, and the account
picker, so adding an account is never whatever the browser is signed in to — and on a Reconnect the account's address as
`login_hint`. The redirect is the loopback port Windows picks, as `http://localhost:<port>`, listened on for IPv4 and IPv6
(`Loopback.cs`); Google takes any port of a loopback address for a Desktop client and documents `localhost` beside
127.0.0.1 and [::1]. The code goes to `https://oauth2.googleapis.com/token` with the verifier, the id and the secret; who
signed in is OpenID Connect's userinfo (`email`, `name`). The scopes, one list (`GoogleAuth.Scopes`):

| Scope | For | Google's class |
| --- | --- | --- |
| `openid email profile` | who signed in | not sensitive |
| `https://www.googleapis.com/auth/calendar.readonly` | the calendars, read | sensitive |
| `https://www.googleapis.com/auth/contacts.readonly` | the contacts, read | sensitive |
| `https://www.googleapis.com/auth/gmail.readonly` | the mail, read | **restricted** |
| `https://www.googleapis.com/auth/tasks` | Google Tasks, read and written | sensitive |

**Kept signed in.** A refresh asks for no scope and gets the person's; Google keeps the refresh token it gave (it hands
out no new one on a refresh) until the person takes the access back, it goes unused six months, or a hundred newer ones
of the same account and client push it out. A refused refresh (`invalid_grant`) is "Google wants you to sign in to … again".
One access token for every kind, held until it nearly expires (`OAuthClient`, a slot per account).

**Consent, kind by kind.** Google's consent screen lets the person untick any of the data scopes. The token says which
were granted (its `scope`), and a kind whose scope is not among them gets no token: `GoogleAuth.TokenAsync` throws a
`ConsentException` for that kind alone, as a 403 `insufficientPermissions` / `ACCESS_TOKEN_SCOPE_INSUFFICIENT` from the
API does (`GoogleApi`). The kind's store tells the Accounts page (`SyncPart.Consent`, the same flag To Do's consent has),
whose heading says "Not synced: Karpo needs your permission for Google Calendar" (Google Contacts, Gmail, Google Tasks)
with **Reconnect to sync calendars** (contacts, mail, tasks); the edit bar's errors say the same; the other kinds are read
as ever. Reconnect is To Do's: the browser with the address as `login_hint`, the new refresh token kept **on the same
account** (`AccountSignIn.SignInAgainAsync`, any kind that signs in in the browser), its id, what is off and its colours
kept. An account whose person granted none of the data scopes is still added: every heading of it says what is missing and
offers its Reconnect — clearer on the page than a sign-in that failed for no reason the person can see.

**Verification.** Until Google verifies the app, its consent screen is in **Testing**: only the test users entered in the
Cloud project can sign in (a hundred at most), Google warns that the app is unverified, and every refresh token expires
after **seven days** — a tester signs in again each week. A public release needs Google's verification first: brand
verification and, for the sensitive scopes (Calendar, Contacts, Tasks), scope verification with a video of the sign-in
and a privacy policy; Gmail's `gmail.readonly` is a **restricted** scope, which also needs a yearly third-party security
assessment (CASA) before it may be offered to everyone. Until then, Gmail can be read as an IMAP server.

**Calendars** (Calendar v3, `https://www.googleapis.com/calendar/v3`). `users/me/calendarList` (paged): a calendar is its
id, the person's own name for it (`summaryOverride`) else its `summary`, and its `backgroundColor`; one deleted is not
listed. Per calendar not turned off, `calendars/{id}/events?singleEvents=true&orderBy=startTime` in the store's window
(`timeMin`/`timeMax` in UTC), 2500 a page, its `fields` trimmed to what is drawn: a series comes as its occurrences with
their exceptions, as Graph's calendarView does. An event with a `date` is all day, its end the day after (exclusive); one
with a `dateTime` is that instant in UTC. One cancelled is not drawn, nor a working location ("Home", "Office"). Shown as
free (`transparency`) or declined by the person (their own attendee's `responseStatus`) is free; its meeting's link is
Meet's (`hangoutLink`), a conference's video entry point (Zoom's, Teams' add-ons), else one in its place or its notes
(`MeetingLinks`); its link is its page in Google Calendar (`htmlLink`), and a day opens Google Calendar's day view
(`CalendarLinks`, the same as a Google CalDAV account's). Each calendar fails alone (`Parts`); pages going on past the most
read are "not all there".

**Contacts** (People, `https://people.googleapis.com/v1`). One book, "Contacts" (`myContacts`: everyone in the person's
contacts), `people/me/connections` with `names,emailAddresses,phoneNumbers,organizations,photos,metadata`, a thousand a
page: a person is their primary name, their addresses, numbers and organisation. Their picture is their first that is not
Google's drawn letter (`photos[].default`): its version a short hash of its address, and the picture asked of People again
(a reader keeps nothing) and fetched from Google's photo server at 128 px **as nobody** — the token never goes there.

**Mail** (Gmail, `https://gmail.googleapis.com/gmail/v1/users/me`). Labels are folders: the inbox first, then Gmail's own —
Sent, Drafts, Spam, Trash, Starred, Important, by Karpo's names for them — then the person's by name, a label in a label
("Work/Clients") as a folder in a folder is, "Work / Clients"; Gmail's categories, UNREAD and CHAT are none. The inbox is
synced unless off, a label only where turned on (§639). Of each synced: its counts (`labels/{id}`: `messagesTotal`,
`messagesUnread`), its newest 30 (`messages?labelIds=`), each by its headers alone (`format=metadata`: From, Subject,
Message-ID), six at a time; unread while it wears UNREAD, its time Gmail's `internalDate`, its preview the snippet, its
link Gmail's search for its Message-ID (`MailLinks.Google`, the same as Gmail's IMAP server's). A label not synced shows no
counts: Gmail lists labels without them, and Karpo asks for none it does not read. The inbox failing fails the read; a
label fails alone.

**Tasks** (Google Tasks, `https://tasks.googleapis.com/tasks/v1`; below, "To Do", for the store and the queue). The lists,
`users/@me/lists` (paged): Google marks none as the default, so `users/@me/lists/@default` — "My Tasks" — is asked for and
that one is. A list's tasks with `showCompleted` and `showHidden` (the ones ticked off in Google's own apps are hidden): every
open one, and of the completed the 200 last ticked off. A task is `needsAction` or `completed`, with no star; its due is a
date Google keeps as that day's UTC midnight, read as the day it says. Google keeps no time a task was made: its last change
(`updated`) stands in, by which the line orders the open tasks and a tried Add is found again — so a task renamed elsewhere
moves to the end of the line. Writes: insert (title, status), patch (title, status; `completed` cleared when put back),
delete; a list renamed (`title`) or deleted, never "My Tasks". **No precondition:** Google documents no If-Match on a
task, so no version is sent and the last change to reach Google wins (TaskQueue's own order and folding still hold). A
task or a list already gone (404) is no failure.

**Tests:** `Accounts\GoogleAuthTests.cs` (the request: PKCE, offline, the consent screen and the picker, every scope, the
hint; a whole sign-in through the loopback port with the secret; a refresh with the secret and no scope, the token Google
gave kept; a kind not granted refused alone, a token that names no scope; a refused refresh; no client id, no Google
account); `Accounts\GoogleReaderTests.cs` (events: a day, an instant in UTC, cancelled, a working location, free, Meet's
link; calendars paged, one failing alone, one off not asked; Google's 403s for a scope and for a rate; a kind refused, the
others read; a person, a picture's version and size, the drawn letter none, the picture fetched without the token; labels'
names, order and skips, a message's headers, the inbox and labels read and one failing alone, the inbox off);
`Accounts\GoogleTasksTests.cs` (a task, "My Tasks", the open and the newest completed, every write and no If-Match, gone
is no failure); `Accounts\AccountSyncRowsTests.AGoogleKindNeverGrantedOffersAReconnectForThatKind`;
`Pim\SyncChoiceStoreTests.AKindNeverGrantedIsToldAsConsentNotAsAFailure`; `Edit\FaultsTests`; `Calendars\EventOpenTests`;
`Accounts\MailLinksTests`.

## To Do

*"add a todo widget as well if we dont have already. it should get its tasks from microsoft office, like the
email etc."* (2026-10-16) The to-do widget (docs\widgets.md §87, "Microsoft To Do") shows either the notebook
on this PC or an account's task list — a Microsoft account's To Do list, or since §687 a Google account's Google Tasks
list (above, "Google": what Google gives and takes); this is the second. What follows of Graph is To Do's; the store, the
queue, the cache and what is synced are every account's that gives tasks (`ITaskReader`).

**Graph** (`Karpo.Accounts\Microsoft\MicrosoftTodo.cs`, behind `ITaskReader`, which `MicrosoftReader` is
too): `/me/todo/lists` (the default "Tasks" is `wellknownListName` `defaultList`), and per list its open tasks
(`$filter=status ne 'completed'`, every page, at most 2000) and its completed ones (`status eq 'completed'`,
at most 200). A task is its title, status (`completed` is done, every other status open), importance (`high`
is To Do's star), due day, created, completed and last-modified times, and its `@odata.etag`. A due day is a
date: To Do keeps it as that day's midnight in the zone it names, or that midnight carried to UTC
("2026-10-19T22:00:00", "UTC" for the 20th in Zurich); a midnight reads as the day it says, any other time is
turned to this PC's zone first (`MicrosoftTodo.Due`). Writes: `POST …/tasks` (title, and `status` where it
was ticked before it was sent), `PATCH …/tasks/{id}` (`title`, `status` `completed` / `notStarted`) and
`DELETE …/tasks/{id}`, each with `If-Match` the version it was made on; a 412 is `TaskConflictException`, a
404 a task already gone (no failure). A list (docs\widgets.md §323, a to-do line's menu): `PATCH
/me/todo/lists/{id}` (`displayName`) renames it and `DELETE /me/todo/lists/{id}` deletes it (a 404 is no
failure), never the default "Tasks"; `TaskStore` shows either at once and reads the account again after,
whether To Do took it or not — unlike a task's change it is not queued. A list is not made from the menu:
"New list" makes one on this PC.

**The scope, and accounts added before.** A sign-in now asks for `Tasks.ReadWrite` beside the reads
(`MicrosoftAuth.Scopes`). An account added before consented to the reads only, and Microsoft refuses a
refresh that asks for more than was consented — so the reads' refresh keeps asking for exactly the reads
(`ReadScopes`) and their token is held as before, and the tasks have a token of their own
(`TasksTokenAsync`, `TaskScopes`: `offline_access Tasks.ReadWrite`). Where that one is refused but the reads'
still comes, the sign-in is good and the consent is missing: `TasksConsentException`. Mail, calendars and
contacts never notice. The Accounts page then says "Not synced: Karpo needs your permission for Microsoft To
Do" under the account's **Tasks** heading, with the accent button **Reconnect to sync tasks**, and a line set
to that account's list is "Reconnect to sync tasks" / "Set up in Settings", which opens the page. Reconnect
signs in in the browser with the account's address as `login_hint`, and keeps the new refresh token **on the
same account** (`AccountBook.SetSecret`): its id, what is off and its colours stay, so every to-do line set to
it goes on. A sign-in that comes back as another account keeps nothing and says so. The dock hears
accounts.json written (`AccountSet.Rewritten`, raised where nothing a store reads by changed) and an account
waiting for consent is read at once.

**The store** (`Karpo.Dock\Pim\TaskStore.cs`) reads as the mail does: from the dock's start (`PimSync`), each
account that gives tasks its own sync at the online interval, a minute after a failure, at once when the accounts
change, and when a line showing a list opens and the last try is a minute old. Its lists all, their tasks
only where the list is on.

**Changes, offline, conflicts** (`Karpo.Accounts\Tasks.cs`). A tick, an add, a rename or a delete on the line
is a `TaskChange` in the account's queue, shown at once — the server's tasks with the queue laid over them
(`TaskQueue.Apply`) — and sent in order before the account is next read. The queue folds as it grows
(`TaskQueue.Enqueue`): a task added and not sent takes its later rename and tick into its add, and goes with
its delete without the server hearing of it; a second rename or tick of a task takes its first's place; a
delete takes the place of everything before it of that task. A new task has an id of the queue's own
(`new:…`) until the server gives it one, and the changes after it are rebound to that id. A change the
account cannot take now (no connection, a sign-in, the consent, a server error after Graph's own tries, a
throttle longer than Graph waits out, an answer that does not read) goes back to the front and waits, however
many passes it takes; only one the account refuses for good (a 4xx but 408 and 429, `TaskStore.Refusal`) is
dropped, and the next read shows what the account has (2026-10-31: a 5xx or a long 429 dropped the change too,
the tick gone without a word). **An Add whose answer never came** (a timeout, a connection dropped, a 5xx, an
answer that did not read) may have been made: it goes back marked (`TaskChange.Tried`), and before it is sent
again the list is read and a task of its words made since it was (give or take ten minutes of clock) that the
store does not know yet is taken for it — Graph takes no key a request could be known again by. A tried Add
takes no later rename, tick or delete into itself: they follow it, to the task it turns out to be. A change
refused for an older version (412) is settled by the last writer: the server's task is fetched, and the change
goes again over its version where the change was made at or after the server's last change, else the
server's stands (`TaskQueue.OursWins`). A delete is the same. The queue is kept on disk with the tasks, so a
change made offline goes at the next start.

**Kept**: `tasks-cache.json` beside the others (`PimCache`), DPAPI-encrypted for this Windows user with the
caches' entropy: per account (its random id) when it was read, each list's id, name and whether it is the
default, its tasks (id, title, done, star, due day, times, version) and the queue. Unlike the mail's, a task
keeps its id and its version, which a waiting change names. Written after every pass and every change, one
write at a time, each of the store as it is then. No address, no token.

**What is synced**: the Accounts page has a **Tasks** heading for a Microsoft or Google account, a row per list with
the CheckList glyph, "12 open tasks" and its switch (`SyncKeys.TaskList`, `tasks:<id>` in `off`), no colour
swatch. A list turned off is listed and not read; a line showing it is "This list isn't synced", which opens
the page. The report (`SyncReport`) has the tasks as its fourth kind (`tasks`, with `consent` and each list's
`default`), absent from a report of before.

**A list To Do refuses** (docs\widgets.md §399). Graph answers a filtered read of some lists (Flagged email's) with a
bare 400 "Invalid request"; such a list is read plain (`tasks?$top=100`) and split here. A list refused however it is
asked (a 4xx for its tasks, but 408 and 429, which fail the account and are tried again) fails alone: the account's
other lists are read and every list is listed; its row says why in place of its count (the report's item `problem`),
and the edit bar's errors have a row for it. Turned off, it is asked no more.

**How much is read.** Every open task of a list (Graph's own most, 20,000): the line lists them all. Of the completed,
the newest 200 by when they last changed (`$orderby=lastModifiedDateTime desc`; a list that will not be ordered says
400 and is read plain), which the line lists under "Completed".

**Tests:** `Accounts\ToDoTests.cs` (a list and a task as Graph gives them, due days in every zone form,
status and importance, the requests and `If-Match`, 412 and 404, a list Graph will not filter read plain, the queue's
folding, its overlay, rebinding, the last writer); `Accounts\MicrosoftAuthTests.cs` (the sign-in asks for To Do, the reads' refresh does not,
the two tokens, consent missing against a sign-in gone); `Pim\TaskStoreTests.cs` (the default list, loading,
every change shown and sent, words of spaces, offline kept and sent at the next start, both sides of a
conflict, consent and the reconnect, a list off and the report, the cache encrypted with no secret or
address); `Accounts\AccountSyncRowsTests.cs` (the Tasks group and its reconnect).

## CalDAV

**Kept whole, synced** (`docs\widgets.md` §616). Karpo keeps every iCalendar object of each CalDAV calendar, any
year, and brings them up to date with what changed: no time-range is ever asked, so no server's matching of a series
by its instances (RFC 4791 §9.9, which not every server gets right) can leave an event out, and a view of any year
is there with no read. Karpo expands the objects itself with `Karpo.Dock\Calendar\ICalendar.cs` (RRULE, RDATE,
EXDATE, overrides, VTIMEZONE), the RFC 5545 code it had before hearth: servers differ too much in what their own
`expand` gets right to lean on it, and this code has its tests (`RecurrenceTests`). A calendar's colour is Apple's
`calendar-color`.

The reader keeps nothing (`ICalendarSync`, `Dav\CalendarSync.cs`): the store hands it, per calendar, each object's
href and etag, the sync token and the tag of its last whole sync. Per sync:

- **One listing of the home** (PROPFIND Depth 1) asks each calendar's name, colour, `DAV:sync-token`, `CS:getctag`
  and `supported-report-set`. A calendar whose ctag (else sync-token) is what its last sync found is unchanged: nothing
  more is asked. A quiet account costs that one request an interval.
- **`sync-collection`** (RFC 6578; REPORT Depth 0, `getetag` only, sync-level 1) from the token kept: what changed and
  what went (404). With no token, the whole listing, compared by etag. A server answering in pages (a 507 for the
  calendar itself, RFC 6578 §3.6) is asked page by page with the token each gives, 1000 times at most and never again
  with the same token. A token it no longer takes (403, 409, 410, `DAV:valid-sync-token`: Nextcloud prunes them) is
  let go once: the whole listing, so only what differs is downloaded again.
- **No `sync-collection`** (not among its reports, or a 400, 403, 405 or 501 to it): the calendar is listed by
  PROPFIND Depth 1, each object's etag, and marked so; the ctag gates as above.
- What is new or of another etag is fetched in `calendar-multiget`s of a hundred, two at a time an account; a multiget
  cut short (a 507, among its answers or as its status) is asked again in halves for what did not come
  (`DavReader.MultigetAsync`, shared with the address books).
- Taken out: what the server says went, and from a whole listing what it no longer lists, only from a listing it gave
  whole. Once a day a calendar's whole listing is compared anyway, for a server that loses deletions.
- The token is kept only when every change came. Else the calendar is **not all there** (`CalendarChanges.Incomplete`):
  what came is shown, nothing unlisted is taken out, and the next sync asks again. A calendar of more than 50,000
  objects keeps 50,000 and says not all is shown.
- 429 or 503 is the account's failure, tried again as any; a calendar refused alone (403, 404) fails alone (below).

Each object is cut to what rows need before it is held or written (`Dav\KeptICalendar.cs`): VCALENDAR, VEVENT,
VTIMEZONE and its parts; of an event its UID, SUMMARY, LOCATION, STATUS, DTSTART, DTEND, DURATION, RRULE, RDATE,
EXDATE, RECURRENCE-ID, TRANSP, CONFERENCE, X-GOOGLE-CONFERENCE and URL, and its notes only as the meeting link in
them. No notes, attendees, organiser, alarms or attachments. An object with no event is held by its etag alone.

Cards are listed and fetched in multigets of a hundred, because
`addressbook-query` is where servers disagree most. A book whose listing the server cut short (a 507), or a multiget
still cut short after its halves, is shown marked incomplete.

## Contacts' pictures

*"the contacts widget should also display the peoples profile picture if applicable"* (2026-10-31,
`docs\widgets.md` §457). Each reader says per person what version of a picture they have (`Person.Photo`) and gives
the picture itself on its own (`IAccountReader.PhotoAsync`; a reader without contacts gives none):

- **Microsoft**: `GET /me/contacts/{id}/photo/$value`, under **Contacts.Read** — the scope the contacts already
  needed; no new permission is asked for. A contact says nothing of whether it has a picture, so its `changeKey`
  (read with the contact now) is the version, and a 404 is "none" (`Graph.BytesAsync`). Throttling is waited out as
  for every request.
- **Google** (§687): the person's own picture at Google's photo server (`photos[].url` not `default`: Google's drawn letter is
  none), under **contacts.readonly**; its version is a short hash of its address, and it is fetched at 128 px as nobody —
  the token never goes to the photo server. A person gone (404) is no picture.
- **CardDAV**: the card's PHOTO — a data: URI (4.0), base64 under `ENCODING=b`, `ENCODING=BASE64` or a bare `BASE64`
  (3.0, 2.1; folded lines joined), or an http(s) address (`VCard.Photo`). The book's read keeps a short hash of it as
  the version, never the picture; a picture is the card read again (a signed-in GET) and its bytes, or the address
  fetched — signed in only on the account's own server (same scheme, host and port), elsewhere as nobody, so the
  password never goes to a host a card names. A card gone (404) is no picture.

**Kept** in `contact-photos\` in the local folder (`ContactPhotos`): one file a person, named by a hash of their id and
their picture's version, holding the picture cut down to 64 px as pixels, DPAPI-encrypted with the caches' entropy
(`PimCache.Protect`); an empty file for "none". Fetched after each read of the contacts, four at a time, only for a
version not kept; an account that fails a picture keeps nothing of that pass and is asked at its next read. A file of
nobody listed is deleted once every account has been read. Nothing is fetched on an isolated run.

## Parts that fail alone

*"in terms of data display and syncing it needs to be bullet proof. i dont want people complaining that not all of
their data shows or that data gets inconsistent"* (2026-10-31). Every reader reads an account's calendars and books
one by one (`Karpo.Accounts\Reading.cs`, `Parts`): one the account will not give — a shared calendar taken back
(403), one gone since it was listed (404), a server error for it — is listed with why (`CalendarRead.Failed`,
`BookRead.Failed`) and the others are read as ever; once its 403 failed the whole account's read, and since a
failed account keeps its last read, every calendar of it stayed as it was for good. A sign-in refused, the network
gone or a timeout is the account's, not a part's. Where every part that is on failed, the account failed, as before.

The stores keep, for a calendar or book that alone failed, what it held before (`AccountCalendars.Held.After`,
`ContactStore.Kept`), with the failure on it: the calendar's events are shown and the calendar is **stale** from its
own last good read (the stale mark, `Freshness`); its row on the Accounts page says why in place of its count, and the
edit bar's errors have a row for it (the report's item `problem`, as §399's To Do lists). One that failed with nothing
kept, and one **not all there** — cut short by the server (`Incomplete`), Graph's pages going on past the most read
(`Pages.More`), or with events Karpo cannot show (`ICalendar.Dropped`: no start it can read, an hourly rule) — is what
the widget says is missing (`Failures.Missing`): "Couldn't read “Shared”: …", "Not everything in “Archive” could be
read, so some of it isn't shown." A calendar a server refuses is asked again at the interval, not in a minute: that
would cost the account's whole read every minute.

**Mail from every inbox.** The list is the 30 newest of all the inboxes and, of each, at least its own newest five
(`MailStore.Merged`, `EachInbox`): only the newest of all, a quiet inbox beside a busy one showed nothing at all.

**A COUNT from long ago.** A rule with a COUNT is counted from its start; given up on after 20,000 periods, a daily
series begun before 1971 lost today's occurrences. It is counted to the window now, however far back it starts, and
leaves marks on the rule every 512 periods so the next expansion — every frame of a view — starts there
(`RecurrenceRule.Mark`).

**All-day Microsoft events.** Graph keeps an all-day event's start and end as midnight in the event's own zone, and
under `Prefer: outlook.timezone="UTC"` sends it either as that midnight with "UTC" beside it or, for some mailboxes,
carried to UTC: an event of the 2nd made in Zurich comes as the 1st at 23:00. Its date was taken, a day early east
of UTC. A midnight is the day it says; anything else is turned back into the zone the event was made in
(`originalStartTimeZone`), else the nearest midnight (`MicrosoftReader.Day`).

## When it reads

*"sync all the time … regardless of whether any widget is placed"* (2026-10-09), and *"all the items
from the accounts should always be synced even if someone does not have the contacts widget placed"*
(2026-10-29). Everything an account gives — calendars, mail, contacts, To Do's tasks — is synced from
the dock's start for as long as it runs: the accounts (accounts.json) decide, not the config
(`Karpo.Dock\Pim\PimSync.cs`, called once from `Dock.Create`, never on an isolated run). So a widget
placed later, a folder opened, or a start without a connection has the events, the messages and the
people at once. Each kind has one store, and the store is the one sync of every account for it: no
widget and no config source starts a sync of its own (`SourceKinds` gives the accounts' widgets no
source); a widget reads the store's memory and at most asks it to start what is already due.

| Store | First read | Then |
| --- | --- | --- |
| Calendar | the dock's start, or the interval after the read kept | at the online interval; at once when the accounts change |
| Contacts | the dock's start, or the interval after the read kept | at the online interval; at once when the accounts change (a book turned off or on among them) |
| Mail | the dock's start, or the interval after the read kept | at the online interval; when the panel opens and the last read is a minute old |
| Tasks | the dock's start, or the interval after the read kept (changes kept are sent at the start) | at the online interval; when a line showing a list opens and the last try is a minute old |

**Cheap when there is nothing to sync.** With no account that gives calendars (or mail, or contacts), the store's
first pass asks nobody and its timer stops; an account added wakes it at once (`OnAccounts`). An
account removed stops at that read: its sync goes, nothing asks it again, and the cache is written
again without it. The intervals and the minute's rest after a failure are the same as before.

The online interval is the one every online widget follows (General → Online, 15 minutes unless
set; `docs\widgets.md` §37). A change to it re-arms every store's timer at once.

**Each account is its own sync.** The calendars, the mail, the contacts and the tasks keep one sync per
account (`AccountCalendars.Sync`, `MailStore.Sync`, `ContactStore.Sync`, `TaskStore.Sync`): read side by
side, each due at the interval after its own last good read, a minute after its own failure,
and at once when the accounts change or somebody presses Refresh. All run from
the dock's start (`PimSync`). A failure rests for a minute, so a widget redrawn on
every frame of a hover asks once. One account failing leaves the others'
calendars, people and messages on screen, and its own as they were last read.

**A restart is no reason to read** (*"make sure that the caches persist restarts and stuff. i dont
want the application to fetch the data just because there was a restart."*, 2026-10-31). Each store's
first read takes its file in first (below, "Offline"); an account kept there less than the interval
ago counts as read then (`PimCache.KeptRead`): it is due the interval after that read, as it would have
been had the dock run on, so the dock's start, a sign-in and a wake read only what is due. One kept the
interval ago or longer, missing from the file, kept for another window (the calendars: another month,
or a calendar not all there), or with a book that failed alone (the contacts) is read at once. The
person's Refresh, an account added or signed in again, and the network coming back after a failure
read at once as ever; To Do's changes kept on disk are sent at the start, and a pass that only sends
reads no list.

## Offline

*"the event should by synchronized in the background for all the widgets, the same for the
emails … so that you can also show events and emails without internet connection. if the refresh
was not possible just show an indicator."* (2026-10-09)

**What is kept.** `Karpo.Dock\Pim\PimCache.cs`, in `Config.LocalFolder`
(`%LOCALAPPDATA%\Karpo`; an isolated run has no cache at all, `PimCache.ForDock()`):

| File | Per account | Bounds |
| --- | --- | --- |
| `calendar-cache.json` | a Microsoft or Google account's id, when it was read, the window it holds all of, each calendar's id, name and colour, and each occurrence's title, place, start, end, all day, floating | the window the calendars are read for, last month to a year after this one (`PimCache.Window`), at most 2000 a calendar |
| `calendar-mirror\<account id>\<hash>.bin` | a CalDAV calendar whole (§616): its id, name, colour and place in the account's listing, when it was synced, its sync token and tag, whether it is whole and when it was last compared whole, and each object's href, etag and iCalendar as kept (`KeptICalendar`: title, place, times, series, zones, the meeting's link) | every object of it, any year, at most 50,000 |
| `mail-cache.json` | its id, when it was read, the unread count, and each message's sender, sender's address, subject, time and unread; the same of each folder it syncs (widgets.md §639), by the folder's id, with its name | the newest 30 (`MailStore.Newest`) |
| `contacts-cache.json` | its id, when it was read, each address book's id and name (off, failed alone, not all there), and each person's id, name, organisation, addresses and numbers | what the read gave |

Only what the rows draw: no password, no address, no message id, no preview, no description. The
caches hold no token and no server; a CalDAV mirror holds its calendar's sync token, tag and each
object's href and etag, because without them the next sync would download the calendar again whole.
A CalDAV calendar is not expanded into the cache: its objects are kept, and expanded in whatever
zone it is shown in, offline too. The caches are written on the pool after every read that brought
something new or lost an account (temp file, then moved over); an account that is gone is dropped
then and never shown from the file.

**The CalDAV mirror** (`Karpo.Dock\Pim\CalendarMirror.cs`, §616). One file per calendar: a JSON header, the
objects as JSON deflated (ZLibStream, fastest), the whole DPAPI-encrypted with its own entropy
(`Karpo.CalendarMirror.v1`) and written the durable way with a `.bak`; the token and the objects are one file, so a
crash never leaves a token that says more than the objects hold. The file's name is a hash of the calendar's id. In
memory the objects stay deflated beside their etags and the parsed `EventIndex` (about 1 MB on disk and 8 MB in
memory for 10,000 events). A sync that changed nothing writes only the header anew (its time); one that changed
something writes the file again. A first download is written and shown every 1,000 objects, so the events come in
steps and a download cut off resumes with what it had. A file another program holds is written at the next sync; one
that does not read is its `.bak`, else deleted and the calendar downloaded again whole; one of another
`KeptICalendar.Version` likewise. Gone with its calendar turned off or no longer listed, and with its account: at
once, and at the next start for an account removed while the dock was not running (never while accounts.json could
not be read). An isolated run keeps its calendars in memory only. Nothing of a file is ever logged; a sync that
brought something logs how much: `Karpo: CalDAV sync: 1 calendars, 12 objects in, 1 out, 0 unchanged, 10234 held, 840 ms`.

**Encrypted for this Windows user** (*"encrypt the cache for the Windows user"*, 2026-10-09). The
JSON goes through DPAPI, `CurrentUser` scope with the cache's own entropy (`Karpo.PimCache.v1`;
the secrets in accounts.json have theirs), through `System.Security.Cryptography.ProtectedData` —
the package `Secrets.cs` already uses, which is a thin P/Invoke of `CryptProtectData` and
publishes under native AOT. The files hold the DPAPI blob and nothing else: another user, another
PC or a copy carried off reads nothing, and no title, subject or sender is on disk in the clear. A
file that does not decrypt or does not parse — another user's, damaged, cut short, someone else's
blob, or the plain JSON 40b5622 wrote — is no cache, and the next good read writes over it. The
plain bytes are wiped after each read and write, and nothing logged says what a file holds (only
the kind of trouble).

**What is shown.** The views only ever read the stores' memory. The first read of a run first takes
the file in, on the pool (`TakeCache`), so a start without a connection shows the kept events and
messages at once. An account whose read fails keeps what it gave last — from this run or the file —
and the store calls itself **stale** (`Freshness`: the oldest such read, and `Stale`); `Problem` is
then null, because nothing is missing. An account that fails with nothing kept is `Problem`, as
before: a first start without a connection says "… could not be reached" where the events or the
mail would be. The next good read clears the stale state.

**The stale mark** (`Karpo.Dock\Islands\StaleMark.cs`): Segoe's SyncError glyph in the secondary ink,
and the tooltip "Last updated 14:05 — couldn't refresh" (`Freshness.Tip`; with the day before the
time when it was not today). On a list line it stands at the trailing gutter (the row of a one-row
line, the top of a taller one), with the line's content kept clear of it and its tooltip over it
(`CardTip`) — on a line with arrows at its trailing end (the month, the day, week and year views)
just before them, the title giving up the room; on the mail face's envelope it is small on the
glyph's lower corner, with no tooltip over it — no face on the bar has one (docs\widgets.md §367) —
and a screen reader hears from when (`MailIsland.Tip`). Neither the calendar nor the mail has a flyout any more
(docs\widgets.md §80, §95): their lines carry the mark.

**For a new widget that shows events or mail** (as the calendar's lines do, `CalendarLineTile`):
read `CalendarStore.Shared` — `EventsIn`, `EventsOn`, `Upcoming`, `Loading`, `Problem`, `HasCalendars`,
`NoAccount`, and `Freshness` — or `MailStore.Shared` (`Messages`, `Unread`, `Loading`, `Problem`,
`Freshness`), never the file and never an account. Every one of these answers from memory at once.
Call `CalendarStore.Open()` on the first look (it starts nothing that is not due), `Refresh()` for a
person's Refresh; attach for news (`Attach`/`Detach`, or `Changed`). Show the stale state with
`StaleMark.Room` / `StaleMark.Sketch` in the line, and `StaleMark.Tip` with `StaleTip` in `CardTip`. The stores run from the dock's start whatever the config holds (`PimSync`); a new kind
needs no source of its own to keep them reading.

**Tests:** `Karpo.Tests\Pim\CalendarMirrorTests.cs` and `CalDavStoreTests.cs` (§616: the mirror's encrypted round
trip, its .bak, version and held file; every year offline with no read, a restart reading nothing, a change reaching
the file, a calendar off and on, an account removed, a first download resumed); `Karpo.Tests\Pim\OfflineCacheTests.cs` (round trip, bounds and cap,
stale on failure and recovery, one inbox failing, a first start offline, views never waiting, a
broken file, nothing of the sign-in in the files, the encrypted round trip with no plain text in the
bytes, a plain-JSON file of before refused and replaced, a tampered or other blob refused);
`PimSyncTests` (synced from the start with no widget, a store with no account resting, an account
added read at once, an account removed not read again and dropped from the files);
`StaleMarkPictures` (stale-mail-lines.png, stale-calendar-lines.png, stale-envelope-*.png, and the
calendar preset's lines and the views: stale-<events|month|dayview|weekview|yearview>-lines.png).

**`--smoke` and the tests reach no account, by construction.** Only
`AccountSet.Shared` reads accounts.json, and only when `Config.IsIsolated` is
false; an isolated run (KARPO_CONFIG_DIR) gets a set with nothing in it. Tests
that want accounts give a store an `AccountSet` of fake readers
(`Karpo.Tests\Pim\FakeReader.cs`).

## The feed that went

A calendar widget once carried an iCalendar address that Karpo downloaded
itself. It is still in the settings file and still never read: the widget's
settings page says so, offers to copy it and has a button that clears it, which
only the person presses (`CalendarOptions.FeedUrl`).
