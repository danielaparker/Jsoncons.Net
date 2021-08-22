﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace JsonCons.JmesPath
{
    readonly struct NameValuePair
    {
        public string Name { get; }
        public IValue Value { get; }

        public NameValuePair(string name, IValue value)
        {
            Name = name;
            Value = value;
        }
    }

    interface IArrayValueEnumerator : IEnumerator<IValue>, IEnumerable<IValue>
    {
    }

    interface IObjectValueEnumerator : IEnumerator<NameValuePair>, IEnumerable<NameValuePair>
    {
    }

    enum JmesPathType
    {
        Null,	      
        Array,
        False,	      
        Number,	      
        Object,	      
        String,	      
        True,   
        Expression
    }

    interface IValue 
    {
        JmesPathType Type {get;}
        IValue this[int index] {get;}
        int GetArrayLength();
        string GetString();
        bool TryGetDecimal(out decimal value);
        bool TryGetDouble(out double value);
        bool TryGetProperty(string propertyName, out IValue property);
        IArrayValueEnumerator EnumerateArray();
        IObjectValueEnumerator EnumerateObject();
        IExpression GetExpression();
        bool IsJsonElement();
        JsonElement GetJsonElement();
        //JsonDocument ToJsonDocument();
    };

    readonly struct JsonElementValue : IValue
    {
        internal class ArrayEnumerator : IArrayValueEnumerator
        {
            JsonElement.ArrayEnumerator _enumerator;

            public ArrayEnumerator(JsonElement.ArrayEnumerator enumerator)
            {
                _enumerator = enumerator;
            }

            public bool MoveNext()
            {
                return _enumerator.MoveNext();
            }

            public void Reset() { _enumerator.Reset(); }

            void IDisposable.Dispose() { _enumerator.Dispose();}

            public IValue Current
            {
                get { return new JsonElementValue(_enumerator.Current); }
            }

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            public IEnumerator<IValue> GetEnumerator()
            {
                return new ArrayEnumerator(_enumerator.GetEnumerator());
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
               return (System.Collections.IEnumerator) GetEnumerator();
            }
        }

        internal class ObjectEnumerator : IObjectValueEnumerator
        {
            JsonElement.ObjectEnumerator _enumerator;

            public ObjectEnumerator(JsonElement.ObjectEnumerator enumerator)
            {
                _enumerator = enumerator;
            }

            public bool MoveNext()
            {
                return _enumerator.MoveNext();
            }

            public void Reset() { _enumerator.Reset(); }

            void IDisposable.Dispose() { _enumerator.Dispose();}

            public NameValuePair Current
            {
                get { return new NameValuePair(_enumerator.Current.Name, new JsonElementValue(_enumerator.Current.Value)); }
            }

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            public IEnumerator<NameValuePair> GetEnumerator()
            {
                return new ObjectEnumerator(_enumerator);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
               return (System.Collections.IEnumerator) GetEnumerator();
            }
        }

        private readonly JsonElement _element;

        internal JsonElementValue(JsonElement element)
        {
            _element = element;
        }

        public JmesPathType Type 
        {
            get
            {
                switch (_element.ValueKind)
                {
                    case JsonValueKind.Array:
                        return JmesPathType.Array;
                    case JsonValueKind.False:
                        return JmesPathType.False;
                    case JsonValueKind.Number:
                        return JmesPathType.Number;
                    case JsonValueKind.Object:
                        return JmesPathType.Object;
                    case JsonValueKind.String:
                        return JmesPathType.String;
                    case JsonValueKind.True:
                        return JmesPathType.True;
                    default:
                        return JmesPathType.Null;
                }
            }
        }

        public IValue this[int index] {get{return new JsonElementValue(_element[index]);}}

        public int GetArrayLength() {return _element.GetArrayLength();}

        public string GetString()
        {
            return _element.GetString();
        }

        public bool TryGetDecimal(out Decimal value)
        {
            return _element.TryGetDecimal(out value);
        }

        public bool TryGetDouble(out double value)
        {
            return _element.TryGetDouble(out value);
        }

        public bool TryGetProperty(string propertyName, out IValue property)
        {
            JsonElement prop;
            bool r = _element.TryGetProperty(propertyName, out prop);
            property = new JsonElementValue(prop);
            return r;
        }

        public IArrayValueEnumerator EnumerateArray()
        {
            return new ArrayEnumerator(_element.EnumerateArray());
        }

        public IObjectValueEnumerator EnumerateObject()
        {
            return new ObjectEnumerator(_element.EnumerateObject());
        }

        public bool IsJsonElement()
        {
            return true;
        }

        public JsonElement GetJsonElement()
        {
            return _element;
        }      

        public IExpression GetExpression()
        {
            throw new InvalidOperationException("Not an expression");
        }      
    };

    readonly struct DoubleValue : IValue
    {
        private readonly double _value;

        internal DoubleValue(double value)
        {
            _value = value;
        }

        public JmesPathType Type {get{return JmesPathType.Number;}}

        public IValue this[int index] { get { throw new InvalidOperationException(); } }

        public int GetArrayLength() { throw new InvalidOperationException(); }

        public string GetString()
        {
            throw new InvalidOperationException();
        }

        public bool TryGetDecimal(out Decimal value)
        {
            if (!(Double.IsNaN(_value) || Double.IsInfinity(_value)) && (_value >= (double)Decimal.MinValue && _value <= (double)Decimal.MaxValue))
            {
                value = Decimal.MinValue;
                return false;
            }
            else
            {
                value = new Decimal(_value);
                return true;
            }
        }

        public bool TryGetDouble(out double value)
        {
            value = _value;
            return true;
        }

        public bool TryGetProperty(string propertyName, out IValue property)
        {
            throw new InvalidOperationException();
        }

        public IArrayValueEnumerator EnumerateArray()
        {
            throw new InvalidOperationException();
        }

        public IObjectValueEnumerator EnumerateObject()
        {
            throw new InvalidOperationException();
        }

        public bool IsJsonElement()
        {
            return false;
        }

        public JsonElement GetJsonElement()
        {
            throw new InvalidOperationException("Not a JsonElement");
        }      

        public IExpression GetExpression()
        {
            throw new InvalidOperationException("Not an expression");
        }      
    }

    readonly struct DecimalValue : IValue
    {
        private readonly Decimal _value;

        internal DecimalValue(Decimal value)
        {
            _value = value;
        }

        public JmesPathType Type {get{return JmesPathType.Number;}}

        public IValue this[int index] { get { throw new InvalidOperationException(); } }

        public int GetArrayLength() { throw new InvalidOperationException(); }

        public string GetString()
        {
            throw new InvalidOperationException();
        }

        public bool TryGetDecimal(out Decimal value)
        {
            value = _value;
            return true;
        }

        public bool TryGetDouble(out double value)
        {
            value = (double)_value;
            return true;
        }

        public bool TryGetProperty(string propertyName, out IValue property)
        {
            throw new InvalidOperationException();
        }

        public IArrayValueEnumerator EnumerateArray()
        {
            throw new InvalidOperationException();
        }

        public IObjectValueEnumerator EnumerateObject()
        {
            throw new InvalidOperationException();
        }

        public bool IsJsonElement()
        {
            return false;
        }

        public JsonElement GetJsonElement()
        {
            throw new InvalidOperationException("Not a JsonElement");
        }      

        public IExpression GetExpression()
        {
            throw new InvalidOperationException("Not an expression");
        }      
    }

    readonly struct StringValue : IValue
    {
        private readonly string _value;

        internal StringValue(string value)
        {
            _value = value;
        }

        public JmesPathType Type {get{return JmesPathType.String;}}

        public IValue this[int index] { get { throw new InvalidOperationException(); } }

        public int GetArrayLength() { throw new InvalidOperationException(); }

        public string GetString()
        {
            return _value;
        }

        public bool TryGetDecimal(out Decimal value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetDouble(out double value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetProperty(string propertyName, out IValue property)
        {
            throw new InvalidOperationException();
        }

        public IArrayValueEnumerator EnumerateArray()
        {
            throw new InvalidOperationException();
        }

        public IObjectValueEnumerator EnumerateObject()
        {
            throw new InvalidOperationException();
        }

        public bool IsJsonElement()
        {
            return false;
        }

        public JsonElement GetJsonElement()
        {
            throw new InvalidOperationException("Not a JsonElement");
        }      

        public IExpression GetExpression()
        {
            throw new InvalidOperationException("Not an expression");
        }      
    }

    readonly struct TrueValue : IValue
    {
        public JmesPathType Type {get{return JmesPathType.True;}}

        public IValue this[int index] { get { throw new InvalidOperationException(); } }

        public int GetArrayLength() { throw new InvalidOperationException(); }

        public string GetString() { throw new InvalidOperationException(); }

        public bool TryGetDecimal(out Decimal value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetDouble(out double value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetProperty(string propertyName, out IValue property)
        {
            throw new InvalidOperationException();
        }

        public IArrayValueEnumerator EnumerateArray()
        {
            throw new InvalidOperationException();
        }

        public IObjectValueEnumerator EnumerateObject()
        {
            throw new InvalidOperationException();
        }

        public bool IsJsonElement()
        {
            return false;
        }

        public JsonElement GetJsonElement()
        {
            throw new InvalidOperationException("Not a JsonElement");
        }      

        public IExpression GetExpression()
        {
            throw new InvalidOperationException("Not an expression");
        }      
    }

    readonly struct FalseValue : IValue
    {
        public JmesPathType Type {get{return JmesPathType.False;}}

        public IValue this[int index] { get { throw new InvalidOperationException(); } }

        public int GetArrayLength() { throw new InvalidOperationException(); }

        public string GetString() { throw new InvalidOperationException(); }

        public bool TryGetDecimal(out Decimal value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetDouble(out double value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetProperty(string propertyName, out IValue property)
        {
            throw new InvalidOperationException();
        }

        public IArrayValueEnumerator EnumerateArray()
        {
            throw new InvalidOperationException();
        }

        public IObjectValueEnumerator EnumerateObject()
        {
            throw new InvalidOperationException();
        }

        public bool IsJsonElement()
        {
            return false;
        }

        public JsonElement GetJsonElement()
        {
            throw new InvalidOperationException("Not a JsonElement");
        }      

        public IExpression GetExpression()
        {
            throw new InvalidOperationException("Not an expression");
        }      
    }

    readonly struct NullValue : IValue
    {
        public JmesPathType Type {get{return JmesPathType.Null;}}

        public IValue this[int index] { get { throw new InvalidOperationException(); } }

        public int GetArrayLength() { throw new InvalidOperationException(); }

        public string GetString() { throw new InvalidOperationException(); }

        public bool TryGetDecimal(out Decimal value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetDouble(out double value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetProperty(string propertyName, out IValue property)
        {
            throw new InvalidOperationException();
        }

        public IArrayValueEnumerator EnumerateArray()
        {
            throw new InvalidOperationException();
        }

        public IObjectValueEnumerator EnumerateObject()
        {
            throw new InvalidOperationException();
        }

        public bool IsJsonElement()
        {
            return false;
        }

        public JsonElement GetJsonElement()
        {
            throw new InvalidOperationException("Not a JsonElement");
        }      

        public IExpression GetExpression()
        {
            throw new InvalidOperationException("Not an expression");
        }      
    }

    readonly struct ArrayValue : IValue
    {
        internal class ArrayEnumerator : IArrayValueEnumerator
        {   
            IList<IValue> _value;
            System.Collections.IEnumerator _enumerator;

            public ArrayEnumerator(IList<IValue> value)
            {
                _value = value;
                _enumerator = value.GetEnumerator();
            }

            public bool MoveNext()
            {
                return _enumerator.MoveNext();
            }

            public void Reset() { _enumerator.Reset(); }

            void IDisposable.Dispose() {}

            public IValue Current
            {
                get { return _enumerator.Current as IValue; }
            }

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            public IEnumerator<IValue> GetEnumerator()
            {
                return _value.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
               return (System.Collections.IEnumerator) GetEnumerator();
            }
        }

        private readonly IList<IValue> _value;

        internal ArrayValue(IList<IValue> value)
        {
            _value = value;
        }

        public JmesPathType Type {get{return JmesPathType.Array;}}

        public IValue this[int index] { get { return _value[index]; } }

        public int GetArrayLength() { return _value.Count; }

        public string GetString()
        {
            throw new InvalidOperationException();
        }

        public bool TryGetDecimal(out Decimal value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetDouble(out double value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetProperty(string propertyName, out IValue property)
        {
            throw new InvalidOperationException();
        }

        public IArrayValueEnumerator EnumerateArray()
        {
            return new ArrayEnumerator(_value);
        }

        public IObjectValueEnumerator EnumerateObject()
        {
            throw new InvalidOperationException();
        }

        public bool IsJsonElement()
        {
            return false;
        }

        public JsonElement GetJsonElement()
        {
            throw new InvalidOperationException("Not a JsonElement");
        }      

        public IExpression GetExpression()
        {
            throw new InvalidOperationException("Not an expression");
        }      
    }

    readonly struct ObjectValue : IValue
    {
        internal class ObjectEnumerator : IObjectValueEnumerator
        {   
            IDictionary<string,IValue> _value;
            System.Collections.IEnumerator _enumerator;

            public ObjectEnumerator(IDictionary<string,IValue> value)
            {
                _value = value;
                _enumerator = value.GetEnumerator();
            }

            public bool MoveNext()
            {
                return _enumerator.MoveNext();
            }

            public void Reset() { _enumerator.Reset(); }

            void IDisposable.Dispose() {}

            public NameValuePair Current
            {
                get {var pair = (KeyValuePair<string, IValue>)_enumerator.Current;
                     return new NameValuePair(pair.Key, pair.Value); }
            }

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            public IEnumerator<NameValuePair> GetEnumerator()
            {
                return new ObjectEnumerator(_value);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
               return (System.Collections.IEnumerator) GetEnumerator();
            }
        }

        private readonly IDictionary<string,IValue> _value;

        internal ObjectValue(IDictionary<string,IValue> value)
        {
            _value = value;
        }

        public JmesPathType Type {get{return JmesPathType.Object;}}

        public IValue this[int index] 
        {
            get { throw new InvalidOperationException(); }
        }

        public int GetArrayLength() 
        { 
            throw new InvalidOperationException();
        }

        public string GetString()
        {
            throw new InvalidOperationException();
        }

        public bool TryGetDecimal(out Decimal value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetDouble(out double value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetProperty(string propertyName, out IValue property)
        {
            return _value.TryGetValue(propertyName, out property);
        }

        public IArrayValueEnumerator EnumerateArray()
        {
            throw new InvalidOperationException();
        }

        public IObjectValueEnumerator EnumerateObject()
        {
            return new ObjectEnumerator(_value);
        }

        public bool IsJsonElement()
        {
            return false;
        }

        public JsonElement GetJsonElement()
        {
            throw new InvalidOperationException("Not a JsonElement");
        }      

        public IExpression GetExpression()
        {
            throw new InvalidOperationException("Not an expression");
        }      
    }

    readonly struct ExpressionValue : IValue
    {
        readonly IExpression _expr;

        internal ExpressionValue(IExpression expr)
        {
            _expr = expr;
        }

        public JmesPathType Type {get{return JmesPathType.Expression;}}

        public IValue this[int index] { get { throw new InvalidOperationException(); } }

        public int GetArrayLength() { throw new InvalidOperationException(); }

        public string GetString() { throw new InvalidOperationException(); }

        public bool TryGetDecimal(out Decimal value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetDouble(out double value)
        {
            throw new InvalidOperationException();
        }

        public bool TryGetProperty(string propertyName, out IValue property)
        {
            throw new InvalidOperationException();
        }

        public IArrayValueEnumerator EnumerateArray()
        {
            throw new InvalidOperationException();
        }

        public IObjectValueEnumerator EnumerateObject()
        {
            throw new InvalidOperationException();
        }

        public bool IsJsonElement()
        {
            return false;
        }

        public JsonElement GetJsonElement()
        {
            throw new InvalidOperationException("Not a JsonElement");
        }      

        public IExpression GetExpression()
        {
            return _expr;
        }      
    };

} // namespace JsonCons.JmesPath
