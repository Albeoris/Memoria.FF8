# Junction tutorial snapshots

Junction lessons use the same dashboard, candidate previews, consequences review and assignment transaction as ordinary play. The original lesson WORD program supplies its input identities and continuation boundaries. Playback resolves input targets to semantic controls and gives each scripted menu action 18 callbacks on the 60 Hz menu clock. A visible lesson dialog waits for its own Confirm press. Junction guidance uses the current dashboard vocabulary and combines repeated original pages.

Tutorial playback snapshot version **2** records the dashboard destination, candidate, review, priority, focus, paging and both effect-table directions. Its retained search field is empty and earlier search values are ignored because Junction has no search. Restoring a pending preview reconstructs its proposal from the saved isolated party and current authenticated rules. Assignments remain committed until Confirm; preparing a GF step only focuses its portrait and never clicks it. Card/detail opens secondary actions without a footer button.

The optional `ActionDelay` field records the remaining callbacks for a prepared pointer action. Earlier snapshots without it resume a prepared action on the next callback. A saved Junction dialog that was folded into the updated guidance releases its old confirmation gate while keeping the saved program counter and party intact.

All lessons project battle commands as learned ability identities. On initialization and restore, nonzero command bytes below 20 are translated through the inverse of the authenticated `CommandAbilityCommands` table. Zeroes, command positions, the fourth saved command byte, and existing ability identities of 20 or greater are retained. Missing or ambiguous mappings fail with an explicit unsupported reason.

The GF steps target acquired GF portraits directly. Earlier version 1 or 2 dashboard snapshots that reference the removed GF-management button have their recognized focus/origin mapped to the first acquired GF portrait. The selected candidate and isolated party remain unchanged; this is a navigation adaptation without rewriting external snapshot files.

Version **1** is accepted through a narrow in-memory adapter when its resource hash, initialization hashes, immutable definitions and program boundary still match:

- Only the isolated tutorial copy is adapted. The supplied snapshot JSON and the loaded game party remain unchanged. Saving playback afterward produces version 2; no archive or external save file is rewritten.
- Recognized earlier Junction navigation is reconstructed from the current prepared semantic input, or the last completed semantic input. Preparing or highlighting a choice recreates a preview without applying it. A completed Apply restores ordinary dashboard focus and is never replayed. A completed picker-opening input may reopen that picker without changing assignments.
- An earlier magic-picker snapshot must identify the same explicit candidate and destination as its resolved semantic input. Unknown screens, unexpected character selection, or a picker state without an unambiguous candidate/destination are rejected instead of guessing.
- Other lessons retain their existing navigation and isolated state; they are written back with the current playback version.

This adapter is exercised with synthetic earlier snapshots built from original tutorial resources, including a pending magic preview, raw command identities, unchanged input JSON and an independent loaded party. Current playback tests also restore every lesson at input and delay boundaries. Previously stored snapshots are not batch migrated.
