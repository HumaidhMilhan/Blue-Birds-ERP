---
name: Skybound Utility
colors:
  surface: '#f9f9ff'
  surface-dim: '#cedbf2'
  surface-bright: '#f9f9ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f0f3ff'
  surface-container: '#e7eeff'
  surface-container-high: '#dee9ff'
  surface-container-highest: '#d7e3fb'
  on-surface: '#101c2d'
  on-surface-variant: '#434654'
  inverse-surface: '#253143'
  inverse-on-surface: '#ebf1ff'
  outline: '#737685'
  outline-variant: '#c3c6d6'
  surface-tint: '#0c56d0'
  primary: '#003d9b'
  on-primary: '#ffffff'
  primary-container: '#0052cc'
  on-primary-container: '#c4d2ff'
  inverse-primary: '#b2c5ff'
  secondary: '#585f6a'
  on-secondary: '#ffffff'
  secondary-container: '#dce3f0'
  on-secondary-container: '#5e6570'
  tertiary: '#1e4184'
  on-tertiary: '#ffffff'
  tertiary-container: '#3a599d'
  on-tertiary-container: '#c3d3ff'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dae2ff'
  primary-fixed-dim: '#b2c5ff'
  on-primary-fixed: '#001848'
  on-primary-fixed-variant: '#0040a2'
  secondary-fixed: '#dce3f0'
  secondary-fixed-dim: '#c0c7d3'
  on-secondary-fixed: '#151c25'
  on-secondary-fixed-variant: '#404751'
  tertiary-fixed: '#d9e2ff'
  tertiary-fixed-dim: '#b0c6ff'
  on-tertiary-fixed: '#001945'
  on-tertiary-fixed-variant: '#224487'
  background: '#f9f9ff'
  on-background: '#101c2d'
  surface-variant: '#d7e3fb'
typography:
  h1:
    fontFamily: Inter
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.02em
  h2:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
    letterSpacing: -0.01em
  h3:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
    letterSpacing: '0'
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
    letterSpacing: '0'
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
    letterSpacing: '0'
  label-sm:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.02em
  code:
    fontFamily: Inter
    fontSize: 13px
    fontWeight: '500'
    lineHeight: 18px
    letterSpacing: '0'
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  unit: 4px
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 32px
  gutter: 20px
  margin: 40px
---

## Brand & Style

The design system is rooted in the "Precision Minimalism" movement, tailored specifically for the operational demands of a poultry ERP. The brand personality is dependable, systematic, and transparent. The goal is to reduce the cognitive load of high-volume data entry and inventory management by utilizing expansive whitespace and a reductionist aesthetic.

The visual style avoids unnecessary decorative elements, focusing instead on structural integrity and logical grouping. It employs a "Corporate Modern" approach with an emphasis on "Flat Utility"—relying on proportion and color temperature rather than shadows or gradients to define the workspace.

## Colors

The color strategy uses a "Monochromatic Blue" foundation to maintain professional composure.
- **Primary Blue:** Used for critical actions, active states, and brand identifiers.
- **Secondary Blue:** A soft tint used for large surface areas, row highlights, and subtle grouping without adding visual weight.
- **Tertiary Blue:** Reserved for high-contrast text and deep structural elements like navigation sidebars.
- **Neutrals:** A range of cool greys that prevent the interface from feeling "stark white" while maintaining a clean, clinical feel appropriate for an agricultural business environment.

## Typography

The design system utilizes **Inter** exclusively to leverage its exceptional legibility in data-heavy environments. The typeface is used in a systematic, utilitarian manner. 
- **Headlines:** Use tighter letter spacing and heavier weights to anchor pages.
- **Body Text:** Optimized for readability in tabular data and inventory lists using the 14px standard.
- **Labels:** Set in Uppercase or Bold 12px for form field headers and metadata to create a clear distinction from user input.

## Layout & Spacing

The layout follows a **Fixed-Fluid Hybrid Grid**. Sidebars and navigation units are fixed-width to ensure tool accessibility, while the main content area (Workdesk) is fluid to accommodate wide data tables.
- **Grid:** A 12-column system for dashboard layouts.
- **Rhythm:** An 8px linear scale (with a 4px half-step for tight components) governs all padding and margins. 
- **Density:** High-density layouts are preferred for ERP modules like "Stock Tracking," while low-density, high-margin layouts are used for "Reporting" and "Settings."

## Elevation & Depth

To maintain a flat and minimal aesthetic, this design system rejects traditional box shadows. Depth is communicated through **Tonal Layering** and **Stroke Definition**:
- **Level 0 (Background):** The base canvas uses the Neutral Background color.
- **Level 1 (Cards/Surface):** White surfaces with a 1px solid border in a light grey-blue tint.
- **Level 2 (Modals/Popovers):** Higher contrast 1px borders or a very subtle 2px blur shadow to suggest "float" without breaking the flat aesthetic.
- **Active States:** Indicated by a 2px left-border accent in Primary Blue for list items and menu selections.

## Shapes

The shape language is "Soft-Square." By using a consistent 4px (0.25rem) radius, the UI maintains a professional, rigid structure while appearing approachable and modern.
- **Buttons/Inputs:** 4px radius for a disciplined look.
- **Data Containers:** 8px (rounded-lg) for large dashboard cards to provide a subtle visual container.
- **Tags/Status Chips:** Fully pill-shaped to differentiate them from interactive buttons.

## Components

- **Buttons:** Solid primary blue for main actions; ghost buttons with blue strokes for secondary actions. Text is always centered and semibold.
- **Input Fields:** Flat white backgrounds with a 1px border. On focus, the border thickens to 2px in primary blue. Error states use a high-visibility red stroke without background changes.
- **Cards:** No shadows. Uses a 1px border. Card headers should have a subtle 1px bottom divider to separate titles from content.
- **Data Tables:** The core of the ERP. Row height is 48px for standard density. Alternate row striping is replaced by a hover-state background change in the Secondary Blue color.
- **Status Chips:** Use low-saturation background tints (e.g., light green for "In Stock", light red for "Out of Stock") with high-saturation text for readability.
- **ERP Specifics:** Include a "Batch Progress Bar" (a thin, flat progress indicator) and "Unit Count Badges" for quick inventory scanning.