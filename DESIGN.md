---
version: alpha
name: Ant Design Modern — AntdUI Desktop
description: A modern Ant Design visual identity for the RpCalculator Windows desktop application.
colors:
  primary: "#1677FF"
  primary-action: "#1677FF"
  primary-action-hover: "#4096FF"
  primary-action-active: "#0958D9"
  on-primary: "#FFFFFF"
  background: "#F5F5F5"
  background-dark: "#141414"
  surface: "#FFFFFF"
  surface-dark: "#1F1F1F"
  surface-hover: "#F0F0F0"
  surface-hover-dark: "#2C2C2C"
  text-primary: "#141414"
  text-primary-dark: "#F5F5F5"
  text-secondary: "#595959"
  text-secondary-dark: "#A6A6A6"
  border: "#D9D9D9"
  border-dark: "#2C2C2C"
  danger: "#FF4D4F"
  danger-strong: "#D9363E"
  on-danger: "#FFFFFF"
  window-close: "#FF5F57"
  window-minimize: "#FFBD2E"
  window-maximize: "#28C840"
  scrollbar: "#BFBFBF"
  scrollbar-dark: "#424242"
  scroll-area: "#F0F0F0"
  scroll-area-dark: "#1A1A1A"
  sidebar: "#FAFAFA"
  sidebar-dark: "#1A1A1A"
typography:
  title-lg:
    fontFamily: Microsoft YaHei UI
    fontSize: 20px
    fontWeight: 700
    lineHeight: 1.4
  title-md:
    fontFamily: Microsoft YaHei UI
    fontSize: 15px
    fontWeight: 700
    lineHeight: 1.4
  subtitle-sm:
    fontFamily: Microsoft YaHei UI
    fontSize: 12px
    fontWeight: 400
    lineHeight: 1.5
  body-md:
    fontFamily: Microsoft YaHei UI
    fontSize: 14px
    fontWeight: 400
    lineHeight: 1.5
  body-sm:
    fontFamily: Microsoft YaHei UI
    fontSize: 12px
    fontWeight: 400
    lineHeight: 1.5
  label-sm:
    fontFamily: Microsoft YaHei UI
    fontSize: 12px
    fontWeight: 400
    lineHeight: 1.4
  mono-data:
    fontFamily: Consolas
    fontSize: 13px
    fontWeight: 400
    lineHeight: 1.5
rounded:
  none: 0px
  sm: 8px
  md: 12px
  lg: 16px
  full: 9999px
spacing:
  xs: 4px
  sm: 8px
  md: 12px
  lg: 16px
  xl: 24px
  xxl: 32px
