# UI Asset Replacement Template & Guidelines

This document outlines the directory structure and naming convention for the new UI asset system in UnknownInventoryTerminal. The system now supports separate background and foreground/sprite assets for all terminal components.

## Directory Structure

Place your custom PNG files in the `Assets/AE2_New/` directory following this structure:

- `Assets/AE2_New/terminal/`
    - `background.png` (Main container background)
- `Assets/AE2_New/states/`
    - `background.png` (States background)
    - `foreground.png` (States foreground/hover state)
- `Assets/AE2_New/text_field/`
    - `background.png` (Text field container)
- `Assets/AE2_New/scroller/`
    - `background.png` (Scroller background)
    - `disabled.png` (Disabled scroller state)

## Naming & Usage Guidelines

- **Category**: Corresponds to the subdirectory (e.g., "terminal", "states").
- **Name**: Corresponds to the file name without the `.png` extension (e.g., "background", "foreground", "disabled").

## Transparency & Layering

- **Background PNGs**: Should contain the base structural UI without any text or icons if possible. Use standard transparency for anti-aliasing.
- **Foreground/Sprite PNGs**: Should contain icons, state indicators, or overlays.
- **Dimensions**: Follow the sizes required by the component layout. For sliced assets, ensure your borders are handled correctly in the asset file (or through code configuration if needed).

## Example Replacement Workflow

1.  Create your custom `background.png` for a specific category.
2.  Place it in `Assets/AE2_New/<category>/background.png`.
3.  The mod will automatically load this file, falling back to the old assets if it is missing.
