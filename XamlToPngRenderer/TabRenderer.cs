using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XamlToPngRenderer
{
    public class TabRenderer
    {
        private readonly Renderer _renderer;
        private readonly bool _verbose;

        public TabRenderer(Renderer renderer, bool verbose)
        {
            _renderer = renderer;
            _verbose = verbose;
        }

        public void RenderTabs(FrameworkElement rootElement, string outputBasePath, int width, int height, int dpi)
        {
            // Traverse visual tree to find TabControls
            // Note: rootElement must be loaded/initialized (done by Renderer usually but we split logic here)
            // Actually, Renderer code assumes it parses XAML then renders.
            // For Tabs, we need to load once, then iterate.
            
            // This method assumes 'rootElement' is already the visual tree root (Window or Control)
            // passed from the main rendering loop before final RenderTargetBitmap call.
            
            // But Renderer.Render is a monolithic method.
            // We should modify Renderer to expose a step or allow us to Drive it.
            
            // Alternatively, we look for TabControl in the element.
            // Since we need to change state (SelectedIndex) and re-render, we need access to the bitmap generation logic.
            
            // Refactor: We will implement this as:
            // Find TabControl -> Loop Items -> Render each to file.
            
            var tabControl = FindVisualChild<TabControl>(rootElement);
            if (tabControl == null)
            {
                Console.WriteLine("No TabControl found for --tabs generation.");
                return;
            }

            int count = tabControl.Items.Count;
            for (int i = 0; i < count; i++)
            {
                tabControl.SelectedIndex = i;
                rootElement.UpdateLayout(); // Force layout update
                
                // Allow UI to refresh bindings if any (synchronously)
                
                string tabHeader = "Tab" + i;
                if (tabControl.Items[i] is TabItem ti && ti.Header != null)
                {
                    tabHeader = ti.Header.ToString();
                    // Sanitize filename
                    foreach (char c in Path.GetInvalidFileNameChars())
                    {
                        tabHeader = tabHeader.Replace(c, '_');
                    }
                }
                
                string suffix = $"_{tabHeader}";
                string outputPath = Path.Combine(
                    Path.GetDirectoryName(outputBasePath),
                    Path.GetFileNameWithoutExtension(outputBasePath) + suffix + ".png"
                );
                
                // Re-use renderer's bitmap generation?
                // We can't access Renderer's private SaveBitmap or logic easily if we don't expose it.
                // We should add a method to Renderer "RenderElement(FrameworkElement, path, ...)"
                // But Render method does parsing too.
                
                // Let's call a public method on Renderer that takes the Element and saves it.
                // I will add `RenderElementToFile` to Renderer.
                
                _renderer.RenderElementToFile(rootElement, outputPath, width, height, dpi);
                Console.WriteLine($"Generated tab image: {outputPath}");
            }
        }

        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T typed) return typed;
                
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}
