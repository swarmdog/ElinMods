# Future Work — Deferred Review Items

Items identified during the code/design review (2026-04-12) that are not being addressed immediately.

## Gameplay Features

### Reforge Recipe Placeholder (D-3)
The Readme lists a "Reforge (Future)" research task at the Astrological Codex, unlocked at Cosmos-Addled rank. No code, data, or recipe entry exists. When implemented, reserve recipe ID `srg_reforge` and add it to the `CanUseRecipe` rank gate.

### Meteor Touched Thing Visual Indicator (D-4)
The tagging system correctly tags both Charas and Things as Meteor Touched, but `GetHeldEmo` only provides visual indicators for Charas. Things on the ground have no visible hint when the player holds an Astral Extractor. Consider adding a proximity message or extending the hint system to items.

### Cosmos-Addled Rank Perk (L-3)
Reaching Cosmos-Addled (1500 GP) currently has no gameplay effect beyond a vague rank-up message. The Readme says it should unlock "Reforge (Future)" and "slightly increase meteor rewards." Consider adding a small perk (e.g., +1 meteorite source from cores, improved post-event odds, or unlock the Reforge recipe when it exists).

### Archivist Services (D-7)
The Readme mentions "special services" from the Astral Archivist but no concrete service design exists. Phase 5 delivered summon + neutral/recruitable NPC. Custom dialog, identification services, or lore interactions need a separate design spec.

## UX / Polish

### Portal Auto-Enter (L-4)
`TraitAstralPortal` has `AutoEnter => true`, meaning the player is instantly teleported to the astral rift by stepping on the portal tile — no confirmation dialog. In a busy town, this could be frustrating. Consider setting `AutoEnter => false` or adding a confirmation.

### Starchart Drop Rate (L-2)
The RNG check for Starchart drops from Yith Growth is commented out, making it a 100% drop rate. This is intentional for testing. **Revert to ~20% before release** by uncommenting the check in `SpawnLootPatch.HandleYithGrowthDrop`.

### FindSummonPoint Duplication (P-3)
`TraitBossScroll` and `TraitArchivistScroll` contain identical `FindSummonPoint` methods. Extract to a shared static helper when convenient.

### TraitBossScroll - multi stage randomized summoning messages (which should probably take longer than whatever the default read time is)