# Relations, interactions and alliances

The research behind the next mechanic, and the design it argues for. Written 2026-09-27.

**What exists today.** `Simulation/RivalRelations.cs` has been in the game since 29 August: a scale
from -100 to +100, five bands (Hostile, Cold, Tense, Neutral, Friendly), a drift of 0.035 a day back
toward each lab's baseline, and a forty-entry memory of what moved it and why. `RivalPanel` draws it.

**And every single thing that moves it is something you did to them.** Seven recorders, all negative:
a smear landing, a smear traced back, a lawsuit filed, an acquisition refused, a lab bought, a person
poached. There is no way to improve a relation, and a good relation buys the player nothing at all.
A player who never attacks anybody sits at Neutral with fourteen labs for fourteen years and the
whole system is invisible to them.

That is the gap. The rest of this document is what the real industry did about it, and what of that
belongs in the game.

---

## 1. What labs actually do with each other

Five shapes, and the game has a shadow of exactly one of them.

### Compute, bought from somebody who also owns a piece of you

The largest deals in the industry are not model deals, they are capacity deals, and the money runs
back and forth between the same names.

- Microsoft put **more than $13bn** into OpenAI, the largest tranche $10bn announced in early 2023,
  explicitly so OpenAI could get the compute it needed.
- Amazon and Google agreed to invest **up to $4bn and $2bn** in Anthropic. AWS became Anthropic's
  primary cloud provider in 2023 and its primary training partner in 2024.
- In late 2025 Microsoft and Nvidia said they would put **up to $15bn** into Anthropic ($5bn and
  $10bn), and Anthropic committed to buying **$30bn** of Azure capacity.
- OpenAI said it would buy **$250bn** of cloud from Microsoft and signed a **$38bn** compute deal
  with AWS.

The shape worth taking: **capacity is the thing that gets traded, and the trade is mutual.** Nobody
gives compute away; they sell it at a price that is also a claim on you.

### Distribution, where somebody else's channel carries your model

- Snowflake and OpenAI: a multi-year **$200M** partnership putting OpenAI models inside Snowflake's
  own products. Snowflake signed a **$200M** deal with Anthropic in the same period.
- Microsoft and Mistral: a **€15M** investment plus distribution through Azure.
- Snowflake and Mistral: Mistral Large to Snowflake's enterprise customers.
- Samsung led a Mistral round, buying access for phones, appliances and semiconductors.

The shape worth taking: **reach is rentable.** A smaller lab can borrow an audience it has not
earned, and pays for it in margin rather than in cash.

### Joint safety research, which is rivals paying into one pot

- The **Frontier Model Forum**, announced July 2023 by Anthropic, Google, Microsoft and OpenAI: an
  industry body for evaluations, benchmarks and a public library of practice.
- An **AI Safety Fund** of **over $10M**, the four members plus philanthropic partners.

The shape worth taking: **the pot is bigger than any member's share of it**, which is the entire
argument for joining one.

### Cross-evaluation, the one that only happened because both sides agreed

- August 2025: OpenAI and Anthropic ran **a first-of-its-kind joint safety evaluation**, each
  running its own internal evals against the other's public models, with special API access and
  some external safeguards relaxed. Published in parallel on 27 August 2025.
- They were reported to have negotiated a **legally binding** version of it.

The shape worth taking: **two rivals can co-operate on exactly one subject and compete on everything
else**, and the co-operation is what makes both products better.

### Licence-and-hire, which is the one the game already models

- Microsoft and Inflection: **$650M** to license the models and hire the team.
- Google and Character.AI: about **$2.7bn** for a non-exclusive licence and the founders.
- Amazon and Adept: a **$25M** licence fee and roughly two thirds of the staff.
- Google and Windsurf: **$2.4bn**, for the founders.

The game has `AcquisitionOffer`, poaching, and three lab dossiers that come apart this way. This is
the covered ground.

---

## 2. What to build

Four layers. Each one is refusable, each costs something, and none of them removes timing pressure,
which is the line every proposal in this project has to clear.

### Layer 1: interactions. Ways a relation goes up

Five, priced against what they are worth. They are **offers to another company**, so each can be
refused, and whether it is accepted reads the same state the game already keeps: their band, their
standing against yours, and whether they are the kind of lab that says yes.

| Interaction | Costs | Moves the relation | Refused when |
|---|---|---|---|
| Publish a finding | research points | +6 | never; it is a gift |
| Cross-evaluate a model | 20 days of one safety person, both sides | +14 | you have no live model |
| Licence them a corpus | a corpus you own | +12, and cash | they hold it already |
| Buy capacity from them | cash, at a premium over the market | +10 | they have none spare |
| Settle a suit early | the settlement | +18 from Hostile | no suit is open |

**Publishing a finding is the cheap one and it has to stay cheap**, because it is the only move a
player with nothing can make, and a relation system that opens at a million dollars is a system the
early game cannot see.

**None of them is instant.** Every one is an offer that lands in the inbox and is answered in days,
for the reason `RegulatoryAction` waits five days for its verdict: an outcome that arrives on the
click is an outcome the player cannot plan around.

### Layer 2: alliance levels, which are earned in days rather than bought

An alliance is not a band. The band is what they think of you today; the alliance is what you have
signed, and it ratchets: **time at a band is the gate, money is only the fee.**

| Level | Needs | Fee | Both sides get |
|---|---|---|---|
| 0 None | | | |
| 1 Understanding | Friendly for 90 days | $250k | the phone, and their capability a month early |
| 2 Working group | level 1 for 180 days | $1.5M | joint research campaigns open |
| 3 Deep alliance | level 2 for 365 days, no hostile act between | $6M | capacity at cost, one shared release |

