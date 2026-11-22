// The Microsoft Store's pictures, made from Karpo's real ones: every screenshot is a stage capture
// (store/shots.txt, artifacts/store/shots) cropped and set under its headline from store/listing/<lang>.json;
// the hero art and the icons are the logo's. Rendered by headless Chrome (or Edge) at the Store's sizes.
//
//   node store/compose.mjs              every language that has a listing
//   node store/compose.mjs en de        those languages
//
// Out: artifacts/store/images/<lang>/screenshot-<n>-<shot>.png (3840 x 2160), and once for all languages
// artifacts/store/images/hero-3840x2160.png, tile-300x300.png and pro-300x300.png, and the MSIX
// package's logos in artifacts/store/package/Images (`node store/compose.mjs --package`). A language's own
// captures (artifacts/store/shots/<lang>/<shot>.png) stand in for the English ones where there are any.

import { execFileSync } from "node:child_process";
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { frames, monitorParts } from "./panorama.mjs";

const repo = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const shots = join(repo, "artifacts", "store", "shots");
const out = join(repo, "artifacts", "store", "images");
const work = join(repo, "artifacts", "store", "compose");
const logo = pathToFileURL(join(repo, "assets", "logo", "karpo.svg")).href;

// The listing's ten are one panorama (panorama.mjs, panoramaHtml). The concepts (concepts.json) each have a layout of
// their own, and show this of their captures (3072 x 1728 px, the bar along the bottom):
//   top    the words over the screenshot, which fills the page's width and runs off its bottom edge
//   left   the words in a column on the left, the screenshot running off the page's right and bottom edges;
//   right  the other way round
//   stack  the words over the bars of several captures (strips), one card each, one under the other
//   grid   the words over four captures' same corner (cells), two by two
//   bento  the words over many widgets, each cut from a capture (tiles: its box in the capture, its slot on the page)
//   callouts  the words over the screenshot, bubbles naming its parts (at: the point in the capture; dx, dy: the bubble)
//   scatter   the words over cards cut from captures and strewn over the page, each turned a little (items)
// left and right take bubbles too. Concepts (concepts.json) have more: duo, triptych, pair, immersive, lens, tall,
// split, timeline, manifesto.
// focus is what must be seen (x, y, w; down to the bottom): the crop grows round it to the shape of the room.
// The page is 1920 x 1080 CSS px at twice the pixels: 3840 x 2160, the Store's 4K.
const page = { w: 1920, h: 1080 };

// Fonts with each script's letters, after Segoe UI Variable's Latin, Greek and Cyrillic.
const scriptFonts = {
  ja: '"Yu Gothic UI"',
  "zh-Hans": '"Microsoft YaHei UI"',
  "zh-Hant": '"Microsoft JhengHei UI"',
  ko: '"Malgun Gothic"',
  hi: '"Nirmala UI"',
  mr: '"Nirmala UI"',
  bn: '"Nirmala UI"',
  te: '"Nirmala UI"',
  ta: '"Nirmala UI"',
  th: '"Leelawadee UI"',
};
const rightToLeft = new Set(["ar", "fa", "ur"]);

function browser() {
  const candidates = [
    join(process.env["ProgramFiles"] ?? "", "Google", "Chrome", "Application", "chrome.exe"),
    join(process.env["ProgramFiles(x86)"] ?? "", "Microsoft", "Edge", "Application", "msedge.exe"),
    join(process.env["ProgramFiles"] ?? "", "Microsoft", "Edge", "Application", "msedge.exe"),
  ];
  const found = candidates.find((c) => existsSync(c));
  if (!found) throw new Error("compose: neither Chrome nor Edge is installed");
  return found;
}

const chrome = browser();

/** Renders html at width x height CSS px, scale times its pixels, to png. */
function render(name, html, png, width, height, scale, clipW, clipH) {
  mkdirSync(work, { recursive: true });
  mkdirSync(dirname(png), { recursive: true });
  const file = join(work, name + ".html");
  writeFileSync(file, html);
  execFileSync(chrome, [
    "--headless=new",
    "--disable-gpu",
    "--hide-scrollbars",
    "--default-background-color=00000000",
    `--force-device-scale-factor=${scale}`,
    `--window-size=${width},${height}`,
    `--screenshot=${png}`,
    "--virtual-time-budget=4000",
    pathToFileURL(file).href,
  ], { stdio: "ignore" });
  if (!existsSync(png)) throw new Error(`compose: ${png} was not made`);
  // Chrome's window has a least size: a smaller picture is cut from the top left of a larger one.
  if (clipW) execFileSync("powershell", ["-NoProfile", "-Command", crop(png, clipW, clipH)], { stdio: "ignore" });
  console.log(`  ${png}`);
}

