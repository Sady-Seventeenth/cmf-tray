using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using MediaColor = System.Windows.Media.Color;

namespace CmfTray;

public sealed class WallpaperTheme
{
    public MediaColor Accent { get; init; }
    public MediaColor ContrastOnAccent { get; init; }
    public MediaColor CardBg { get; init; }
    public MediaColor AccentSubtle { get; init; }
    public uint AcrylicGradient { get; init; }

    public static WallpaperTheme Extract()
    {
        try
        {
            Color rawColor = GetWallpaperOrDwmColor();
            // Ensure accent has good visibility/saturation on dark backgrounds
            var hsv = RgbToHsv(rawColor);
            
            // Saturation minimum 0.4, Lightness/Value at least 0.65 for punchy accent on dark cards
            hsv.s = Math.Clamp(hsv.s, 0.40f, 1.0f);
            hsv.v = Math.Clamp(hsv.v, 0.65f, 0.95f);
            Color accentRgb = HsvToRgb(hsv.h, hsv.s, hsv.v);

            // Contrast text/icon color on top of accent
            double lum = (0.299 * accentRgb.R + 0.587 * accentRgb.G + 0.114 * accentRgb.B) / 255.0;
            MediaColor contrast = lum > 0.55
                ? MediaColor.FromRgb(0x14, 0x16, 0x18)
                : MediaColor.FromRgb(0xFF, 0xFF, 0xFF);

            // Dark card background: deep dark tint of the wallpaper hue
            Color darkTint = HsvToRgb(hsv.h, 0.25f, 0.14f);
            MediaColor cardBg = MediaColor.FromArgb(0xEA, darkTint.R, darkTint.G, darkTint.B);

            // Subtle accent tint for cards / hover
            MediaColor subtle = MediaColor.FromArgb(0x35, accentRgb.R, accentRgb.G, accentRgb.B);

            // Acrylic blur gradient: AABBGGRR for SetWindowCompositionAttribute
            uint acrylic = 0xAA000000 | ((uint)darkTint.B << 16) | ((uint)darkTint.G << 8) | (uint)darkTint.R;

            return new WallpaperTheme
            {
                Accent = MediaColor.FromRgb(accentRgb.R, accentRgb.G, accentRgb.B),
                ContrastOnAccent = contrast,
                CardBg = cardBg,
                AccentSubtle = subtle,
                AcrylicGradient = acrylic
            };
        }
        catch
        {
            // Default clean fallback
            return Default();
        }
    }

    public static WallpaperTheme Default() => new()
    {
        Accent = MediaColor.FromRgb(0xD7, 0x19, 0x20), // Nothing iconic red
        ContrastOnAccent = MediaColor.FromRgb(0xFF, 0xFF, 0xFF),
        CardBg = MediaColor.FromArgb(0xEA, 0x16, 0x18, 0x1B),
        AccentSubtle = MediaColor.FromArgb(0x33, 0xD7, 0x19, 0x20),
        AcrylicGradient = 0xAA181616
    };

    private static Color GetWallpaperOrDwmColor()
    {
        // 1. Try reading wallpaper image file
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            string? wpPath = key?.GetValue("Wallpaper") as string;
            if (!string.IsNullOrEmpty(wpPath) && File.Exists(wpPath))
            {
                using var fs = new FileStream(wpPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var bmp = new Bitmap(fs);
                using var thumb = new Bitmap(48, 48);
                using (var g = Graphics.FromImage(thumb))
                {
                    g.DrawImage(bmp, 0, 0, 48, 48);
                }

                Color? best = null;
                float bestScore = -1f;

                for (int x = 0; x < 48; x++)
                {
                    for (int y = 0; y < 48; y++)
                    {
                        Color p = thumb.GetPixel(x, y);
                        var hsv = RgbToHsv(p);

                        // Avoid pure blacks / pure whites
                        if (hsv.v > 0.15f && hsv.v < 0.95f && hsv.s > 0.15f)
                        {
                            float score = hsv.s * (1.0f - Math.Abs(hsv.v - 0.65f));
                            if (score > bestScore)
                            {
                                bestScore = score;
                                best = p;
                            }
                        }
                    }
                }

                if (best.HasValue) return best.Value;
            }
        }
        catch { }

        // 2. Fallback to Windows DWM accent color
        try
        {
            using var dwmKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (dwmKey?.GetValue("AccentColor") is int dwmVal)
            {
                // DWM AccentColor is stored as 0xAABBGGRR
                byte r = (byte)(dwmVal & 0xFF);
                byte g = (byte)((dwmVal >> 8) & 0xFF);
                byte b = (byte)((dwmVal >> 16) & 0xFF);
                if (r != 0 || g != 0 || b != 0)
                    return Color.FromArgb(r, g, b);
            }
        }
        catch { }

        return Color.FromArgb(0xD7, 0x19, 0x20);
    }

    private static (float h, float s, float v) RgbToHsv(Color c)
    {
        float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float delta = max - min;

        float h = 0f;
        if (delta > 0.0001f)
        {
            if (max == r) h = (g - b) / delta + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / delta + 2;
            else h = (r - g) / delta + 4;
            h /= 6f;
        }

        float s = max <= 0 ? 0 : delta / max;
        return (h, s, max);
    }

    private static Color HsvToRgb(float h, float s, float v)
    {
        if (s <= 0)
        {
            int val = (int)(v * 255);
            return Color.FromArgb(val, val, val);
        }

        h = (h - (float)Math.Floor(h)) * 6.0f;
        int i = (int)Math.Floor(h);
        float f = h - i;
        float p = v * (1.0f - s);
        float q = v * (1.0f - s * f);
        float t = v * (1.0f - s * (1.0f - f));

        float r = 0, g = 0, b = 0;
        switch (i)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }

        return Color.FromArgb(
            Math.Clamp((int)(r * 255), 0, 255),
            Math.Clamp((int)(g * 255), 0, 255),
            Math.Clamp((int)(b * 255), 0, 255));
    }
}