**Levels can fall.** A hostile act drops it by one immediately, which is the asymmetry the whole
system needs: two years to build, one afternoon to lose. Same shape as `Standing`, where fans
arrive at 0.004 a day and leave at 0.0012.

**The 365 days at level 2 is the important number.** It cannot be paid for, it cannot be rushed, and
it means a deep alliance is a decision made in year two that pays in year four. That is the spine of
this game applied to a relationship: not purchased, timed.

### Layer 3: the joint research consortium

This is the one the author asked for by name, and the Frontier Model Forum is the thing it is
modelled on: **members pay in, and what comes out is larger than any one member put in.**

A **research campaign** is a named programme with a term. It pays **research points daily over its
whole length** rather than a lump at the end, which is what makes it feel like a laboratory rather
than a purchase, and it is the same shape `ResearchProject` already has.

Proposed numbers, to be measured with `DeepCampaignProbe` before they are believed:

| | Alone | In a consortium of two | of three |
|---|---|---|---|
| Points a day | 1.0x | **2.2x** | **3.0x** |
| Cash a day | 1.0x | 0.6x each | 0.45x each |
| Effective value | 1.0x | **3.7x** | **6.7x** |

So two labs each paying 60% of what one would pay get 2.2 times the points: **the money goes about
three and a half times as far**, which is the "2x-3x drożej samemu" the author asked for, measured
from the other side.

Three rules hold it up:

1. **A member who leaves early forfeits the term's remaining points and pays a break fee.** Without
   that, joining and leaving on the last profitable day is the dominant line.
2. **The multiplier is on points, never on cash.** A consortium that made money would be an income
   guarantee, which is against the design.
3. **Membership is public.** It shows on the ranking board and on the wire, because everybody in the
   real ones announced them, and because a rival deciding whether to attack you should be able to
   see who would be annoyed.

### Layer 4: the telephone

**A lab you have ever reached level 1 with is callable, forever, whatever happens afterwards.**
That is the author's own idea and it is the right one: the phone is the game's one warm surface, and
a number you keep after a friendship cools is exactly how that works in life.

`PhonePanel.RingAbout` already exists for the cousin, and `RingFrom` for a stranger. A lab you have
signed with sits between the two: its own thread, kept, but not the cousin's.

---

## 3. What this must not become

- **Not a second economy.** `RivalExpansion` is one multiplier because giving fourteen labs a fleet
  and a payroll means inventing two dozen uncheckable numbers per company. An alliance moves the
  numbers that already exist.
- **Not a guaranteed income.** Everything above pays in points, capacity, reach or time. Nothing
  pays a dividend.
- **Not a way to skip a calendar gate.** A consortium makes research cheaper per point. It does not
  open a node before its date, and it must never be allowed to.
- **Not fourteen relationships to manage daily.** The interactions are offers with terms, not a
  meter to top up. If this ever needs a daily click per lab, it is wrong.

---

## 4. Order of work

1. `RelationOffer` and the five interactions, with the inbox answering them. Relations can go up.
2. `Alliance` levels and the ratchet, saved.
3. Research campaigns and the consortium.
4. The phone, the wire, and the ranking board showing who is signed with whom.

Balance for step 3 is measured, not asserted: `DeepCampaignProbe` gets a fourth operator that
allies, and the multipliers above move to whatever that measurement says.

---

## Sources

- [Microsoft, Nvidia partner with Anthropic (CIO Dive)](https://www.ciodive.com/news/Microsoft-ignite-anthropic-nvidia-AI-agents-partnership/805831/)
- [Anthropic valued at $350bn after Microsoft and Nvidia deal (CNBC)](https://www.cnbc.com/2025/11/18/anthropic-ai-azure-microsoft-nvidia.html)
- [The billion-dollar infrastructure deals powering the AI boom (TechCrunch)](https://techcrunch.com/2026/02/28/billion-dollar-infrastructure-deals-ai-boom-data-centers-openai-oracle-nvidia-microsoft-google-meta/)
- [Partnerships between cloud service providers and AI developers (FTC)](https://www.ftc.gov/system/files/ftc_gov/pdf/p246201_aipartnerships6breport_redacted_0.pdf)
- [Introducing the Frontier Model Forum](https://www.frontiermodelforum.org/updates/announcing-the-frontier-model-forum/)
- [Frontier Model Forum executive director and AI Safety Fund (Google)](https://blog.google/company-news/outreach-and-initiatives/public-policy/google-microsoft-anthropic-open-ai-frontier-model-forum-executive-director/)
- [Findings from a pilot Anthropic-OpenAI alignment evaluation exercise (OpenAI)](https://openai.com/index/openai-anthropic-safety-evaluation/)
- [Snowflake and OpenAI forge $200 million partnership (Snowflake)](https://www.snowflake.com/en/news/press-releases/snowflake-and-openAI-forge-200-million-partnership-to-bring-enterprise-ready-ai-to-the-worlds-most-trusted-data-platform/)
- [Snowflake partners with Mistral AI (Snowflake)](https://www.snowflake.com/en/news/press-releases/snowflake-partners-with-mistral-ai-to-bring-industry-leading-language-models-to-enterprises-through-snowflake-cortex/)
- [What is Mistral AI (TechCrunch)](https://techcrunch.com/2026/07/04/what-is-mistral-ai-everything-to-know-about-the-openai-competitor/)
- [Google's hiring of Character.AI's founders (Fortune)](https://fortune.com/2024/08/02/google-character-ai-founders-microsoft-inflection-amazon-adept/)
- [Amazon hires execs from Adept and licenses its technology (CNBC)](https://www.cnbc.com/2024/06/28/amazon-hires-execs-from-ai-startup-adept-and-licenses-its-technology.html)
