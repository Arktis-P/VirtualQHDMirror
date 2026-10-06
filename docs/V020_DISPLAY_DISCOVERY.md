# v0.20 Display Discovery and Topology Assessment

## Scope

This first public-release stage is intentionally **machine-shape agnostic**.

VQM must work on:

- a laptop with an internal panel plus one or more external monitors,
- a desktop with only external monitors,
- a system with one physical low-resolution target,
- a system with two or more physical low-resolution targets,
- one or multiple compatible virtual QHD displays.

The discovery layer does not assume that an internal panel exists, and it does not treat an internal panel as globally ineligible.

## Separation of responsibilities

The first stage now separates three questions.

### 1. What displays exist?

Use Windows CCD APIs to enumerate every active display path and capture stable identity, resolution, desktop position, connector type, friendly name, device path, and EDID availability.

### 2. What kind of display is each one?

Each path is classified conservatively as:

- `Physical`
- `Virtual`
- `Unknown`

`Internal` / `External` is stored only as placement metadata. It is **not** the same thing as physical/virtual and it is not a hard exclusion rule.

A desktop normally has zero Internal displays. That is valid.

### 3. How many mirror pairs can this topology support?

For the initial v0.20 scope:

- virtual workspace candidate: active Virtual display at 2560 × 1440,
- physical target candidate: active Physical display at 1920 × 1080,
- internal FHD panels are allowed to appear as candidates,
- external FHD monitors are allowed to appear as candidates.

No pairing is performed automatically unless there is **exactly one virtual QHD source and exactly one physical FHD target**.

## Why internal panels are no longer excluded

The previous draft treated an internal laptop panel as something to always exclude from mirroring targets. That assumption was specific to the development laptop.

For a public application:

- a desktop may have only external displays,
- a laptop may have an FHD internal panel that the user actually wants to use,
- a laptop may have both an internal FHD panel and several external FHD displays.

Therefore Internal/External is a preference signal for later UI or remembered user choice, not a discovery eligibility rule.

## Multiple low-resolution physical monitors

Let:

- `P` = number of physical FHD targets,
- `V` = number of active virtual QHD sources.

The first-stage assessment is:

| P | V | Meaning |
| --- | --- | --- |
| 0 | any | no usable physical destination |
| any | 0 | virtual QHD source missing |
| 1 | 1 | safe single-pair auto selection |
| 2+ | 1 | ambiguous: choose one physical target, clone one virtual source, or create more virtual sources |
| 1 | 2+ | ambiguous: choose which virtual source drives the one physical target |
| 2+ | 2+ | multi-session topology; up to `min(P,V)` independent 1:1 pairs are possible |

For **independent expanded workspaces**, the desired long-term form is generally:

`physical FHD A ↔ virtual QHD A`  
`physical FHD B ↔ virtual QHD B`

That is different from sending one virtual QHD source to both physical monitors, which only duplicates the same workspace.

If `P > V`, discovery reports how many additional virtual QHD sources would be required to give every physical FHD target an independent 1:1 workspace.

## Automatic selection rule

Current rule:

- `P = 1` and `V = 1`: one pair may be recommended.
- every other topology: enumerate all candidates, expose the topology state, and make no automatic pairing decision.

This intentionally avoids guessing that:

- the first enumerated display is preferred,
- an external monitor is always preferred over an internal panel,
- every physical monitor should be mirrored,
- every user wants duplicate output,
- every user wants multiple independent virtual workspaces.

Those become configuration/UI decisions in a later stage.

## Current resolution scope

The raw inventory supports arbitrary active resolutions.

The **default mirror candidate policy** is still FHD physical (1920 × 1080) → QHD virtual (2560 × 1440), because that is the product behavior currently proven by v0.14.1.

The inventory itself is resolution-agnostic. "Low-resolution physical monitor" is therefore a broader future concept than the current FHD candidate profile. 1920 × 1200, ultrawide, 1366 × 768 and other physical resolutions should be added as explicit target profiles after their scaling/input behavior is validated, rather than silently treating every display below QHD as equivalent.

## Validation scenarios

The discovery stage should eventually be checked against at least:

1. laptop internal + one external FHD + one virtual QHD,
2. desktop with one external FHD + one virtual QHD,
3. desktop with two external FHD + one virtual QHD,
4. desktop with two external FHD + two virtual QHD,
5. laptop with internal FHD + external FHD + one virtual QHD,
6. multiple physical monitors but no virtual display,
7. virtual display present but no FHD physical target,
8. ambiguous/unknown indirect display path.

The current .NET shell displays Kind, Placement, Resolution, Connector, Source and classification reason so these cases can be inspected without changing the working v0.14.1 mirror core.