/** A PowerShell line that cuts png to its top-left w x h, keeping its transparency. */
function crop(png, w, h) {
  const p = png.replace(/'/g, "''");
  return `Add-Type -AssemblyName System.Drawing; $b = [Drawing.Bitmap]::FromFile('${p}'); $c = $b.Clone((New-Object Drawing.Rectangle 0, 0, ${w}, ${h}), $b.PixelFormat); $b.Dispose(); $c.Save('${p}', [Drawing.Imaging.ImageFormat]::Png); $c.Dispose()`;
}

const escape = (s) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");

// Karpo's teal (assets/logo/karpo.svg's #0AA39E): every colour of the pages that is not a capture's, a service's, white or
// the monitors' metal. Lights as [r, g, b], for rgba().
const teal = {
  ground: ["#062A2C", "#03171A"], // the night under it all, top to bottom
  fill: "#08827D", // bubbles, check marks, the gives tiles: white words on it at 4.7 : 1
  line: [127, 220, 212], // #7FDCD4 the label, leads, the thread, rays, what a lens marks
  dot: [69, 211, 204], // #45D3CC a lead's dot, a flow's head
  mark: [10, 163, 158], // #0AA39E the mark's own, as light
  sea: [22, 168, 128], // a greener light
  cyan: [0, 190, 178], // a bluer one, still green of cyan
  deep: [0, 112, 108], // a darker one
  words: [222, 242, 240], // the subline's and a tag's white, a little teal
  shade: [1, 18, 20], // shadows and veils, of the ground
  lane: ["#0C3A3C", "#062226"], // an account's lane, top to bottom
};
const rgba = (rgb, a) => `rgba(${rgb.join(", ")}, ${a})`;
const hex = (rgb) => "#" + rgb.map((c) => c.toString(16).padStart(2, "0")).join("").toUpperCase();
const night = `linear-gradient(180deg, ${teal.ground[0]} 0%, ${teal.ground[1]} 100%)`;

const background = `
  background:
    radial-gradient(1100px 620px at 50% -8%, ${rgba(teal.mark, 0.5)}, ${rgba(teal.mark, 0)} 70%),
    radial-gradient(900px 700px at 8% 110%, ${rgba(teal.deep, 0.5)}, ${rgba(teal.deep, 0)} 70%),
    radial-gradient(900px 700px at 96% 105%, ${rgba(teal.sea, 0.3)}, ${rgba(teal.sea, 0)} 70%),
    ${night};`;

// The panorama's light: blobs of colour standing over the cuts, half on each page, over the night.
const blobs = [
  { x: 0, y: -60, r: [1100, 620], rgb: teal.mark, a: 0.5 },
  { x: 1920, y: 1140, r: [1000, 720], rgb: teal.sea, a: 0.38 },
  { x: 3840, y: -80, r: [1100, 640], rgb: teal.cyan, a: 0.42 },
  { x: 5760, y: 1140, r: [1000, 720], rgb: teal.deep, a: 0.5 },
  { x: 7680, y: -60, r: [1100, 620], rgb: teal.sea, a: 0.42 },
  { x: 9600, y: 1140, r: [1000, 720], rgb: teal.mark, a: 0.42 },
  { x: 11520, y: -80, r: [1100, 640], rgb: teal.cyan, a: 0.42 },
  { x: 13440, y: 1140, r: [1000, 720], rgb: teal.sea, a: 0.38 },
  { x: 15360, y: -60, r: [1100, 620], rgb: teal.deep, a: 0.55 },
  { x: 17280, y: 1140, r: [1000, 720], rgb: teal.cyan, a: 0.38 },
  { x: 19200, y: -60, r: [1100, 620], rgb: teal.mark, a: 0.5 },
];

// And a line through all of it, a slow wave low on the pages, under the cards.
const thread = (x) => 990 + 46 * Math.sin((2 * Math.PI * x) / 2560);

/**
 * The listing's screenshots as one picture (panorama.mjs), page n of it: the whole panorama laid out, moved n pages
 * to the left, so what crosses a cut is the same on both pages.
 */
function panoramaHtml(lang, listing, dir, n) {
  const at = (name) => (existsSync(join(dir, name + ".png")) ? join(dir, name + ".png") : join(shots, name + ".png"));
  const width = page.w * frames.length;
  const light = blobs
    .map((b) => `radial-gradient(${b.r[0]}px ${b.r[1]}px at ${b.x}px ${b.y}px, ${rgba(b.rgb, b.a)}, ${rgba(b.rgb, 0)} 70%)`)
    .join(", ");
  const points = [];
  // Only what the page shows is drawn: itself and what runs over from the pages beside it.
  for (let x = (n - 1) * page.w; x <= (n + 2) * page.w; x += 20) points.push(`${x},${thread(x).toFixed(1)}`);
  const content = frames
    .map((frame, i) => {
      if (Math.abs(i - n) > 1) return "";
      const s = listing.screenshots[i];
      const left = i * page.w;
      const worded = (bubbles, first = 0) => bubbles.map((b, k) => ({ ...b, text: s.callouts[first + k] }));
      const cards = frame.cards
        .map((c) => {
          if (c.screen) {
            const box = { x: left + c.x, y: c.y, w: c.w, h: Math.round((c.w * 9) / 16) };
            const bubbles = c.bubbles ? bubblesHtml(worded(c.bubbles), whole, box) : "";
            return `<div class="layer" style="z-index:${c.z ?? 1};">${monitor((c.cuts ?? [c.shot]).map(at), box, c.tilt ?? 0)}${bubbles}</div>`;
          }
          const box = { x: left + c.x, y: c.y, w: c.w, h: Math.round((c.w * c.crop.h) / c.crop.w) };
          const turn = c.r ? `transform: rotate(${c.r}deg);` : "";
          const bubbles = c.bubbles ? bubblesHtml(worded(c.bubbles, c.first), c.crop, box) : "";
          const screen = c.lens === undefined ? null : frame.cards[c.lens];
          const rays = screen ? raysHtml(c.crop, { x: left + screen.x, y: screen.y, w: screen.w }, box) : "";
          return `<div class="layer" style="z-index:${c.z ?? 1};">${rays}${card(at(c.shot), c.crop, box, c.r ? "18px" : c.lens === undefined ? "24px" : "14px", false, null, turn)}${bubbles}</div>`;
        })
        .join("");
      const wall = frame.wall ? wallHtml(frame.wall, left, at) : "";
      const accounts = frame.lanes ? lanesHtml(frame.lanes, left, s.callouts, at) : "";
      const w = frame.words;
      const place = w.band
        ? `left:${left + 160}px; width:${page.w - 320}px; top:44px; text-align:center;`
        : `left:${left + w.x}px; width:${w.w}px; top:0; height:${page.h}px; display:flex; flex-direction:column; justify-content:center; text-align:start;`;
      return `${wall}${cards}${accounts}<div class="text ${w.band ? "band" : "column"}" style="${place}">
  <div class="label"><img src="${logo}" alt="">${escape(s.label)}</div>
  <h1>${escape(s.headline)}</h1>
  <p>${escape(s.sub)}</p>
</div>`;
    })
    .join("\n");
  return `<!doctype html>
<html lang="${lang}" dir="${rightToLeft.has(lang) ? "rtl" : "ltr"}"><head><meta charset="utf-8"><style>
html, body { margin: 0; width: ${page.w}px; height: ${page.h}px; overflow: hidden; background: ${teal.ground[1]}; }
#world { position: absolute; left: ${-n * page.w}px; top: 0; width: ${width}px; height: ${page.h}px; direction: ltr;
  background: ${light}, ${night}; color: #fff; font-family: ${fonts(lang, "Display")}; -webkit-font-smoothing: antialiased; }
.layer { position: absolute; inset: 0; pointer-events: none; }
.text { position: absolute; z-index: 20; direction: ${rightToLeft.has(lang) ? "rtl" : "ltr"}; }
.label { display: inline-flex; align-items: center; gap: 12px; font-family: ${fonts(lang, "Text")}; font-weight: 600; font-size: 22px; color: ${hex(teal.line)}; letter-spacing: 0.01em; }
.column .label { font-size: 26px; }
.label img { width: 30px; height: 30px; }
.column .label img { width: 36px; height: 36px; }
h1, p { text-wrap: balance; word-break: auto-phrase; }
h1 { margin: 10px 0 0; font-weight: 600; font-size: 60px; line-height: 1.08; letter-spacing: -0.012em; }
.column h1 { margin-top: 22px; font-size: 70px; }
p { margin: 10px 0 0; font-family: ${fonts(lang, "Text")}; font-size: 26px; line-height: 1.35; color: ${rgba(teal.words, 0.74)}; }
.column p { margin-top: 24px; font-size: 30px; }
.card { position: absolute; overflow: hidden; background: #000; box-shadow: 0 0 0 1.5px rgba(255, 255, 255, 0.16), 0 30px 90px rgba(0, 0, 0, 0.6); }
.card img { position: absolute; }
.bubble { position: absolute; transform: translate(-50%, -50%); white-space: nowrap; padding: 10px 18px 12px; border-radius: 999px; background: ${teal.fill};
  color: #fff; font: 600 22px/1 ${fonts(lang, "Text")}; box-shadow: 0 8px 24px rgba(0, 0, 0, 0.45); z-index: 6; }
.lead { position: absolute; width: 2px; background: ${rgba(teal.line, 0.9)}; z-index: 5; }
.dot { position: absolute; width: 14px; height: 14px; border-radius: 50%; background: ${hex(teal.dot)}; box-shadow: 0 0 0 4px ${rgba(teal.dot, 0.35)}; z-index: 6; }
.thread { position: absolute; left: 0; top: 0; }
.monitor { position: absolute; background: #04050a;
  box-shadow: 0 0 0 1.5px rgba(214, 224, 240, 0.42), 0 0 0 3px ${rgba(teal.shade, 0.9)}, 0 0 90px ${rgba(teal.mark, 0.24)}, 0 46px 110px rgba(0, 0, 0, 0.62); }
.neck { position: absolute; background: linear-gradient(180deg, rgba(0, 0, 0, 0.55), rgba(0, 0, 0, 0) 45%), linear-gradient(90deg, #4d535f, #a4abb8 38%, #c6ccd6 50%, #a4abb8 62%, #4d535f); }
.foot { position: absolute; border-radius: 999px 999px 6px 6px / 14px 14px 6px 6px; background: linear-gradient(180deg, #d5dae2, #8d94a1 55%, #5a606c);
  box-shadow: inset 0 1px 0 rgba(255, 255, 255, 0.7); }
.floor { position: absolute; border-radius: 50%; background: radial-gradient(closest-side, rgba(0, 0, 0, 0.55), rgba(0, 0, 0, 0)); }
.screen { position: absolute; overflow: hidden; border-radius: 6px; background: #000; }
.screen img { position: absolute; left: 0; top: 0; width: 100%; height: 100%; }
.glass { position: absolute; inset: 0; background: linear-gradient(118deg, rgba(255, 255, 255, 0.07), rgba(255, 255, 255, 0) 26%); box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.05); }
.screen svg { position: absolute; left: 0; top: 0; }
.rays { position: absolute; }
.marked { position: absolute; border-radius: 6px; box-shadow: 0 0 0 2px ${hex(teal.line)}, 0 0 18px ${rgba(teal.dot, 0.7)}; }
.wall { position: absolute; top: 0; z-index: 2; }
.lane { position: absolute; z-index: 10; border-radius: 30px; background: linear-gradient(180deg, ${teal.lane[0]}, ${teal.lane[1]});
  box-shadow: 0 0 0 1.5px rgba(255, 255, 255, 0.14), 0 30px 80px rgba(0, 0, 0, 0.45); direction: ${rightToLeft.has(lang) ? "rtl" : "ltr"}; }
.lane .card { box-shadow: 0 0 0 1.5px rgba(255, 255, 255, 0.14), 0 16px 40px rgba(0, 0, 0, 0.5); }
.gives { position: absolute; top: 20px; height: 68px; display: flex; align-items: center; gap: 14px; font: 600 30px/1.08 ${fonts(lang, "Display")}; }
.gives i { flex: none; display: inline-flex; align-items: center; justify-content: center; width: 48px; height: 48px; border-radius: 14px; background: ${teal.fill};
  font: normal 26px/1 "Segoe Fluent Icons", "Segoe MDL2 Assets"; box-shadow: 0 6px 16px rgba(0, 0, 0, 0.35); }
.gives span { text-wrap: balance; }
.flow { position: absolute; width: 2px; border-radius: 1px; background: linear-gradient(180deg, ${hex(teal.line)}, ${rgba(teal.line, 0.15)}); }
.flow::before { content: ""; position: absolute; left: -6px; top: -4px; width: 14px; height: 14px; border-radius: 50%; background: ${hex(teal.dot)}; box-shadow: 0 0 0 4px ${rgba(teal.dot, 0.3)}; }
.sources { position: absolute; bottom: 26px; display: flex; flex-direction: column; gap: 12px; direction: ltr; }
.source { display: flex; align-items: center; gap: 14px; font: 600 25px/1 ${fonts("en", "Text")}; color: ${rgba(teal.words, 0.92)}; white-space: nowrap; }
.logo { display: inline-flex; align-items: center; justify-content: center; width: 44px; height: 44px; border-radius: 12px; flex: none;
  box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.18), 0 6px 16px rgba(0, 0, 0, 0.35); }
.logo.plain { background: #fff; }
.logo.glyph { background: rgba(255, 255, 255, 0.14); }
.logo.glyph i { font: normal 24px/1 "Segoe Fluent Icons", "Segoe MDL2 Assets"; }
</style></head><body><div id="world">
<svg class="thread" width="${width}" height="${page.h}" viewBox="0 0 ${width} ${page.h}">
  <defs><filter id="soft"><feGaussianBlur stdDeviation="6"/></filter></defs>
  <polyline points="${points.join(" ")}" fill="none" stroke="${rgba(teal.dot, 0.35)}" stroke-width="14" filter="url(#soft)"/>
  <polyline points="${points.join(" ")}" fill="none" stroke="${hex(teal.line)}" stroke-width="3" opacity="0.85"/>
</svg>
${content}
</div></body></html>`;
}

/** A whole capture, every screen the same 16:9. */
const whole = { x: 0, y: 0, w: 3072, h: 1728 };

/**
 * A whole screen in a monitor, its screen box: its bezel round it, its stand under it (panorama.mjs's monitorParts). Several captures (cuts) share the screen in strips side by side, each cut leaning tilt of the screen's
 * width, its top that much right of its foot: the same screen in each, so the strips are one screen restyled.
 */
function monitor(captures, box, tilt) {
  const { bezel, neck, foot } = monitorParts(box.w);
  const bottom = box.y + box.h + bezel;
  const n = captures.length;
  const lean = tilt * box.w;
  const edge = (k, top) => (k === 0 ? -lean - 4 : k === n ? box.w + lean + 4 : (k * box.w) / n + (top ? lean : -lean));
  const strips = captures
    .map((capture, k) => {
      const clip = n === 1 ? "" : `clip-path: polygon(${edge(k, true)}px 0, ${edge(k + 1, true)}px 0, ${edge(k + 1, false)}px 100%, ${edge(k, false)}px 100%);`;
      return `<img src="${pathToFileURL(capture).href}" alt="" style="${clip}">`;
    })
    .join("");
  const seams = n === 1 ? "" : `<svg width="${box.w}" height="${box.h}" viewBox="0 0 ${box.w} ${box.h}">${Array.from({ length: n - 1 }, (_, i) => i + 1)
    .map((k) => `<line x1="${edge(k, true)}" y1="0" x2="${edge(k, false)}" y2="${box.h}" stroke="rgba(0, 0, 0, 0.35)" stroke-width="8"/><line x1="${edge(k, true)}" y1="0" x2="${edge(k, false)}" y2="${box.h}" stroke="#fff" stroke-width="3"/>`)
    .join("")}</svg>`;
  const middle = box.x + box.w / 2;
  // The neck narrows upward a little, and the screen's shadow lies on its top.
  const taper = Math.round(neck.w * 0.08);
  return `<div class="floor" style="left:${middle - foot.w * 0.8}px; top:${bottom + neck.h + foot.h - 6}px; width:${foot.w * 1.6}px; height:${foot.h * 3}px;"></div>
    <div class="neck" style="left:${middle - neck.w / 2}px; top:${bottom - 2}px; width:${neck.w}px; height:${neck.h + 2}px; clip-path: polygon(${taper}px 0, ${neck.w - taper}px 0, 100% 100%, 0 100%);"></div>
    <div class="foot" style="left:${middle - foot.w / 2}px; top:${bottom + neck.h}px; width:${foot.w}px; height:${foot.h}px;"></div>
    <div class="monitor" style="left:${box.x - bezel}px; top:${box.y - bezel}px; width:${box.w + 2 * bezel}px; height:${box.h + 2 * bezel}px; border-radius:${bezel + 8}px;"></div>
    <div class="screen" style="left:${box.x}px; top:${box.y}px; width:${box.w}px; height:${box.h}px;">${strips}${seams}<div class="glass"></div></div>`;
}

/**
 * A lens: what a card enlarges of a screen (its crop), marked on the screen (its box on the page), and the light
 * widening from there to the card over or under it.
 */
function raysHtml(crop, screen, box) {
  const k = screen.w / whole.w;
  const from = { x: screen.x + crop.x * k, y: screen.y + crop.y * k, w: crop.w * k, h: crop.h * k };
  // Over or under what it enlarges, the light runs down or up; beside it, across.
  const beside = box.x + box.w <= from.x + 8 || box.x >= from.x + from.w - 8;
  let corners;
  let fades;
  if (beside) {
    const left = box.x < from.x;
    const [near, far] = left ? [from.x, box.x + box.w] : [from.x + from.w, box.x];
    corners = [[near, from.y], [near, from.y + from.h], [far, box.y + box.h], [far, box.y]];
    fades = left ? ["1", "0"] : ["0", "1"];
  } else {
    const over = box.y + box.h / 2 < from.y + from.h / 2;
    const [near, far] = over ? [from.y, box.y + box.h] : [from.y + from.h, box.y];
    corners = [[from.x, near], [from.x + from.w, near], [box.x + box.w, far], [box.x, far]];
    fades = over ? ["0", "1"] : ["1", "0"];
  }
  const x0 = Math.min(...corners.map((c) => c[0])) - 4;
  const y0 = Math.min(...corners.map((c) => c[1])) - 4;
  const w = Math.max(...corners.map((c) => c[0])) + 4 - x0;
  const h = Math.max(...corners.map((c) => c[1])) + 4 - y0;
  const points = corners.map(([x, y]) => `${(x - x0).toFixed(1)},${(y - y0).toFixed(1)}`).join(" ");
  // Bright at the card, faint at what it enlarges.
  const axis = beside ? `x1="${fades[0]}" y1="0" x2="${fades[1]}" y2="0"` : `x1="0" y1="${fades[0]}" x2="0" y2="${fades[1]}"`;
  return `<svg class="rays" style="left:${x0}px; top:${y0}px;" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">
      <defs><linearGradient id="ray" ${axis}><stop offset="0" stop-color="${rgba(teal.line, 0.3)}"/><stop offset="1" stop-color="${rgba(teal.line, 0.05)}"/></linearGradient></defs>
      <polygon points="${points}" fill="url(#ray)"/></svg>
    <div class="marked" style="left:${from.x - 3}px; top:${from.y - 3}px; width:${from.w + 6}px; height:${from.h + 6}px;"></div>`;
}

/**
 * A wall of widgets (panorama.mjs's wall): lists of cards side by side, each w wide and gap apart, the whole of it turned
 * r degrees about its page's middle and fading out under the headline. Its ends run on into the pages beside it.
 */
function wallHtml(wall, left, at) {
  const room = 600;
  const lists = wall.lists
    .map((list, i) => {
      let y = list.dy ?? 0;
      return list.cards
        .map((c) => {
          const box = { x: i * (wall.w + wall.gap), y, w: wall.w, h: Math.round((wall.w * c.crop.h) / c.crop.w) };
          y += box.h + wall.gap;
          return card(at(c.shot), c.crop, box, "18px");
        })
        .join("");
    })
    .join("");
  return `<div class="wall" style="left:${left - room}px; width:${page.w + 2 * room}px; height:${page.h}px;
      -webkit-mask-image: linear-gradient(to bottom, transparent ${wall.fade[0]}px, #000 ${wall.fade[1]}px);">
    <div style="position:absolute; left:${room + wall.x}px; top:${wall.y}px; transform-origin:${page.w / 2 - wall.x}px ${page.h / 2 - wall.y}px; transform:rotate(${wall.r}deg);">${lists}</div></div>`;
}

// What an account gives, by the glyph it is drawn with (Segoe Fluent Icons), in the order of the accounts page's callouts.
const giveGlyphs = ["", "", "", "", ""];

// The services' logos (store/logos, Simple Icons' SVGs), each white on its brand's colour; Microsoft's four squares in
// their own colours; a server with no logo of its own as what it gives, a Segoe Fluent Icons glyph.
const logos = {
  microsoft: { squares: ["#F25022", "#7FBA00", "#00A4EF", "#FFB900"] },
  microsoftoutlook: { colour: "#0078D4" },
  microsoftteams: { colour: "#5B5FC7" },
  gmail: { colour: "#EA4335" },
  icloud: { colour: "#3693F3" },
  googlemeet: { colour: "#00897B" },
  // Zoom's is its word: larger, to read as well as the others.
  zoom: { colour: "#0B5CFF", size: 38 },
  webex: { colour: "#00A0D1" },
  mail: { glyph: "" },
  calendar: { glyph: "" },
  contacts: { glyph: "" },
};

function logoHtml(key) {
  const logo = logos[key];
  if (logo.squares) {
    const [a, b, c, d] = logo.squares;
    return `<span class="logo plain"><svg viewBox="0 0 23 23" width="26" height="26"><rect width="11" height="11" fill="${a}"/><rect x="12" width="11" height="11" fill="${b}"/><rect y="12" width="11" height="11" fill="${c}"/><rect x="12" y="12" width="11" height="11" fill="${d}"/></svg></span>`;
  }
  if (logo.glyph) return `<span class="logo glyph"><i>${logo.glyph}</i></span>`;
  const svg = readFileSync(join(repo, "store", "logos", key + ".svg"), "utf8").replace(/<title>.*?<\/title>/, "").replace("<svg ", `<svg fill="#fff" width="${logo.size ?? 26}" height="${logo.size ?? 26}" `);
  return `<span class="logo" style="background:${logo.colour};">${svg}</span>`;
}

/**
 * What Karpo reads from the person's accounts (panorama.mjs's lanes): a lane each, side by side. From its top: what it
 * gives, in the listing's words (the callout its gives numbers); that as Karpo shows it, a piece of a capture; and the
 * services it comes from, logo and name.
 */
function lanesHtml(lanes, left, words, at) {
  const inner = lanes.w - 2 * lanes.pad;
  return lanes.lanes
    .map((lane, i) => {
      const x = left + lanes.x + i * (lanes.w + lanes.gap);
      const box = { x: lanes.pad, y: 104, w: inner, h: Math.round((inner * lane.crop.h) / lane.crop.w) };
      const from = lane.from.map(([name, logo]) => `<span class="source">${logoHtml(logo)}<span>${escape(name)}</span></span>`).join("");
      // The services stand on the lane's floor, 56 px each; a line runs from them up into what they give.
      const top = box.y + box.h + 16;
      const foot = lanes.h - 26 - lane.from.length * 56 - 4;
      const flow = foot - top > 40 ? `<div class="flow" style="left:${lanes.pad + 21}px; top:${top}px; height:${foot - top}px;"></div>` : "";
      return `<div class="lane" style="left:${x}px; top:${lanes.y}px; width:${lanes.w}px; height:${lanes.h}px;">
        <div class="gives" style="left:${lanes.pad}px; width:${inner}px;"><i>${giveGlyphs[lane.gives]}</i><span>${escape(words[lane.gives])}</span></div>
        ${card(at(lane.shot), lane.crop, box, "14px")}${flow}
        <div class="sources" style="left:${lanes.pad}px; width:${inner}px;">${from}</div></div>`;
    })
    .join("");
}

/** A piece of a capture in a card: its box on the page, the crop, the corners it rounds; cover fills the box, cut at its far sides. */
function card(capture, crop, box, radius, cover = false, chip = null, style = "") {
  const k = cover ? Math.max(box.w / crop.w, box.h / crop.h) : box.w / crop.w;
  return `<div class="card" style="left:${box.x}px; top:${box.y}px; width:${box.w}px; height:${box.h}px; border-radius:${radius}; ${style}">
    <img src="${pathToFileURL(capture).href}" alt="" style="left:${-crop.x * k}px; top:${-crop.y * k}px; width:${3072 * k}px; height:${1728 * k}px;">${chip ? `<div class="chip">${escape(chip)}</div>` : ""}</div>`;
}

/**
 * Callouts over a card showing crop in box: each bubble's words where its dx, dy (page px) put them from its point
 * (at, capture px), above it by default, every other one higher; a line from the bubble to a dot on the point.
 */
function bubblesHtml(bubbles, crop, box) {
  return (bubbles ?? [])
    .map((bubble, i) => {
      const point = onPage(bubble.at, crop, box);
      const at = { x: point.x + (bubble.dx ?? 0), y: point.y + (bubble.dy ?? (i % 2 === 0 ? -140 : -230)) };
      const length = Math.hypot(point.x - at.x, point.y - at.y);
      const angle = (Math.atan2(point.y - at.y, point.x - at.x) * 180) / Math.PI;
      return `<div class="lead" style="left:${at.x}px; top:${at.y - 1}px; width:${length}px; height:2px; transform-origin:0 50%; transform:rotate(${angle}deg);"></div>
        <div class="dot" style="left:${point.x - 7}px; top:${point.y - 7}px;"></div>
        <div class="bubble" style="left:${at.x}px; top:${at.y}px;">${escape(bubble.text)}</div>`;
    })
    .join("");
}

/** A capture's point (x, y px) on the page, for a card showing crop in box. */
const onPage = (point, crop, box) => {
  const k = box.w / crop.w;
  return { x: box.x + (point[0] - crop.x) * k, y: box.y + (point[1] - crop.y) * k };
};

/**
 * The crop of a capture of room's shape round focus (x, y, w, down to the bottom): as tall as the focus needs, as wide as
 * that makes it, about the focus's middle and inside the capture; wider than the capture, the whole width, cut at the top;
 * never so small that the page enlarges it past 1.6 times.
 */
function around(focus, room) {
  const aspect = room.w / room.h;
  let h = 1728 - focus.y;
  let w = h * aspect;
  if (w < focus.w) {
    w = focus.w;
    h = w / aspect;
  }
  // Never more than 1.6 times the capture's pixels on the 4K page (0.8 CSS px each): a smaller crop goes soft.
  if (room.w / w > 0.8) {
    w = room.w / 0.8;
    h = w / aspect;
  }
  if (w > 3072) {
    w = 3072;
    h = w / aspect;
  }
  h = Math.min(h, 1728);
  const x = Math.min(Math.max(0, focus.x + focus.w / 2 - w / 2), 3072 - w);
  return { x, y: 1728 - h, w, h };
}

/** The captures a frame of the panorama is made of. */
const uses = (frame) => [...new Set([...frame.cards.flatMap((c) => c.cuts ?? [c.shot]), ...(frame.wall?.lists ?? []).flatMap((l) => l.cards.map((c) => c.shot))])];

const fonts = (lang, family) => `${scriptFonts[lang] ? scriptFonts[lang] + ", " : ""}"Segoe UI Variable ${family}", "Segoe UI", sans-serif`;

function screenshotHtml(lang, s, capture, given) {
  const placed = given;
  // The callouts worded in the listing's language, where the picture places them.
  const picture = placed.bubbles ? { ...placed, bubbles: placed.bubbles.map((b, i) => ({ ...b, text: s.callouts?.[i] ?? b.text })) } : placed;
  const at = (name) => (existsSync(join(dirname(capture), name + ".png")) ? join(dirname(capture), name + ".png") : join(shots, name + ".png"));
  const side = ["left", "right", "tall", "manifesto"].includes(picture.layout);
  // The words: a column beside the screenshot, or a band over it.
  const words = side
    ? `left:${picture.layout === "right" ? 1400 : 70}px; width:${picture.layout === "tall" ? 620 : 460}px; top:0; bottom:0; display:flex; flex-direction:column; justify-content:center; text-align:start;`
    : "left:120px; right:120px; top:40px; text-align:center;";
  const under = 205;
  let cards = "";
  let glow;
  switch (picture.layout) {
    case "left":
    case "right": {
      // Off the page's far edge and its bottom, its one corner on the page rounded.
      const left = picture.layout === "left";
      const box = { x: left ? 580 : -40, y: 60, w: 1380, h: page.h - 20 };
      const crop = around(picture.focus, box);
      cards = card(capture, crop, box, left ? "24px 0 0 0" : "0 24px 0 0") + bubblesHtml(picture.bubbles, crop, box);
      glow = { x: box.x + 200, y: box.y + 80, w: box.w - 300, h: box.h };
      break;
    }
    case "stack": {
      const crop = { x: picture.strip.x ?? 0, y: picture.strip.y, w: picture.strip.w ?? 3072, h: 1728 - picture.strip.y };
      const w = 1800;
      const h = Math.round((crop.h * w) / crop.w);
      const gap = 24;
      const all = picture.strips.length * h + (picture.strips.length - 1) * gap;
      const top = Math.round(under + (page.h - under - all) / 2);
      cards = picture.strips.map((name, i) => card(at(name), crop, { x: (page.w - w) / 2, y: top + i * (h + gap), w, h }, "20px", false, picture.chips?.[i])).join("");
      glow = { x: 260, y: top + 40, w: page.w - 520, h: all - 80 };
      break;
    }
    case "grid": {
      // As wide as the page lets two be, or as the room under the words lets two rows be, whichever is less.
      const gap = 20;
      const h = Math.round(Math.min(((page.w - 120 - gap) / 2) * picture.cell.h / picture.cell.w, (page.h - under - gap - 30) / 2));
      const w = Math.round((h * picture.cell.w) / picture.cell.h);
      const left = (page.w - 2 * w - gap) / 2;
      const top = Math.round(under + (page.h - under - (2 * h + gap)) / 2);
      cards = picture.cells
        .map((name, i) => card(at(name), picture.cell, { x: left + (i % 2) * (w + gap), y: top + Math.floor(i / 2) * (h + gap), w, h }, "20px", false, picture.chips?.[i]))
        .join("");
      glow = { x: 260, y: top + 40, w: page.w - 520, h: 2 * h };
      break;
    }
    case "duo": {
      // Two displays side by side, each a whole capture, its chip under it.
      const gap = 56;
      const w = (page.w - 120 - gap) / 2;
      const h = page.h - under - 80;
      const top = under + 6;
      cards = picture.screens
        .map((screen, i) => card(at(screen.shot), screen.crop, { x: 60 + i * (w + gap), y: top, w, h }, "18px", true) +
          `<div class="tag" style="left:${60 + i * (w + gap)}px; width:${w}px; top:${top + h + 22}px;">${escape(screen.chip)}</div>`)
        .join("");
      glow = { x: 200, y: top + 40, w: page.w - 400, h };
      break;
    }
    case "triptych":
    case "pair": {
      // Cards side by side, as tall as the room, each as wide as its crop makes it; a chip under each where it has one.
      const gap = 36;
      const h = picture.items.some((item) => item.chip) ? page.h - under - 90 : page.h - under - 40;
      const widths = picture.items.map((item) => (h * item.crop.w) / item.crop.h);
      const all = widths.reduce((a, b) => a + b, 0) + gap * (picture.items.length - 1);
      let x = (page.w - all) / 2;
      cards = picture.items
        .map((item, i) => {
          const box = { x, y: under + 10, w: widths[i], h };
          x += widths[i] + gap;
          return card(at(item.shot), item.crop, box, "20px") +
            (item.chip ? `<div class="tag" style="left:${box.x}px; width:${box.w}px; top:${box.y + h + 20}px;">${escape(item.chip)}</div>` : "");
        })
        .join("");
      glow = { x: 200, y: under + 80, w: page.w - 400, h: h - 100 };
      break;
    }
    case "callouts": {
      // The screen across the page, bubbles over the wallpaper each pointing down at a part of the bar.
      const box = { x: 40, y: under, w: page.w - 80, h: page.h - under + 30 };
      const crop = around(picture.focus, { ...box, h: page.h - under });
      cards = card(capture, crop, box, "24px 24px 0 0") + bubblesHtml(picture.bubbles, crop, box);
      glow = { x: 240, y: under + 60, w: page.w - 480, h: page.h - under };
      break;
    }
    case "immersive": {
      // The screen is the page, the words over its wallpaper on a shade that fades away under them.
      const crop = around(picture.focus, { x: 0, y: 0, w: page.w, h: page.h });
      cards = card(capture, crop, { x: 0, y: 0, w: page.w, h: page.h }, "0", false, null, "box-shadow:none;") +
        `<div style="position:absolute; inset:0; background:linear-gradient(180deg, ${rgba(teal.shade, 0.82)} 0%, ${rgba(teal.shade, 0.35)} 30%, ${rgba(teal.shade, 0)} 48%);"></div>`;
      glow = { x: 0, y: 0, w: 0, h: 0 };
      break;
    }
    case "lens": {
      // The screen across the page, and a magnifier over one thing on it, a line to where it is.
      const box = { x: 40, y: under, w: page.w - 80, h: page.h - under + 30 };
      const crop = around(picture.focus, { ...box, h: page.h - under });
      cards = card(capture, crop, box, "24px 24px 0 0");
      const target = onPage(picture.lens.at, crop, box);
      const d = picture.lens.size ?? 420;
      const z = picture.lens.zoom ?? 0.8;
      const c = { x: picture.lens.center[0], y: picture.lens.center[1] };
      const dx = target.x - c.x;
      const dy = target.y - c.y;
      const length = Math.hypot(dx, dy) - d / 2;
      const angle = (Math.atan2(dy, dx) * 180) / Math.PI;
      cards += `<div class="lead" style="left:${c.x}px; top:${c.y}px; width:${length}px; height:3px; transform-origin:0 50%; transform:rotate(${angle}deg) translateX(${d / 2}px);"></div>
        <div class="dot" style="left:${target.x - 7}px; top:${target.y - 7}px;"></div>
        <div class="lens" style="left:${c.x - d / 2}px; top:${c.y - d / 2}px; width:${d}px; height:${d}px;">
          <img src="${pathToFileURL(capture).href}" alt="" style="position:absolute; left:${d / 2 - (picture.lens.view ?? picture.lens.at)[0] * z}px; top:${d / 2 - (picture.lens.view ?? picture.lens.at)[1] * z}px; width:${3072 * z}px; height:${1728 * z}px;"></div>`;
      glow = { x: 240, y: under + 60, w: page.w - 480, h: page.h - under };
      break;
    }
    case "tall": {
      // A portrait card on the right, as tall as the page lets it, beside a column of words.
      const h = page.h - 80;
      const w = Math.round((h * picture.crop.w) / picture.crop.h);
      const box = { x: Math.round(760 + (page.w - 760 - w) / 2), y: 40, w, h };
      cards = card(capture, picture.crop, box, "24px");
      glow = { x: box.x - 100, y: 100, w: w + 200, h: h - 100 };
      break;
    }
    case "split": {
      // One crop of two captures, the second over the first from a diagonal, each corner its chip.
      const box = { x: 40, y: under, w: page.w - 80, h: page.h - under + 30 };
      const crop = around(picture.focus, { ...box, h: page.h - under });
      const [a, b] = picture.halves;
      const top = { x: box.x + box.w * 0.58, y: box.y };
      const bottom = { x: box.x + box.w * 0.42, y: box.y + box.h };
      const length = Math.hypot(bottom.x - top.x, bottom.y - top.y);
      const angle = (Math.atan2(bottom.y - top.y, bottom.x - top.x) * 180) / Math.PI;
      cards = card(at(a.shot), crop, box, "24px 24px 0 0") +
        card(at(b.shot), crop, box, "24px 24px 0 0", false, null, "clip-path: polygon(58% 0, 100% 0, 100% 100%, 42% 100%); box-shadow:none;") +
        `<div style="position:absolute; z-index:6; left:${top.x}px; top:${top.y}px; width:${length}px; height:4px; background:#fff; transform-origin:0 50%; transform:rotate(${angle}deg); box-shadow:0 0 18px rgba(0,0,0,.6);"></div>
         <div class="chip" style="left:${box.x + 24}px; top:${box.y + 24}px;">${escape(a.chip)}</div>
         <div class="chip" style="left:auto; right:${page.w - box.x - box.w + 24}px; top:${box.y + 24}px;">${escape(b.chip)}</div>`;
      glow = { x: 240, y: under + 60, w: page.w - 480, h: page.h - under };
      break;
    }
    case "timeline": {
      // Moments of the day left to right, each a whole capture under its time, a line through the times.
      const gap = 40;
      const n = picture.moments.length;
      const w = (page.w - 120 - gap * (n - 1)) / n;
      const full = picture.crop ?? { x: 0, y: 0, w: 3072, h: 1728 };
      const h = Math.round((w * full.h) / full.w);
      const top = Math.round(under + 120 + (page.h - under - 120 - h) / 2);
      cards = `<div style="position:absolute; left:${60 + w / 2}px; width:${(n - 1) * (w + gap)}px; top:${top - 64}px; height:2px; background:${rgba(teal.line, 0.45)};"></div>` +
        picture.moments
          .map((moment, i) => {
            const x = 60 + i * (w + gap);
            return `<div class="time" style="left:${x}px; width:${w}px; top:${top - 118}px;">${escape(moment.time)}</div>
              <div class="dot" style="left:${x + w / 2 - 9}px; top:${top - 72}px; width:18px; height:18px;"></div>
              <div class="tag" style="left:${x}px; width:${w}px; top:${top - 44}px; margin:0;">${escape(moment.chip)}</div>` +
              card(at(moment.shot), full, { x, y: top, w, h }, "16px");
          })
          .join("");
      glow = { x: 200, y: top, w: page.w - 400, h };
      break;
    }
    case "manifesto": {
      // Big statements in the words' column, the screenshot running off the right and the bottom.
      const box = { x: 580, y: 60, w: 1380, h: page.h - 20 };
      cards = card(capture, around(picture.focus, box), box, "24px 0 0 0");
      glow = { x: box.x + 200, y: box.y + 80, w: box.w - 300, h: box.h };
      break;
    }
    case "scatter": {
      // Cards strewn over the page under the words, each turned a little, overlapping as on a table.
      cards = picture.items
        .map((item) => {
          const w = item.w;
          const h = Math.round((w * item.crop.h) / item.crop.w);
          return card(at(item.shot), item.crop, { x: item.x, y: item.y, w, h }, "18px", false, null, `transform: rotate(${item.r}deg); z-index:${item.z ?? 1};`);
        })
        .join("");
      glow = { x: 200, y: under + 60, w: page.w - 400, h: 700 };
      break;
    }
    case "bento": {
      // The slots' 875 px of height held to the room under the words, 16 px above the page's bottom.
      const area = { x: (page.w - 1789) / 2, y: under + 5 };
      const k = (page.h - 16 - area.y) / 875;
      cards = picture.tiles
        .map((tile) => card(at(tile.shot), tile.box,
          { x: area.x + tile.slot.x, y: Math.round(area.y + tile.slot.y * k), w: tile.slot.w, h: Math.round(tile.slot.h * k) }, "16px", true))
        .join("");
      glow = { x: 300, y: area.y + 60, w: page.w - 600, h: 760 };
      break;
    }
    default: {
      // The page's width, off its bottom edge, its top corners rounded.
      const box = { x: 40, y: under, w: page.w - 80, h: page.h - under + 30 };
      cards = card(capture, around(picture.focus, { ...box, h: page.h - under }), box, "24px 24px 0 0");
      glow = { x: 240, y: under + 60, w: page.w - 480, h: page.h - under };
    }
  }
  return `<!doctype html>
<html lang="${lang}" dir="${rightToLeft.has(lang) ? "rtl" : "ltr"}"><head><meta charset="utf-8"><style>
html, body { margin: 0; width: ${page.w}px; height: ${page.h}px; overflow: hidden; }
body { ${background} color: #fff; font-family: ${fonts(lang, "Display")}; -webkit-font-smoothing: antialiased; }
.text { position: absolute; z-index: 20; ${words} }
.label { display: inline-flex; align-items: center; gap: 12px; font-family: ${fonts(lang, "Text")}; font-weight: 600; font-size: ${side ? 26 : 22}px; color: ${hex(teal.line)}; letter-spacing: 0.01em; }
.label img { width: ${side ? 36 : 30}px; height: ${side ? 36 : 30}px; }
h1, p { text-wrap: balance; word-break: auto-phrase; }
h1 { margin: ${side ? 22 : 10}px 0 0; font-weight: 600; font-size: ${side ? 70 : 60}px; line-height: 1.08; letter-spacing: -0.012em; }
p { margin: ${side ? 24 : 10}px 0 0; font-family: ${fonts(lang, "Text")}; font-size: ${side ? 30 : 26}px; line-height: 1.35; color: ${rgba(teal.words, 0.74)}; }
.glow { position: absolute; left: ${glow.x}px; width: ${glow.w}px; top: ${glow.y}px; height: ${glow.h}px; background: ${rgba(teal.mark, 0.42)}; filter: blur(100px); border-radius: 50%; }
.card { position: absolute; overflow: hidden; background: #000; box-shadow: 0 0 0 1.5px rgba(255, 255, 255, 0.16), 0 30px 90px rgba(0, 0, 0, 0.6); }
.card img { position: absolute; }
.chip { position: absolute; left: 16px; top: 16px; padding: 8px 16px 9px; border-radius: 999px; background: ${rgba(teal.shade, 0.78)}; color: #fff;
  font: 600 20px/1 ${fonts(lang, "Text")}; box-shadow: 0 0 0 1px rgba(255, 255, 255, 0.18); backdrop-filter: blur(8px); z-index: 5; }
.tag { position: absolute; text-align: center; font: 600 24px/1.2 ${fonts(lang, "Text")}; color: ${rgba(teal.words, 0.9)}; }
.time { position: absolute; text-align: center; font: 600 44px/1 ${fonts(lang, "Display")}; color: #fff; }
.bubble { position: absolute; transform: translate(-50%, -50%); white-space: nowrap; padding: 10px 18px 12px; border-radius: 999px; background: ${teal.fill};
  color: #fff; font: 600 22px/1 ${fonts(lang, "Text")}; box-shadow: 0 8px 24px rgba(0, 0, 0, 0.45); z-index: 6; }
.lead { position: absolute; width: 2px; background: ${rgba(teal.line, 0.9)}; z-index: 5; }
.dot { position: absolute; width: 14px; height: 14px; border-radius: 50%; background: ${hex(teal.dot)}; box-shadow: 0 0 0 4px ${rgba(teal.dot, 0.35)}; z-index: 6; }
.lens { position: absolute; overflow: hidden; border-radius: 50%; background: #000; z-index: 7;
  box-shadow: 0 0 0 6px rgba(255, 255, 255, 0.92), 0 30px 80px rgba(0, 0, 0, 0.7); }
.points { list-style: none; margin: 34px 0 0; padding: 0; font: 600 32px/1.25 ${fonts(lang, "Display")}; }
.points li { margin: 0 0 18px; padding-left: 52px; position: relative; }
.points li::before { content: "✓"; position: absolute; left: 0; top: -2px; width: 36px; height: 36px; border-radius: 50%; background: ${teal.fill};
  font-size: 22px; line-height: 36px; text-align: center; }
#under { position: absolute; inset: 0; }
</style></head><body>
<div class="text">
  <div class="label"><img src="${logo}" alt="">${escape(s.label)}</div>
  <h1>${escape(s.headline)}</h1>
  <p>${escape(s.sub)}</p>
  ${picture.points ? `<ul class="points">${picture.points.map((point) => `<li>${escape(point)}</li>`).join("")}</ul>` : ""}
</div>
<div id="under"><div class="glow"></div>
${cards}</div>
${side ? "" : `<script>
  // Words that need more room than the band over the pictures (a longer language, a second line) push the pictures
  // down and make them smaller, about the page's middle, so nothing stands over them.
  document.fonts.ready.then(() => {
    const bottom = document.querySelector(".text").getBoundingClientRect().bottom + 14;
    if (bottom > ${under}) {
      const k = (${page.h} - bottom) / (${page.h} - ${under});
      const under = document.getElementById("under");
      under.style.transformOrigin = "50% ${under}px";
      under.style.transform = \`translateY(\${bottom - ${under}}px) scale(\${k})\`;
    }
  });
</script>`}
</body></html>`;
}

// The 16:9 hero: no words and no UI (the Store lays the title over it and crops it), the logo large in
// the middle of the upper two thirds on the brand's teal.
function heroHtml() {
  return `<!doctype html><html><head><meta charset="utf-8"><style>
html, body { margin: 0; width: ${page.w}px; height: ${page.h}px; overflow: hidden; }
body { ${background} }
.ring { position: absolute; border-radius: 50%; border: 1.5px solid ${rgba(teal.line, 0.14)}; left: 50%; top: 40%; transform: translate(-50%, -50%); }
.glow { position: absolute; left: 50%; top: 40%; width: 760px; height: 760px; transform: translate(-50%, -50%); background: radial-gradient(closest-side, ${rgba(teal.mark, 0.55)}, ${rgba(teal.mark, 0)}); }
.logo { position: absolute; left: 50%; top: 40%; width: 420px; transform: translate(-50%, -50%); filter: drop-shadow(0 30px 60px ${rgba(teal.shade, 0.7)}); }
</style></head><body>
<div class="ring" style="width:640px;height:640px"></div>
<div class="ring" style="width:900px;height:900px;opacity:.7"></div>
<div class="ring" style="width:1180px;height:1180px;opacity:.45"></div>
<div class="ring" style="width:1480px;height:1480px;opacity:.25"></div>
<div class="glow"></div>
<img class="logo" src="${logo}" alt="">
</body></html>`;
}

// The 300 x 300 tile icon: the mark, a square to its edges, filling it; PRO's amber under its bar, clear of the tiles.
function tileHtml(pro) {
  return `<!doctype html><html><head><meta charset="utf-8"><style>
html, body { margin: 0; width: 300px; height: 300px; overflow: hidden; background: transparent; }
img { position: absolute; left: 0; top: 0; width: 300px; height: 300px; }
.pro { position: absolute; right: 10px; bottom: 16px; padding: 7px 16px 9px; border-radius: 999px; background: linear-gradient(180deg, #ffd45c, #f5a623);
  color: #3a2400; font: 700 34px/1 "Segoe UI Variable Display", "Segoe UI", sans-serif; letter-spacing: 0.04em; box-shadow: 0 4px 14px rgba(0, 0, 0, 0.35); }
</style></head><body><div style="position: relative; width: 300px; height: 300px;"><img src="${logo}" alt="">${pro ? '<div class="pro">PRO</div>' : ""}</div></body></html>`;
}

// The package's logos, the names and sizes Package.appxmanifest's visual elements take: the mark alone on nothing
// (Windows 11 sets no plate behind it). It is a square to its edges, so where it stands for the app (the taskbar and
// Start sizes, the Store logo) it fills its box as the exe's icon does, and at a size assets/logo has drawn on whole
// pixels it is that file; the tiles keep room round it.
function packageIcons() {
  const dir = join(repo, "artifacts", "store", "package", "Images");
  const icon = (name, w, h, inset) => {
    const drawn = join(repo, "assets", "logo", `karpo-${w}.png`);
    if (inset === 0 && w === h && existsSync(drawn)) {
      mkdirSync(dir, { recursive: true });
      copyFileSync(drawn, join(dir, name + ".png"));
      console.log(`  ${join(dir, name + ".png")}`);
      return;
    }
    const side = Math.round(Math.min(w, h) * (1 - 2 * inset));
    const html = `<!doctype html><html><head><meta charset="utf-8"><style>html, body { margin: 0; background: transparent; }
      div { position: relative; width: ${w}px; height: ${h}px; } img { position: absolute; width: ${side}px; height: ${side}px; left: ${(w - side) / 2}px; top: ${(h - side) / 2}px; }</style></head>
      <body><div><img src="${logo}" alt=""></div></body></html>`;
    render(`pkg-${name}`, html, join(dir, name + ".png"), Math.max(w, 400), Math.max(h, 400), 1, w, h);
  };
  const scales = { 100: 1, 125: 1.25, 150: 1.5, 200: 2, 400: 4 };
  for (const [scale, k] of Object.entries(scales)) {
    icon(`Square44x44Logo.scale-${scale}`, Math.round(44 * k), Math.round(44 * k), 0);
    icon(`Square150x150Logo.scale-${scale}`, Math.round(150 * k), Math.round(150 * k), 0.18);
    icon(`Wide310x150Logo.scale-${scale}`, Math.round(310 * k), Math.round(150 * k), 0.18);
    icon(`SmallTile.scale-${scale}`, Math.round(71 * k), Math.round(71 * k), 0.14);
    icon(`LargeTile.scale-${scale}`, Math.round(310 * k), Math.round(310 * k), 0.22);
    icon(`StoreLogo.scale-${scale}`, Math.round(50 * k), Math.round(50 * k), 0);
  }
  for (const size of [16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256]) {
    for (const form of ["", "_altform-unplated", "_altform-lightunplated"]) {
      icon(`Square44x44Logo.targetsize-${size}${form}`, size, size, 0);
    }
  }
}

// Concepts (store/concepts.json): pictures tried before any is chosen for the listing, English alone, each with its
// layout given in full, to artifacts/store/concepts, and a sheet of them all to choose from.
if (process.argv.includes("--concepts")) {
  const concepts = JSON.parse(readFileSync(join(repo, "store", "concepts.json"), "utf8"));
  const folder = join(repo, "artifacts", "store", "concepts");
  const only = process.argv.slice(process.argv.indexOf("--concepts") + 1).filter((a) => !a.startsWith("--"));
  // All of them made again from nothing; some named, those alone, the others left as they are.
  if (only.length === 0) rmSync(folder, { recursive: true, force: true });
  concepts.forEach((concept, i) => {
    const n = String(i + 1).padStart(2, "0");
    if (only.length > 0 && !only.includes(concept.id)) return;
    const capture = join(shots, (concept.picture.shot ?? concept.id) + ".png");
    render(`concept-${n}`, screenshotHtml("en", concept, capture, concept.picture), join(folder, `${n}-${concept.id}.png`), page.w, page.h, 2);
  });
  process.exit(0);
}

if (process.argv.includes("--package")) {
  packageIcons();
  process.exit(0);
}

const languages = process.argv.length > 2
  ? process.argv.slice(2)
  : readdirSync(join(repo, "store", "listing")).filter((f) => f.endsWith(".json")).map((f) => f.slice(0, -5));

console.log("hero and icons");
render("hero", heroHtml(), join(out, "hero-3840x2160.png"), page.w, page.h, 2);
render("tile", tileHtml(false), join(out, "tile-300x300.png"), 300, 300, 1);
render("pro", tileHtml(true), join(out, "pro-300x300.png"), 300, 300, 1);

for (const lang of languages) {
  const listing = JSON.parse(readFileSync(join(repo, "store", "listing", lang + ".json"), "utf8"));
  console.log(lang);
  // A language's set made again whole: a picture renumbered or dropped leaves nothing behind.
  rmSync(join(out, lang), { recursive: true, force: true });
  const order = listing.screenshots.map((s) => s.shot).join(", ");
  if (order !== frames.map((f) => f.shot).join(", ")) throw new Error(`compose: ${lang}'s screenshots (${order}) aren't panorama.mjs's frames`);
  // The language's own captures where it has every one the panorama is made of, else the English ones.
  const needed = [...new Set(frames.flatMap(uses))];
  const dir = needed.every((name) => existsSync(join(shots, lang, name + ".png"))) ? join(shots, lang) : shots;
  const missing = needed.filter((name) => !existsSync(join(shots, name + ".png")));
  if (missing.length > 0) {
    console.warn(`  no capture ${missing.join(", ")}: dotnet run scripts/stage/Stage.cs -- store shots <scene>`);
    continue;
  }
  frames.forEach((frame, i) => {
    const n = String(i + 1).padStart(2, "0");
    // A window taller than the page, cut back to it: Chrome leaves the last rows of a window undrawn under turned cards.
    render(`${lang}-${n}`, panoramaHtml(lang, listing, dir, i), join(out, lang, `screenshot-${n}-${frame.shot}.png`), page.w, page.h + 200, 2, page.w * 2, page.h * 2);
  });
}
