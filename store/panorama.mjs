// The listing's screenshots as one long picture, cut into the Store's pages (compose.mjs): side by side in
// the Store's row they are one scene. Four parts, in the order somebody meets Karpo: what you see (the day on the
// taskbar, the bar's buttons), what a click opens (its folders, this PC), what it reads (the accounts), and how it
// becomes yours (a dashboard, the widgets, the edge, the look). One bar on every page (config.json's `work`), and from
// page to page other widgets: a kind is shown once where it can be. The wall runs over the cuts into the pages beside
// it; words and monitors never do.
//
// A frame is one page, 1920 x 1080 px, in the order of listing/<lang>.json's screenshots (its shot the same):
//   words  where its label, headline and sub stand: a band across the top (center) or a column (x, w), its
//          middle at the page's
//   cards  pieces of captures (3072 x 1728 px): crop in the capture, x and y of the card in the page's px (less than
//          0 or past 1920 is in the page before or after), w its width (its height follows the crop), r a turn in
//          degrees, z over or under its neighbours, bubbles its callouts (at: the point in the capture; dx, dy: the
//          bubble from it in page px), worded by the listing's callouts in their order from the card's first (0 where
//          it says none). A screen is a whole capture in a monitor, 16:9 as every screen here is, the whole monitor and
//          its stand on its page (monitorBox): x, y and w its screen's box, its height following; cuts the captures
//          sharing it in strips, left to right, each cut leaning tilt of its width. A lens is a card enlarging a piece
//          of the screen it names (lens: that card's place among the frame's cards), marked there, light widening to it
//   wall   lists of cards side by side, each w wide and gap apart from x, y (a list's dy lower), the whole turned r
//          degrees about the page's middle, fading in between fade's two heights (under the headline): the widgets,
//          more than a page holds
//   lanes  what Karpo reads from the accounts, a lane each, w wide, h tall and gap apart from x, y, pad inside: what it
//          gives (the callout of that number), that as Karpo shows it (a piece of a capture), and the services it comes
//          from, each a name and its logo (compose.mjs's logos)
/** A monitor's parts round a screen w px wide: its thin bezel all round, the neck and the foot of its stand. */
export function monitorParts(w) {
  return {
    bezel: Math.round(w * 0.0075),
    neck: { w: Math.round(w * 0.1), h: Math.round(w * 0.05) },
    foot: { w: Math.round(w * 0.28), h: Math.round(w * 0.012) },
  };
}

/** Where a screen card's whole monitor stands in its page, the stand's foot included. */
export function monitorBox(card) {
  const { bezel, neck, foot } = monitorParts(card.w);
  const h = Math.round((card.w * 9) / 16);
  return { left: card.x - bezel, top: card.y - bezel, right: card.x + card.w + bezel, bottom: card.y + h + bezel + neck.h + foot.h };
}

// Where things are in the captures (3072 x 1728 px). The bar's buttons, their middles along it (the folders' the gap
// between its two dashboards):
const bar = { apps: 266, mail: 590, meeting: 946, pc: 2003, weather: 2261, folders: 2497, quick: 2679, day: 2911 };
// A folder's list is 720 px wide; a dashboard's four start at 132, 864, 1596 and 2328, all 374 to 1574 down. The
// variety folder's start at 24.
const list = (n) => 132 + n * 732;
const other = (n) => 24 + n * 732;
const piece = (x, y, h, w = 720) => ({ x, y, w, h });
const pc = piece(1277, 494, 1080, 1452);
const meetings = piece(586, 1174, 400);
const quick = piece(2319, 1054, 520);
// Every band page's monitor: the same place, the same size.
const screen = { x: 320, y: 248, w: 1280 };

