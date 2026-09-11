---
name: winui-design
description: "Design and implement WinUI 3 and Windows App SDK user interfaces following Microsoft Fluent Design guidelines."
---

# WinUI 3 Design Guidelines

## Core Principles
1. **Material & Depth**:
   - Use `MicaBackdrop` on main window on Windows 11.
   - Use card patterns (`CardBackgroundFillColorDefaultBrush`, `CardStrokeColorDefaultBrush`, `CornerRadius="8"`).
   - Use `LayerFillColorDefaultBrush` for nested item cards or headers.
2. **Windowing**:
   - Extends content into titlebar with `ExtendsContentIntoTitleBar = true`.
   - Set custom title bar using `SetTitleBar(AppTitleBar)`.
   - Handle DPI scaling when setting initial window size via `GetDpiForWindow(hwnd)`.
3. **Typography & Spacing**:
   - Primary headers: `TitleTextBlockStyle` (SemiBold).
   - Card headers: `SubtitleTextBlockStyle`.
   - Section headers: `BodyStrongTextBlockStyle`.
   - Standard text: `BodyTextBlockStyle`.
   - Captions/metadata: `CaptionTextBlockStyle` with `TextFillColorSecondaryBrush`.
   - Page margins: `28,20,28,28` for desktop content.
4. **Input Controls**:
   - Search/typeahead: `AutoSuggestBox` (with `AutoSuggestionBoxTextChangeReason.UserInput`).
   - Numbers: `NumberBox` with `SpinButtonPlacementMode="Inline"`.
   - Toggles: `CheckBox` or `ToggleSwitch`.
   - Categories/Modes: `SelectorBar`, `RadioButtons`, or `NavigationView`.
5. **Feedback & Asynchrony**:
   - Status & alerts: `InfoBar` (Informational, Success, Warning, Error) bound to view models.
   - Progress: Determinate `ProgressBar` or `ProgressRing`.
