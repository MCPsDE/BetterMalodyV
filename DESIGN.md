---
name: BetterMalodyV
description: A native Fluent Windows utility that reuses the established ThemeLoader visual system.
colors:
  accent: "#69AFED"
  accent-light: "#0F6CBD"
  accent-fill: "#0F6CBD"
  accent-hover: "#115EA3"
  accent-pressed: "#005493"
  accent-soft: "#1B3448"
  accent-soft-light: "#E5F1FB"
  danger: "#F4958E"
  danger-light: "#C43124"
  danger-fill: "#C43124"
  danger-hover: "#A4261C"
  danger-pressed: "#8B1F17"
  success: "#7ED27E"
  success-light: "#107C10"
  canvas: "#181818"
  canvas-light: "#F5F5F5"
  surface: "#202020"
  surface-light: "#FFFFFF"
  surface-alt: "#292929"
  surface-alt-light: "#FAFAFA"
  ink: "#F5F5F5"
  ink-light: "#242424"
  muted-ink: "#B8B8B8"
  muted-ink-light: "#616161"
  line: "#444444"
  line-light: "#E1E1E1"
  control-pressed: "#333333"
  control-pressed-light: "#EDEDED"
  on-fill: "#FFFFFF"
  sponsor-start: "#642BB1"
  sponsor-middle: "#A61D58"
  sponsor-end: "#18488F"
typography:
  page-title:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "22pt"
    fontWeight: 700
  document-title:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "23pt"
    fontWeight: 700
  document-heading:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "16pt"
    fontWeight: 700
  document-subheading:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "12pt"
    fontWeight: 700
  section-title:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "11pt"
    fontWeight: 700
  document-body:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "10.5pt"
    fontWeight: 400
  body:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "10pt"
    fontWeight: 400
  label:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "9pt"
    fontWeight: 400
  sponsor-label:
    fontFamily: "Microsoft YaHei UI, Segoe UI Variable Text, Segoe UI, sans-serif"
    fontSize: "10pt"
    fontWeight: 700
  diagnostic:
    fontFamily: "Cascadia Mono, Consolas, Courier New, monospace"
    fontSize: "9pt"
    fontWeight: 400
rounded:
  action: "4px"
spacing:
  compact: "8px"
  control: "12px"
  group: "16px"
  inset: "20px"
  section: "24px"
  page: "28px"
  outer: "32px"
  installer-outer: "36px"
components:
  button-primary:
    backgroundColor: "{colors.accent-fill}"
    textColor: "{colors.on-fill}"
    typography: "{typography.body}"
    rounded: "{rounded.action}"
    height: "36px"
  button-primary-hover:
    backgroundColor: "{colors.accent-hover}"
  button-primary-pressed:
    backgroundColor: "{colors.accent-pressed}"
  button-secondary:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.ink}"
    typography: "{typography.body}"
    rounded: "{rounded.action}"
    height: "36px"
  button-secondary-hover:
    backgroundColor: "{colors.surface-alt}"
  button-secondary-pressed:
    backgroundColor: "{colors.control-pressed}"
  button-danger:
    backgroundColor: "{colors.danger-fill}"
    textColor: "{colors.on-fill}"
    typography: "{typography.body}"
    rounded: "{rounded.action}"
    height: "36px"
  button-danger-hover:
    backgroundColor: "{colors.danger-hover}"
  button-danger-pressed:
    backgroundColor: "{colors.danger-pressed}"
  button-disabled:
    backgroundColor: "{colors.surface-alt}"
    textColor: "{colors.muted-ink}"
  document:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.ink}"
    typography: "{typography.document-body}"
  diagnostic-log:
    backgroundColor: "{colors.surface-light}"
    textColor: "{colors.muted-ink-light}"
    typography: "{typography.diagnostic}"
    height: "100px"
  brand-bar:
    backgroundColor: "{colors.canvas}"
    textColor: "{colors.muted-ink}"
    typography: "{typography.label}"
    padding: "10px 24px"
    height: "58px"
  brand-bar-installer:
    backgroundColor: "{colors.canvas-light}"
    textColor: "{colors.muted-ink-light}"
    typography: "{typography.label}"
    padding: "10px 24px"
    height: "40px"
  appearance-toggle:
    backgroundColor: "{colors.surface-alt}"
    textColor: "{colors.muted-ink}"
    width: "92px"
    height: "38px"
  appearance-toggle-selected:
    backgroundColor: "{colors.accent-soft}"
    textColor: "{colors.accent}"
  sponsor:
    textColor: "{colors.on-fill}"
    typography: "{typography.sponsor-label}"
    rounded: "{rounded.action}"
    width: "124px"
