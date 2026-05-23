# MoreTeams

Adds extra team colors to Bopl Battle so each of 5-8 players can be on their own unique team.

## Designed for FixedMoreBopl

This mod is a companion to [FixedMoreBopl](https://thunderstore.io/c/bopl-battle/p/geddesworks/FixedMoreBopl/), which expands Bopl Battle from 4 to 8 local couch players. FixedMoreBopl handles adding extra player slots, but the vanilla game doesn't have enough teams for everyone to play solo. MoreTeams fixes that by expanding the team palette so all 8 players can each be on their own team.

**You need FixedMoreBopl installed for this mod to be useful.** Without it, you're limited to 4 players and the vanilla teams are already sufficient.

## What it does

Expands the team selector in character select with up to 12 additional visually distinct team colors:

Orange, Pink, Cyan, Lime, Maroon, Teal, Gold, Lavender, Coral, Mint, Navy, Peach

Each new team has its own fill, border, and saturated color variants so they look correct across all UI elements (character select, ability select, win screen, etc.).

## Configuration

Edit `BepInEx/config/com.geddesworks.boplmorecolors.cfg`:

- **ExtraColorCount** (0-12, default 8) — How many extra teams to add beyond the vanilla palette.
- **EnableDiagnostics** (default true) — Log team expansion details to BepInEx console.

## Requirements

- BepInEx 5.x
- [FixedMoreBopl](https://thunderstore.io/c/bopl-battle/p/geddesworks/FixedMoreBopl/) (required dependency)
