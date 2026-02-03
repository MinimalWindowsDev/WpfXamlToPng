using System;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;

namespace XamlToPngRenderer
{
    public class ResourceLoader
    {
        private readonly bool _verbose;
        private readonly XamlPreprocessor _preprocessor;

        public ResourceLoader(bool verbose)
        {
            _verbose = verbose;
            _preprocessor = new XamlPreprocessor(verbose);
        }

        private void Log(string message)
        {
            if (_verbose)
                Console.WriteLine($"[DEBUG] {message}");
        }

        public ResourceDictionary LoadResources(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (!File.Exists(path))
            {
                Console.WriteLine($"Warning: Resource file not found: {path}");
                return null;
            }

            Log($"Loading resources from: {path}");

            // For App.xaml, we often have <Application.Resources>...
            // We need to extract the ResourceDictionary part or load the whole thing if it is a ResourceDictionary.
            
            // Simple approach: Parse XAML, look for ResourceDictionary
            // If it's an Application, grab contents of Application.Resources
            
            try 
            {
                string xamlContent = File.ReadAllText(path);
                var doc = XDocument.Parse(xamlContent);
                
                // If root is Application, strip it down to just the resources
                if (doc.Root.Name.LocalName == "Application")
                {
                    Log("Detected Application definition. Extracting Resources...");
                    
                    // We need to parse strictly the ResourceDictionary or manually construct one
                    // Easier: Parse the App.xaml cleaning it first
                    
                    // Actually, XamlReader.Load cannot load "Application" root easily in partial trust/simple app domain sometimes?
                    // Let's manually extracting the child <Application.Resources> -> <ResourceDictionary>
                    
                    var outputResources = doc.Root.Elements()
                        .FirstOrDefault(e => e.Name.LocalName == "Resources" || e.Name.LocalName == "Application.Resources");
                        
                    if (outputResources != null && outputResources.Elements().Any())
                    {
                        var resourceDictElement = outputResources.Elements().First(); // Should be ResourceDictionary
                        
                        // We need to clean this XAML too (remove CLR namespaces, etc)
                        string resXaml = resourceDictElement.ToString();
                        
                        // ISSUE 1 FIX: Preprocess the resource XAML before parsing
                        resXaml = _preprocessor.ProcessXaml(resXaml);
                        Log($"Preprocessed resource XAML ({resXaml.Length} chars)");
                        
                        try
                        {
                            var rd = (ResourceDictionary)XamlReader.Parse(resXaml);
                            Log($"Successfully loaded {rd.Count} resources");
                            return rd;
                        }
                        catch (Exception parseEx)
                        {
                            Log($"ERROR loading resources: {parseEx.Message}");
                            // Return empty dict rather than null to allow continued operation
                            return new ResourceDictionary();
                        }
                    }
                }
                else if (doc.Root.Name.LocalName == "ResourceDictionary")
                {
                     // Direct resource dictionary
                     return (ResourceDictionary)XamlReader.Load(File.OpenRead(path));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading resources: {ex.Message}");
                if (_verbose) Console.WriteLine(ex.StackTrace);
            }

            return null;
        }
    }
}
