# Reporting an error

Select **Report error** in the error window. Memoria prepares one local ZIP, reveals it in the file manager and opens a filled GitHub issue draft. Sign in to GitHub if needed, drag the ZIP into the issue body, wait for the upload and select **Submit new issue**. Reports and attachments are public. You may add what you remember doing directly in GitHub.

Opening the draft does not attach the file or submit an issue. If login loses the text, use **Open GitHub again** or **Copy full description**. **Show ZIP**, **Copy report path** and the selectable URL remain available when the browser or file manager cannot open. Memoria never copies anything to the clipboard without your action. Offline preparation still works; the browser needs a connection to GitHub later.

Reports live in the user data directory resolved by `ApplicationPaths`, under `reports`. Each completed report directory contains only its ZIP. Recovery evidence is staged in the separate private sibling directory `reports.pending`; successful packaging removes the staged incident and save. A small presentation marker may remain there to avoid offering the same incident again. The latest ten completed reports are retained; reports being prepared or displayed are protected. Ordinary save banks are untouched. A retained incident can be offered by the launcher after the game renderer exits.

The ZIP includes `report.md`, the available exception tree in `exception.txt`, detached `runtime.json` and `environment.json`, a bounded semantic history in `events.jsonl`, `manifest.json` and recovery metadata. Exported diagnostic text and JSON normalize local paths to neutral labels and redact recognized private fields and credentials. Review the ZIP before sharing: arbitrary free-form diagnostic text can contain information beyond the recognized patterns. Native termination may provide only process/bootstrap evidence. A managed stack is not invented for such failures.

Automatic reports exclude complete reproduction saves and raw log files. Saves can contain account identity, player names and opaque native buffers; arbitrary logs can contain private identifiers or credentials that cannot be reliably scrubbed. The report records these omissions without copying the raw files or their names into the ZIP. Manual sharing of saves or logs requires a dedicated privacy review; an automatic report is not a reviewed save-sharing package.

The package budget is 20 MiB. Semantic history may be omitted to fit this limit. Required exception and context that cannot fit cause preparation to fail while pending recovery evidence remains local. Successful packaging does not retain loose incident, save, description, completion or lease files beside the ZIP. The complete sanitized description remains in the ZIP and copy action even when the browser URL uses shorter text.

## GitHub handoff

The reporter uses only the fixed repository issue URL and the documented `title` and `body` query parameters. The complete percent-encoded URL is capped at 2,000 ASCII characters; full text stays in the ZIP and the copy action. No account token, reporting service, GitHub API submission, browser automation or automatic upload is involved.

GitHub documents [prefilled issue URLs](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/creating-an-issue#creating-an-issue-from-a-url-query) and [browser attachment support and limits](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/attaching-files). ZIP files require a manual drop or file selection. The application can observe preparation and browser launch, but cannot observe upload or issue submission.

For manual acceptance, use synthetic diagnostics and an explicitly designated test repository. Check both signed-in prefill and signed-out login, reopen after login, one ZIP upload and final submission. Do not post test issues to the production repository. These authenticated checks require a tester's browser/account; portable automated tests cover local capture, archive contents, bounded Unicode URLs and failure handling.