---

# Design System: BetterMalodyV

## Overview

**Creative North Star: "Fluent 2 Windows utility"**

BetterMalodyV deliberately reuses the user's established MalodyVThemeLoader world: quiet neutral layers, restrained blue actions and familiar native controls for Chinese-speaking Malody V players. The manager defaults to dark and saves a light or dark preference; the installer always uses light. The supplied M artwork and the shared multicolor sponsor control carry the identity.

This is a record of the shipped WinForms implementation, not a new identity proposal. The manager's reading strategy and the installer's operation strategy live in `.impeccable/briefs/desktop.md`. Unity search controls retain their separate native game context.

**Key Characteristics:**

- Paired dark and light neutral surfaces with blue action feedback.
- Native Chinese UI typography, keyboard behavior and visible focus.
- Full command labels with measured sizing and shared row reflow.
- Supplied M artwork with its outer black background removed.
- One multicolor sponsor action in the manager footer.

## Colors

The frontmatter records `AppVisuals` and `BrandBar` values. Unsuffixed mode-dependent colors describe dark; `-light` describes the manager's light option and fixed-light installer. Action fills, hover, pressed and sponsor colors are shared across appearances.

### Primary

- **Readable Blue** (`accent`): status feedback and the selected appearance icon; its light counterpart is Fluent blue.
- **Action Blue** (`accent-fill`): primary actions, with deeper hover and pressed fills and white text.
- **Soft Selection Blue** (`accent-soft`): selected appearance segment.

### Neutral

- **Quiet Canvas** (`canvas`): page, document and footer ground.
- **Working Surface** (`surface`): fields, secondary actions, environment inset and logs.
- **Inset Neutral** (`surface-alt`): secondary hover and disabled actions.
- **Primary Ink / Secondary Ink** (`ink`, `muted-ink`): primary reading text / supporting text and disabled labels.
- **Fine Rule / Pressed Neutral** (`line`, `control-pressed`): outlines / immediate pressed feedback.

Readable danger and success tokens follow appearance; the shared destructive-button primitive has its own solid fill. The installer currently uses a secondary uninstall command with confirmation. The sponsor alone uses the three sponsor colors, painted at 12 degrees with stops at 0, 0.52 and 1; hover overlays white at alpha 16/255 and pressed overlays black at alpha 35/255.

**The State Rule.** Pair status color with words or an enabled, disabled or selected control state.

## Typography

The UI resolves the first installed family from Microsoft YaHei UI, Segoe UI Variable Text and Segoe UI, then the system sans-serif fallback. Diagnostics resolve Cascadia Mono, Consolas and Courier New, then the system fallback. Point sizes in the frontmatter are native font units; no fixed line-height or tracking scale is implemented.

Page titles use `page-title`. The selectable Markdown document uses `document-title` for H1, `document-heading` for H2, `document-subheading` for deeper headings and `document-body` for paragraphs. Emphasis retains native bold or italic. Installer section headings use `section-title`; commands and supporting body copy use `body`; metadata, status and footer credit use `label`. Logs alone use `diagnostic`. Inline and fenced Markdown code retain the document UI face in the current renderer.

**The Native Type Rule.** Keep the Chinese desktop UI on the installed native family chain and retain point-based sizing.

**The Full Command Rule.** Measure the actual command font at the current DPI and retain the complete single-line label; grow or reflow the container instead of shrinking, wrapping or ellipsizing commands.

## Layout

Measurements are 96-DPI logical pixels except typography. Both forms use `AutoScaleMode.Dpi`, docking and native scrolling. The manager starts at 960 × 780 client pixels with a 720 × 580 minimum outer window. Its content has 32 px horizontal padding, 24 px top and 16 px bottom padding. A 58 px logo sits in a 76 px header allocation. Header, action row and status surround one flexible scrolling document; there is no navigation rail.

The installer starts at 880 × 720 client pixels with a 760 × 660 minimum outer window. Its content uses 36 px horizontal padding, 28 px top and 24 px bottom padding. The path, environment inset and 100 px diagnostic log share a native scrolling viewport. Progress, operation status and bottom commands remain outside that viewport. The environment inset uses 20 px padding.

`ActionButton` measures unwrapped text, adds DPI-scaled content clearance and updates its minimum size when text, font, padding or DPI changes. `ButtonRow` uses an 8 px scaled gap and at least 40 px row height, distributing spare width horizontally; if preferred widths do not fit, it stacks full-width commands. The factory's 36 px height is a starting value, not a limit.

