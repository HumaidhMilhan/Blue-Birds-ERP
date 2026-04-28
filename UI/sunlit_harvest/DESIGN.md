---
name: Sunlit Harvest
colors:
  surface: '#faf9f6'
  surface-dim: '#dbdad7'
  surface-bright: '#faf9f6'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f4f3f1'
  surface-container: '#efeeeb'
  surface-container-high: '#e9e8e5'
  surface-container-highest: '#e3e2e0'
  on-surface: '#1a1c1a'
  on-surface-variant: '#584238'
  inverse-surface: '#2f312f'
  inverse-on-surface: '#f2f1ee'
  outline: '#8c7166'
  outline-variant: '#e0c0b2'
  surface-tint: '#a04100'
  primary: '#9c3f00'
  on-primary: '#ffffff'
  primary-container: '#c45100'
  on-primary-container: '#fffbff'
  inverse-primary: '#ffb693'
  secondary: '#7d5800'
  on-secondary: '#ffffff'
  secondary-container: '#ffb702'
  on-secondary-container: '#6b4b00'
  tertiary: '#914809'
  on-tertiary: '#ffffff'
  tertiary-container: '#b05f22'
  on-tertiary-container: '#fffbff'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#ffdbcc'
  primary-fixed-dim: '#ffb693'
  on-primary-fixed: '#351000'
  on-primary-fixed-variant: '#7a3000'
  secondary-fixed: '#ffdea9'
  secondary-fixed-dim: '#ffba27'
  on-secondary-fixed: '#271900'
  on-secondary-fixed-variant: '#5e4100'
  tertiary-fixed: '#ffdbc7'
  tertiary-fixed-dim: '#ffb688'
  on-tertiary-fixed: '#311300'
  on-tertiary-fixed-variant: '#733600'
  background: '#faf9f6'
  on-background: '#1a1c1a'
  surface-variant: '#e3e2e0'
typography:
  headline-lg:
    fontFamily: Work Sans
    fontSize: 32px
    fontWeight: '700'
    lineHeight: '1.2'
  headline-md:
    fontFamily: Work Sans
    fontSize: 24px
    fontWeight: '600'
    lineHeight: '1.3'
  headline-sm:
    fontFamily: Work Sans
    fontSize: 20px
    fontWeight: '600'
    lineHeight: '1.4'
  body-lg:
    fontFamily: Work Sans
    fontSize: 18px
    fontWeight: '400'
    lineHeight: '1.6'
  body-md:
    fontFamily: Work Sans
    fontSize: 16px
    fontWeight: '400'
    lineHeight: '1.5'
  body-sm:
    fontFamily: Work Sans
    fontSize: 14px
    fontWeight: '400'
    lineHeight: '1.5'
  label-md:
    fontFamily: Work Sans
    fontSize: 12px
    fontWeight: '600'
    lineHeight: '1'
    letterSpacing: 0.05em
  label-sm:
    fontFamily: Work Sans
    fontSize: 11px
    fontWeight: '500'
    lineHeight: '1'
    letterSpacing: 0.02em
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  base: 8px
  xs: 4px
  sm: 12px
  md: 24px
  lg: 40px
  xl: 64px
  gutter: 16px
  margin: 24px
---

## Brand & Style

The design system is built on the philosophy of "Warm Minimalism." It rejects the cold, sterile aesthetic common in industrial ERPs in favor of an atmosphere that feels like a sunlit morning on a well-run farm. The target audience includes poultry shop owners, warehouse managers, and retail staff who require high-speed data entry and clear inventory visibility.

The style is strictly **Minimalist** with a focus on functional clarity. By stripping away gradients, heavy shadows, and unnecessary decorations, the system ensures that information—such as bird counts, feed levels, and sales data—remains the focal point. The emotional response is one of organized calm: professional enough to handle complex logistics, yet approachable enough for local retail operations.

## Colors

