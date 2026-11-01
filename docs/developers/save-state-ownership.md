# Save-state ownership

Save lifetime and data authority are separate contracts. A process may retain physical bytes after their former resource owner has been invalidated. Those bytes can also be shared with another scene. Their presence does not authorize treating them as a live model.

`SnapshotOwnershipAttribute` declares a member's lifetime, authority and reconstruction operation. Unclassified members remain saved. A newly declared derived member requires an implemented reconstruction and persistence policy; unsupported declarations fail during serializer configuration.

The first classified member is `NativeBattleModelCpuResourceRow.Lengths`:

| Boundary | Policy |
| --- | --- |
| Save outside an active battle | Omit dormant derived resource extents without changing live state. |
| New battle | Invalidate derived extents when resource cache ownership is invalidated. |
| Model load | Rebuild section extents from the selected resource and the actual load operation. |
| Appended weapon geometry | Extend the body extent through the installed appended data. |
| Save or restore during a battle | Preserve current extents until a reconstruction contract for live allocations replaces them. |

Shared CPU banks, staging, knownness, pointer fields and storage graph aliases retain their existing authority. Saving does not clear those physical owners. Resource rows, graphics state and immutable resource definitions should be audited separately before their persistence changes.

Further cleanup must provide a resource identity and reconstruction recipe, preserve mutable deltas, and reconstruct directly at the saved boundary. Restoration must not replay battle entry, advance callbacks, repeat effects or consume random numbers. Test inactive field/world continuation, active battle continuation, pending loads, aliased resources and patched geometry. A format change that cannot reconstruct an older live state needs an explicit migration of the selected save.
