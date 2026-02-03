using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace XamlToPngRenderer
{
    // A very basic YAML parser that handles the structure of scenarios.yaml
    // Supports:
    // - Key: Value
    // - Lists defined by "- "
    // - Nested objects via indentation
    public class SimpleYamlParser
    {
        public object Parse(string yamlPath)
        {
            if (!File.Exists(yamlPath)) return null;
            
            var lines = File.ReadAllLines(yamlPath);
            var root = new Dictionary<string, object>();
            
            ParseBlock(lines, 0, root);
            
            return root;
        }

        private int ParseBlock(string[] lines, int startLine, Dictionary<string, object> parent, int currentIndent = 0)
        {
            int i = startLine;
            string lastKey = null;
            
            while (i < lines.Length)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                {
                    i++;
                    continue; // Skip empty or comment
                }

                int indent = GetIndent(line);
                if (indent < currentIndent) return i; // End of block

                string content = line.Trim();
                
                if (content.StartsWith("- "))
                {
                    // List item
                    if (lastKey != null && parent[lastKey] is List<object> list)
                    {
                         // Check if it's a scalar list or object list
                         string val = content.Substring(2).Trim();
                         if (string.IsNullOrEmpty(val))
                         {
                             // Object list item
                             var item = new Dictionary<string, object>();
                             list.Add(item);
                             // If next lines are indented further, parse into this item
                             // But YAML lists with "- " typically imply the object starts at this line but indented effectively
                             // Or the properties are on following lines.
                             // For "- name: foo", it's an object with key name.
                             
                             // Let's handle generic case:
                             // - name: foo
                             //   other: bar
                             
                             // We parse the current line as a key-value or empty
                             // But the indentation of children of this list item aligns with the text after "- " ?
                             // Or usually 2 spaces more than the "-"?
                             
                             // Simple hack for our specific scenario file:
                             // - name: disconnected
                             //   context_overrides: ...
                             
                             // Treat the current line content as part of the object if it has a key
                             
                             if (val.Contains(":"))
                             {
                                 // It has a key on the same line
                                 // Let's re-process this line as if it was "  name: disconnected" but inside the list item
                                 // indent of properties should be indent + 2 usually
                                 
                                 // Actually, we can just recursively call ParseBlock for the list item
                                 // The indent for the block should be indent + something
                                 
                                 // Let's assume the indent of the list item children is same as indent of '- ' + 2
                                 
                                 // Process the current line's key/value
                                 ParseLineInto(val, item);
                                 
                                 // Continue parsing next lines into this item until indent drops
                                 i = ParseBlock(lines, i + 1, item, indent + 2); // Recursive call
                                 continue;
                             }
                             else
                             {
                                 // Scalar or block start
                                 i = ParseBlock(lines, i + 1, item, indent + 2);
                                 continue; 
                             }
                         }
                    }
                    else
                    {
                         // List starting? But where is the key?
                         // YAML structure: 
                         // key:
                         //   - item
                         
                         // We should have encountered the kye before.
                    }
                }
                else if (content.Contains(":"))
                {
                    // Key: Value
                    int colonIndex = content.IndexOf(':');
                    string key = content.Substring(0, colonIndex).Trim();
                    string val = content.Substring(colonIndex + 1).Trim();
                    
                    lastKey = key;
                    
                    if (string.IsNullOrEmpty(val))
                    {
                        // Block or List start
                        // Check next line to see if it starts with "- " (List) or normal indent (Object)
                        if (i + 1 < lines.Length)
                        {
                            string nextLine = lines[i + 1];
                            // Skip empty/commands
                            int nextI = i + 1;
                            while(nextI < lines.Length && (string.IsNullOrWhiteSpace(lines[nextI]) || lines[nextI].TrimStart().StartsWith("#"))) nextI++;
                            
                            if (nextI < lines.Length)
                            {
                                int nextIndent = GetIndent(lines[nextI]);
                                if (nextIndent > indent)
                                {
                                    string nextContent = lines[nextI].Trim();
                                    if (nextContent.StartsWith("- "))
                                    {
                                        var list = new List<object>();
                                        parent[key] = list;
                                        i = ParseBlock(lines, nextI, parent, nextIndent); // Parse list
                                        continue; 
                                    }
                                    else
                                    {
                                        var obj = new Dictionary<string, object>();
                                        parent[key] = obj;
                                        i = ParseBlock(lines, nextI, obj, nextIndent);
                                        continue;
                                    }
                                }
                            }
                        }
                        parent[key] = null;
                        i++;
                    }
                    else
                    {
                        // Scalar
                        parent[key] = ParseScalar(val);
                        i++;
                    }
                }
                else
                {
                    // Continuation? Ignore for now
                    i++;
                }
            }
            return i;
        }
        
        private void ParseLineInto(string content, Dictionary<string, object> target)
        {
             if (content.Contains(":"))
             {
                 var parts = content.Split(new[] {':'}, 2);
                 string key = parts[0].Trim();
                 string val = parts[1].Trim();
                 if (!string.IsNullOrEmpty(val))
                 {
                     target[key] = ParseScalar(val);
                 }
             }
        }
        
        private object ParseScalar(string val)
        {
            if (val == "true") return true;
            if (val == "false") return false;
            if (int.TryParse(val, out int i)) return i;
            if (val.StartsWith("\"") && val.EndsWith("\"")) return val.Substring(1, val.Length - 2);
            return val;
        }

        private int GetIndent(string line)
        {
            int spaces = 0;
            foreach (char c in line)
            {
                if (c == ' ') spaces++;
                else break;
            }
            return spaces;
        }
    }
}
