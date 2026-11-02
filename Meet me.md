# Meet me

You have a folder with a game in it. Here is what it is and who made it, in the time it takes to
wait for a download you have already finished.

## Start here

Nothing to install, nothing to sign into, and the game does not talk to the internet at any point.

**Windows.** Run **Scaling Laws.exe**. Windows will probably warn you that it does not recognise
the publisher. That is because I have not bought a code signing certificate, and I have not bought
one because this is free. Click **More info**, then **Run anyway**, or do not, and read the source
instead: it is all public.

**macOS.** Right click **Scaling Laws.app** and choose **Open**, rather than double clicking it.
macOS quarantines anything a browser downloaded and a plain double click on an app from an
unidentified developer is simply refused, with a message that sounds like the file is broken. It is
not. If you would rather do it in Terminal, `xattr -dr com.apple.quarantine "Scaling Laws.app"`
once has the same effect. One bundle covers Intel and Apple Silicon. macOS 12 or newer.

**Linux.** Run **Scaling Laws.x86_64**. The archive carries the executable bit, so there should be
no `chmod` to do; if your extractor dropped it anyway, `chmod +x "Scaling Laws.x86_64"` puts it
back.

First run takes a few seconds longer than the ones after it. The music is computed rather than
loaded, so the game writes the notes before it plays them.

## The Mac and Linux builds have never been run

This is the first release for either, and I want you to know what that means before you spend an
evening on it.

Both compiled, both have the right shape, and nobody has opened either one. I build on Windows,
there is no Mac and no Linux machine in this flat, and a virtual one cannot answer the question
because Unity wants a real graphics driver and a virtual machine does not give it one. Testing in
a VM would have told me the build was broken when it was fine.

What I did instead was check everything that can be checked without the hardware. Nothing in the
game touches a platform: no system calls, no registry, no Windows paths, and every file the game
loads comes out of its own bundle rather than off your disk. The Mac binary really is universal
and the Linux one really is a 64-bit ELF; I read that back out of the files rather than trusting
the build.

One thing I do expect to misbehave on Linux: the short film at the opening is H.264 and Unity's
Linux player is unreliable with that codec. If it does not play you get about six seconds of black
with SKIP on screen, and then the game carries on normally. That is a watchdog doing its job, not
a hang.

If either build does something stranger than that, I would genuinely like to know. See below.

## What you are about to play

It is 1 January 2022. You have twelve million dollars, no product, no team and a garage.

You train models, sell tokens, and try to still be standing in 2036 against fourteen labs that
shipped on the dates their real counterparts shipped on. The spine of the whole thing is one
sentence: **upgrades are not purchased, they are timed.** Hardware ages. The frontier moves every
month. Money spent a year early sits in an asset losing a quarter of its value with every successor
that launches.

There is no guaranteed profit anywhere in it. The price of a token falls by about half a year,
demand saturates, and a company that ships one model and coasts is bankrupt inside three years.
That is the design rather than a balance problem, and if it feels unfair in the third year, that is
the third year working.

The game is in **English and Polish**, and it picks whichever your machine is set to. Both are
complete: 3,884 phrases each.

## Who made it

Marcin Firmuga, in Radom, Poland. Work by day, this by night.

The music and four sounds were written by **Samanta**, who also sat down with the first build for
one evening, a pen and a piece of paper, and found four kinds of bug that 1,115 automated tests had
not. One of them was a whole paragraph I had written that had never once appeared on screen.

I work with an AI assistant and say so plainly. What that cannot do is sit down for an evening, get
confused at a particular moment, and write down where.

## The one thing worth telling me

**The exact moment you got confused.** Not whether you liked it, not a score out of ten. The moment
where you did not know what the screen wanted from you, or what a number meant, or what you were
supposed to click.

There is a link inside the game for exactly this: on the main menu, in the pause menu, and in a letter the game sends you. It takes a minute and it is
worth more to me right now than a hundred downloads nobody opens.

If something crashes there is a log, and it is not next to the executable:

- **Windows**: paste `%USERPROFILE%\AppData\LocalLow\HCK Labs\Scaling Laws` into any Explorer window
- **macOS**: `~/Library/Logs/HCK Labs/Scaling Laws`
- **Linux**: `~/.config/unity3d/HCK Labs/Scaling Laws`

`Player.log` in there is the whole story, and sending it costs you one attachment.

## Honest about what is not finished

- **The Mac and Linux builds have not been run by anybody.** The section above says why, and it is
  the thing on this list I would most like fixed by somebody writing to me.
- **Nobody has clicked the basement floor in a test.** The grid arithmetic is covered and the
  click-to-tile projection is not, because the test runner sends no pointer events. Yours may still
  be among the first real clicks on it.
- **The Steam page is not up yet.** The paperwork is going through. Every build until it does is
  free here.
- Fourteen rival labs can be bought, sued, poached from and allied with, and the balance of that
  last one is three weeks old. If an alliance ever feels like the obvious move every single time,
  that is worth a message.

## Where everything else is

- **CHANGELOG.md**, in this folder: everything that is different from the first build, at length.
- **README.md**, in this folder: what is simulated and how, for whoever wants the engineering.
- The source, the tests and the issue tracker: **github.com/HuckleR2003/ScalingLaws-AI**

Free, and the code is public. If you get an evening out of it, that is the whole point.

Marcin