components:
  window:
    backgroundColor: "{colors.background}"
    textColor: "{colors.text-primary}"
    rounded: "{rounded.lg}"
    padding: "{spacing.xl}"
  window-dark:
    backgroundColor: "{colors.background-dark}"
    textColor: "{colors.text-primary-dark}"
    rounded: "{rounded.lg}"
    padding: "{spacing.xl}"
  card:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text-primary}"
    rounded: "{rounded.lg}"
    padding: "{spacing.xl}"
  card-dark:
    backgroundColor: "{colors.surface-dark}"
    textColor: "{colors.text-primary-dark}"
    rounded: "{rounded.lg}"
    padding: "{spacing.xl}"
  button-primary:
    backgroundColor: "{colors.primary-action}"
    textColor: "{colors.on-primary}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: 12px
    height: 36px
  button-primary-hover:
    backgroundColor: "{colors.primary-action-hover}"
    textColor: "{colors.on-primary}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: 12px
    height: 36px
  button-primary-active:
    backgroundColor: "{colors.primary-action-active}"
    textColor: "{colors.on-primary}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: 12px
    height: 36px
  button-default:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text-primary}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: 12px
    height: 36px
  button-default-hover:
    backgroundColor: "{colors.surface-hover}"
    textColor: "{colors.text-primary}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: 12px
    height: 36px
  button-danger:
    backgroundColor: "{colors.background}"
    textColor: "{colors.danger-strong}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: 12px
    height: 36px
  input:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text-primary}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: 12px
    height: 36px
  select:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text-primary}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: 12px
    height: 36px
  segmented:
    backgroundColor: "{colors.background}"
    textColor: "{colors.text-primary}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.md}"
    padding: "{spacing.xs}"
    height: 36px
  segmented-item-active:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text-primary}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.sm}"
  input-dark:
    backgroundColor: "{colors.surface-dark}"
    textColor: "{colors.text-primary-dark}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: 12px
    height: 36px
  select-dark:
    backgroundColor: "{colors.surface-dark}"
    textColor: "{colors.text-primary-dark}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: 12px
    height: 36px
  button-default-hover-dark:
    backgroundColor: "{colors.surface-hover-dark}"
    textColor: "{colors.text-primary-dark}"
    typography: "{typography.body-md}"
    rounded: "{rounded.md}"
    padding: 12px
    height: 36px
  caption-dark:
    backgroundColor: "{colors.background-dark}"
    textColor: "{colors.text-secondary-dark}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.none}"
  divider-dark:
    backgroundColor: "{colors.border-dark}"
    textColor: "{colors.text-primary-dark}"
    typography: "{typography.body-sm}"
    height: 1px
  sidebar:
    backgroundColor: "{colors.sidebar}"
    textColor: "{colors.text-primary}"
    rounded: "{rounded.none}"
  sidebar-dark:
    backgroundColor: "{colors.sidebar-dark}"
    textColor: "{colors.text-primary-dark}"
    rounded: "{rounded.none}"
  scroll-area:
    backgroundColor: "{colors.scroll-area}"
    textColor: "{colors.text-primary}"
    rounded: "{rounded.sm}"
  scroll-area-dark:
    backgroundColor: "{colors.scroll-area-dark}"
    textColor: "{colors.text-primary-dark}"
    rounded: "{rounded.sm}"
  scrollbar-thumb:
    backgroundColor: "{colors.scrollbar}"
    rounded: "{rounded.full}"
    width: 10px
    height: 30px
  scrollbar-thumb-dark:
    backgroundColor: "{colors.scrollbar-dark}"
    rounded: "{rounded.full}"
    width: 10px
    height: 30px
  caption:
    backgroundColor: "{colors.background}"
    textColor: "{colors.text-secondary}"
    typography: "{typography.label-sm}"
    rounded: "{rounded.none}"
  danger-dot:
    backgroundColor: "{colors.danger}"
    rounded: "{rounded.full}"
    height: 10px
    width: 10px
  progress-track:
    backgroundColor: "{colors.border}"
    rounded: "{rounded.full}"
    height: 10px
  progress-fill:
    backgroundColor: "{colors.primary}"
    rounded: "{rounded.full}"
    height: 10px
  status-bar:
    backgroundColor: "{colors.background}"
    textColor: "{colors.text-primary}"
    typography: "{typography.body-sm}"
    height: 38px
  window-button:
    backgroundColor: "{colors.surface}"
    textColor: "{colors.text-primary}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.none}"
    height: 36px
    width: 46px
  window-button-hover:
    backgroundColor: "{colors.surface-hover}"
    textColor: "{colors.text-primary}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.none}"
    height: 36px
    width: 46px
  window-button-close-hover:
    backgroundColor: "{colors.danger-strong}"
    textColor: "{colors.on-danger}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.none}"
    height: 36px
    width: 46px
  traffic-light-close:
    backgroundColor: "{colors.window-close}"
    rounded: "{rounded.full}"
    height: 14px
    width: 14px
  traffic-light-minimize:
    backgroundColor: "{colors.window-minimize}"
    rounded: "{rounded.full}"
    height: 14px
    width: 14px
  traffic-light-maximize:
    backgroundColor: "{colors.window-maximize}"
    rounded: "{rounded.full}"
    height: 14px
    width: 14px
---

# Ant Design Modern — AntdUI Desktop

## Overview

