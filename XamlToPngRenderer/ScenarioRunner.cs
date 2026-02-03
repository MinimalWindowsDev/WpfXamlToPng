using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace XamlToPngRenderer
{
    public class ScenarioRunner
    {
        private readonly Renderer _renderer;
        private readonly ResourceLoader _resourceLoader;
        private readonly MockDataContext _contextLoader;
        private readonly bool _verbose;

        public ScenarioRunner(bool verbose)
        {
            _verbose = verbose;
            _renderer = new Renderer(verbose);
            _resourceLoader = new ResourceLoader(verbose);
            _contextLoader = new MockDataContext(verbose);
        }

        public void RunScenarios(string scenarioPath)
        {
            if (!File.Exists(scenarioPath))
            {
                Console.WriteLine($"Error: Scenario file not found: {scenarioPath}");
                return;
            }

            Console.WriteLine($"Processing scenarios from: {scenarioPath}");
            var parser = new SimpleYamlParser();
            var data = parser.Parse(scenarioPath) as Dictionary<string, object>;

            if (data == null)
            {
                Console.WriteLine("Error: Failed to parse scenario file.");
                return;
            }

            // Read base options
            string baseRes = GetString(data, "base_resources");
            string baseCtx = GetString(data, "base_context");
            string outputDir = GetString(data, "output_dir") ?? "output";
            
            // Resolve relative paths
            string dir = Path.GetDirectoryName(scenarioPath);
            if (!string.IsNullOrEmpty(baseRes)) baseRes = Path.Combine(dir, baseRes);
            if (!string.IsNullOrEmpty(baseCtx)) baseCtx = Path.Combine(dir, baseCtx);
            outputDir = Path.Combine(dir, outputDir);

            // Load base resources/context
            ResourceDictionary resources = null;
            if (baseRes != null) resources = _resourceLoader.LoadResources(baseRes);

            object globalContext = null;
            if (baseCtx != null) globalContext = _contextLoader.LoadContext(baseCtx);

            var scenarios = data["scenarios"] as List<object>;
            if (scenarios == null)
            {
                Console.WriteLine("No scenarios found in file.");
                return;
            }

            // Need target XAML? The scenarios YAML doesn't seem to specify the target XAML in the example?
            // "XamlToPngRenderer.exe -s scenarios.yaml"
            // The request says: "XamlToPngRenderer.exe -s scenarios.yaml"
            // The example yaml has `output: MainWindow_Connected.png`. It doesn't say `input: MainWindow.xaml`.
            // Maybe it assumes it's implied or I missed it?
            // "Feature 4: State Variations via Scenario Files"
            // The command only takes `-s scenarios.yaml`.
            // Either the YAML specifies the input file or we assume it matches `MainWindow.xaml`?
            // Or maybe the CLI should still accept input file? `XamlToPngRenderer.exe -s yaml input.xaml`?
            // The example usage: `XamlToPngRenderer.exe -s scenarios.yaml`.
            // The YAML example lacks `input`.
            // I will assume the YAML *should* have `input` or I'll default to looking for `input` key in global/scenario.
            // Or maybe I am supposed to pass the XAML file as argument along with -s?
            // The prompt "XamlToPngRenderer.exe -s scenarios.yaml" implies standalone.
            // I will look for `target_xaml` in YAML (global or per scenario).
            
            // Let's verify prompt text again.
            // "XamlToPngRenderer.exe -s scenarios.yaml" -> "output: MainWindow_Disconnected.png".
            // It doesn't show `input:` in yaml.
            // I will add `target_xaml` support to my parser/implementation and I'll add access to it in the yaml for my test.
            
            string globalTarget = GetString(data, "target") ?? GetString(data, "input");
            if (globalTarget != null) globalTarget = Path.Combine(dir, globalTarget);

            if (scenarios != null)
            {
                foreach (var sObj in scenarios)
                {
                    if (sObj is Dictionary<string, object> s)
                    {
                        string name = GetString(s, "name");
                        string output = GetString(s, "output");
                        string input = GetString(s, "input") ?? globalTarget;
                        bool tabs = s.ContainsKey("tabs") ? (bool)ParseBool(s["tabs"]) : false;
                        int? tabIndex = s.ContainsKey("tab_index") ? (int?)Convert.ToInt32(s["tab_index"]) : null;

                        if (input == null)
                        {
                            Console.WriteLine($"Error: No input XAML specified for scenario '{name}'.");
                            continue;
                        }
                        
                        input = Path.Combine(dir, input); // Ensure absolute if relative to yaml
                        
                        if (string.IsNullOrEmpty(output))
                        {
                            output = $"{name}.png";
                        }
                        
                        string fullOutputPath = Path.Combine(outputDir, output);

                        Console.WriteLine($"Running scenario: {name}");

                        // Clone context?
                        // We need a fresh context for each scenario if we modify it
                        // Ideally we reload or deep clone. ExpandoObject doesn't support deep clone easily.
                        // Simplest: Reload context if baseCtx is present.
                        object context = null;
                        if (baseCtx != null) context = _contextLoader.LoadContext(baseCtx);
                        
                        // Apply overrides
                        if (s.ContainsKey("context_overrides") && s["context_overrides"] is Dictionary<string, object> overrides)
                        {
                            ApplyOverrides(context, overrides);
                        }

                        // Run Renderer
                        // We can't easily pass tab_index to Renderer.Render...
                        // If tab_index is set, we need to iterate tabs or set explicit tab.
                        // Implemented Renderer doesn't support setting TabIndex.
                        // TabRenderer iterates all tabs.
                        
                        // If "tab_index" is specified: Render just that tab?
                        // If "tabs" is true: Render all tabs (generating multiple files).
                        
                        // We need to extend Renderer or handle it here via TabRenderer logic
                        // But TabRenderer takes an ALREADY loaded element?
                        // No, TabRenderer.RenderTabs iterates.
                        
                        // If we want to render a Specific tab index, we need logic for that.
                        
                        // Let's implement basic render first.
                        try
                        {
                             // We probably need to modify Renderer to allow us to manipulate the element BEFORE rendering but AFTER loading/context
                             // Or we add `Action<FrameworkElement>` callback to Render method.
                             
                             Action<FrameworkElement> preRenderAction = (element) => {
                                 if (tabIndex.HasValue)
                                 {
                                     var tabControl = FindVisualChild<System.Windows.Controls.TabControl>(element);
                                     if (tabControl != null)
                                     {
                                         tabControl.SelectedIndex = tabIndex.Value;
                                     }
                                 }
                             };

                             // Render main image (or just trigger render to get element)
                             FrameworkElement loadedElement = null;
                             Action<FrameworkElement> combinedAction = (e) => {
                                 preRenderAction(e);
                                 loadedElement = e;
                             };
                             
                             _renderer.Render(input, fullOutputPath, null, null, 96, resources, context, combinedAction);
                             
                             // Handle "tabs: true" - generate all tabs
                             if (tabs && loadedElement != null)
                             {
                                 var tabRenderer = new TabRenderer(_renderer, _verbose);
                                 tabRenderer.RenderTabs(loadedElement, fullOutputPath, 
                                     (int)(loadedElement.Width > 0 ? loadedElement.Width : 800), 
                                     (int)(loadedElement.Height > 0 ? loadedElement.Height : 600), 
                                     96);
                             }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Scenario '{name}' failed: {ex.Message}");
                        }
                    }
                }
            }
        }

        private string GetString(Dictionary<string, object> dict, string key)
        {
            if (dict.ContainsKey(key) && dict[key] != null) return dict[key].ToString();
            return null;
        }
        
        private bool ParseBool(object val)
        {
            if (val is bool b) return b;
            return val.ToString().ToLower() == "true";
        }

        private void ApplyOverrides(object context, Dictionary<string, object> overrides)
        {
            if (context is IDictionary<string, object> dict)
            {
                foreach (var kvp in overrides)
                {
                    dict[kvp.Key] = kvp.Value;
                }
            }
        }
        
        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null) return null;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child is T typed) return typed;
                var res = FindVisualChild<T>(child);
                if (res != null) return res;
            }
            return null;
        }
    }
}
