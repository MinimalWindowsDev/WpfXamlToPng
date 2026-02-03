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
            ResourceDictionary resources = null, object dataContext = null, Action<FrameworkElement> preRenderAction = null, string imageBase = null)
        {
            Log($"Loading XAML from: {xamlPath}");
            var xamlContent = File.ReadAllText(xamlPath);
            
            // Preprocess
            var cleanXaml = _preprocessor.ProcessXaml(xamlContent);
            if (!string.IsNullOrEmpty(imageBase))
            {
                cleanXaml = _preprocessor.ResolveImagePaths(cleanXaml, imageBase);
            }
            
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
                Log($"DataContext type: {dataContext?.GetType().Name}");
                Log($"DataContext contents: {DumpObject(dataContext)}");
            }

            // Determine size
            double renderWidth = width ?? element.Width;
            double renderHeight = height ?? element.Height;

            if (dataContext == null && (double.IsNaN(renderWidth) || double.IsNaN(renderHeight)))
            {
                 // Check if XAML has bindings
                 if (cleanXaml.Contains("{Binding"))
                 {
                     Log("WARNING: XAML contains {Binding} expressions but no DataContext (-c) was provided. Bindings will fail.");
                 }

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

            // Layout - First pass
            var size = new Size(renderWidth, renderHeight);
            Log("Calling Measure & Arrange...");
            renderTarget.Measure(size);
            renderTarget.Arrange(new Rect(size));
            renderTarget.UpdateLayout();
            
            // ISSUE 2 FIX: STEP 1 - Expand all Expanders FIRST (before images)
            Log("Pass 1: Expanding all Expanders...");
            ExpandAllExpanders(renderTarget);
            
            // STEP 2: Force layout regeneration after expansion
            renderTarget.UpdateLayout();
            ApplyTemplatesRecursively(renderTarget);
            renderTarget.UpdateLayout();
            renderTarget.Measure(size);
            renderTarget.Arrange(new Rect(size));
            renderTarget.UpdateLayout();
            
            // STEP 3: NOW resolve images (visual tree is complete)
            string imageBasePath = imageBase;
            if (string.IsNullOrEmpty(imageBasePath))
            {
                 imageBasePath = Path.GetDirectoryName(Path.GetFullPath(xamlPath));
            }
            Log("Pass 2: Resolving images...");
            ResolveImages(renderTarget, imageBasePath);
            
            // Allow state modification (e.g. tabs)
            preRenderAction?.Invoke(renderTarget);
            
            // Re-measure after modifications
            renderTarget.UpdateLayout();
            renderTarget.Measure(size);
            renderTarget.Arrange(new Rect(size));
            renderTarget.UpdateLayout();

            // Force visual tree
            ApplyTemplatesRecursively(renderTarget);
            
            // Final layout pass
            renderTarget.UpdateLayout();

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

        private string DumpObject(object obj, int depth = 0)
        {
            if (obj == null) return "null";
            if (depth > 2) return "...";
            
            var sb = new System.Text.StringBuilder();
            if (obj is System.Collections.IDictionary dict)
            {
                foreach (System.Collections.DictionaryEntry kvp in dict)
                {
                    sb.AppendLine($"{"".PadLeft(depth*2)}{kvp.Key}: {DumpObject(kvp.Value, depth+1)}");
                }
            }
            // IDictionary<string, object> is common for ExpandoObject
            else if (obj is System.Collections.Generic.IDictionary<string, object> genericDict)
            {
                foreach (var kvp in genericDict)
                {
                    sb.AppendLine($"{"".PadLeft(depth*2)}{kvp.Key}: {DumpObject(kvp.Value, depth+1)}");
                }
            }
            else if (obj is System.Collections.IEnumerable list && !(obj is string))
            {
                int count = 0;
                foreach(var item in list) count++;
                sb.AppendLine($"[{count} items]");
                if (depth < 1)
                {
                     foreach(var item in list)
                     {
                         sb.AppendLine($"{"".PadLeft(depth*2)}- {DumpObject(item, depth+1)}");
                     }
                }
            }
            else
            {
                sb.Append(obj.ToString());
            }
            return sb.ToString();
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
                 
                 // If Window had resources directly, copy them to container
                 if (window.Resources.Count > 0 || window.Resources.MergedDictionaries.Count > 0)
                 {
                     container.Resources = window.Resources;
                 }
                 
                 // CRITICAL FIX: Propagate DataContext to the new container!
                 // When content is moved from Window to Border, it loses DataContext inheritance
                 container.DataContext = window.DataContext;
                 Log($"Propagated DataContext to Border: {container.DataContext != null}");
                 
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

        // ISSUE 2 FIX: Separate method to expand all Expanders before image resolution
        private void ExpandAllExpanders(DependencyObject root)
        {
            if (root is Expander expander && !expander.IsExpanded)
            {
                Log($"Expanding Expander: {expander.Name ?? "(unnamed)"}");
                expander.IsExpanded = true;
            }
            
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                ExpandAllExpanders(VisualTreeHelper.GetChild(root, i));
            }
        }

        private void ResolveImages(FrameworkElement root, string basePath)
        {
             // Simple recursive finder for Images
             ProcessElementForImages(root, basePath);
        }
        
        private void ProcessElementForImages(DependencyObject obj, string basePath)
        {
            if (obj is Image img)
            {
                Log($"Scanning for images in visual tree...");
                Log($"Found Image control, Source type: {img.Source?.GetType().Name ?? "null"}");
                Log($"Image.Source value: {(img.Source as BitmapImage)?.UriSource?.ToString() ?? "null"}");

                try 
                {
                    // Attempt to fix relative paths if they failed to load
                    if (img.Source is BitmapImage bi) 
                    {
                        // If uriSource is relative, try to resolve it against basePath
                        if (bi.UriSource != null && !bi.UriSource.IsAbsoluteUri && !string.IsNullOrEmpty(basePath))
                        {
                            var absPath = Path.Combine(basePath, bi.UriSource.ToString());
                            if (File.Exists(absPath))
                            {
                                Log($"Fixing relative image path: {bi.UriSource} -> {absPath}");
                                var newBi = new BitmapImage();
                                newBi.BeginInit();
                                newBi.UriSource = new Uri(absPath);
                                newBi.CacheOption = BitmapCacheOption.OnLoad; // Force load now
                                newBi.EndInit();
                                img.Source = newBi;
                            }
                            else
                            {
                                Log($"Warning: Image not found at {absPath}");
                            }
                        }
                    }
                    else if (img.Source == null && !string.IsNullOrEmpty(basePath))
                    {
                         // If Source is null, it might be a binding failure OR a parse failure of a relative path.
                         // Try to manually resolve binding
                         ResolveBindingImageSource(img, basePath);
                    }
                }
                catch (Exception e)
                {
                    Log($"Error processing image: {e.Message}");
                }
            }
            
            // NOTE: Expander expansion is now done separately in ExpandAllExpanders
            // before this method is called, so visual tree is already complete

            int count = VisualTreeHelper.GetChildrenCount(obj);
            for(int i=0; i<count; i++)
            {
                ProcessElementForImages(VisualTreeHelper.GetChild(obj, i), basePath);
            }
        }

        // ISSUE 5 FIX: Enhanced binding resolution with multiple fallback methods
        private void ResolveBindingImageSource(Image img, string basePath)
        {
             try
             {
                 var bindingExpr = System.Windows.Data.BindingOperations.GetBindingExpression(img, Image.SourceProperty);
                 if (bindingExpr == null)
                 {
                     Log("  No binding expression on Image.Source");
                     return;
                 }
                 
                 var binding = bindingExpr.ParentBinding;
                 var path = binding?.Path?.Path;
                 Log($"  Image has binding path: '{path}'");
                 
                 var dataItem = bindingExpr.DataItem;
                 if (dataItem == null)
                 {
                     Log("  WARNING: Binding DataItem is null - DataContext not set or binding failed");
                     return;
                 }
                 
                 Log($"  DataItem type: {dataItem.GetType().Name}");
                 
                 // Try multiple ways to get the value
                 object value = null;
                 
                 // Method 1: Indexer (for BindableWrapper/Dictionary)
                 var indexer = dataItem.GetType().GetProperty("Item", new[] { typeof(string) });
                 if (indexer != null)
                 {
                     value = indexer.GetValue(dataItem, new object[] { path });
                     if (value != null) Log($"  Retrieved via indexer: {value}");
                 }
                 
                 // Method 2: Direct property
                 if (value == null)
                 {
                     var prop = dataItem.GetType().GetProperty(path);
                     if (prop != null)
                     {
                         value = prop.GetValue(dataItem);
                         if (value != null) Log($"  Retrieved via property: {value}");
                     }
                 }
                 
                 // Method 3: TryGetMember for DynamicObject
                 if (value == null && dataItem is System.Dynamic.DynamicObject dynObj)
                 {
                     var binder = new SimpleGetMemberBinder(path);
                     dynObj.TryGetMember(binder, out value);
                     if (value != null) Log($"  Retrieved via DynamicObject: {value}");
                 }
                 
                 // Now resolve the value
                 if (value is string imagePath && !string.IsNullOrEmpty(imagePath))
                 {
                     var absPath = Path.IsPathRooted(imagePath) 
                         ? imagePath 
                         : Path.GetFullPath(Path.Combine(basePath, imagePath));
                         
                     if (File.Exists(absPath))
                     {
                         Log($"  Resolving bound image: {imagePath} -> {absPath}");
                         var bi = new BitmapImage();
                         bi.BeginInit();
                         bi.UriSource = new Uri(absPath);
                         bi.CacheOption = BitmapCacheOption.OnLoad;
                         bi.EndInit();
                         img.Source = bi;
                     }
                     else
                     {
                         Log($"  WARNING: Bound image file not found: {absPath}");
                     }
                 }
                 else if (value is BitmapImage)
                 {
                     Log($"  Image already has BitmapImage value");
                 }
                 else
                 {
                     Log($"  WARNING: Could not resolve image value (type: {value?.GetType().Name ?? "null"})");
                 }
             }
             catch (Exception ex)
             {
                 Log($"  ERROR in ResolveBindingImageSource: {ex.Message}");
             }
        }
        
        // Helper class for DynamicObject.TryGetMember
        private class SimpleGetMemberBinder : System.Dynamic.GetMemberBinder
        {
            public SimpleGetMemberBinder(string name) : base(name, false) { }
            public override System.Dynamic.DynamicMetaObject FallbackGetMember(System.Dynamic.DynamicMetaObject target, System.Dynamic.DynamicMetaObject errorSuggestion) => null;
        }
    }
}
