# Battle effect family runner

The App test runner exercises externally supplied complete standalone battle snapshots through `BattleSession.AdvanceNativeGraphicsPresentation`, the same entry point as the existing effect presentation tests. It supplies no state defaults, owners, action packets or replay. The manifest and original resources remain external.

Build the App tests once for the unchanged binary set. Pass that build's executable to the launcher; it starts a fresh process for every case and phase and performs no rebuilds. No reset between cases is assumed.

```powershell
./tools/Invoke-BattleEffectFamily.ps1 -Manifest $manifest -GameDirectory $game -TestExecutable $tests -OutputDirectory $output
```

Use a distinct output directory for each invocation. Existing result and fixture files are preserved. The launcher writes per-phase JSON and `summary.json`, exits nonzero for a blocker, and restores its environment variables. Processes execute sequentially.

The neutral JSON manifest uses this shape (entry and callback limits below are illustrative, not native facts):

```json
{
  "Cases": [
    {
      "Id": "case-a",
      "Effect": 1,
      "Entry": 1,
      "InitialSnapshot": "initial.battle.json",
      "ColdSnapshot": "cold.battle.json",
      "ExpectedNextSnapshot": "next.battle.json",
      "MaximumCallbacks": 1,
      "ProcessTimeoutSeconds": 60,
      "TerminationBasis": "Replace with the known native bound and termination condition",
      "ContactPointer": "/NativePartyPresentation/Rows/0/HitPoints",
      "ExpectedContactTransitions": 1,
      "FinalValues": [
        { "Pointer": "/NativeEffectMemory/Active", "Value": 0 }
      ]
    }
  ]
}
```

Paths resolve relative to the manifest directory. Every fixture must be a complete snapshot accepted unchanged by the current engine. The initial and cold snapshots must contain a live action104 descriptor and the installed original effect binding and resources. Supply real entry addresses, an explicit case-specific callback bound, its basis, and all relevant final owner assertions. Observables use JSON pointers with `~0` and `~1` escapes. Missing observables block execution. Zero or multiple observable transitions are allowed: a transition is not necessarily a native contact, and multi-target effects require suitable additional acceptance checks.

Admission also resolves the action104 descriptor's physical header pointer, verifies its effect WORD against the case identity and checks any started action's header identity. A stale completed effect cannot establish initialization: the expected initialized effect must have a live returned task. Every advance must move the native callback counter exactly once.

Each case requires a positive `ProcessTimeoutSeconds` chosen for that fixture; there is no universal timeout default. The launcher redirects standard output/error and enforces that limit on its owned hidden test process. A timeout terminates only that process tree, records a process blocker and preserves partial output. Direct test invocation requires external process supervision to enforce its process timeout.

The `run` phase reports resource loading, admission, observed effect initialization, descriptor completion, live task and active owner release, and observable transition counts. One additional presentation callback checks that the descriptor does not reappear and that the observable stays unchanged. Reaching the bound while the action is live is a blocker. The `cold` phase independently loads its supplied snapshot, checks unchanged roundtrip state, advances exactly one callback and compares the complete JSON state and rendered pixels against the supplied expected-next fixture. An empty pixel match alone does not establish significant graphics restoration.

The run phase can also publish complete initial, first live initialized, corresponding naturally reached next-callback, and completed snapshots. Set `MEMORIA_FF8_EFFECT_FAMILY_FIXTURE_OUTPUT` to an empty output directory; the launcher sets a distinct directory per phase. Every written fixture is immediately independently loaded and checked for an unchanged roundtrip. A later cold process can consume the emitted `cold.battle.json` and `next.battle.json` using a separately prepared manifest. The cold check never derives its reference from its own execution.

Direct invocation uses `MEMORIA_FF8_GAME_DIR`, `MEMORIA_FF8_EFFECT_FAMILY_MANIFEST`, `MEMORIA_FF8_EFFECT_FAMILY_CASE`, `MEMORIA_FF8_EFFECT_FAMILY_PHASE` (`run` or `cold`) and optional `MEMORIA_FF8_EFFECT_FAMILY_RESULT`, filtered to `*NativeBattleEffectFamilyRunnerTests`. Each process must select exactly one case and phase.

`checked` means the requested mechanical phase passed. It is not acceptance of an effect. The result keeps native comparison, native contacts, significant graphics boundaries, camera, sound routing and audibility pending. Complete those obligations through their existing checks and native observations. A blocked resource, missing owner, unsupported callback or mismatch is recorded as the first blocker; it is never equality or accepted coverage.

Each result includes measured phase and callback elapsed seconds, SHA256 identities for consumed fixtures and the App, Runtime, Assets, Rendering and test binaries, and bytes written as standalone fixtures. Callback time includes attempted callbacks that fail; phase time includes loading, hashing and validation. The launcher additionally records each process wall time, exact command and executable SHA256. These measurements have different scopes and must not be added as independent wall time. Snapshot byte totals exclude compact result JSON and optional artifacts produced by other tools. No full callback trace is recorded.