The shared manager footer uses 24 px horizontal and 10 px vertical padding, flexible credit space, then allocations for the appearance label (44 px), toggle plus trailing margin (108 px) and sponsor (124 px). The installer footer is credit-only. The capture matrix and its synthetic-scaling limits are recorded in `.impeccable/review/documentation.md`.

## Elevation & Depth

Neutral layers, whitespace and fine outlines create grouping without custom shadows. `WindowsBackdrop` requests Windows 11 Mica and a matching title-bar mode when supported, retaining solid neutral surfaces on failure. Bitmap captures do not establish compositor behavior. Native windows and dialogs retain operating-system depth. Hover, press and appearance feedback are immediate; no custom transition animation is implemented. The sponsor fill is the local gradient exception.

## Shapes

Panels, fields and document surfaces are rectangular. Action and sponsor buttons use the DPI-scaled `action` radius. The appearance control uses a pill track and rounded selected half. Secondary commands have fine outlines; primary and destructive primitives use solid fills. Custom painting clears rounded corners to the nearest opaque ancestor.

## Components

### Actions and fields

`ActionButton` supplies primary, secondary and destructive variants with the frontmatter states, muted disabled feedback and native dotted keyboard focus. Text painting is single-line without overflow ellipsis. `ButtonRow` owns measured horizontal/stacked layout. Preserve native button activation and accessible names. The installer path remains an editable native text box beside the intentional dialog command “浏览…”. Busy operations disable conflicting controls and closing; completion or failure restores valid actions.

### Markdown document

`MarkdownView` is a borderless, read-only native RichTextBox with selectable text and vertical scrolling. It parses Markdown as text, renders headings, lists and emphasis, displays link destinations, and opens HTTP/HTTPS links in the default browser. List indentation is 22 px per depth with a 12 px hanging indent. Reading position is restored by character position after rerender. The manager watches the editable local `docs/FEATURES.md`, falls back to the embedded document and offers refresh through its command and F5.

### BrandBar

The exact credit is `by 猫猫虫小窝(451586654)`. The manager pairs “外观” with a drawn sun-left/moon-right toggle and “赞助支持”, opening `https://ifdian.net/a/mcpsde`. The toggle's accessible role is CheckButton; checked means dark. Click/Enter toggles, Left selects light and Right selects dark. The selected segment uses soft blue with readable accent; the other icon uses secondary ink. Both footer controls retain dotted keyboard focus.

The manager stores appearance in `%LocalAppData%\BetterMalodyV\appearance.json`; saving succeeds before the palette changes. Missing/unreadable preferences default to dark. The installer uses `BrandBar(showManagerActions: false)` and a process-local light override, without changing that preference.

### Native game search extension

Stable, Beta and Alpha are equal-width, equal-height native toggles aligned inside the search input’s right edge; All remains a separate reset action. The fills are #CCCCCC at 15% selected and 7% unselected opacity. `NativeUi` draws a two-stroke checkmark sprite that appears only when selected. Original input font/size/ink and a reserved left textViewport are retained. Width uses the longest actual status label, and commands remain complete on one line. An active-panel controller supplements the native Show hook.

The room update action uses #CCCCCC at 12% opacity, sits beside the selected song and reserves title/artist width. It retains the artist label's native font, size and ink, with English fallback when the font lacks the tested CJK character. Current evidence uses actual shipped SearchAdv and SceneRoom in an isolated host at 1280×720 and 1920×1080, with simulated room responses and downloads; it does not establish a real two-account multiplayer session or physical DPI.

### Artwork

Use the embedded `Assets/mtp.png` and `mtp.ico`, derived from the user's BetterMalodyV.jpg by removing the outer black background. Preserve the M artwork and its transparency; manager placement uses proportion-preserving Zoom. No new artwork or decision comp was required for the explicit reuse contract.

## Do's and Don'ts

### Do:

- Do use the paired appearance palette and keep readable accents separate from solid action fills.
- Do keep complete single-line commands through shared text measurement and row reflow.
- Do retain selectable document text, native keyboard behavior, visible focus and explicit operation feedback.
- Do preserve the supplied M artwork, exact author credit and manager sponsor label and destination.
- Do keep the installer light with a credit-only footer and preserve the manager preference.

### Don't:

- Don't spread the sponsor gradient into workspace surfaces or ordinary actions.
- Don't hide undersized commands with wrapping, clipping, smaller text or overflow ellipsis.
- Don't replace missing game-font CJK characters with unreadable boxes; retain the English fallback.
- Don't present synthetic scaling or isolated runtime fixtures as physical-DPI or real multiplayer verification.
