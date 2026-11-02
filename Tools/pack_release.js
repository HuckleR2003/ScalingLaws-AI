// Packs a release for every platform that has been built, the same way each time.
//
//   node Tools/pack_release.js 0.8.0
//
// **This exists because the packing step is where three releases went wrong in two days.** A zip
// written with PowerShell's Compress-Archive put backslashes in the entry names, which macOS
// unpacks as one file with slashes in its name rather than as a bundle. A zip written from inside
// the build folder came out as 205 loose files with no folder around them. And the three documents
// a player is told to read were not in any archive at all, because they lived beside the build
// rather than in it.
//
// So: one command, bsdtar for the archive, the Unix modes written in afterwards, and every claim
// checked by reading the finished file back with a different reader than the one that wrote it.
const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");

const TAR = "C:/Windows/System32/tar.exe";
const OUT = "Build~";

// What a player is told to open, copied in beside the game. `Meet me.md` points at the other two
// by name, so shipping it alone makes the document wrong.
const DOCUMENTS = ["Meet me.md", "CHANGELOG.md", "README.md"];

const PLATFORMS = [
  {
    name: "Windows",
    folder: "Scaling Laws Windows",
    archive: version => `ScalingLaws-${version}-windows.zip`,
    // Windows has no executable bit to lose, so the modes are left alone.
    executable: null
  },
  {
    name: "macOS",
    folder: "Scaling Laws Mac",
    archive: version => `ScalingLaws-${version}-mac.zip`,
    // Anything inside a bundle's Contents/MacOS is handled by the mode tool's own rule.
    executable: "bundle"
  },
  {
    name: "Linux",
    folder: "Scaling Laws Linux",
    archive: version => `ScalingLaws-${version}-linux.zip`,
    executable: "Scaling Laws Linux/Scaling Laws.x86_64"
  }
];

const version = process.argv[2];

if (!version || !/^\d+\.\d+\.\d+$/.test(version)) {
  console.error("uzycie: node Tools/pack_release.js <wersja>, na przyklad 0.8.0");
  process.exit(1);
}

function run(file, args) {
  return execFileSync(file, args, { encoding: "utf8", maxBuffer: 64 * 1024 * 1024 });
}

// Reads the finished archive back. bsdtar listing rather than the writer's own word for it.
//
// **Trim every line.** bsdtar on Windows ends them with a carriage return, so a line is
// "...README.md\r" and `endsWith("README.md")` is false on an archive that holds the file. The
// first run of this reported 0 of 3 documents in three archives that each contained all three.
function inspect(archive) {
  const listing = run(TAR, ["-tvf", archive])
    .split("\n")
    .map(line => line.trim())
    .filter(line => line.length > 0);

  const backslashes = listing.filter(l => l.includes("\\")).length;

  // A name can contain spaces, so it is whatever follows the HH:MM field rather than a column.
  const nameOf = line => (line.match(/\d\d:\d\d\s+(.*)$/) || [, line])[1];

  const executables = listing.filter(l => /^-rwx/.test(l)).map(nameOf);
  const documents = DOCUMENTS.filter(doc => listing.some(l => nameOf(l).endsWith(doc)));

  return { entries: listing.length, backslashes, executables, documents };
}

let failed = false;

for (const platform of PLATFORMS) {
  const folder = path.join(OUT, platform.folder);

  if (!fs.existsSync(folder)) {
    console.log(`${platform.name}: nie ma ${folder}, pomijam`);
    continue;
  }

  // The documents go next to the game rather than inside the Mac bundle, where a player would
  // have to right click and show package contents to find them.
  for (const doc of DOCUMENTS) {
    if (!fs.existsSync(doc)) {
      console.error(`${platform.name}: nie ma ${doc} w katalogu projektu`);
      failed = true;
      continue;
    }

    fs.copyFileSync(doc, path.join(folder, doc));
  }

  const archive = path.join(OUT, platform.archive(version));

  if (fs.existsSync(archive)) { fs.unlinkSync(archive); }

  // -a picks the format from the extension, and the folder is named rather than its contents, so
  // the archive holds one folder instead of scattering itself over the player's downloads.
  run(TAR, ["-a", "-c", "-f", archive, "--exclude", "*_DoNotShip*", "-C", OUT, platform.folder]);

  if (platform.executable) {
    const args = [path.join("Tools", "zip_unix_modes.js"), archive];

    if (platform.executable !== "bundle") { args.push(platform.executable); }

    process.stdout.write(run(process.execPath, args));
  }

  const seen = inspect(archive);
  const size = fs.statSync(archive).size;
  const trouble = [];

  if (seen.backslashes > 0) { trouble.push(`${seen.backslashes} wpisow z backslashem`); }

  if (platform.executable && seen.executables.length !== 1) {
    trouble.push(`plikow wykonywalnych ${seen.executables.length}, oczekiwano 1`);
  }

  if (seen.documents.length !== DOCUMENTS.length) {
    trouble.push(`dokumentow ${seen.documents.length} z ${DOCUMENTS.length}`);
  }

  console.log(`${platform.name}: ${platform.archive(version)}, `
    + `${(size / 1048576).toFixed(1)} MB, wpisow ${seen.entries}, `
    + `dokumenty ${seen.documents.length}/${DOCUMENTS.length}, backslashy ${seen.backslashes}`
    + (platform.executable ? `, +x na: ${seen.executables.join(", ") || "niczym"}` : ""));

  if (trouble.length > 0) {
    console.error("  PROBLEM: " + trouble.join("; "));
    failed = true;
  }
}

process.exit(failed ? 1 : 0);