This design system utilizes a high-visibility, warm palette optimized for the varied display quality found in retail and warehouse environments (e.g., POS terminals, older LCD monitors). 

- **Primary (Burnt Orange):** Used for primary actions and critical status indicators. It provides a strong visual anchor without the harshness of pure red.
- **Secondary (Yolk Yellow):** Used for warnings, highlighting secondary information, and subtle accents. It provides warmth and energy.
- **Neutral (Warm Eggshell):** The foundation of the UI. Backgrounds use a warm white (#FAF9F6) to reduce eye strain compared to stark #FFFFFF.
- **Accents:** Deep browns are used for text and iconography instead of pure black to maintain the warm, organic characteristic of the poultry industry.

Avoid cool blues or purples entirely to ensure the brand identity remains distinct and thematic.

## Typography

**Work Sans** is the sole typeface for this design system. It was selected for its exceptional legibility on low-resolution screens and its grounded, professional character. 

- **Headlines:** Use Bold (700) or SemiBold (600) weights to create clear section breaks in data-heavy screens.
- **Body Text:** Standard inventory lists and descriptions use the Regular (400) weight. 
- **Labels:** Small labels for table headers and input captions use SemiBold (600) with a slight letter spacing increase to ensure readability at small sizes.

Maintain high contrast between text and background at all times. Use the tertiary deep brown for all primary text to keep the interface feeling soft yet legible.

## Layout & Spacing

The layout utilizes a **fluid grid** system to accommodate various screen sizes from mobile inventory scanners to wide-screen office desktops. 

- **Grid:** A 12-column grid system with 16px gutters and 24px outer margins.
- **Rhythm:** An 8px baseline grid dictates all vertical spacing. Elements should be spaced in increments of 8px (8, 16, 24, 32, etc.) to maintain a tight, organized ERP structure.
- **Density:** Provide two density modes: "Standard" for retail/POS use (larger touch targets) and "Compact" for back-office inventory management (more rows visible per screen).

## Elevation & Depth

This design system is strictly **Flat**. It avoids the use of shadows to ensure the UI remains performant and visually clean on lower-end hardware.

- **Low-contrast Outlines:** Depth is created using 1px or 2px solid borders. Use a slightly darker warm-grey border (#E5E2DA) to define card boundaries and input fields against the warm white background.
- **Tonal Layers:** High-priority areas (like the active sidebar or a highlighted table row) should use a subtle fill of the secondary color at a very low opacity (5-10%) rather than a shadow.
- **Active States:** Use solid color blocks for active states (e.g., a selected navigation item should have a primary color fill).

## Shapes

The shape language is **Soft (Level 1)**. 

- **Standard Radius:** All buttons, input fields, and cards utilize a 4px (0.25rem) corner radius. This creates a professional look that is slightly more approachable than sharp 90-degree corners, without feeling overly consumer-grade or "playful."
- **Large Radius:** Larger containers or modal overlays may use 8px (0.5rem) to signify a different functional layer.
- **Icons:** Use stroke-based icons with rounded caps to match the soft-cornered UI elements.

## Components

- **Buttons:** Use solid fills for primary actions (Primary Orange with White text). Use 1px bordered buttons for secondary actions. No shadows; use a 10% darker fill on hover.
- **Input Fields:** 1px border (#E5E2DA) with a warm-white fill. On focus, the border changes to the Primary Orange.
- **Data Tables:** The core of the ERP. Use zebra-striping with a very faint warm-yellow (#FFFBEB) to distinguish rows. Headers should be sticky with a slightly thicker bottom border.
- **Chips/Badges:** Used for status (e.g., "In Stock", "Out of Stock"). Use highly saturated background colors with dark text for maximum visibility on shop-floor tablets.
- **Cards:** Simple containers with a 1px border. Do not use background colors for cards; keep them the same as the page background to maintain a "flat" hierarchy.
- **Inventory Metrics:** Large, bold numerical displays for critical counts (e.g., "Total Birds") using the headline-lg style and Primary Orange color.