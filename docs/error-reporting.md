# Reporting an error

Select **Report error** in the error window. Memoria prepares one local ZIP, reveals it in the file manager and opens a filled GitHub issue draft. Sign in to GitHub if needed, drag the ZIP into the issue body, wait for the upload and select **Submit new issue**. Reports and attachments are public. You may add what you remember doing directly in GitHub.

Opening the draft does not attach the file or submit an issue. If login loses the text, use **Open GitHub again** or **Copy full description**. **Show ZIP**, **Copy report path** and the selectable URL remain available when the browser or file manager cannot open. Memoria never copies anything to the clipboard without your action. Offline preparation still works; the browser needs a connection to GitHub later.

Reports live in the user data directory resolved by `ApplicationPaths`, under `reports`. The latest ten completed reports are retained; reports being prepared or displayed are protected. Ordinary save banks are untouched. A retained incident can be offered by the launcher after the game renderer exits.

The ZIP includes `report.md`, the complete available exception tree in `exception.txt`, detached `runtime.json` and `environment.json`, a bounded semantic history in `events.jsonl`, relevant logs and `manifest.json`. A validated reproduction save, when available, is included as `repro/state.json`. Missing observations, save failures and size omissions are recorded. Native termination may provide only process/bootstrap evidence. A managed stack is not invented for such failures.

The package budget is 20 MiB. Older log/history data is omitted first. If the save still cannot fit, the ZIP explains its exclusion and the full save remains local. A save is never truncated. The complete description and exception remain available locally even when the browser URL must use shorter text.

## Using a reproduction save

Read `report.md` and `manifest.json` first. Use matching game edition, language, mods and resources. Only a save explicitly marked validated is an eligible continuation; an earlier save may precede the failure and does not guarantee reproduction. Faulted or unsupported state can produce diagnostics without a loadable save.

Extract the ZIP into a separate local directory and launch the game with `--state <absolute-path-to-repro/state.json>` using the engine executable and the required game resource arguments. This is a normal game launch: use a separate configured save/settings directory and disable platform achievements for investigation. It is not an isolated replay mode. Preserve your ordinary save banks and settings before testing.

For example, run the engine from the distribution root with `./Data/Memoria.FF8.exe --state <report-directory>/repro/state.json --game-dir <matching-game-directory> --settings-dir <separate-settings-directory> --save-state <separate-save-directory>/checkpoint.json --achievements-local-only`. Review the save-bank settings in that separate directory before writing any menu saves; a settings override alone does not guarantee an isolated player-save bank. See the [command-line reference](reference/command-line.md).

## GitHub handoff

The reporter uses only the fixed repository issue URL and the documented `title` and `body` query parameters. The complete percent-encoded URL is capped at 2,000 ASCII characters; full text stays in the ZIP and the copy action. No account token, reporting service, GitHub API submission, browser automation or automatic upload is involved.

GitHub documents [prefilled issue URLs](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/creating-an-issue#creating-an-issue-from-a-url-query) and [browser attachment support and limits](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/attaching-files). ZIP files require a manual drop or file selection. The application can observe preparation and browser launch, but cannot observe upload or issue submission.

For manual acceptance, use synthetic diagnostics and an explicitly designated test repository. Check both signed-in prefill and signed-out login, reopen after login, one ZIP upload and final submission. Do not post test issues to the production repository. These authenticated checks require a tester's browser/account; portable automated tests cover local capture, archive contents, bounded Unicode URLs and failure handling.
