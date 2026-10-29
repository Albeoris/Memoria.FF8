# In-game localization workbench

[Documentation](../README.md) / Players

Open **Debug Room → Localization** to edit an existing translation project while viewing its text in the game. The workbench uses keyboard and mouse. Its authoring mode starts **Off** each time the application starts; remembering a project does not enable the mode.

Choose **Editing mod** and check its package/project identity, location, Target and writable state. Disabled localization packages can be selected. Saving always writes that selected project. If the project is read-only, use the existing localization patch/copy workflow; saves are not redirected to another package.

For project creation and JSON/XLSX conversion, see [Localization projects](localization.md). For file formats and validation contracts, see the [translator guide](../modding/localization.md).

## Pick and navigate

Turn authoring mode on. **Pick text** highlights bound text and shows its canonical key. Click to open its editor. Picking captures the click before the underlying game button activates. When several independently bound fragments overlap, choose the intended key from the candidate list.

Switch the toolbar to **Navigate** to use ordinary game/menu pointer controls. **F10** switches between Pick text and Navigate by default. Workbench controls retain their own input ownership. Journal browsing does not pause the whole game; editing or dragging an active occurrence captures its presentation safely. Closing the editor drains its closing input before normal controls resume.

The editor shows the selected package, complete key, category/file and context. A formatted label opens its **template**: for example, `Lv {0}` retains `{0}` while the captured sample can show `Lv 12`. Player names, quantities and other runtime arguments are not copied into the saved template. Player-assigned names remain save data.

Text baked into images follows the artwork workflow. Runtime-only values have an owner rather than an invented translation key. Operating-system dialogs are outside the game-text picker.

## Understand the language roles

| Role | Purpose |
| --- | --- |
| Target | The complete editable translation, including any existing leading presentation prefix. |
| Source Reference | The explicitly designated dataset to translate from. Its identity is independent of tab/column order. |
| Source Scripts / base | The dataset that determines original row inventory, keys and script/call contracts. |
| Other references | Additional read-only comparisons in the project's declared order. |

Tabs use Target, Source Reference, Source Scripts/base, then other references. When two reference roles identify the same dataset, one reference tab retains both roles. Target remains distinct even when its language label matches a reference.

Older projects may show an inferred Source Reference. Review that role in project details and select the intended reference before a deliberate project save. Reading an older project alone does not rewrite it; reference display order remains independent.

An empty or absent Target displays a marked inherited value: designated Source Reference, then base text, then **No text available**. Opening a row, inspecting a reference, switching tabs and entering Preview do not save that inherited text. Unchanged empty Target remains empty.

The first editing operation can seed the Target draft from its designated fallback. Editing from a reference tab returns to Target and applies that first input to Target's own draft and selection. Navigation, selection, Copy and language shortcuts remain usable in references. **Use as draft** explicitly starts from the inspected reference. Returning to Target restores its draft, caret, selection and draft-local undo history.

The editor and journal select languages independently. **Alt+1**, **Alt+2** and **Alt+3** select the first three tabs of the focused surface; missing slots do nothing. These selections do not change the running game's scripts or interface language.

## Edit, preview and save

1. Edit Target. Syntax colors and context-aware completion help distinguish prose, native controls, formatting fields and errors.
2. Press **Enter** or click **Preview**. Validation runs and the draft is rendered through the production presentation path. Files remain unchanged.
3. Review every preview page and choice. Browsing choices does not select a gameplay answer, advance a dialogue, replay events/audio or add journal occurrences.
4. Release Enter, then press Enter again, or click **Save**. The update-existing transaction checks the captured revision, validates the candidate and writes the selected project. A held key or one repeat sequence cannot both preview and save.

Esc in Preview returns to Edit with the draft intact. Esc in Edit cancels the draft and restores committed presentation. Completion handles its own Enter/Esc first. While an IME composition owns input, Enter/Esc commit/cancel composition; they do not preview, save or close the editor. Closing composition input is drained until release.

Changing the selected project, turning the mode off or closing with an unfinished draft offers Preview/Save, Discard or Cancel. Moving focus does not save it. A successful save triggers the existing validated publication path and reflows presentation without changing script results or native choice identities.

| Default shortcut | Action |
| --- | --- |
| Enter | Preview; a separate press in Preview saves. |
| Esc | Dismiss completion, return from Preview, or cancel Edit. |
| Shift+Enter | Insert a line break. |
| Ctrl+Enter | Insert `{Next}` where the row supports native page breaks. |
| Ctrl+Space or `{` | Open native/template completion. |
| Up/Down in completion | Select a suggestion. |
| Tab or Enter in completion | Accept that suggestion without previewing. |
| Ctrl+A / C / X / V | Select all / Copy / Cut / Paste. |
| Ctrl+Z / Y | Undo / redo within the current draft. |
| Home/End, arrows; Shift with navigation | Move caret or extend selection. |
| PageUp / PageDown in Preview | Browse preview pages. |
| Ctrl+PageUp / Ctrl+PageDown in Preview | Inspect preview choices. |
| Alt+1 / 2 / 3 | Select the focused surface's first three language tabs. |
| F10 | Switch Pick text / Navigate while authoring is enabled. |

