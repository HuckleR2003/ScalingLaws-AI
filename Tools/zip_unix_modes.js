// Puts Unix permissions into a zip written on Windows.
//
// **The problem this solves is a support question, not a cosmetic one.** A zip written here records
// no executable bit, because NTFS has no such thing, so a Mac or Linux player downloads a correct
// build and the operating system refuses to start it. The usual answer is to tell them to run
// `chmod +x`, which works and which a fair number of people will not do.
//
// The ZIP format has carried the answer since the 1990s. Each entry's central directory header has
// a 4 byte "external file attributes" field whose **high 16 bits are the Unix mode** when the
// "version made by" high byte says the archive came from Unix (3). Every extractor that matters
// honours it: Info-ZIP unzip, macOS Archive Utility, the GNOME and KDE file managers, Python's
// zipfile. Extractors that do not understand it ignore those bits and behave exactly as before,
// which is the state we are already in, so this cannot make anything worse.
//
//   node Tools/zip_unix_modes.js <archive.zip> [path/to/make/executable ...]
//
// Everything named on the command line, plus anything inside a `.app` bundle's `Contents/MacOS`,
// becomes 0755. Directories become 0755. Everything else becomes 0644.
//
// **It rewrites the file in place and verifies by reading its own work back**, because an edit to a
// byte offset is exactly the kind of change that appears to work: a wrong offset corrupts a field
// nothing reads until somebody tries to open the archive.
const fs = require("fs");

const FILE = 0o100644;
const EXEC = 0o100755;
const DIR = 0o040755;

const END_SIGNATURE = 0x06054b50;
const CENTRAL_SIGNATURE = 0x02014b50;

function patch(path, executables) {
  const zip = fs.readFileSync(path);

  // The end of central directory record is at the end, after a comment of unknown length, so it is
  // found by scanning backwards for its signature rather than by assuming it is the last 22 bytes.
  let end = -1;

  for (let at = zip.length - 22; at >= 0; at--) {
    if (zip.readUInt32LE(at) === END_SIGNATURE) { end = at; break; }
  }

  if (end < 0) { throw new Error("nie znalazlem konca katalogu centralnego, to nie jest zip"); }

  const count = zip.readUInt16LE(end + 10);
  let at = zip.readUInt32LE(end + 16);

  const wanted = new Set(executables);
  const report = [];

  for (let i = 0; i < count; i++) {
    if (zip.readUInt32LE(at) !== CENTRAL_SIGNATURE) {
      throw new Error(`wpis ${i} nie zaczyna sie naglowkiem katalogu centralnego`);
    }

    const nameLength = zip.readUInt16LE(at + 28);
    const extraLength = zip.readUInt16LE(at + 30);
    const commentLength = zip.readUInt16LE(at + 32);
    const name = zip.toString("utf8", at + 46, at + 46 + nameLength);

    const isDirectory = name.endsWith("/");
    const insideBundle = name.includes(".app/Contents/MacOS/");
    const mode = isDirectory ? DIR : (wanted.has(name) || insideBundle) ? EXEC : FILE;

    // The high byte of "version made by" names the filesystem the archive came from. Left at 0 it
    // says FAT, and an extractor reading FAT ignores the mode entirely, so this half is not
    // optional: without it the other half is written and never read.
    zip.writeUInt8(3, at + 5);
    zip.writeUInt32LE((mode << 16) >>> 0, at + 38);

    if (mode === EXEC) { report.push(name); }

    at += 46 + nameLength + extraLength + commentLength;
  }

  fs.writeFileSync(path, zip);

  return { count, executable: report };
}

// Reads the file back off disk and decodes the same fields, so a wrong offset shows up here rather
// than on somebody's machine.
function verify(path) {
  const zip = fs.readFileSync(path);
  let end = -1;

  for (let scan = zip.length - 22; scan >= 0; scan--) {
    if (zip.readUInt32LE(scan) === END_SIGNATURE) { end = scan; break; }
  }

  const count = zip.readUInt16LE(end + 10);
  let at = zip.readUInt32LE(end + 16);
  const modes = {};

  for (let i = 0; i < count; i++) {
    const nameLength = zip.readUInt16LE(at + 28);
    const extraLength = zip.readUInt16LE(at + 30);
    const commentLength = zip.readUInt16LE(at + 32);
    const name = zip.toString("utf8", at + 46, at + 46 + nameLength);
    const madeOn = zip.readUInt8(at + 5);
    const mode = zip.readUInt32LE(at + 38) >>> 16;

    modes[name] = { madeOn, mode };
    at += 46 + nameLength + extraLength + commentLength;
  }

  return modes;
}

const [archive, ...executables] = process.argv.slice(2);

if (!archive) {
  console.error("uzycie: node Tools/zip_unix_modes.js <archive.zip> [plik do +x ...]");
  process.exit(1);
}

const result = patch(archive, executables);
const back = verify(archive);

const wrongFilesystem = Object.entries(back).filter(([, v]) => v.madeOn !== 3);
const executableBack = Object.entries(back).filter(([, v]) => (v.mode & 0o111) !== 0 && !((v.mode & 0o040000) !== 0));

if (wrongFilesystem.length > 0) {
  console.error(`wpisow bez znacznika Unix: ${wrongFilesystem.length}`);
  process.exit(1);
}

console.log(`${archive}`);
console.log(`  wpisow: ${result.count}, wszystkie oznaczone jako Unix`);
console.log(`  wykonywalnych plikow po odczycie z powrotem: ${executableBack.length}`);

for (const [name] of executableBack) {
  console.log(`    ${name}`);
}

if (executableBack.length === 0) {
  console.error("  zadnego pliku wykonywalnego, czyli nic sie nie stalo");
  process.exit(1);
}
