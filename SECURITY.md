# Security

## What this game does with your machine

Scaling Laws is a single-player Unity game. It has **no networking of any kind**: no telemetry, no
analytics, no crash reporting, no account, no update check. Nothing leaves your computer.

It writes exactly two things:

| What | Where |
|---|---|
| Your campaign save | Unity's `PlayerPrefs` for this application |
| Your settings, including language | the same place |

Uninstalling removes the game. Clearing the application's `PlayerPrefs` removes the save.

## Reporting something

If you find a way this build can damage a machine, read or write outside the two locations above,
or reach the network, that is a bug worth reporting privately rather than in an issue.

Email **kematex2202@gmail.com** with `Scaling Laws security` in the subject. Include the build
version from the main menu and what you did to get there.

Anything that is a normal crash, a bad number or a screen that does not work belongs in a public
[issue](https://github.com/HuckleR2003/ScalingLaws-AI/issues) instead, and is very welcome there.

## Builds

Releases are built from the commit they are tagged against. The tag is on the commit, so what you
downloaded can be checked against what is in this repository.

The repository does not contain the imported Asset Store packs used for the office furniture and the
character models. Their licences forbid redistribution, so a fresh clone shows them as missing
references. Nothing that decides anything in the game is in those packs.
