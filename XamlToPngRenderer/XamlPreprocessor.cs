using System;
using System.Xml.Linq;
using System.Linq;

namespace XamlToPngRenderer
{
    public class XamlPreprocessor
    {
        private readonly bool _verbose;

        public XamlPreprocessor(bool verbose)
        {
            _verbose = verbose;
        }

        private void Log(string message)
        {
            if (_verbose)
                Console.WriteLine($"[DEBUG] {message}");
        }

        public string ProcessXaml(string xamlContent)
        {
            var doc = XDocument.Parse(xamlContent);
            Log($"Root element: {doc.Root.Name.LocalName}");

            // 1. Remove x:Class attribute
            XNamespace xNs = "http://schemas.microsoft.com/winfx/2006/xaml";
            var classAttr = doc.Root.Attribute(xNs + "Class");
            if (classAttr != null)
            {
                Log($"Removing x:Class attribute: {classAttr.Value}");
                classAttr.Remove();
            }
            
            // 2. Remove specific namespace declarations (like viewmodels clr-namespace)
            // Implementation for Feature 5 will go here
            RemoveClrNamespaces(doc.Root);

            // 3. Remove event handlers
            RemoveEventHandlers(doc.Root);
            
            // 4. Remove DataType attributes that reference types
            RemoveDataTypeAttributes(doc.Root);

            // 5. Convert StaticResource to DynamicResource (to allow late binding of external resources)
            // This is done on the string representation because replacing markup extensions in XDocument is hard
            string xaml = doc.ToString();
            xaml = ConvertStaticToDynamicResource(xaml);
            
            return xaml;
        }

        private string ConvertStaticToDynamicResource(string xaml)
        {
            // Simple regex replacement for {StaticResource Key} -> {DynamicResource Key}
            // This avoids "Provide value on 'System.Windows.StaticResourceExtension' threw an exception"
            // during XamlReader.Parse when resources haven't been added yet.
            return System.Text.RegularExpressions.Regex.Replace(xaml, @"\{StaticResource\s+", "{DynamicResource ");
        }

        private void RemoveEventHandlers(XElement element)
        {
            string[] eventAttributes = { 
                "Click", "Loaded", "Closing", "Closed", "MouseDown",
                "MouseUp", "KeyDown", "KeyUp", "TextChanged", "SelectionChanged",
                "MouseDoubleClick", "MouseEnter", "MouseLeave"
            };

            foreach (var attrName in eventAttributes)
            {
                var attr = element.Attribute(attrName);
                if (attr != null)
                {
                    Log($"Removing event handler: {attrName}=\"{attr.Value}\"");
                    attr.Remove();
                }
            }

            foreach (var child in element.Elements())
            {
                RemoveEventHandlers(child);
            }
        }

        private void RemoveClrNamespaces(XElement element)
        {
            // Remove xmlns that start with clr-namespace
            var attributes = element.Attributes().ToList();
            foreach (var attr in attributes)
            {
                if (attr.IsNamespaceDeclaration && attr.Value.StartsWith("clr-namespace:"))
                {
                     Log($"Removing CLR namespace: {attr.Name}=\"{attr.Value}\"");
                     attr.Remove();
                }
            }
            
            foreach (var child in element.Elements())
            {
                RemoveClrNamespaces(child);
            }
        }
        
        private void RemoveDataTypeAttributes(XElement element)
        {
             // Remove DataType="{x:Type ...}" often used in DataTemplates
             var attr = element.Attribute("DataType");
             if (attr != null && attr.Value.Contains("{x:Type"))
             {
                 Log($"Removing DataType attribute: {attr.Value}");
                 attr.Remove();
             }
             
             foreach (var child in element.Elements())
             {
                 RemoveDataTypeAttributes(child);
             }
        }
    }
}
