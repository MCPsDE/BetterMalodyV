# Desktop surfaces

## Scope and modes

- **Manager — Read:** Malody V players ask what the installed plugin changes and how to use its filters. One selectable, scrolling document comes from editable local Markdown, with embedded fallback. Headings and lists provide wayfinding; refresh/F5 and update checking are supporting commands. The fixed footer carries appearance and sponsorship.
- **Installer — Operate:** Players select or correct the game directory, inspect BepInEx compatibility, then install/repair, open the manager or uninstall. Path diagnosis, environment feedback and a scrollable log support the task. The primary install/repair action, progress and concise operation status stay outside the content viewport. Invalid, installed, busy, completed and failed states govern available commands.
- **Game extension — Operate:** The existing advanced-search panel receives combinable Stable/Beta/Alpha native toggles and an All reset. It retains the search input's TMP font, readable English fallback and measured width-based row wrapping. The fixture is isolated test data, not a full lobby or multiplayer capture.

## Direction contract

**THESIS:** Make plugin changes readable and installation explicit within the user's established native Windows utility. The manager owns one continuous document rather than a dashboard of feature cards.

**OWN-WORLD:** Reuse MalodyVThemeLoader's Fluent neutral surfaces, paired dark/light manager appearance, fixed-light installer, Microsoft YaHei UI family chain, fine outlines, restrained blue actions and exact multicolor sponsor BrandBar. Preserve the supplied transparent M asset.

**STORY:** Install into the selected game directory, then continue launching Malody V normally. Read the shipped changes and usage guidance in the manager; edit its local Markdown when maintaining documentation. Actual filter operation remains inside the game.

**FIRST VIEWPORT:** The manager places the M logo beside the title and version, followed by two full-width supporting commands, a flexible document and concise status above the fixed BrandBar. The installer places title and metadata above a scrolling directory/environment/log region, followed by stable progress, status and measured action rows above a credit-only footer.

**FORM:** Explicit constrained reuse of the established native WinForms world. No new identity exercise, seed selection or decision comp applies. Signature behavior is editable Markdown refreshing in place; feedback and appearance changes are immediate, without custom animation.

**FINISH:** unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance

## Handoff

The native review matched the reuse contract. Its one material minimum-installer clipping finding was resolved using `installer-minimum-native.png`, an actual OS capture that shows the scrolling viewport clipping correctly. Ship disposition is scoped to that finding. The other 24 desktop images form a synthetic size/scale matrix; their DrawToBitmap native-scrollbar painting artifact is not a design token. See `../review/documentation.md` for evidence boundaries. No redesign decision remains open in this handoff.
