# Excluded runtime ownership

Gameplay saves contain logical field/world continuation and progression. Full standalone snapshots also contain process memory. Removing a process-memory block from a gameplay save requires an authority for every nested owner; calling the whole block runtime is insufficient.

`GameplaySaveRootOwnership` lists root persistence responsibilities. `GameplaySaveRuntimeOwnership` partitions mixed excluded blocks, with a restoration category and concrete source for each direct member. Homogeneous descendants inherit the partition's source: for example, a retired allocation's byte ranges share its allocation lifetime. An external execution profile or logical continuation must be separated from cold loader data even when they live in the same record.

| Category | Restoration obligation |
| --- | --- |
| Composite | Explicitly partition the members of this type. |
| SelectedResources | Obtain values from the selected executable/resources or current schema initializer. |
| FreshProcessReset | Deliberately begin a new normal-load process epoch; identify its factory. |
| DeferredProducer | Remain absent until the named active-operation producer runs; supported capture rejects active battles. |
| SelectedEnvironment | Select externally for a fresh process and retain the existing owner across same-process reconstruction. |
| LogicalContinuation | Capture a resource-free semantic projection and restore it before the next consumer callback. |
| Derived | Rebuild an inspection/canonical alias from its current authoritative owners. |
| RejectedActiveOperation | Reject capture/load at an unsupported active boundary. |
| CompatibilityGate | Historical wire alias with an explicit rejection or canonical projection. |

The reviewed `GameplaySaveRuntimeSchema.txt` in App.Tests freezes the recursively reachable native member graph, including collection element/value types, property types, nested nullability, required members, wire names, conditional omission and property converters. This is a source schema containing names and types, never captured game data. Adding, removing or changing a nested member fails the inventory even if its root already has a category. New JsonInclude fields/nonpublic properties fail explicitly until a field-specific contract is implemented. Type-name collisions also fail.

When changing an excluded type:

1. Follow every affected root-to-member path and decide its lifetime and authority. Use a composite partition when descendants have different sources.
2. Add or amend the explicit partition and its restoration source. For a homogeneous child, re-review the inherited source against the changed field. Do not approve a field just by updating the schema.
3. Add a meaningful nondefault capture/load/next-callback regression if logical or external state changes. Portable partition tests compare every classified logical field against the source and every reset field against the initialized owner.
4. Update only the reviewed type entries after that review. The schema test reports the previous and current signatures. There is no automatic acceptance of the currently reflected graph.

Focused checks:

```powershell
dotnet run --project tests/Memoria.FF8.App.Tests -c Release -- --filter-class '*GameplaySaveRuntimeOwnershipTests' --filter-class '*GameplaySaveFieldPresentationTests' --filter-class '*GameplaySaveProcessPresentationTests' --filter-class '*GameplaySaveExecutionStackTests'
```

The field/world continuation checks require `MEMORIA_FF8_GAME_DIR`; missing optional game resources are reported as skips. These checks do not run battle-effect algorithms. Follow the existing effect-test scope when a change also affects an effect implementation.

New captures set `GameplaySaveProgression.RuntimeOwnershipVersion`. Both normal player loading and the lower-level state restoration require the current marker. Older untagged semantic documents remain readable for inspection/export but require an explicit migration from an authoritative full standalone before gameplay. Their missing hidden bar initialization, shake phase, secondary music or salary popup cannot be recovered by guessing defaults. Current tagged captures may omit presentation records that are known cold. Full standalone snapshot loading is unchanged.
