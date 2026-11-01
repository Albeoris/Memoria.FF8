# Junction tutorial snapshots

Junction lessons use the same dashboard, candidate previews, consequences review and assignment transaction as ordinary play. The original lesson WORD program still supplies its input identities, callback delays and continuation boundaries. Its input targets are adapted to semantic dashboard controls. The stat lesson demonstrates **Optimize stats** with the current stat-only constraints; the original Auto algorithm remains a separate engine contract.

Tutorial playback snapshot version **2** records the dashboard destination, candidate, review, priority, focus, paging, search, sort and both effect-table directions. Restoring a pending preview reconstructs its proposal from the saved isolated party and current authenticated rules. The committed assignment and statistics remain visible until Apply.

Version **1** is accepted through a narrow in-memory adapter when its resource hash, initialization hashes, immutable definitions and program boundary still match:

- Only the isolated tutorial copy is adapted. The supplied snapshot JSON and the loaded game party remain unchanged. Saving playback afterward produces version 2; no archive or external save file is rewritten.
- For Junction lessons 0–3, nonzero command bytes below 20 are original battle-command identities. Each is translated through the inverse of the authenticated `CommandAbilityCommands` table to the corresponding learned ability identity. Zeroes, command positions, the fourth saved command byte, and existing ability identities of 20 or greater are retained. Missing or ambiguous mappings fail with an explicit unsupported reason.
- Recognized earlier Junction navigation is reconstructed from the current prepared semantic input, or the last completed semantic input. Preparing or highlighting a choice recreates a preview without applying it. A completed Apply restores ordinary dashboard focus and is never replayed. A completed picker-opening input may reopen that picker without changing assignments.
- An earlier magic-picker snapshot must identify the same explicit candidate and destination as its resolved semantic input. Unknown screens, unexpected character selection, or a picker state without an unambiguous candidate/destination are rejected instead of guessing.
- Other lessons retain their existing navigation and isolated state; they are written back with the current playback version.

This adapter is exercised with synthetic earlier snapshots built from original tutorial resources, including a pending magic preview, raw command identities, unchanged input JSON and an independent loaded party. Current playback tests also restore every lesson at input and delay boundaries. Previously stored snapshots are not batch migrated.