**Debug Room → Localization → Editor shortcuts** shows configurable editor bindings, including page break and picking. Choose a command, release the current key, then press the new chord. Overlapping shortcuts are rejected. Saved workspace settings retain the bindings. Caret movement and deletion repeat after an initial delay; Preview/Save and Cancel do not repeat.

**Copy raw** retains control tags. **Copy text + key** includes the identity needed to discuss an edit. Clicking the full key copies it. Keys and raw text remain available in Edit and Preview. Clipboard initialization or ownership failures are shown; a failed paste preserves the draft.

## Layout and selected-mod preview

**Preview selected mod** is enabled by default inside authoring mode. It temporarily overlays that project's validated contributions even if the package is disabled or a higher normal-priority package would mask it. It does not enable the package or reorder the saved stack. Empty Target contributes nothing. Leaving authoring removes the temporary layers and restores the normal stack: low priority at the top, high at the bottom.

Eligible dialogue rows show origin controls and an origin handle. Drag or adjust X/Y in the captured occurrence, review the result, then save. Coordinates are part of the leading Target prefix, not a separate position database. Saving an origin for empty Target deliberately seeds a source body so the coordinates can be persisted. Resetting the origin preserves the rest of Target's presentation settings.

**Clear complete Target** removes both its text contribution and every inline layout override. It does not leave hidden coordinates behind. The fallback remains a display aid until another explicit edit is saved.

## Journal and independent changes

Expand **Journal** to inspect **Dialogues** already observed in this session or saved **Changes**. Its size, position and collapsed state belong to the authoring workspace. Drag the title handle to move it, drag its resize handle, or reset its layout in Debug Room.

Search supports substring, wildcard (`*` and `?`) and regular-expression modes, with a case-sensitivity toggle. Filtering runs against a snapshot in the background after a short debounce; drawing uses cached visible results. Invalid or overly expensive patterns report a reason. The journal retains a bounded recent history.

Open a dialogue item to edit its canonical row. A faithful detached preview uses captured context where available. Missing historical context is reported; the editor does not teleport, rerun a script or invent live values. The journal's language selection changes only its own view.

Each saved Changes entry has independent **Undo** and **Redo** proposals. They merge the fields changed by that entry into the latest Target, preserving unrelated newer body/layout changes where possible. Review the proposal in Preview and explicitly Save it. This is separate from Ctrl+Z/Ctrl+Y inside a draft. Replaying a saved entry does not manufacture another history entry for the same operation.

## External changes, conflicts and failures

If another editor changes the package after the row was opened, Save reports a conflict. **Base**, **Ours** and **Theirs** are immutable evidence; their editable candidate panes are working copies of complete Target strings. Editing a candidate never modifies reference datasets or the original script metadata. Reset a candidate to its evidence, or choose it for Preview, validate it and save against the latest revision. Another external change can require another conflict review.

Workbook locks, validation failures and interrupted writes keep the draft available with a saving/failed/externally changed status. Close an application that holds the workbook exclusively and retry after review. Merely being able to read an Excel workbook does not establish safe simultaneous writing. Unknown metadata, row identities and project layouts are retained by the update transaction.

## System fonts, native fonts and encoding profiles

Open **Config → Interface → Text and fonts** for text presentation. System fonts and adaptive layout provide ordinary Unicode presentation. Native fonts and **Original-compatible** layout use the selected profile's original metrics and limits. **Auto** selects the applicable installed/profile assignment; a named profile selects its own namespace. Unsupported glyphs, native encodings or context-specific controls are diagnostics, rather than silent shrinking or substitution.

The Windows launcher's **Tools: Encoding Editor** manages named profiles externally. Glyph images, Unicode aliases and atlas metrics are separate from native byte mappings. One profile may use several atlas pages, while several enabled profiles retain independent namespaces. Edition, resource family, field, dictionary language and Target/reference role select the applicable mappings; codepages are not merged. Export each enabled profile with its dependencies into a mod and retain its notices. Other hosts can use profile files and the localization CLI. See [font and encoding profiles](../modding/encoding-profiles.md) for format, import/export and validation details.

Windows uses its owning native window's Unicode/IMM lifecycle. The patched Linux graphics runtime uses the actual SDL2 window event queue, including complete UTF-8 commits. SDL3 clipboard/dialog services are independent of that graphics queue. The stock macOS native runtime currently lacks the required IME composition bridge and reports that exact unsupported capability. A synthetic input/smoke pass does not establish live IME candidate-window acceptance on every desktop.
