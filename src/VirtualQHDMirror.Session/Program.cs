using System;
using System.Linq;
using System.Windows.Forms;
using VirtualQHDMirror.Core.Displays;
using VirtualQHDMirror.Core.Input;
using VirtualQHDMirror.Core.Rendering;

namespace VirtualQHDMirror.Session;

internal static class Program
{
    [STAThread]
    private static int Main(
        string[] args)
    {
        try
        {
            if (!SessionArguments.TryParse(
                    args,
                    out var options))
            {
                return 10;
            }

            Application.SetHighDpiMode(
                HighDpiMode.PerMonitorV2);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(
                false);

            var discovery =
                new DisplayDiscoveryService();

            var result =
                discovery.DiscoverActiveDisplays();

            var source =
                result.Displays.FirstOrDefault(
                    display =>
                        string.Equals(
                            display.SourceName,
                            options.SourceDevice,
                            StringComparison.OrdinalIgnoreCase));

            var destination =
                result.Displays.FirstOrDefault(
                    display =>
                        string.Equals(
                            display.SourceName,
                            options.DestinationDevice,
                            StringComparison.OrdinalIgnoreCase));

            if (source is null ||
                destination is null)
            {
                return 11;
            }

            var monitorRects =
                result.Displays
                    .Where(display =>
                        display.Width > 0 &&
                        display.Height > 0)
                    .Select(display =>
                        new PixelRect(
                            display.X,
                            display.Y,
                            display.Width,
                            display.Height))
                    .ToArray();

            using var form =
                new MirrorSessionForm(
                    source,
                    destination,
                    monitorRects,
                    options.RenderMode);

            Application.Run(
                form);

            return form.RenderFailureOccurred
                ? 20
                : 0;
        }
        catch
        {
            return 99;
        }
    }

    private sealed record SessionArguments(
        string SourceDevice,
        string DestinationDevice,
        MirrorRenderMode RenderMode)
    {
        public static bool TryParse(
            string[] args,
            out SessionArguments options)
        {
            options = null!;

            string? source = null;
            string? destination = null;
            var renderMode =
                MirrorRenderMode.Adaptive;

            for (var index = 0;
                 index < args.Length;
                 index++)
            {
                switch (args[index])
                {
                    case "--source-device"
                        when index + 1 < args.Length:
                        source = args[++index];
                        break;

                    case "--destination-device"
                        when index + 1 < args.Length:
                        destination = args[++index];
                        break;

                    case "--render-mode"
                        when index + 1 < args.Length:
                        if (!TryParseRenderMode(
                                args[++index],
                                out renderMode))
                        {
                            return false;
                        }

                        break;

                    default:
                        return false;
                }
            }

            if (string.IsNullOrWhiteSpace(
                    source) ||
                string.IsNullOrWhiteSpace(
                    destination))
            {
                return false;
            }

            options = new SessionArguments(
                source,
                destination,
                renderMode);

            return true;
        }

        private static bool TryParseRenderMode(
            string value,
            out MirrorRenderMode mode)
        {
            mode = value.ToLowerInvariant() switch
            {
                "adaptive" =>
                    MirrorRenderMode.Adaptive,

                "30" =>
                    MirrorRenderMode.Fixed30,

                "60" =>
                    MirrorRenderMode.Fixed60,

                "120" =>
                    MirrorRenderMode.Experimental120,

                _ =>
                    (MirrorRenderMode)(-1)
            };

            return Enum.IsDefined(
                mode);
        }
    }
}
