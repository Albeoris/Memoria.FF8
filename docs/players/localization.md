# Localization projects

[Documentation](../README.md) / Players

Memoria can create translation projects, convert them between JSON and XLSX, and apply explicitly selected localization packages. A package changes displayed text; it does not replace field scripts, change gameplay choices, or rename characters in a save. Resource export from another edition or language does not establish playable support for it. The currently supported playable edition remains English Steam 2013.

## Create a project

Open **Mods → Create Mods → New localization**.

1. Choose the **Base game language**, which determines every exported row and key. An English base produces `$en_` keys even when Japanese is the first reference. This selection does not switch the running game or its scripts.
2. Enter a **Target** identifier and a package name. Target is a nonempty label such as `ru (Literary edition)`, not a filename or a restricted language-code list. An independent translation needs an identifier distinct from built-in languages and existing translations.
3. Choose available **References** in reading order. Japanese is initially first when available, followed by the base language. Use **Move earlier** and **Move later** to reorder columns. The base language remains selected. Reference order controls reading order only; it does not set runtime translation priority or add rows.
4. Choose JSON or XLSX, then **All-in-one** or **Separate category/file payloads**. Changing the format keeps entered options.
5. Choose a destination parent folder. The picker starts at the configured Memoria mod root and creates a proposed child package name. Check the complete child path in the preview; exports do not go directly into the shared mod root.
6. Select **Prepare preview**. Review the exact destination, languages, categories, record count and source gaps. **Write package** creates the reviewed project. Extraction and writing have cancellation controls.

New packages have `enabled=0`. Creating one outside a watched mod root does not install it. A package inside a watched root is discovered normally, but activation remains a separate action in Installed Mods. Existing destinations are rejected so a new export cannot overwrite translator edits; choose a new destination for conversion or another project.

Keyboard text entry accepts Unicode. The controller character picker reuses the English, Symbols and Cyrillic pages. Mouse and controller selection follow the existing menu conventions.

## Choose translation precedence

Enable the desired packages in **Installed Mods**, then open **Localization stack** from Create Mods. When several independent translations need a decision, the host opens this selection explicitly.

Select any subset and review their order: **LOW priority is at the TOP; HIGH priority is at the BOTTOM**. The bottommost selected contribution with a nonempty Target supplies a given translated record. Empty or missing Target values do not hide lower translations; original installed text supplies the final fallback. No selected translation means original text.

A stack can contain different Target IDs. Its **Primary target** is displayed explicitly and can be changed among the selected translations before **Apply selected stack**. The saved primary identifier and package order describe the chosen profile. Cancel keeps the previously saved selection.

Patches use a separate project identity and declare their base relationship. Use **Create localization patch** to select a base package and create a disabled patch with empty Targets. A base and its patches must be selected in a compatible order; a common Target label alone does not declare a dependency.

## Use existing translations as references

The References selector also list loaded projects by their package name and Target label. An **own contribution** dataset contains only that project's translated Target values. Untranslated rows remain missing; native fallback is never presented as if it were a translation.

**Effective active profile** reads the selected contributions in their committed stack order. It is a source snapshot for authoring another project. It does not activate a different stack or silently turn a mixed profile into a newly named independent translation. Loaded-project datasets can be prose references; the base game language selects the native row inventory.

## Convert and edit

**Mods → Create Mods → Convert localization** reads an existing package and writes a new one with the selected JSON/XLSX format and single/split layout. Conversion does not require installed game resources and preserves project identity, translation values and retained metadata. It creates a disabled output package and leaves the input unchanged.

Read [the translator guide](../modding/localization.md) before editing. Keep the manifest and any extensions sidecar beside the payloads. Edit Target text, preserve stable identities and native placeholders, validate the result, and only then enable it.

## Coverage and current limits

Exports inventory all currently implemented resource families and report unavailable families explicitly. These include field dialogue and contexts; kernel names and help for magic, items, weapons, characters, abilities and GF/battle records; menu tables; monster names, Scan and enemy dialogue; card text; world/location text; tutorials and SeeD exam text; and plain publication text. A category in the inventory does not imply that every original string or every display consumer is supported.

- The authored **System** catalog includes registered interface labels, prompts, help, settings and formatted messages. It covers gameplay menus, save/load, battle and card presentation, mod authoring and developer views. Templates keep runtime names, numbers and technical identifiers as arguments.
- Japanese field-source decoding is supported using the shared Japanese codepage and registered field-specific character tables. Unknown indices still fail strict decoding.
- Japanese non-field text uses the game's common Japanese character pages. World item-name controls, empty enemy-script sections, namedic padding and card pointer tables are supported. Each available language uses its own World bank. Physically absent edition/language resources remain explicit capability diagnostics.
- Information titles and bodies can be translated. Structured gate, link and number controls retain their native behavior.
- Native menu, tutorial, startup and card-result images are exported as PNG references. Add a corresponding Target PNG to translate lettering drawn into an image; see the translator guide. Native European wordmark fragments retain their glyph references and source-font context.
- Player-assigned character/GF/PocketStation names remain save-owned values, inserted through the original name controls.
- Dynamic field choices support explicit source-to-target line maps. Validation preserves option order, relative results and cancellation behavior. Incompatible contracts fail before publication. Cross-language correspondence does not imply that arbitrary scripts are compatible.
- JSON/XLSX conversion and workbook schema checks have automated coverage. Acceptance in the actual Microsoft Excel application has not been run because Excel was unavailable in the validation environment.

Workbooks show `File`, `Key`, `Target`, the ordered reference columns, then `ReferenceKeys`. Technical columns are hidden and retained for validation and conversion. An English base with references `jp,en` therefore shows `Target`, `Reference [jp]`, then `Reference [en]`, with no separate Source column. Reference-only rows are not exported. Missing references and empty original strings remain distinct in retained data; ambiguous or unavailable correspondence stays blank rather than being guessed. Review the manifest's `sourceGaps` diagnostics for unavailable resources and mapping limits.
