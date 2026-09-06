# Glossary

One English term, one Polish term, everywhere.

The phrase book holds 2,505 entries in each language. At that size the risk stops being a missing
translation and becomes two names for one thing: a player reads FRONT DZISIAJ on one row of the
model creator and GRANICA DZISIAJ on another, for the same number, and concludes they are two
numbers. No test can see that. `LocalisationTests` checks that a key exists.
`LocalisationCoverageTests` checks that it resolves. Neither knows what a word means.

Counts below were measured over the two dictionaries on 2026-09-05, not remembered. The method is
at the bottom so they can be measured again.

---

## Settled

These are already consistent in the book. They are written down so nobody spends an afternoon
deciding them a second time.

| English | Polish | Used | Note |
|---|---|---|---|
| capability | zdolność | 19 of 20 | The 0 to 100 score. Never "możliwości", which reads as options. |
| corpus, corpora | korpus | 19 of 19 | Never "zbiór danych". A corpus is a named, licensed thing. |
| throughput | przepustowość | all | Of a cluster or a run. |
| cluster | klaster | 12 | |
| fleet | flota | 18 | Everything the company rents and owns, taken together. |
| rack, cabinet | szafa | 10 | |
| upgrade | ulepszenie | 4 | The post-training work, not a new model. |
| release | wydanie | 9 | The act and the version. |
| brand | marka | 7 | |
| reputation | reputacja | 12 | Separate from brand and must stay separate. |
| incident | incydent | 9 | |
| token | token | all | Not "żeton". |
| model | model | all | |
| research points | punkty badań | all | Abbreviated **pkt** where a number precedes it, because Polish declines the noun and the abbreviation does not. |

## Decided here, because the book was split

Four terms were used two ways. The counts are what the book actually contained before this file
existed.

### frontier → **granica**

| found | count |
|---|---|
| granica, granicy | 6 |
| front, froncie | 6 |
| czoło stawki | 1 |
| left as "Frontier" | 2 |

**Use granica.** It is the word the rest of the book already builds sentences around: `banner.about`
says "przy granicy", `mg.frontier_is` says "granica to", `manage.scores_against` says "przy granicy
możliwości". "Front" reads as a battle line, and in a game about a capability frontier that is the
wrong picture.

Leave proper names alone: `grant.frontier.body` is the name of a consortium and stays "Frontier".

**One live consequence.** The book holds two keys with identical English and different Polish:

    ["create.frontier"]     = "FRONTIER TODAY"  /  "FRONT DZISIAJ"
    ["create.fig_frontier"] = "FRONTIER TODAY"  /  "GRANICA DZISIAJ"

Only the second has a caller. The first is dead and should go, rather than be repaired.

### serving → **serwowanie**

| found | count |
|---|---|
| serwowanie, serwowania | 6 |
| obsługa, obsługuje | 4 |

**Use serwowanie** for the noun: the act of answering requests, the thing a serving bill is for.
`upgrade.efficiency` already says "WYDAJNOŚĆ SERWOWANIA".

**"Obsługiwać" stays as the verb** where the sentence is about a company doing the work: "każdy
darmowy token i tak obsługujesz". The noun and the verb are different jobs and both are right.

### compute → **moc**

| found | count |
|---|---|
| moc, mocy | 28 |
| moc obliczeniowa | 2 |

**Use moc.** The bottom bar already says MOC, and so does the upgrade screen.

**"Moc obliczeniowa" is reserved for one place**: `compute.title`, the heading of the page itself,
where there is room for the full name and a heading benefits from it. Everywhere else, in a row, a
tile or a sentence, it is moc.

### audience → **publiczność**

| found | count |
|---|---|
| publiczność | 6 |
| odbiorcy | 2 |

**Use publiczność** for the market segment, which is what `AudienceCatalog` models. "Odbiorcy" is
fine in ordinary prose about people receiving something, but not as the name of the segment.

## Terms with no good short Polish

Recorded so the decision is not made three different ways under time pressure.

| English | Polish | Why |
|---|---|---|
| red teaming | red teaming | The industry term, used untranslated in Polish practice. "Zespół czerwony" means nothing to anybody. |
| ASSA | ASSA | The game's own acronym. |
| frontier lab | laboratorium na granicy możliwości | Long, and correct. Do not shorten to "front lab". |
| tokens per parameter | tokeny na parametr | |
| petaflop, petaflop-day | petaflop, petaflopodzień | |

## The same English, two Polish translations

There are **49 pairs of keys holding identical English and different Polish**. Some are correct:
FANS is a crowd on the header and a cooling fan in a cabinet, and those are two words in Polish
because they are two things.

Most are not. **Six of them are the same safety tier, named one way in the research tree and
another in the model creator**, for a thing the player unlocks in the first screen and then uses in
the second:

| English | in the tree | in the creator |
|---|---|---|
| Licensed Stacked-ASSA | Licencjonowana ASSA warstwowa | Licencjonowane Stacked-ASSA |
| Advanced ASSA | Zaawansowana ASSA | Zaawansowane ASSA |
| Automated Red Teaming | Automatyczny red teaming | Zautomatyzowany red teaming |
| Adversarial Campaigns | Kampanie adwersarialne | Kampanie adwersaryjne |
| Continuous Red Team | Stały zespół red team | Ciągły red team |
| Encrypted Data Vaults | Szyfrowane sejfy danych | Szyfrowane skarbce danych |

`node.*` and `safety.*` describe the same twelve rungs. **Either one key should serve both, or the
two should agree.** One key is better: a tier renamed in one place and not the other is the same
fault appearing again a year from now.

The full list of 49 is worth a pass. The mechanical part of it is testable, which is more than can
be said for most translation faults: no two keys may hold identical English and different Polish
unless they are listed as a deliberate homograph.

## Rules

**One English term, one Polish term.** If a second reading seems necessary, the English probably
carries two meanings and wants two keys, not one key with a flexible translation.

**Never build a term out of a number and a noun without checking the plural.** Polish takes three
forms, and the rule is arithmetic on the last two digits, not "one or many". Either route it through
`Loc.Counted` and the `noun.*` keys, or move the number to the end of the sentence, or use an
abbreviation that does not decline. All three are used in this project and all three are fine.

**A name is not a term.** Company names, lab names, hardware names and the game's own title stay as
written in every language. `HuggyFace` is not translated. `NVIDIA H100 SXM5` is not translated.

**When a term is new, add it here in the same commit that introduces it.** A glossary that is
updated afterwards is a glossary nobody trusts.

## How the counts were produced

Both dictionaries are parsed into `key → text` pairs and joined on the key, which gives 2,505
English and Polish sentences side by side. For a term, every pair whose English contains that word
is selected, and the Polish halves are searched for the candidate translations. The result is a
count per candidate.

It takes about a minute and is worth re-running whenever this file is edited, because the counts are
the argument. A glossary that asserts a preference without saying what the book currently does is a
preference, not a decision.
