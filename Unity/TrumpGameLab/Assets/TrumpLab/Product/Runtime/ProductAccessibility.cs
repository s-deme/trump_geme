#nullable enable

using System;
using UnityEngine;

namespace TrumpLab.Product
{
    public enum ProductGraphicRole
    {
        Background = 0,
        Surface = 1,
        NormalText = 2,
        LargeText = 3,
        MutedText = 4,
        ControlBackground = 5,
        ControlText = 6,
        ActiveControlBackground = 7,
        DisabledControlBackground = 8,
        DisabledControlText = 9,
        FocusIndicator = 10,
        PositiveBackground = 11,
        PositiveText = 12,
        ErrorBackground = 13,
        ErrorText = 14
    }

    public static class ProductWcagContrast
    {
        public const double NormalTextMinimum = 4.5d;
        public const double LargeTextMinimum = 3d;
        public const double FocusIndicatorMinimum = 3d;
        public const double ActiveControlMinimum = 3d;

        public static double RelativeLuminance(Color color)
        {
            double red = Linearize(Mathf.Clamp01(color.r));
            double green = Linearize(Mathf.Clamp01(color.g));
            double blue = Linearize(Mathf.Clamp01(color.b));
            return (0.2126d * red) + (0.7152d * green) + (0.0722d * blue);
        }

        public static double ContrastRatio(Color first, Color second)
        {
            double firstLuminance = RelativeLuminance(first);
            double secondLuminance = RelativeLuminance(second);
            double lighter = Math.Max(firstLuminance, secondLuminance);
            double darker = Math.Min(firstLuminance, secondLuminance);
            return (lighter + 0.05d) / (darker + 0.05d);
        }

        public static bool MeetsNormalText(Color foreground, Color background) =>
            ContrastRatio(foreground, background) >= NormalTextMinimum;

        public static bool MeetsLargeText(Color foreground, Color background) =>
            ContrastRatio(foreground, background) >= LargeTextMinimum;

        public static bool MeetsFocusIndicator(Color foreground, Color background) =>
            ContrastRatio(foreground, background) >= FocusIndicatorMinimum;

        public static bool MeetsActiveControl(Color foreground, Color background) =>
            ContrastRatio(foreground, background) >= ActiveControlMinimum;

        private static double Linearize(double component) => component <= 0.04045d
            ? component / 12.92d
            : Math.Pow((component + 0.055d) / 1.055d, 2.4d);
    }

    public sealed class ProductUiPalette
    {
        private static readonly ProductUiPalette NormalPalette = new ProductUiPalette(
            highContrast: false,
            background: Rgb(0x10, 0x18, 0x20),
            surface: Rgb(0x1B, 0x26, 0x3B),
            normalText: Rgb(0xF2, 0xEE, 0xD2),
            largeText: Rgb(0xFF, 0xFF, 0xFF),
            mutedText: Rgb(0xC6, 0xD1, 0xDC),
            controlBackground: Rgb(0x1F, 0x70, 0x48),
            controlText: Rgb(0xFF, 0xFF, 0xFF),
            activeControlBackground: Rgb(0x0B, 0x5C, 0xAD),
            disabledControlBackground: Rgb(0x4A, 0x55, 0x68),
            disabledControlText: Rgb(0xFF, 0xFF, 0xFF),
            focusIndicator: Rgb(0xFF, 0xD1, 0x66),
            positiveBackground: Rgb(0x17, 0x6B, 0x3A),
            positiveText: Rgb(0xFF, 0xFF, 0xFF),
            errorBackground: Rgb(0x8E, 0x1B, 0x1B),
            errorText: Rgb(0xFF, 0xFF, 0xFF));

