using System;
using System.IO;
using System.Windows;

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
            string batchDir = null;
            string resourcePath = null;
            string imageBase = null;


            string contextPath = null;
            string scenarioPath = null;
            bool generateTabs = false;
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
                        if (i + 1 < args.Length) width = int.Parse(args[++i]);
                        break;
                    case "-h":
                    case "--height":
                        if (i + 1 < args.Length) height = int.Parse(args[++i]);
                        break;
                    case "-d":
                    case "--dpi":
                        if (i + 1 < args.Length) dpi = int.Parse(args[++i]);
                        break;
                    case "-r":
                    case "--resources":
                        if (i + 1 < args.Length) resourcePath = args[++i];
                        break;
                    case "-c":
                    case "--context":
                        if (i + 1 < args.Length) contextPath = args[++i];
                        break;
                    case "-s":
                    case "--scenario":
                        if (i + 1 < args.Length) scenarioPath = args[++i];
                        break;
                    case "-t":
                    case "--tabs":
                        generateTabs = true;
                        break;
                    case "-b":
                    case "--batch":
                        if (i + 1 < args.Length) batchDir = args[++i];
                        break;
                    case "-i":
                    case "--image-base":
                        if (i + 1 < args.Length) imageBase = args[++i];
                        break;
                    default:
                        if (inputPath == null)
                            inputPath = args[i];
                        else if (outputPath == null)
                            outputPath = args[i];
                        break;
                }
            }

            if ((inputPath == null || outputPath == null) && scenarioPath == null && batchDir == null)
            {
                PrintUsage();
                return 1;
            }
            
            // Scenario mode handles everything if present
            if (scenarioPath != null)
            {
                 var runner = new ScenarioRunner(_verbose);
                 runner.RunScenarios(scenarioPath);
                 return 0;
            }
            
            // Batch mode handles directory iteration
            if (batchDir != null)
            {
                if (outputPath == null) 
                {
                    Console.WriteLine("Error: Output directory must be specified for batch mode.");
                    return 1;
                }
                
                if (!Directory.Exists(batchDir))
                {
                    Console.WriteLine($"Error: Batch directory not found: {batchDir}");
                    return 1;
                }
                
                string[] files = Directory.GetFiles(batchDir, "*.xaml");
                Console.WriteLine($"Batch processing {files.Length} files from {batchDir}...");
                
                // Initialize shared components
                var renderer = new Renderer(_verbose);
                var resLoader = new ResourceLoader(_verbose);
                var ctxLoader = new MockDataContext(_verbose);
                
                ResourceDictionary resources = null;
                if (resourcePath != null) resources = resLoader.LoadResources(resourcePath);
                
                object dataContext = null;
                if (contextPath != null) dataContext = ctxLoader.LoadContext(contextPath);
                
                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    string outFile = Path.Combine(outputPath, fileName + ".png");
                    try
                    {
                        Console.WriteLine($"Processing: {fileName}");
                        renderer.Render(file, outFile, width, height, dpi, resources, dataContext, null, imageBase);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to process {fileName}: {ex.Message}");
                    }
                }
                return 0;
            }

            if (!File.Exists(inputPath))
            {
                Console.WriteLine($"Error: Input file not found: {inputPath}");
                return 1;
            }

            try
            {
                // Initialize components
                var renderer = new Renderer(_verbose);
                var resLoader = new ResourceLoader(_verbose);
                var ctxLoader = new MockDataContext(_verbose);

                // Load dependencies
                ResourceDictionary resources = null;
                if (resourcePath != null)
                {
                    resources = resLoader.LoadResources(resourcePath);
                    if (resources == null) Console.WriteLine("Warning: Failed to load resources.");
                }

                object dataContext = null;
                if (contextPath != null)
                {
                    dataContext = ctxLoader.LoadContext(contextPath);
                    if (dataContext == null) Console.WriteLine("Warning: Failed to load context.");
                }

                // Capture element for tab processing
                FrameworkElement loadedElement = null;
                Action<FrameworkElement> captureAction = (e) => loadedElement = e;

                // Render
                renderer.Render(inputPath, outputPath, width, height, dpi, resources, dataContext, captureAction, imageBase);
                
                // Process Tabs if requested
                if (generateTabs && loadedElement != null)
                {
                     Console.WriteLine("Generating tab screenshots...");
                     var tabRenderer = new TabRenderer(renderer, _verbose);
                     tabRenderer.RenderTabs(loadedElement, outputPath, width ?? (int)loadedElement.Width, height ?? (int)loadedElement.Height, dpi);
                }
                
                Console.WriteLine($"Success: {outputPath}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (_verbose)
                {
                    Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        Console.WriteLine(ex.InnerException.StackTrace);
                    }
                }
                return 1;
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: XamlToPngRenderer.exe [options] <input.xaml> <output.png>");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -v, --verbose     Enable verbose debug logging");
            Console.WriteLine("  -w, --width N     Render width in pixels (default: from XAML)");
            Console.WriteLine("  -h, --height N    Render height in pixels (default: from XAML)");
            Console.WriteLine("  -d, --dpi N       DPI for rendering (default: 96)");
            Console.WriteLine("  -r, --resources   Path to App.xaml or ResourceDictionary");
            Console.WriteLine("  -c, --context     Path to JSON file for DataContext");
            Console.WriteLine("  -s, --scenario    Path to scenarios.yaml file");
            Console.WriteLine("  -t, --tabs        Generate screenshots for each TabItem");
            Console.WriteLine("  -b, --batch       Directory containing XAML files to process");
            Console.WriteLine("  -i, --image-base  Base directory for resolving relative image paths");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  XamlToPngRenderer.exe -r App.xaml -c data.json MainWindow.xaml output.png");
            Console.WriteLine("  XamlToPngRenderer.exe -s scenarios.yaml");
            Console.WriteLine("  XamlToPngRenderer.exe -r App.xaml -i Images/ MainWindow.xaml output.png");
            Console.WriteLine("  XamlToPngRenderer.exe -b Views/ -r App.xaml output_dir/");
        }
    }
}