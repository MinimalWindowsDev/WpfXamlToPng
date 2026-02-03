using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XamlToPngRenderer
{
    public class Renderer
    {
        private readonly bool _verbose;
        private readonly XamlPreprocessor _preprocessor;

        public Renderer(bool verbose)
        {
            _verbose = verbose;
            _preprocessor = new XamlPreprocessor(verbose);
        }

        private void Log(string message)
        {
            if (_verbose)
                Console.WriteLine($"[DEBUG] {message}");
        }

        public void Render(string xamlPath, string pngPath, int? width, int? height, int dpi, 
            ResourceDictionary resources = null, object dataContext = null, Action<FrameworkElement> preRenderAction = null)
        {
            Log($"Loading XAML from: {xamlPath}");
            var xamlContent = File.ReadAllText(xamlPath);
            
            // Preprocess
            var cleanXaml = _preprocessor.ProcessXaml(xamlContent);
            
            // Parse
            FrameworkElement element;
            try
            {
                element = (FrameworkElement)XamlReader.Parse(cleanXaml);
            }
            catch (Exception ex)
            {
                 // Fallback: try removing more aggressive things if parse fails?
                 // For now just rethrow
                 throw new Exception($"Failed to parse XAML: {ex.Message}", ex);
            }
            
            // Apply Resources
            if (resources != null)
            {
                Log("Merging external resources...");
                if (element.Resources == null) element.Resources = new ResourceDictionary();
                element.Resources.MergedDictionaries.Add(resources);
            }

            // Apply DataContext
            if (dataContext != null)
            {
                Log("Setting DataContext...");
                element.DataContext = dataContext;
            }

            // Determine size
            double renderWidth = width ?? element.Width;
            double renderHeight = height ?? element.Height;

            if (dataContext == null && (double.IsNaN(renderWidth) || double.IsNaN(renderHeight)))
            {
                // If it's a Window, we might be able to use its Width/Height properties if they were set in XAML
                // But xaml definition usually has them.
                if (double.IsNaN(renderWidth)) renderWidth = 800; // Default fallback
                if (double.IsNaN(renderHeight)) renderHeight = 600;
                Log($"Size not specified, defaulting to {renderWidth}x{renderHeight}");
            }
            else if (double.IsNaN(renderWidth) || double.IsNaN(renderHeight))
            {
                 throw new InvalidOperationException("Window/Control has no explicit size. Provide -w and -h arguments.");
            }

            // Prepare for rendering (Window vs Control)
            FrameworkElement renderTarget = PrepareRenderTarget(element, renderWidth, renderHeight);

            // Layout
            var size = new Size(renderWidth, renderHeight);
            Log("Calling Measure & Arrange...");
            renderTarget.Measure(size);
            renderTarget.Arrange(new Rect(size));
            
            // Allow state modification (e.g. tabs)
            preRenderAction?.Invoke(renderTarget);
            
            renderTarget.UpdateLayout();

            // Force visual tree
            ApplyTemplatesRecursively(renderTarget);

            // Render
            double scale = dpi / 96.0;
            int pixelWidth = (int)(renderWidth * scale);
            int pixelHeight = (int)(renderHeight * scale);
            
            var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(renderTarget);
            
            // Save
            SaveBitmap(bitmap, pngPath);
            Log("Render complete.");
        }

        public void RenderElementToFile(FrameworkElement element, string pngPath, double width, double height, int dpi)
        {
            double scale = dpi / 96.0;
            int pixelWidth = (int)(width * scale);
            int pixelHeight = (int)(height * scale);
            
            var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
            bitmap.Render(element);
            
            // Save
            SaveBitmap(bitmap, pngPath);
        }
        
        private FrameworkElement PrepareRenderTarget(FrameworkElement element, double width, double height)
        {
             if (element is Window window)
             {
                 // Detach content
                 var content = window.Content as UIElement;
                 window.Content = null;
                 
                 var container = new Border
                 {
                     Width = width,
                     Height = height,
                     Background = window.Background ?? Brushes.White,
                     Child = content
                 };
                 // If Window had resources directly, we might lose them unless we copy them to container or sub-elements
                 // But we added merged dicts to 'element' (the window).
                 // The content is now child of border. Resources allow inheritance.
                 // However, the Window properties itself are lost.
                 // We should move Resources to the Border if possible
                 
                 if (window.Resources.Count > 0 || window.Resources.MergedDictionaries.Count > 0)
                 {
                     container.Resources = window.Resources;
                 }
                 
                 return container;
             }
             else
             {
                 if (element is Control c && c.Background == null)
                 {
                     c.Background = Brushes.White;
                 }
                 return element;
             }
        }

        private void SaveBitmap(BitmapSource bitmap, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string outputDir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            using (var stream = File.Create(path))
            {
                encoder.Save(stream);
            }
        }

        private void ApplyTemplatesRecursively(DependencyObject obj)
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
    }
}
