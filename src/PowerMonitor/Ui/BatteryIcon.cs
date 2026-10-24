using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using PowerMonitor.Core.Settings;

namespace PowerMonitor.Ui;

internal static class BatteryIcon
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon Create(int? soc, bool? gridOn, bool failed, int pixels = 32)
    {
        pixels = Math.Clamp(pixels, 16, 256);
        using var bitmap = new Bitmap(pixels, pixels, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.Clear(Color.Transparent);
        var inset = Math.Max(1, pixels / 16);
        using (var brush = new SolidBrush(ColorFor(soc, gridOn, failed)))
            graphics.FillRectangle(brush, inset, inset, pixels - inset * 2, pixels - inset * 2);

        var text = soc?.ToString() ?? (failed ? "!" : "--");
        var fontPixels = pixels * (text.Length >= 3 ? 7.5f : 10f) / 32f;
        using var font = new Font("Segoe UI", fontPixels, FontStyle.Bold, GraphicsUnit.Pixel);
        var size = graphics.MeasureString(text, font);
        using var textBrush = new SolidBrush(Color.FromArgb(22, 24, 28));
        graphics.DrawString(text, font, textBrush, (pixels - size.Width) / 2f, (pixels - size.Height) / 2f);

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static Color ColorFor(int? soc, bool? gridOn, bool failed)
    {
        if (soc is <= AppSettings.LowSocPercent || (failed && soc is null))
            return Color.FromArgb(255, 111, 125);
        if (gridOn == false)
            return Color.FromArgb(255, 180, 94);
        if (soc is null)
            return Color.FromArgb(143, 165, 174);
        return Color.FromArgb(98, 214, 176);
    }
}
