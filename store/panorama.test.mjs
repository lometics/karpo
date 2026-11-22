// The panorama's frames (panorama.mjs): run with `node --test store/panorama.test.mjs`.
import assert from "node:assert/strict";
import { test } from "node:test";
import { readFileSync } from "node:fs";
import { frames, monitorBox } from "./panorama.mjs";

const english = JSON.parse(readFileSync(new URL("./listing/en.json", import.meta.url), "utf8"));
const capture = { w: 3072, h: 1728 };

const page = { w: 1920, h: 1080 };

test("every monitor stands whole on its own page, its stand included", () => {
  for (const frame of frames) {
    for (const card of frame.cards.filter((c) => c.screen)) {
      const box = monitorBox(card);
      assert.ok(box.left >= 0 && box.right <= page.w, `${frame.shot}: the monitor crosses a cut (${box.left}..${box.right})`);
      assert.ok(box.top >= 0 && box.bottom <= page.h, `${frame.shot}: the monitor's stand leaves the page (${box.top}..${box.bottom})`);
    }
  }
});

test("a monitor of four cuts names four captures, and a screen's shot is its first", () => {
  for (const card of frames.flatMap((f) => f.cards).filter((c) => c.cuts)) {
    assert.ok(card.screen, "cuts belong to a screen");
    assert.equal(card.cuts[0], card.shot);
  }
});

test("a lens enlarges a screen of its own page", () => {
  for (const frame of frames) {
    for (const card of frame.cards.filter((c) => c.lens !== undefined)) {
      assert.ok(frame.cards[card.lens]?.screen, `${frame.shot}: a lens of no screen`);
      assert.equal(frame.cards[card.lens].shot, card.shot, `${frame.shot}: a lens of another capture than its screen's`);
    }
  }
});

test("every callout of a page has one bubble, and every bubble a callout", () => {
  frames.forEach((frame, i) => {
    if (frame.lanes) return;
    const said = frame.cards.flatMap((c) => (c.bubbles ?? []).map((_, k) => (c.first ?? 0) + k)).sort((a, b) => a - b);
    const callouts = english.screenshots[i].callouts ?? [];
    assert.deepEqual(said, callouts.map((_, k) => k), `${frame.shot}: ${said.length} bubbles for ${callouts.length} callouts`);
  });
});

test("every piece is cut from inside its capture", () => {
  for (const frame of frames) {
    const pieces = [...frame.cards, ...(frame.wall?.lists ?? []).flatMap((l) => l.cards), ...(frame.lanes?.lanes ?? [])].filter((c) => c.crop);
    for (const { shot, crop } of pieces) {
      assert.ok(crop.x >= 0 && crop.y >= 0 && crop.x + crop.w <= capture.w && crop.y + crop.h <= capture.h, `${frame.shot}: ${shot}'s piece leaves its capture`);
    }
  }
});

test("the frames are the listing's screenshots, in their order", () => {
  assert.deepEqual(frames.map((f) => f.shot), english.screenshots.map((s) => s.shot));
});

test("the accounts' lanes give every callout of their page once", () => {
  frames.forEach((frame, i) => {
    if (!frame.lanes) return;
    assert.deepEqual(frame.lanes.lanes.map((l) => l.gives), english.screenshots[i].callouts.map((_, k) => k));
  });
});

test("no page shows a clock but the dashboard's, and the week only the first", () => {
  // Where the clocks and the week are in the captures: the dashboard's first list down to its timer, the date's folder.
  const pieces = frames.flatMap((f) => [...f.cards, ...(f.wall?.lists ?? []).flatMap((l) => l.cards), ...(f.lanes?.lanes ?? [])].map((c) => ({ page: f.shot, ...c })));
  const clocks = pieces.filter((c) => c.shot === "folders" && (c.screen || (c.crop.x < 852 && c.crop.y < 1094)));
  assert.deepEqual(clocks.map((c) => c.page), ["folders"]);
  const week = pieces.filter((c) => c.shot === "day" && (c.screen || c.crop.x < 2316));
  assert.deepEqual(week.map((c) => c.page), ["day"]);
});