The product should feel like a modern Ant Design desktop utility: calm, precise, and quietly beautiful. Rounded cards float over a cool gray canvas with light shadows. Content is spacious, labels are secondary, and a single Ant Design blue accent (#1677FF) marks the primary action. Window controls use the AntdUI PageHeader native minimize/maximize/close buttons. The design is theme-aware: light mode uses #F5F5F5/#FFFFFF surfaces, dark mode uses #141414/#1F1F1F with identical geometry.

## Colors

The palette is the Ant Design 5 neutral gray scale plus one blue accent and one red danger color. Blue is reserved for the single most important action per screen; red is reserved for destructive actions and the close button.

- **Primary (#1677FF):** brand accent and progress fill. For button text contrast, button surfaces use the slightly deeper **primary-action (#1677FF)**.
- **Background (#F5F5F5):** window canvas in light mode. Dark mode background is **#141414**.
- **Surface (#FFFFFF / #1F1F1F):** cards, inputs, and selectors.
- **Text primary (#141414 / #F5F5F5):** titles and key values.
- **Text secondary (#595959 / #A6A6A6):** captions and helper text.
- **Border (#D9D9D9 / #2C2C2C):** hairline separation for cards and inputs.
- **Sidebar (#FAFAFA / #1A1A1A):** slightly deeper neutral than the main canvas, separating navigation from content without hard lines.
- **Scroll area (#F0F0F0 / #1A1A1A):** subtle tint for scrollable date/result lists so users can see the region is scrollable.
- **Danger (#FF4D4F):** destructive actions and close hover.
- **Traffic lights:** close #FF5F57, minimize #FFBD2E, maximize #28C840.

## Typography

Use the system font stack **Microsoft YaHei UI → Microsoft YaHei → Segoe UI**. Never load a custom bitmap or variable font for the main UI. Titles use Microsoft YaHei UI bold (title-lg 20px / title-md 15px). Body and labels use regular weights (body-md 14px, body-sm 12px). Identifiers, metrics, and dates use **Consolas** (mono-data) so hexadecimal IDs and numbers align. Only two weights are allowed per screen: regular and bold. In WinForms the px values map to point sizes (20px≈15pt title, 15px≈11pt, 14px≈10.5pt, 13px≈9.75pt, 12px≈9pt).

## Layout

The window is 1080×800 minimum 900×700. Layout is three-column: a 260px left navigation sidebar, a fluid main content area, and a 240px right quick-status panel. The 64px header and a floating 76px bottom bar span the full width. The main area shows only the section selected in the sidebar instead of flattening all features into one long scroll. Content inside a section scrolls when needed; date/result lists get a visibly tinted scroll surface. Each card has 24px internal padding. Use an 8px-based spacing scale (4/8/12/16/24/32). Inputs, selectors, segmented controls, and buttons are 36px high. The result date list reserves 170px of height.

## Elevation & Depth

Depth is tonal, not heavy. Cards sit one level above the canvas with a 24px-radius shadow at 8% opacity, 4px downward offset, and a 1px border. Interactive controls use flat fill changes rather than 3D bevels: hover lightens or darkens the fill, press deepens it. The window itself uses a 16px rounded rectangle with a 28px soft shadow.

## Shapes

All interactive elements use rounded rectangles: 12px for buttons, inputs, selectors, and segmented controls; 16px for cards and the window; 8px for small chips. Full circles (9999px) are used only for traffic-light window buttons and the progress bar. Windows title-bar buttons are the exception: flat rectangles with 0px radius, matching the native Windows silhouette.

## Components

- **Sidebar:** 260px fixed; nav items are 232×40 left-aligned text rows; the active item gets a translucent blue background (`rgba(0,100,255,0.1)`), blue text, and a 4px left indicator bar in primary-action blue.
- **Right status panel:** 240px fixed, same tint as sidebar, showing current best, processed count, speed, and ETA.
- **Bottom floating bar:** white/light surface container with 16px radius, 16px shadow, 10% opacity, 2px offset, floating inside 16px page margins.
- **Buttons:** primary is the only blue-filled button per screen. Default buttons use surface fill with a 1px border. Danger buttons keep the canvas background and red text.
- **Inputs & selectors:** white/light surface, 12px radius, 1px border, 36px height, 12px horizontal padding.
- **Segmented control:** gray canvas fill, 4px padding; the active segment becomes a white surface pill.
- **Progress bar:** 10px full-round track; the fill uses the primary blue.
- **Window controls (Windows):** 46×36 flat rectangles; close hover fills with danger red.
- **Window controls (PageHeader):** AntdUI native minimize/maximize/close buttons in the top-right.
- **Date lists / scroll areas:** tinted scroll background (scroll-area tokens) with mono-data rows and 8px vertical rhythm; hover uses surface-hover.

## Do's and Don'ts

- Do use the primary blue for exactly one action per screen.
- Do keep 24px internal card padding and 16px gaps between cards.
- Do use the system font stack; never embed a custom UI font.
- Do keep normal text contrast at WCAG AA (4.5:1) — buttons use #1677FF instead of #1677FF for this reason.
- Don't mix PageHeader window buttons with custom window buttons in the same title bar.
- Don't use more than two font weights on one screen.
- Don't place window controls on the left when running on Windows.
