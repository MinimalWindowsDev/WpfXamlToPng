using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace XamlToPngRenderer
{
    class Program
    {
        private static bool _verbose = false;

        [STAThread]
        static int Main(string[] args)
        {
            // Parse arguments
            string inputPath = null;
            string outputPath = null;
            int? width = null;
            int? height = null;
            int dpi = 96;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-v":
                    case "--verbose":
                        _verbose = true;
                        break;
                    case "-w":
                    case "--width":
                        width = int.Parse(args[++i]);
                        break;
                    case "-h":
                    case "--height":
                        height = int.Parse(args[++i]);
                        break;
                    case "-d":
                    case "--dpi":
                        dpi = int.Parse(args[++i]);
                        break;
                    default:
                        if (inputPath == null)
                            inputPath = args[i];
                        else if (outputPath == null)
                            outputPath = args[i];
                        break;
                }
            }

            if (inputPath == null || outputPath == null)
            {
                Console.WriteLine("Usage: XamlToPngRenderer.exe [options] <input.xaml> <output.png>");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  -v, --verbose     Enable verbose debug logging");
                Console.WriteLine("  -w, --width N     Render width in pixels (default: from XAML)");
                Console.WriteLine("  -h, --height N    Render height in pixels (default: from XAML)");
                Console.WriteLine("  -d, --dpi N       DPI for rendering (default: 96)");
                Console.WriteLine();
                Console.WriteLine("Example:");
                Console.WriteLine("  XamlToPngRenderer.exe -v MainWindow.xaml output.png");
                return 1;
            }

            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"Error: Input file not found: {inputPath}");
                return 1;
            }

            try
            {
                RenderXamlToPng(inputPath, outputPath, width, height, dpi);
                Console.WriteLine($"Success: {outputPath}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (_verbose)
                {
                    Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
                }
                return 1;
            }
        }

        static void Log(string message)
        {
            if (_verbose)
                Console.WriteLine($"[DEBUG] {message}");
        }

        static void RenderXamlToPng(string xamlPath, string pngPath, int? width, int? height, int dpi)
        {
            Log($"Loading XAML from: {xamlPath}");

            // Load and preprocess XAML to remove x:Class attribute
            var xamlContent = File.ReadAllText(xamlPath);
            var doc = XDocument.Parse(xamlContent);

            Log($"Root element: {doc.Root.Name.LocalName}");

            // Remove x:Class attribute (prevents "Specified class name doesn't match" error)
            XNamespace xNs = "http://schemas.microsoft.com/winfx/2006/xaml";
            var classAttr = doc.Root.Attribute(xNs + "Class");
            if (classAttr != null)
            {
                Log($"Removing x:Class attribute: {classAttr.Value}");
                classAttr.Remove();
            }

            // Also remove any event handler attributes (they reference code-behind methods)
            RemoveEventHandlers(doc.Root);

            // Parse the cleaned XAML
            Log("Parsing cleaned XAML...");
            FrameworkElement element;
            using (var reader = doc.CreateReader())
            {
                element = System.Windows.Markup.XamlReader.Load(reader) as FrameworkElement;
            }

            if (element == null)
            {
                throw new InvalidOperationException("XAML root must be a FrameworkElement");
            }

            Log($"Parsed element type: {element.GetType().FullName}");
            Log($"Element.Width: {element.Width} (NaN means Auto)");
            Log($"Element.Height: {element.Height} (NaN means Auto)");

            // Determine size
            double renderWidth = width ?? element.Width;
            double renderHeight = height ?? element.Height;

            if (double.IsNaN(renderWidth) || double.IsNaN(renderHeight))
            {
                throw new InvalidOperationException(
                    "Window/Control has no explicit size. Provide -w and -h arguments.");
            }

            Log($"Render size: {renderWidth} x {renderHeight}");

            // Handle Window specially - extract content and ensure background
            FrameworkElement renderTarget = element;
            Brush backgroundBrush = Brushes.White;

            if (element is Window window)
            {
                Log("Element is a Window - extracting content for rendering");

                // Get background from Window if set
                if (window.Background != null)
                {
                    backgroundBrush = window.Background;
                    Log($"Window.Background: {window.Background}");
                }
                else
                {
                    Log("Window.Background is null - will use white background");
                }

                // Get content reference and detach from window FIRST
                var content = window.Content as UIElement;
                window.Content = null;
                Log($"Detached content: {content?.GetType().Name ?? "null"}");

                // Create a container that mimics the window's content area
                var container = new Border
                {
                    Width = renderWidth,
                    Height = renderHeight,
                    Background = backgroundBrush,
                    Child = content
                };

                renderTarget = container;
                Log($"Created Border container with Background: {backgroundBrush}");
            }
            else if (element is Control control)
            {
                Log($"Element is a Control - Background: {control.Background}");
                if (control.Background == null)
                {
                    control.Background = Brushes.White;
                    Log("Set default white background on Control");
                }
            }

            var size = new Size(renderWidth, renderHeight);

            // Measure and Arrange (required for off-screen rendering)
            Log("Calling Measure...");
            renderTarget.Measure(size);
            Log($"DesiredSize after Measure: {renderTarget.DesiredSize}");

            Log("Calling Arrange...");
            renderTarget.Arrange(new Rect(size));
            Log($"ActualWidth x ActualHeight after Arrange: {renderTarget.ActualWidth} x {renderTarget.ActualHeight}");

            Log("Calling UpdateLayout...");
            renderTarget.UpdateLayout();

            // Force visual tree creation
            Log("Calling ApplyTemplate on visual tree...");
            ApplyTemplatesRecursively(renderTarget);

            // Render to bitmap
            double scale = dpi / 96.0;
            int pixelWidth = (int)(renderWidth * scale);
            int pixelHeight = (int)(renderHeight * scale);

            Log($"Bitmap size: {pixelWidth} x {pixelHeight} pixels at {dpi} DPI (scale: {scale})");

            var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);

            Log("Rendering to bitmap...");
            bitmap.Render(renderTarget);

            // Check if bitmap is empty
            if (_verbose)
            {
                var pixels = new byte[pixelWidth * pixelHeight * 4];
                bitmap.CopyPixels(pixels, pixelWidth * 4, 0);
                int nonZeroPixels = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i] != 0) nonZeroPixels++;
                }
                Log($"Non-zero bytes in bitmap: {nonZeroPixels} / {pixels.Length}");
                if (nonZeroPixels == 0)
                {
                    Log("WARNING: Bitmap is completely empty/transparent!");
                }
            }

            // Encode and save as PNG
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string outputDir = Path.GetDirectoryName(pngPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Log($"Creating output directory: {outputDir}");
                Directory.CreateDirectory(outputDir);
            }

            Log($"Saving PNG to: {pngPath}");
            using (var fileStream = File.Create(pngPath))
            {
                encoder.Save(fileStream);
            }

            Log("Done.");
        }

        static void ApplyTemplatesRecursively(DependencyObject obj)
        {
            if (obj is FrameworkElement fe)
            {
                fe.ApplyTemplate();
            }

            int childCount = VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                ApplyTemplatesRecursively(child);
            }
        }

        static void RemoveEventHandlers(XElement element)
        {
            // Common event handler attributes to remove
            string[] eventAttributes = { "Click", "Loaded", "Closing", "Closed", "MouseDown",
                "MouseUp", "KeyDown", "KeyUp", "TextChanged", "SelectionChanged" };

            foreach (var attr in eventAttributes)
            {
                var removed = element.Attribute(attr);
                if (removed != null)
                {
                    if (_verbose) Console.WriteLine($"[DEBUG] Removing event handler: {attr}");
                    removed.Remove();
                }
            }

            foreach (var child in element.Elements())
            {
                RemoveEventHandlers(child);
            }
        }
    }
}