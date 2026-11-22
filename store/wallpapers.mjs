// The stage's wallpapers, fetched: each picture of store/wallpapers.json from its source, as
// store/wallpapers/<name>.png. The folder is git-ignored: the pictures are other people's illustrations, free to use
// under the licence each entry names, and the repository keeps the list, not the files. Stage.cs fits a picture to
// its stage itself.
//
//   node store/wallpapers.mjs            every picture that is missing
//   node store/wallpapers.mjs --all      every picture again
//   node store/wallpapers.mjs <name>...  these
//
// A downloaded file is data: it is kept only if it is a PNG, and only ever read as a picture.
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const store = resolve(dirname(fileURLToPath(import.meta.url)));
const list = JSON.parse(readFileSync(join(store, "wallpapers.json"), "utf8")).wallpapers;
const folder = join(store, "wallpapers");
const png = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

const args = process.argv.slice(2);
const all = args.includes("--all");
const named = args.filter((a) => !a.startsWith("--"));
const unknown = named.filter((n) => !list.some((w) => w.name === n));
if (unknown.length > 0) {
  console.error(`wallpapers: not in wallpapers.json: ${unknown.join(", ")}`);
  process.exit(1);
}

mkdirSync(folder, { recursive: true });
let failed = false;
for (const entry of list) {
  const file = join(folder, entry.name + ".png");
  if (named.length > 0 ? !named.includes(entry.name) : !all && existsSync(file)) continue;
  try {
    const answer = await fetch(entry.download, { signal: AbortSignal.timeout(60000) });
    if (!answer.ok) throw new Error(`HTTP ${answer.status}`);
    const bytes = Buffer.from(await answer.arrayBuffer());
    if (!bytes.subarray(0, 8).equals(png)) throw new Error("not a PNG");
    writeFileSync(file, bytes);
    console.log(`  ${entry.name}: ${bytes.length} bytes, ${entry.licence}, by ${entry.author}`);
  } catch (error) {
    console.error(`  ${entry.name}: ${error.message} (${entry.download})`);
    failed = true;
  }
}
if (failed) process.exit(1);