        private static readonly ProductUiPalette HighContrastPalette = new ProductUiPalette(
            highContrast: true,
            background: Rgb(0x00, 0x00, 0x00),
            surface: Rgb(0x10, 0x10, 0x10),
            normalText: Rgb(0xFF, 0xFF, 0xFF),
            largeText: Rgb(0xFF, 0xFF, 0xFF),
            mutedText: Rgb(0xE6, 0xE6, 0xE6),
            controlBackground: Rgb(0x00, 0x5A, 0x9C),
            controlText: Rgb(0xFF, 0xFF, 0xFF),
            activeControlBackground: Rgb(0x7A, 0x4E, 0x00),
            disabledControlBackground: Rgb(0x59, 0x59, 0x59),
            disabledControlText: Rgb(0xFF, 0xFF, 0xFF),
            focusIndicator: Rgb(0xFF, 0xD8, 0x00),
            positiveBackground: Rgb(0x00, 0x6B, 0x2B),
            positiveText: Rgb(0xFF, 0xFF, 0xFF),
            errorBackground: Rgb(0x9B, 0x00, 0x00),
            errorText: Rgb(0xFF, 0xFF, 0xFF));

        private ProductUiPalette(bool highContrast, Color background, Color surface,
            Color normalText, Color largeText, Color mutedText, Color controlBackground,
            Color controlText, Color activeControlBackground,
            Color disabledControlBackground, Color disabledControlText,
            Color focusIndicator, Color positiveBackground, Color positiveText,
            Color errorBackground, Color errorText)
        {
            IsHighContrast = highContrast;
            Background = background;
            Surface = surface;
            NormalText = normalText;
            LargeText = largeText;
            MutedText = mutedText;
            ControlBackground = controlBackground;
            ControlText = controlText;
            ActiveControlBackground = activeControlBackground;
            DisabledControlBackground = disabledControlBackground;
            DisabledControlText = disabledControlText;
            FocusIndicator = focusIndicator;
            PositiveBackground = positiveBackground;
            PositiveText = positiveText;
            ErrorBackground = errorBackground;
            ErrorText = errorText;
        }

        public static ProductUiPalette Normal => NormalPalette;
        public static ProductUiPalette HighContrast => HighContrastPalette;

        public bool IsHighContrast { get; }
        public Color Background { get; }
        public Color Surface { get; }
        public Color NormalText { get; }
        public Color LargeText { get; }
        public Color MutedText { get; }
        public Color ControlBackground { get; }
        public Color ControlText { get; }
        public Color ActiveControlBackground { get; }
        public Color DisabledControlBackground { get; }
        public Color DisabledControlText { get; }
        public Color FocusIndicator { get; }
        public Color PositiveBackground { get; }
        public Color PositiveText { get; }
        public Color ErrorBackground { get; }
        public Color ErrorText { get; }

        public Color ColorFor(ProductGraphicRole role)
        {
            switch (role)
            {
                case ProductGraphicRole.Background: return Background;
                case ProductGraphicRole.Surface: return Surface;
                case ProductGraphicRole.NormalText: return NormalText;
                case ProductGraphicRole.LargeText: return LargeText;
                case ProductGraphicRole.MutedText: return MutedText;
                case ProductGraphicRole.ControlBackground: return ControlBackground;
                case ProductGraphicRole.ControlText: return ControlText;
                case ProductGraphicRole.ActiveControlBackground:
                    return ActiveControlBackground;
                case ProductGraphicRole.DisabledControlBackground:
                    return DisabledControlBackground;
                case ProductGraphicRole.DisabledControlText: return DisabledControlText;
                case ProductGraphicRole.FocusIndicator: return FocusIndicator;
                case ProductGraphicRole.PositiveBackground: return PositiveBackground;
                case ProductGraphicRole.PositiveText: return PositiveText;
                case ProductGraphicRole.ErrorBackground: return ErrorBackground;
                case ProductGraphicRole.ErrorText: return ErrorText;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role,
                        "Unknown product graphic role.");
            }
        }

        private static Color Rgb(byte red, byte green, byte blue) =>
            new Color32(red, green, blue, byte.MaxValue);
    }
}
