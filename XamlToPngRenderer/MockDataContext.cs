using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.IO;
using System.Web.Script.Serialization; // Requires reference to System.Web.Extensions

namespace XamlToPngRenderer
{
    public class MockDataContext
    {
        private readonly bool _verbose;

        public MockDataContext(bool verbose)
        {
            _verbose = verbose;
        }

        public object LoadContext(string jsonPath)
        {
            if (string.IsNullOrEmpty(jsonPath) || !File.Exists(jsonPath))
                return null;

            string json = File.ReadAllText(jsonPath);
            var serializer = new JavaScriptSerializer();
            
            try 
            {
                var dict = serializer.Deserialize<Dictionary<string, object>>(json);
                return ToExpando(dict);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing JSON context: {ex.Message}");
                if (_verbose) Console.WriteLine(ex.StackTrace);
                return null;
            }
        }

        private object ToExpando(object obj)
        {
            if (obj is Dictionary<string, object> dict)
            {
                var expando = new ExpandoObject() as IDictionary<string, object>;
                foreach (var kvp in dict)
                {
                    expando[kvp.Key] = ToExpando(kvp.Value);
                }
                return expando;
            }
            else if (obj is System.Collections.ArrayList list) // JavaScriptSerializer deserializes arrays to ArrayList
            {
                var resultList = new List<object>();
                foreach (var item in list)
                {
                    resultList.Add(ToExpando(item));
                }
                return resultList;
            }
            else if (obj is object[] arr)
            {
                 var resultList = new List<object>();
                foreach (var item in arr)
                {
                    resultList.Add(ToExpando(item));
                }
                return resultList;
            }
            return obj;
        }
    }
}