export const frames = [
  {
    shot: "day",
    words: { band: true },
    cards: [{ shot: "day", screen: true, ...screen }],
  },
  {
    shot: "tour",
    words: { band: true },
    cards: [
      { shot: "bar", screen: true, ...screen },
      // The bar's two ends enlarged, each over its place on the screen.
      {
        shot: "bar",
        lens: 0,
        crop: { x: 0, y: 1572, w: 1250, h: 156 },
        x: 250,
        y: 400,
        w: 940,
        z: 3,
        bubbles: [{ at: [bar.apps, 1596], dy: -62 }, { at: [bar.mail, 1596], dy: -62 }, { at: [bar.meeting, 1596], dy: -62 }],
      },
      {
        shot: "bar",
        lens: 0,
        crop: { x: 1860, y: 1572, w: 1212, h: 156 },
        x: 750,
        y: 690,
        w: 911,
        z: 3,
        first: 3,
        bubbles: [
          { at: [bar.pc, 1596], dx: -50, dy: -62 },
          { at: [bar.weather, 1716], dx: -80, dy: 56 },
          { at: [bar.folders, 1596], dy: -62 },
          { at: [bar.quick, 1716], dx: 70, dy: 56 },
          { at: [bar.day, 1596], dx: 40, dy: -62 },
        ],
      },
    ],
  },
  {
    shot: "clicks",
    words: { band: true },
    cards: [
      // The news and the sea from the second dashboard, the next meetings, quick settings.
      { shot: "more", crop: piece(list(1), 374, 900), x: 110, y: 290, w: 430, r: -5, z: 2 },
      { shot: "meeting", crop: meetings, x: 420, y: 760, w: 470, r: 3, z: 4 },
      { shot: "more", crop: piece(list(0), 374, 1200), x: 760, y: 250, w: 420, r: 2, z: 3 },
      { shot: "quick", crop: quick, x: 1290, y: 300, w: 470, r: 5, z: 2 },
      { shot: "more", crop: piece(list(3), 374, 720), x: 1260, y: 660, w: 440, r: -3, z: 4 },
    ],
  },
  {
    shot: "files",
    words: { band: true },
    cards: [
      {
        shot: "pc",
        screen: true,
        ...screen,
        bubbles: [
          { at: [pc.x + 24, pc.y + 60], dx: -200, dy: -30 },
          { at: [pc.x + 24, pc.y + 330], dx: -200, dy: 0 },
          { at: [pc.x + 24, pc.y + 810], dx: -200, dy: 0 },
          { at: [pc.x + 1092, pc.y + 20], dx: 0, dy: -70 },
        ],
      },
    ],
  },
  {
    shot: "connect",
    words: { band: true },
    cards: [],
    lanes: {
      x: 70,
      y: 250,
      w: 340,
      h: 790,
      gap: 20,
      pad: 16,
      lanes: [
        { gives: 0, shot: "variety", crop: piece(other(0), 414, 960), from: [["Microsoft 365", "microsoft"], ["Outlook.com", "microsoftoutlook"], ["iCloud", "icloud"], ["CalDAV", "calendar"]] },
        { gives: 1, shot: "variety", crop: piece(other(3), 694, 600), from: [["Microsoft 365", "microsoft"], ["Outlook.com", "microsoftoutlook"], ["iCloud", "icloud"], ["CardDAV", "contacts"]] },
        { gives: 2, shot: "day", crop: piece(2328, 334, 760), from: [["Microsoft 365", "microsoft"], ["Outlook.com", "microsoftoutlook"], ["Gmail", "gmail"], ["iCloud", "icloud"], ["IMAP", "mail"]] },
        { gives: 3, shot: "day", crop: piece(2328, 1094, 480), from: [["Microsoft 365", "microsoft"], ["Outlook.com", "microsoftoutlook"]] },
        { gives: 4, shot: "meeting", crop: meetings, from: [["Teams", "microsoftteams"], ["Zoom", "zoom"], ["Google Meet", "googlemeet"], ["Webex", "webex"]] },
      ],
    },
  },
  {
    shot: "folders",
    words: { band: true },
    cards: [
      {
        shot: "folders",
        screen: true,
        ...screen,
        bubbles: [
          { at: [list(0) + 360, 420], dy: -75 },
          { at: [list(1) + 360, 420], dy: -125 },
          { at: [list(2) + 360, 420], dy: -75 },
          { at: [list(3) + 360, 420], dy: -125 },
          { at: [3040, 1350], dx: 108, dy: 0 },
        ],
      },
    ],
  },
  {
    shot: "widgets",
    words: { band: true },
    cards: [],
    // Five lists of widgets the other pages don't show, tilted, running on into the pages beside.
    wall: {
      x: -90,
      y: 110,
      w: 400,
      gap: 20,
      r: -5,
      fade: [200, 300],
      lists: [
        {
          dy: 60,
          cards: [
            { shot: "variety", crop: piece(other(0), 414, 960) },
            { shot: "variety", crop: piece(other(3), 1294, 280) },
            { shot: "day", crop: piece(2328, 1094, 480) },
            { shot: "variety", crop: piece(other(2), 1214, 360) },
          ],
        },
        {
          dy: 0,
          cards: [
            { shot: "variety", crop: piece(other(1), 494, 560) },
            { shot: "variety", crop: piece(other(1), 1054, 520) },
            { shot: "pc", crop: piece(pc.x + 732, pc.y, 1080) },
          ],
        },
        {
          dy: 80,
          cards: [
            { shot: "variety", crop: piece(other(2), 534, 400) },
            { shot: "more", crop: piece(list(2), 374, 1200) },
            { shot: "folders", crop: piece(list(0), 1094, 480) },
          ],
        },
        {
          dy: 20,
          cards: [
            { shot: "variety", crop: piece(other(3), 694, 600) },
            { shot: "variety", crop: piece(other(2), 934, 280) },
            { shot: "pc", crop: piece(pc.x, pc.y, 1080) },
            { shot: "variety", crop: piece(other(0), 1374, 200) },
          ],
        },
        {
          dy: 100,
          cards: [
            { shot: "folders", crop: piece(list(2), 1094, 480) },
            { shot: "folders", crop: piece(list(1), 934, 640) },
            { shot: "quick", crop: quick },
            { shot: "meeting", crop: meetings },
          ],
        },
      ],
    },
  },
  {
    shot: "side",
    words: { band: true },
    cards: [{ shot: "side", screen: true, ...screen }],
  },
  {
    shot: "looks",
    words: { band: true },
    cards: [
      // One screen, the second dashboard open, cut into Windows' own look and three of Karpo's palettes.
      { shot: "more", screen: true, cuts: ["more", "more-paper", "more-harbor", "more-bloom"], tilt: 0.07, ...screen },
    ],
  },
];
