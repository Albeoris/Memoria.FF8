# Scheduled battle presentation

The graphical host accepts an optional `presentation` block inside a `--battle-test` recipe's existing `driver`. This mode admits one ordinary semantic invocation. The default diagnostic protocol still runs twice. Presentation mode cannot compose fresh-entry scoring, Shot/Counter cycles, combat repairs or terminal holds.

```json
{
  "presentation": {
    "signal_path": "start.json",
    "receipt_directory": "receipts",
    "run_id": "unique-coordinator-run",
    "fill_selected_actor_atb": true,
    "tail_seconds": 3
  }
}
```

Use absolute paths when the coordinator and host have different working directories. Paths for `ready.json`, `started.json`, `completed.json` and `blocked.json` must be fresh. Other diagnostic outputs may share the receipt directory. The coordinator supplies the existing complete battle recipe, installed resources, `--resolution`, and optional `--mute-audio`; this block supplies no game content or fallback state.

After genuine entry and drain, presentation mode fills only the selected player's existing raw ATB current value to its actual maximum and invokes normal full-gauge registration. Other actors retain their gauges. This preparation admits no command or effect, and refuses compelled or script-owned player commands. Enemy-ability presentation keeps its existing readiness contract. Set `fill_selected_actor_atb` to `false` to retain natural gauge growth; its default is `true` only when a presentation block exists.

After target verification, command readiness, drain and the declared baseline, the host publishes `ready.json`. Native battle advancement and interactive battle input are held while drawing continues. No focal command is submitted before release. Start the recorder, retain any desired leading frames, then atomically publish this signal by renaming a complete temporary file:

```json
{
  "run_id": "unique-coordinator-run",
  "start_at_utc": "2030-01-01T12:00:05Z"
}
```

The target must be in the future when the host first reads it. A mismatched identity, malformed signal or lost readiness blocks execution. Choose enough scheduling lead for every endpoint to read the signal. Submission happens on the first presented host frame at or after the shared UTC target; this protocol measures actual skew and does not promise simultaneous frame delivery across processes.

Lifecycle receipts are immutable JSON files with `schema_version`, `run_id`, `pid`, `expected_effect`, `state`, `utc`, `start_at_utc`, `callback`, `frame` and nullable `blocker`. `started.json` records UTC immediately after the semantic command submission returns; it is not an effect-dispatch timestamp. The existing diagnostic telemetry retains actual effect-dispatch identity and completion checks. `completed.json` appears only after the verified focal dispatch, complete drain and configured `tail_frames`. The host then holds the final battle image for `tail_seconds` before normal diagnostic finalization and exit. Set that tail long enough to cover the other endpoint and recording stop. `blocked.json` identifies a failed driver prerequisite or deadline. Overall driver timeout still bounds waiting and the trailing hold.

These receipts coordinate presentation and recording. They do not establish native equivalence or effect acceptance. The coordinator must check matching effect identities, recording state, measured start skew and both completion receipts.
