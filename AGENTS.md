# BetterMalodyV development

Use PowerShell 7 for build/release scripts. Apply patches through the Codex apply_patch tool; never use Bash heredocs in PowerShell.

Read PRODUCT.md, DESIGN.md and docs/DEVELOPMENT.md before changing behavior. The user explicitly requested reuse of MalodyVThemeLoader's installation and visual system.

Reference inputs are read-only: E:\UnityProjects\Emiria, E:\Desktop\MalodyVThemeLoader and E:\Program Files (x86)\Steam\steamapps\common\MalodyV. Put generated files and isolated game hosts under artifacts; never end a user's real game process. Isolated Steam test hosts need their own steam_appid.txt to avoid forwarding to the installed game.

Runtime changes are limited to local song/chart selection UI and its status predicate. Preserve raw chart collections, original state values, editing, scoring, judgement, replay, authentication and network behavior. Do not patch Guardian or entitlement checks.

Use shared Windows components. Keep full action labels on one line with actual font/DPI measurement and reflow; no clipped commands, wrapping or smaller fonts as a workaround. Native numeric controls must retain themed steppers, typing, arrow keys and accessibility. Test affected screens in dark/light where applicable, minimum/normal size and relevant synthetic scales; distinguish these from physical DPI tests.

docs/FEATURES.md is user-editable product content and the manager's source. Upgrade pristine copies; preserve edited installed copies. Routine UI corrections use patch releases. Keep the plugin's BepInPlugin version synchronized with Directory.Build.props.

Build with scripts/build-release.ps1. Validate the current game interop and relevant native, installer, runtime-reuse and update tests before release. Publish only source and review assets to Git; generated game bindings, BepInEx and installers stay out of Git. Release EXEs and SHA256SUMS.txt belong in artifacts/release and GitHub Releases.
