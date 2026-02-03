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
                var wrapper = new BindableWrapper();
                foreach (var kvp in dict)
                {
                    wrapper.SetMember(kvp.Key, ToExpando(kvp.Value));
                }
                return wrapper;
            }
            else if (obj is System.Collections.ArrayList list)
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

    // ISSUE 4 FIX: BindableWrapper with ICustomTypeDescriptor for DataGrid binding
    public class BindableWrapper : DynamicObject, INotifyPropertyChanged, ICustomTypeDescriptor
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

        public void SetMember(string name, object value)
        {
            _values[name] = value;
            OnPropertyChanged(name);
        }
        
        // Expose keys for iteration
        public IEnumerable<string> Keys => _values.Keys;

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return _values.TryGetValue(binder.Name, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            SetMember(binder.Name, value);
            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Check for indexer access (dictionary style)
        public object this[string key]
        {
            get => _values.ContainsKey(key) ? _values[key] : null;
            set => SetMember(key, value);
        }
        
        // ICustomTypeDescriptor implementation for WPF DataGrid binding
        public PropertyDescriptorCollection GetProperties()
        {
            var props = new List<PropertyDescriptor>();
            foreach (var kvp in _values)
            {
                props.Add(new DynamicPropertyDescriptor(kvp.Key, kvp.Value?.GetType() ?? typeof(object)));
            }
            return new PropertyDescriptorCollection(props.ToArray());
        }
        
        public PropertyDescriptorCollection GetProperties(Attribute[] attributes) => GetProperties();
        public AttributeCollection GetAttributes() => AttributeCollection.Empty;
        public string GetClassName() => nameof(BindableWrapper);
        public string GetComponentName() => null;
        public TypeConverter GetConverter() => null;
        public EventDescriptor GetDefaultEvent() => null;
        public PropertyDescriptor GetDefaultProperty() => null;
        public object GetEditor(Type editorBaseType) => null;
        public EventDescriptorCollection GetEvents() => EventDescriptorCollection.Empty;
        public EventDescriptorCollection GetEvents(Attribute[] attributes) => EventDescriptorCollection.Empty;
        public object GetPropertyOwner(PropertyDescriptor pd) => this;
    }
    
    // Helper class for ICustomTypeDescriptor property descriptors
    public class DynamicPropertyDescriptor : PropertyDescriptor
    {
        private readonly Type _propertyType;
        
        public DynamicPropertyDescriptor(string name, Type propertyType) 
            : base(name, null)
        {
            _propertyType = propertyType;
        }
        
        public override Type ComponentType => typeof(BindableWrapper);
        public override bool IsReadOnly => false;
        public override Type PropertyType => _propertyType;
        public override bool CanResetValue(object component) => false;
        public override void ResetValue(object component) { }
        public override bool ShouldSerializeValue(object component) => true;
        
        public override object GetValue(object component)
        {
            if (component is BindableWrapper wrapper)
                return wrapper[Name];
            return null;
        }
        
        public override void SetValue(object component, object value)
        {
            if (component is BindableWrapper wrapper)
                wrapper[Name] = value;
        }
    }
}
