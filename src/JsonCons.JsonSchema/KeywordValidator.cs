﻿using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using JsonCons.Utilities;
using System.Text.RegularExpressions;

namespace JsonCons.JsonSchema
{
    abstract class KeywordValidator 
    {
        static readonly JsonElement nullValue;

        internal string AbsoluteKeywordLocation {get;}

        static KeywordValidator()
        {
            using JsonDocument doc = JsonDocument.Parse("null");
            nullValue = doc.RootElement.Clone();
        }

        internal KeywordValidator(string absoluteKeywordLocation)
        {
            AbsoluteKeywordLocation = absoluteKeywordLocation;
        }

        internal void Validate(JsonElement instance, 
                               JsonPointer instanceLocation, 
                               ErrorReporter reporter,
                               IList<PatchElement> patch)
        {
            OnValidate(instance,instanceLocation,reporter,patch);
        }

        internal abstract void OnValidate(JsonElement instance, 
                                          JsonPointer instanceLocation, 
                                          ErrorReporter reporter,
                                          IList<PatchElement> patch);

        internal virtual bool TryGetDefaultValue(JsonPointer instanceLocation, 
                                                  JsonElement instance, 
                                                  ErrorReporter reporter,
                                                  out JsonElement defaultValue)
        {
            defaultValue = nullValue;
            return false;
        }
    }

    interface IKeywordValidatorFactory
    {
        KeywordValidator CreateKeywordValidator(JsonElement schema,
                                                IList<SchemaLocation> uris,
                                                IList<string> keys);
    }

    struct PatchElement
    {
        string _path;
        JsonElement _value;

        internal PatchElement(string path, JsonElement value)
        {
            _path = path;
            _value = value;
        }

        public override string ToString()
        {
            var buffer = new StringBuilder();
            buffer.Append("{");
            buffer.Append("\"op\":\"add\"");
            buffer.Append("\"path\":");
            buffer.Append($"{JsonSerializer.Serialize(_path)}");
            buffer.Append("\"value\":");
            buffer.Append($"{JsonSerializer.Serialize(_value)}");
            buffer.Append("}");
            return buffer.ToString();
        }
    }

    class StringValidator : KeywordValidator 
    {
        int? _maxLength;
        string _maxLengthLocation;

        int? _minLength;
        string _minLengthLocation;

        Regex _pattern;
        string _patternLocation;

        IFormatValidator _formatValidator; 
        
        string _contentEncoding;
        string _contentEncodingLocation;

        string _contentMediaType;
        string _contentMediaTypeLocation;

        internal StringValidator(string absoluteKeywordLocation,
                                 int? maxLength, string maxLengthLocation,
                                 int? minLength, string minLengthLocation,
                                 Regex pattern, string patternLocation,
                                 IFormatValidator formatValidator, 
                                 string contentEncoding, string contentEncodingLocation,
                                 string contentMediaType, string contentMediaTypeLocation)
            : base(absoluteKeywordLocation)
        {
            _maxLength = maxLength;
            _maxLengthLocation = maxLengthLocation;
            _minLength = minLength;
            _minLengthLocation = minLengthLocation;
            _pattern = pattern;
            _patternLocation = patternLocation;
            _formatValidator = formatValidator;
            _contentEncoding = contentEncoding;
            _contentEncodingLocation = contentEncodingLocation;
            _contentMediaType = contentMediaType;
            _contentMediaTypeLocation = contentMediaTypeLocation;
        }

        internal static StringValidator Create(JsonElement schema, IList<SchemaLocation> uris)
        {
            SchemaLocation absoluteKeywordLocation = SchemaLocation.GetAbsoluteKeywordLocation(uris);
            int? maxLength = null;
            string maxLengthLocation = "";
            int? minLength = null;
            string minLengthLocation = "";
            Regex pattern = null;
            string patternLocation = "";
            IFormatValidator formatValidator = null; 
            string contentEncoding = null;
            string contentEncodingLocation = "";
            string contentMediaType = null;
            string contentMediaTypeLocation = "";

            JsonElement element;
            if (schema.TryGetProperty("maxLength", out element))
            {   
                maxLength = element.GetInt32();
                maxLengthLocation = SchemaLocation.Append(absoluteKeywordLocation, "maxLength").ToString();
            }
            if (schema.TryGetProperty("minLength", out element))
            {   
                minLength = element.GetInt32();
                minLengthLocation = SchemaLocation.Append(absoluteKeywordLocation, "minLength").ToString();
            }

            if (schema.TryGetProperty("pattern", out element))
            {   
                string patternString = element.GetString();
                pattern = new Regex(patternString);
                patternLocation = SchemaLocation.Append(absoluteKeywordLocation, "pattern").ToString();
            }
            if (schema.TryGetProperty("format", out element))
            {   
                string format = element.GetString();
                string formatLocation = SchemaLocation.Append(absoluteKeywordLocation, "format").ToString();
                switch (format)
                {
                    case "date-time":
                        formatValidator = new DateTimeValidator(formatLocation);
                        break;
                    case "date":
                        formatValidator = new DateValidator(formatLocation);
                        break;
                    case "time":
                        formatValidator = new TimeValidator(formatLocation);
                        break;
                    case "email":
                        formatValidator = new EmailValidator(formatLocation);
                        break;
                    case "hostname":
                        formatValidator = new HostnameValidator(formatLocation);
                        break;
                    case "ipv4":
                        formatValidator = new Ipv4Validator(formatLocation);
                        break;
                    case "ipv6":
                        formatValidator = new Ipv6Validator(formatLocation);
                        break;
                    case "regex":
                        formatValidator = new RegexValidator(formatLocation);
                        break;
                    default:
                        break;
                }
                formatLocation = SchemaLocation.Append(absoluteKeywordLocation, "format").ToString();
            }
            if (schema.TryGetProperty("contentEncoding", out element))
            {   
                contentEncoding = element.GetString();
                contentEncodingLocation = SchemaLocation.Append(absoluteKeywordLocation, "contentEncoding").ToString();
            }
            if (schema.TryGetProperty("contentMediaType", out element))
            {   
                contentMediaType = element.GetString();
                contentMediaTypeLocation = SchemaLocation.Append(absoluteKeywordLocation, "contentMediaType").ToString();
            }
            return new StringValidator(absoluteKeywordLocation.ToString(),
                                       maxLength, maxLengthLocation,
                                       minLength, minLengthLocation,
                                       pattern, patternLocation,
                                       formatValidator,
                                       contentEncoding, contentEncodingLocation,
                                       contentMediaType, contentMediaTypeLocation);
        }

        internal override void OnValidate(JsonElement instance,
                                          JsonPointer instanceLocation,
                                          ErrorReporter reporter,
                                          IList<PatchElement> patch)
        {
            if (instance.ValueKind != JsonValueKind.String)
            {
                reporter.Error(new ValidationOutput("", 
                                                    this.AbsoluteKeywordLocation, 
                                                    instanceLocation.ToString(), 
                                                    "Instance must be a string"));
            }
            string str = instance.GetString();
            ValidateString(str, instanceLocation, reporter);
        }

        internal void ValidateString(string str,
                                     JsonPointer instanceLocation,
                                     ErrorReporter reporter)
        {
            string content = null;
            if (_contentEncoding != null)
            {
                if (_contentEncoding == "base64")
                {
                    try
                    {
                        content = Convert.ToBase64String(Encoding.UTF8.GetBytes(str));
                    }
                    catch (Exception)
                    {
                        reporter.Error(new ValidationOutput("contentEncoding", 
                                                            _contentEncodingLocation, 
                                                            instanceLocation.ToString(), 
                                                            "Content is not a base64 string"));
                        if (reporter.FailEarly)
                        {
                            return;
                        }
                    }
                }
                else if (_contentEncoding.Length != 0)
                {
                    reporter.Error(new ValidationOutput("contentEncoding", 
                                                    _contentEncodingLocation,
                                                    instanceLocation.ToString(), 
                                                    $"Unable to check for contentEncoding '{_contentEncoding}'"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }
            else
            {
                content = str;
            }
            if (content == null)
            {
                return;
            }

            if (_contentMediaType != null) 
            {
                if (_contentMediaType.Equals("application/Json"))
                {
                    try
                    {
                        using JsonDocument doc = JsonDocument.Parse(content);
                    }
                    catch (Exception e)
                    {
                        reporter.Error(new ValidationOutput("contentMediaType", 
                                                            _contentMediaTypeLocation,
                                                            instanceLocation.ToString(), 
                                                            $"Content is not JSON: {e.Message}"));
                    }
                }
            } 

            if (_minLength != null) 
            {
                byte[] bytes = Encoding.UTF32.GetBytes(content.ToCharArray());
                int length = bytes.Length/4;
                if (length < _minLength) 
                {
                    reporter.Error(new ValidationOutput("minLength", 
                                                    _minLengthLocation, 
                                                    instanceLocation.ToString(), 
                                                    $"Expected minLength: {_minLength}, actual: {length}"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }

            if (_maxLength != null) 
            {
                byte[] bytes = Encoding.UTF32.GetBytes(content.ToCharArray());
                int length = bytes.Length/4;
                if (length > _maxLength)
                {
                    reporter.Error(new ValidationOutput("maxLength", 
                                                    _maxLengthLocation, 
                                                    instanceLocation.ToString(), 
                                                    $"Expected maxLength: {_maxLength}, actual: {length}"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }

            if (_pattern != null)
            {
                var match = _pattern.Match(content);
                if (match.Success)
                {
                    reporter.Error(new ValidationOutput("pattern", 
                                                    _patternLocation, 
                                                    instanceLocation.ToString(), 
                                                    $"String '{content}' does not match pattern '{_pattern}'"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }

            if (_formatValidator != null) 
            {
                _formatValidator.Validate(content, instanceLocation.ToString(), reporter);
                if (reporter.ErrorCount > 0 && reporter.FailEarly)
                {
                    return;
                }
            }
        }
    }

    class NotValidator : KeywordValidator
    {
        KeywordValidator _rule;

        internal NotValidator(string absoluteKeywordLocation, KeywordValidator rule)
            : base(absoluteKeywordLocation)
        {
            _rule = rule;
        }

        internal static NotValidator Create(IKeywordValidatorFactory validatorFactory, 
                                            JsonElement schema, 
                                            IList<SchemaLocation> uris)
        {
            SchemaLocation absoluteKeywordLocation = SchemaLocation.GetAbsoluteKeywordLocation(uris);

            var keys = new List<string>();
            keys.Add("not");
            KeywordValidator rule = validatorFactory.CreateKeywordValidator(schema, uris, keys);
            return new NotValidator(absoluteKeywordLocation.ToString(), rule);
        }

        internal override void OnValidate(JsonElement instance, 
                                          JsonPointer instanceLocation, 
                                          ErrorReporter reporter, 
                                          IList<PatchElement> patch) 
        {
            CollectingErrorReporter localReporter = new CollectingErrorReporter();
            _rule.Validate(instance, instanceLocation, localReporter, patch);

            if (localReporter.Errors.Count != 0)
            {
                reporter.Error(new ValidationOutput("not", 
                                                    this.AbsoluteKeywordLocation, 
                                                    instanceLocation.ToString(), 
                                                    "Instance must not be valid against schema"));
            }
        }

        internal override bool TryGetDefaultValue(JsonPointer instanceLocation, 
                                                  JsonElement instance, 
                                                  ErrorReporter reporter,
                                                  out JsonElement defaultValue)
        {
            return _rule.TryGetDefaultValue(instanceLocation, instance, reporter, out defaultValue);
        }
    }

    interface ICombiningCriterion 
    {
        string Key {get;}

        bool IsComplete(JsonElement instance, 
                        JsonPointer instanceLocation, 
                        ErrorReporter reporter, 
                        CollectingErrorReporter localReporter, 
                        int count);
    };

    struct AllOfCriterion : ICombiningCriterion
    {
        public string Key {get {return "allOf";}}

        public bool IsComplete(JsonElement instance, 
                               JsonPointer instanceLocation, 
                               ErrorReporter reporter, 
                               CollectingErrorReporter localReporter, 
                               int count)
        {
            if (localReporter.Errors.Count == 0)
                reporter.Error(new ValidationOutput(Key, 
                                                    "",
                                                    instanceLocation.ToString(), 
                                                    "At least one keyword_validator failed to match, but all are required to match. ", 
                                                    localReporter.Errors));
            return localReporter.Errors.Count == 0;
        }
    }

    struct AnyOfCriterion : ICombiningCriterion
    {
        public string Key {get {return "anyOf";}}

        public bool IsComplete(JsonElement instance, 
                               JsonPointer instanceLocation, 
                               ErrorReporter reporter, 
                               CollectingErrorReporter localReporter, 
                               int count)
        {
            return count == 1;
        }
    }

    struct OneOfCriterion : ICombiningCriterion
    {
        public string Key {get {return "oneOf";}}

        public bool IsComplete(JsonElement instance, 
                               JsonPointer instanceLocation, 
                               ErrorReporter reporter, 
                               CollectingErrorReporter localReporter, 
                               int count)
        {
            if (count > 1)
            {
                reporter.Error(new ValidationOutput("oneOf", 
                    "", 
                    instanceLocation.ToString(), 
                    $"{count} subschemas matched, but exactly one is required to match"));
            }
            return count > 1;
        }
    }

    sealed class CombiningValidator : KeywordValidator
    {
        internal ICombiningCriterion AllOf = new AllOfCriterion();
        internal ICombiningCriterion AnyOf = new AnyOfCriterion();
        internal ICombiningCriterion OneOf = new OneOfCriterion();

        ICombiningCriterion _criterion; 
        IList<KeywordValidator> _validators;

        internal CombiningValidator(string absoluteKeywordLocation,
                                    ICombiningCriterion criterion,
                                    IList<KeywordValidator> validators)
            : base(absoluteKeywordLocation)
        {
            _criterion = criterion;
            _validators = validators;
            // Validate value of allOf, anyOf, and oneOf "MUST be a non-empty array"
        }

        internal static CombiningValidator Create(IKeywordValidatorFactory validatorFactory,
                                                  JsonElement schema, 
                                                  IList<SchemaLocation> uris,
                                                  ICombiningCriterion criterion)
        {
            SchemaLocation absoluteKeywordLocation = SchemaLocation.GetAbsoluteKeywordLocation(uris);

            var validators = new List<KeywordValidator>();
            for (int i = 0; i < schema.GetArrayLength(); ++i)
            {
                var keys = new List<string>();
                keys.Add(criterion.Key);
                keys.Add(i.ToString());
                validators.Add(validatorFactory.CreateKeywordValidator(schema[i], uris, keys));
            }

            return new CombiningValidator(absoluteKeywordLocation.ToString(), 
                                                 criterion,
                                                 validators);
        }

        internal override void OnValidate(JsonElement instance, 
                                          JsonPointer instanceLocation, 
                                          ErrorReporter reporter, 
                                          IList<PatchElement> patch) 
        {
            int count = 0;

            var localReporter = new CollectingErrorReporter();
            foreach (var s in _validators) 
            {
                int mark = localReporter.Errors.Count;
                s.Validate(instance, instanceLocation, localReporter, patch);
                if (mark == localReporter.Errors.Count)
                    count++;

                if (_criterion.IsComplete(instance, instanceLocation, reporter, localReporter, count))
                    return;
            }

            if (count == 0)
            {
                reporter.Error(new ValidationOutput("combined", 
                                                 this.AbsoluteKeywordLocation, 
                                                 instanceLocation.ToString(), 
                                                 "No KeywordValidator matched, but one of them is required to match", 
                                                 localReporter.Errors));
            }
        }
    }

    static class JsonAccessors 
    {
        internal static bool TryGetInt64(JsonElement element, out Int64 result)
        {
            if (element.ValueKind != JsonValueKind.Number)
            {
                result = 0;
                return false;
            }
            if (!element.TryGetInt64(out result))
            {
                Decimal dec;
                if (!element.TryGetDecimal(out dec))
                {
                    return false;
                }
                Decimal ceil = Decimal.Ceiling(dec);
                if (ceil != dec)
                {
                    return false;
                }
                if (ceil < Int64.MinValue || ceil > Int64.MaxValue)
                {
                    return false;
                }
                result = Decimal.ToInt64(ceil);
            }
            return true;
        }

        internal static bool TryGetInt32(JsonElement element, out Int32 result)
        {
            if (element.ValueKind != JsonValueKind.Number)
            {
                result = 0;
                return false;
            }
            if (!element.TryGetInt32(out result))
            {
                Decimal dec;
                if (!element.TryGetDecimal(out dec))
                {
                    return false;
                }
                Decimal ceil = Decimal.Ceiling(dec);
                if (ceil != dec)
                {
                    return false;
                }
                if (ceil < Int32.MinValue || ceil > Int32.MaxValue)
                {
                    return false;
                }
                result = Decimal.ToInt32(ceil);
            }
            return true;
        }

        internal static bool TryGetDouble(JsonElement element, out double result)
        {
            if (element.ValueKind != JsonValueKind.Number)
            {
                result = 0;
                return false;
            }
            if (!element.TryGetDouble(out result))
            {
                return false;
            }
            return true;
        }

        internal static bool TryGetBoolean(JsonElement element, out bool result)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.True:
                    result = true;
                    return true;
                case JsonValueKind.False:
                    result = false;
                    return true;
                default:
                    result = false;
                    return false;
            }
        }

        internal static bool TryGetString(JsonElement element, out string result)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                result = "";
                return false;
            }
            result = element.GetString();
            return true;
        }

        internal static bool TryGetListOfString(JsonElement element, out IList<string> result)
        {
            result = new List<string>();
            if (element.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            foreach (var item in element.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String)
                {
                    return false;
                }
                result.Add(item.GetString());
            }
            return true;
        }

        internal static bool IsMultipleOf(double x, double multipleOf) 
        {
            return x >= multipleOf && multipleOf % x == 0;
        }
    }

    class IntegerValidator : KeywordValidator
    {
        Int64? _maximum;
        string _maximumLocation = "";
        Int64? _minimum;
        string _minimumLocation = "";
        Int64? _exclusiveMaximum;
        string _exclusiveMaximumLocation = "";
        Int64? _exclusiveMinimum;
        string _exclusiveMinimumLocation = "";
        double? _multipleOf;
        string _multipleOfLocation = "";

        internal IntegerValidator(string absoluteKeywordLocation,
                                  Int64? maximum,
                                  string maximumLocation,
                                  Int64? minimum,
                                  string minimumLocation,
                                  Int64? exclusiveMaximum,
                                  string exclusiveMaximumLocation,
                                  Int64? exclusiveMinimum,
                                  string exclusiveMinimumLocation,
                                  double? multipleOf,
                                  string multipleOfLocation)
            : base(absoluteKeywordLocation)
        {
            _maximum = maximum;
            _maximumLocation = maximumLocation;
            _minimum = minimum;
            _minimumLocation = minimumLocation;
            _exclusiveMaximum = exclusiveMaximum;
            _exclusiveMaximumLocation = exclusiveMaximumLocation;
            _exclusiveMinimum = exclusiveMinimum;
            _exclusiveMinimumLocation = exclusiveMinimumLocation;
            _multipleOf = multipleOf;
            _multipleOfLocation = multipleOfLocation;
        }

        internal static IntegerValidator Create(JsonElement sch, 
                                                IList<SchemaLocation> uris, 
                                                ISet<string> keywords)
        {
            SchemaLocation absoluteKeywordLocation = SchemaLocation.GetAbsoluteKeywordLocation(uris);
            Int64? maximum = null;
            string maximumLocation = "";
            Int64? minimum = null;
            string minimumLocation = "";
            Int64? exclusiveMaximum = null;
            string exclusiveMaximumLocation = "";
            Int64? exclusiveMinimum = null;
            string exclusiveMinimumLocation = "";
            double? multipleOf = null;
            string multipleOfLocation = "";

            JsonElement element;
            if (sch.TryGetProperty("maximum", out element)) 
            {
                maximumLocation = SchemaLocation.Append(absoluteKeywordLocation,"maximum").ToString();
                Int64 val;
                if (!JsonAccessors.TryGetInt64(element, out val))
                {
                    throw new JsonSchemaException("'maximum' must be an Int64", maximumLocation);
                }
                maximum = val;
                keywords.Add("maximum");
            }

            if (sch.TryGetProperty("minimum", out element)) 
            {
                minimumLocation = SchemaLocation.Append(absoluteKeywordLocation,"minimum").ToString();
                Int64 val;
                if (!JsonAccessors.TryGetInt64(element, out val))
                {
                    throw new JsonSchemaException("'minimum' must be an Int64", minimumLocation);
                }
                minimum = val;
                keywords.Add("minimum");
            }

            if (sch.TryGetProperty("exclusiveMaximum", out element)) 
            {
                exclusiveMaximumLocation = SchemaLocation.Append(absoluteKeywordLocation,"exclusiveMaximum").ToString();
                Int64 val;
                if (!JsonAccessors.TryGetInt64(element, out val))
                {
                    throw new JsonSchemaException("'exclusiveMaximum' must be an Int64", exclusiveMaximumLocation);
                }
                exclusiveMaximum = val;
                keywords.Add("exclusiveMaximum");
            }

            if (sch.TryGetProperty("exclusiveMinimum", out element)) 
            {
                exclusiveMinimumLocation = SchemaLocation.Append(absoluteKeywordLocation,"exclusiveMinimum").ToString();
                Int64 val;
                if (!JsonAccessors.TryGetInt64(element, out val))
                {
                    throw new JsonSchemaException("'exclusiveMinimum' must be an Int64", exclusiveMinimumLocation);
                }
                exclusiveMinimum = val;
                keywords.Add("exclusiveMinimum");
            }

            if (sch.TryGetProperty("multipleOf", out element)) 
            {
                multipleOfLocation = SchemaLocation.Append(absoluteKeywordLocation, "multipleOf").ToString();
                double val;
                if (!JsonAccessors.TryGetDouble(element, out val))
                {
                    throw new JsonSchemaException("'multipleOf' must be a number", multipleOfLocation);
                }
                multipleOf = val;
                keywords.Add("multipleOf");
            }
            return new IntegerValidator(absoluteKeywordLocation.ToString(),
                                        maximum,
                                        maximumLocation,
                                        minimum,
                                        minimumLocation,
                                        exclusiveMaximum,
                                        exclusiveMaximumLocation,
                                        exclusiveMinimum,
                                        exclusiveMinimumLocation,
                                        multipleOf,
                                        multipleOfLocation);
        }

        internal override void OnValidate(JsonElement instance, 
                                          JsonPointer instanceLocation, 
                                          ErrorReporter reporter, 
                                          IList<PatchElement> patch) 
        {
            Int64 value;
            if (!JsonAccessors.TryGetInt64(instance, out value))
            {
                reporter.Error(new ValidationOutput("integer", 
                                                    this.AbsoluteKeywordLocation, 
                                                    instanceLocation.ToString(), 
                                                    "Instance is not an integer"));
                if (reporter.FailEarly)
                {
                    return;
                }
            }
            if (_multipleOf.HasValue && value != 0) // exclude zero
            {
                if (!JsonAccessors.IsMultipleOf(value, (double)_multipleOf))
                {
                    reporter.Error(new ValidationOutput("multipleOf", 
                                                        _multipleOfLocation, 
                                                        instanceLocation.ToString(), 
                                                        $"{instance} is not a multiple of _multipleOf"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }

            if (_maximum.HasValue)
            {
                if (value > (Int64)_maximum)
                {
                    reporter.Error(new ValidationOutput("maximum", 
                                                        _maximumLocation, 
                                                        instanceLocation.ToString(), 
                                                        $"{instance} exceeds maximum of + {_exclusiveMinimum}"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }

            if (_minimum != null)
            {
                if (value < _minimum)
                {
                    reporter.Error(new ValidationOutput("minimum", 
                                                        _minimumLocation, 
                                                        instanceLocation.ToString(), 
                                                        $"{instance} is below minimum of + {_exclusiveMinimum}"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }

            if (_exclusiveMaximum.HasValue)
            {
                if (value >= _exclusiveMaximum)
                {
                    reporter.Error(new ValidationOutput("exclusiveMaximum", 
                                                        _exclusiveMaximumLocation, 
                                                        instanceLocation.ToString(), 
                                                        $"{instance} exceeds maximum of + {_exclusiveMinimum}"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }

            if (_exclusiveMinimum.HasValue)
            {
                if (value <= _exclusiveMinimum)
                {
                    reporter.Error(new ValidationOutput("exclusiveMinimum", 
                                                        _exclusiveMinimumLocation, 
                                                        instanceLocation.ToString(), 
                                                        $"{instance} is below minimum of + {_exclusiveMinimum}"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }
        }
    }

    class DoubleValidator : KeywordValidator
    {
        double? _maximum;
        string _maximumLocation = "";
        double? _minimum;
        string _minimumLocation = "";
        double? _exclusiveMaximum;
        string _exclusiveMaximumLocation = "";
        double? _exclusiveMinimum;
        string _exclusiveMinimumLocation = "";
        double? _multipleOf;
        string _multipleOfLocation = "";

        internal DoubleValidator(string absoluteKeywordLocation,
                                  double? maximum,
                                  string maximumLocation,
                                  double? minimum,
                                  string minimumLocation,
                                  double? exclusiveMaximum,
                                  string exclusiveMaximumLocation,
                                  double? exclusiveMinimum,
                                  string exclusiveMinimumLocation,
                                  double? multipleOf,
                                  string multipleOfLocation)
            : base(absoluteKeywordLocation)
        {
            _maximum = maximum;
            _maximumLocation = maximumLocation;
            _minimum = minimum;
            _minimumLocation = minimumLocation;
            _exclusiveMaximum = exclusiveMaximum;
            _exclusiveMaximumLocation = exclusiveMaximumLocation;
            _exclusiveMinimum = exclusiveMinimum;
            _exclusiveMinimumLocation = exclusiveMinimumLocation;
            _multipleOf = multipleOf;
            _multipleOfLocation = multipleOfLocation;
        }

        internal static DoubleValidator Create(JsonElement sch, 
                                                IList<SchemaLocation> uris, 
                                                ISet<string> keywords)
        {
            SchemaLocation absoluteKeywordLocation = SchemaLocation.GetAbsoluteKeywordLocation(uris);
            double? maximum = null;
            string maximumLocation = "";
            double? minimum = null;
            string minimumLocation = "";
            double? exclusiveMaximum = null;
            string exclusiveMaximumLocation = "";
            double? exclusiveMinimum = null;
            string exclusiveMinimumLocation = "";
            double? multipleOf = null;
            string multipleOfLocation = "";

            JsonElement element;
            if (sch.TryGetProperty("maximum", out element)) 
            {
                maximumLocation = SchemaLocation.Append(absoluteKeywordLocation,"maximum").ToString().ToString();
                double val;
                if (!JsonAccessors.TryGetDouble(element, out val))
                {
                    throw new JsonSchemaException("'maximum' must be an double", maximumLocation);
                }
                maximum = val;
                keywords.Add("maximum");
            }

            if (sch.TryGetProperty("minimum", out element)) 
            {
                minimumLocation = SchemaLocation.Append(absoluteKeywordLocation,"minimum").ToString();
                double val;
                if (!JsonAccessors.TryGetDouble(element, out val))
                {
                    throw new JsonSchemaException("'minimum' must be an double", minimumLocation);
                }
                minimum = val;
                keywords.Add("minimum");
            }

            if (sch.TryGetProperty("exclusiveMaximum", out element)) 
            {
                exclusiveMaximumLocation = SchemaLocation.Append(absoluteKeywordLocation,"exclusiveMaximum").ToString();
                double val;
                if (!JsonAccessors.TryGetDouble(element, out val))
                {
                    throw new JsonSchemaException("'exclusiveMaximum' must be an double", exclusiveMaximumLocation);
                }
                exclusiveMaximum = val;
                keywords.Add("exclusiveMaximum");
            }

            if (sch.TryGetProperty("exclusiveMinimum", out element)) 
            {
                exclusiveMinimumLocation = SchemaLocation.Append(absoluteKeywordLocation,"exclusiveMinimum").ToString();
                double val;
                if (!JsonAccessors.TryGetDouble(element, out val))
                {
                    throw new JsonSchemaException("'exclusiveMinimum' must be an double", exclusiveMinimumLocation);
                }
                exclusiveMinimum = val;
                keywords.Add("exclusiveMinimum");
            }

            if (sch.TryGetProperty("multipleOf", out element)) 
            {
                multipleOfLocation = SchemaLocation.Append(absoluteKeywordLocation, "multipleOf").ToString();
                double val;
                if (!JsonAccessors.TryGetDouble(element, out val))
                {
                    throw new JsonSchemaException("'multipleOf' must be a number", multipleOfLocation);
                }
                multipleOf = val;
                keywords.Add("multipleOf");
            }
            return new DoubleValidator(absoluteKeywordLocation.ToString(),
                                        maximum,
                                        maximumLocation,
                                        minimum,
                                        minimumLocation,
                                        exclusiveMaximum,
                                        exclusiveMaximumLocation,
                                        exclusiveMinimum,
                                        exclusiveMinimumLocation,
                                        multipleOf,
                                        multipleOfLocation);
        }

        internal override void OnValidate(JsonElement instance, 
                                          JsonPointer instanceLocation, 
                                          ErrorReporter reporter, 
                                          IList<PatchElement> patch) 
        {
            double value;
            if (!JsonAccessors.TryGetDouble(instance, out value))
            {
                reporter.Error(new ValidationOutput("integer", 
                                                    this.AbsoluteKeywordLocation, 
                                                    instanceLocation.ToString(), 
                                                    "Instance is not an integer"));
                if (reporter.FailEarly)
                {
                    return;
                }
            }
            if (_multipleOf.HasValue && value != 0) // exclude zero
            {
                if (!JsonAccessors.IsMultipleOf(value, (double)_multipleOf))
                {
                    reporter.Error(new ValidationOutput("multipleOf", 
                                                        _multipleOfLocation, 
                                                        instanceLocation.ToString(), 
                                                        $"{instance} is not a multiple of _multipleOf"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }

            if (_maximum.HasValue)
            {
                if (value > (double)_maximum)
                {
                    reporter.Error(new ValidationOutput("maximum", 
                                                        _maximumLocation, 
                                                        instanceLocation.ToString(), 
                                                        $"{instance} exceeds maximum of + {_exclusiveMinimum}"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }

            if (_minimum != null)
            {
                if (value < _minimum)
                {
                    reporter.Error(new ValidationOutput("minimum", 
                                                        _minimumLocation, 
                                                        instanceLocation.ToString(), 
                                                        $"{instance} is below minimum of + {_exclusiveMinimum}"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }

            if (_exclusiveMaximum.HasValue)
            {
                if (value >= _exclusiveMaximum)
                {
                    reporter.Error(new ValidationOutput("exclusiveMaximum", 
                                                        _exclusiveMaximumLocation, 
                                                        instanceLocation.ToString(), 
                                                        $"{instance} exceeds maximum of + {_exclusiveMinimum}"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }

            if (_exclusiveMinimum.HasValue)
            {
                if (value <= _exclusiveMinimum)
                {
                    reporter.Error(new ValidationOutput("exclusiveMinimum", 
                                                        _exclusiveMinimumLocation, 
                                                        instanceLocation.ToString(), 
                                                        $"{instance} is below minimum of + {_exclusiveMinimum}"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }
        }
    }

    // NullValidator

    class NullValidator : KeywordValidator
    {
        internal NullValidator(string absoluteKeywordLocation)
            : base(absoluteKeywordLocation)
        {
        }

        internal static NullValidator Create(IList<SchemaLocation> uris)
        {
            SchemaLocation absoluteKeywordLocation = SchemaLocation.GetAbsoluteKeywordLocation(uris);
            return new NullValidator(absoluteKeywordLocation.ToString());
        }

        internal override void OnValidate(JsonElement instance,
                                          JsonPointer instanceLocation,
                                          ErrorReporter reporter,
                                          IList<PatchElement> patch) 
        {
            if (instance.ValueKind != JsonValueKind.Null)
            {
                reporter.Error(new ValidationOutput("null", 
                                                    this.AbsoluteKeywordLocation, 
                                                    instanceLocation.ToString(), 
                                                    "Expected to be null"));
            }
        }
    }

    class TrueValidator : KeywordValidator
    {
        TrueValidator(string absoluteKeywordLocation)
            : base(absoluteKeywordLocation)
        {
        }

        internal static TrueValidator Create(IList<SchemaLocation> uris)
        {
            SchemaLocation absoluteKeywordLocation = SchemaLocation.GetAbsoluteKeywordLocation(uris);
            return new TrueValidator(absoluteKeywordLocation.ToString());
        }

        internal override void OnValidate(JsonElement instance,
                                          JsonPointer instanceLocation,
                                          ErrorReporter reporter,
                                          IList<PatchElement> patch) 
        {
        }
    }

    class FalseValidator : KeywordValidator
    {
        FalseValidator(string absoluteKeywordLocation)
            : base(absoluteKeywordLocation)
        {
        }

        internal static FalseValidator Create(IList<SchemaLocation> uris)
        {
            SchemaLocation absoluteKeywordLocation = SchemaLocation.GetAbsoluteKeywordLocation(uris);
            return new FalseValidator(absoluteKeywordLocation.ToString());
        }

        internal override void OnValidate(JsonElement instance,
                                          JsonPointer instanceLocation,
                                          ErrorReporter reporter,
                                          IList<PatchElement> patch) 
        {
            reporter.Error(new ValidationOutput("false", 
                                                this.AbsoluteKeywordLocation, 
                                                instanceLocation.ToString(), 
                                                "False schema always fails"));
        }
    }

    class RequiredValidator : KeywordValidator
    {
        IList<string> _items;

        internal RequiredValidator(string absoluteKeywordLocation, 
                                   IList<string> items)
            : base(absoluteKeywordLocation)
        {
            _items = items; 
        }

        internal static RequiredValidator Create(IList<SchemaLocation> uris,
                                                 IList<string> items)
        {
            SchemaLocation absoluteKeywordLocation = SchemaLocation.GetAbsoluteKeywordLocation(uris);
            return new RequiredValidator(absoluteKeywordLocation.ToString(), items);
        }

        internal override void OnValidate(JsonElement instance,
                                          JsonPointer instanceLocation, 
                                          ErrorReporter reporter,
                                          IList<PatchElement> patch)
        {
            JsonElement element;
            foreach (var key in _items)
            {
                if (!instance.TryGetProperty(key, out element))
                {
                    reporter.Error(new ValidationOutput("required", 
                                                        this.AbsoluteKeywordLocation, 
                                                        instanceLocation.ToString(), 
                                                        $"Required property '{key}' not found"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }
        }
    }

    struct RegexValidatorPair
    {
        internal Regex Pattern {get;}
        internal KeywordValidator Validator {get;}

        internal RegexValidatorPair(Regex pattern, KeywordValidator validator)
        {
            Pattern = pattern;
            Validator = validator;
        }
    }

    class ObjectValidator : KeywordValidator
    {
        int? _maxProperties;
        string _maxPropertiesLocation;
        int? _minProperties;
        string _minPropertiesLocation;
        RequiredValidator _requiredValidator;
        IDictionary<string,KeywordValidator> _properties;
        IList<RegexValidatorPair> _patternProperties;
        KeywordValidator _additionalProperties;
        IDictionary<string,KeywordValidator> _dependencies;
        StringValidator _propertyNameValidator;

        ObjectValidator(string absoluteKeywordLocation,
                        int? maxProperties,
                        string maxPropertiesLocation,
                        int? minProperties,
                        string minPropertiesLocation,
                        RequiredValidator requiredValidator,
                        IDictionary<string,KeywordValidator> properties,
                        IList<RegexValidatorPair> patternProperties,
                        KeywordValidator additionalProperties,
                        IDictionary<string,KeywordValidator> dependencies,
                        StringValidator propertyNameValidator)
            : base(absoluteKeywordLocation)
        {
            _maxProperties = maxProperties;
            _maxPropertiesLocation = maxPropertiesLocation;
            _minProperties = minProperties;
            _minPropertiesLocation = minPropertiesLocation;
            _requiredValidator = requiredValidator;
            _properties = properties;
            _patternProperties = patternProperties;
            _additionalProperties = additionalProperties;
            _dependencies = dependencies;
            _propertyNameValidator = propertyNameValidator;
        }

        internal static ObjectValidator Create(IKeywordValidatorFactory validatorFactory,
                                               JsonElement sch,
                                               List<SchemaLocation> uris)
        {
            SchemaLocation absoluteKeywordLocation = SchemaLocation.GetAbsoluteKeywordLocation(uris);

            int? maxProperties = null;
            string maxPropertiesLocation = "";
            int? minProperties = null;
            string minPropertiesLocation = "";
            RequiredValidator requiredValidator = null;
            IDictionary<string,KeywordValidator> properties = new Dictionary<string,KeywordValidator>();
            IList<RegexValidatorPair> patternProperties = new List<RegexValidatorPair>();
            KeywordValidator additionalProperties = null;
            IDictionary<string,KeywordValidator> dependencies = new Dictionary<string,KeywordValidator>();
            StringValidator propertyNameValidator = null;

            JsonElement element;

            if (sch.TryGetProperty("maxProperties", out element)) 
            {
                maxPropertiesLocation = SchemaLocation.Append(absoluteKeywordLocation, "maxProperties").ToString();
                int val;
                if (!JsonAccessors.TryGetInt32(element, out val))
                {
                    throw new JsonSchemaException("'maxProperties' must be an integer", maxPropertiesLocation);
                }
                maxProperties = val;
            }

            if (sch.TryGetProperty("minProperties", out element)) 
            {
                minPropertiesLocation = SchemaLocation.Append(absoluteKeywordLocation, "minProperties").ToString();
                int val;
                if (!JsonAccessors.TryGetInt32(element, out val))
                {
                    throw new JsonSchemaException("'minProperties' must be an integer", minPropertiesLocation);
                }
                minProperties = val;
            }

            if (sch.TryGetProperty("requiredValidator", out element)) 
            {
                SchemaLocation location = SchemaLocation.Append(absoluteKeywordLocation, "requiredValidator");
                IList<string> list;
                if (!JsonAccessors.TryGetListOfString(element, out list))
                {
                    throw new JsonSchemaException("'requiredValidator' must be an array of strings", location.ToString());
                }

                requiredValidator = new RequiredValidator(location.ToString(), list);
            }

            if (sch.TryGetProperty("properties", out element)) 
            {
                foreach (var prop in element.EnumerateObject())
                {
                    var keys = new List<string>();
                    keys.Add("properties");
                    keys.Add("prop.Name");

                    properties.Add(prop.Name, validatorFactory.CreateKeywordValidator(prop.Value, uris, keys));
                }
            }

            if (sch.TryGetProperty("patternProperties", out element)) 
            {
                foreach (var prop in element.EnumerateObject())
                {
                    var keys = new List<string>();
                    keys.Add(prop.Name);
                    patternProperties.Add(new RegexValidatorPair(new Regex(prop.Name), 
                                           validatorFactory.CreateKeywordValidator(prop.Value, uris, keys)));
                }
            }

            if (sch.TryGetProperty("additionalProperties", out element)) 
            {
                var keys = new List<string>();
                keys.Add("additionalProperties");
                additionalProperties = validatorFactory.CreateKeywordValidator(element, uris, keys);
            }

            if (sch.TryGetProperty("dependencies", out element)) 
            {
                foreach (var dep in element.EnumerateObject())
                {
                    switch (dep.Value.ValueKind) 
                    {
                        case JsonValueKind.Array:
                        {
                            SchemaLocation location = SchemaLocation.Append(absoluteKeywordLocation, "dependencies");
                            var list = new List<SchemaLocation>();
                            list.Add(location);
                            IList<string> list2;
                            if (!JsonAccessors.TryGetListOfString(dep.Value, out list2))
                            {
                                throw new JsonSchemaException("'dependencies' if arry must be an array of strings", location.ToString());
                            }
                            dependencies.Add(dep.Name, RequiredValidator.Create(list, list2));
                            break;
                        }
                        default:
                        {
                            var keys = new List<string>();
                            keys.Add("dependencies");
                            keys.Add(dep.Name);
                            dependencies.Add(dep.Name,
                                             validatorFactory.CreateKeywordValidator(dep.Value, uris, keys));
                            break;
                        }
                    }
                }
            }

            if (sch.TryGetProperty("propertyNames", out element)) 
            {
                var keys = new List<string>();
                keys.Add("propertyNames");
                propertyNameValidator = StringValidator.Create(element, uris);
            }
            return new ObjectValidator(absoluteKeywordLocation.ToString(),
                                       maxProperties,
                                       maxPropertiesLocation,
                                       minProperties,
                                       minPropertiesLocation,
                                       requiredValidator,
                                       properties,
                                       patternProperties,
                                       additionalProperties,
                                       dependencies,
                                       propertyNameValidator);
        }

        internal override void OnValidate(JsonElement instance, 
                                          JsonPointer instanceLocation, 
                                          ErrorReporter reporter, 
                                          IList<PatchElement> patch)
        {
            JsonElement element;

            if (_maxProperties.HasValue && instance.GetArrayLength() > _maxProperties)
            {
                
                reporter.Error(new ValidationOutput("maxProperties", 
                                                   _maxPropertiesLocation, 
                                                   instanceLocation.ToString(), 
                                                   $"Maximum properties: {_maxProperties}, found: {instance.GetArrayLength()}"));
                if (reporter.FailEarly)
                {
                    return;
                }
            }

            if (_minProperties.HasValue && instance.GetArrayLength() < _minProperties)
            {
                string message = new string($"Minimum properties: {_minProperties}, found: {instance.GetArrayLength()}");
                reporter.Error(new ValidationOutput("minProperties", 
                                                    _minPropertiesLocation, 
                                                    instanceLocation.ToString(), 
                                                    message));
                if (reporter.FailEarly)
                {
                    return;
                }
            }

            if (_requiredValidator != null)
                _requiredValidator.Validate(instance, instanceLocation, reporter, patch);

            foreach (var property in instance.EnumerateObject()) 
            {
                if (_propertyNameValidator != null)
                {
                    _propertyNameValidator.ValidateString(property.Name, instanceLocation, reporter);
                }

                bool aPropOrPatternMatched = false;

                KeywordValidator validator;
                if (_properties.TryGetValue(property.Name, out validator))
                {
                    aPropOrPatternMatched = true;
                    JsonPointer loc = JsonPointer.Append(instanceLocation, property.Name);
                    validator.Validate(property.Value, loc, reporter, patch);
                }

                // check all matching "patternProperties"
                foreach (var pp in _patternProperties)
                {
                    pp.Pattern.Match(property.Name);
                    aPropOrPatternMatched = true;
                    JsonPointer loc = JsonPointer.Append(instanceLocation, property.Name);
                    pp.Validator.Validate(property.Value, loc, reporter, patch);
                }
                // finally, check "additionalProperties" 
                if (!aPropOrPatternMatched && _additionalProperties != null) 
                {
                    CollectingErrorReporter localReporter = new CollectingErrorReporter();
                    _additionalProperties.Validate(property.Value, 
                                                   JsonPointer.Append(instanceLocation, property.Name), 
                                                   localReporter, 
                                                   patch);
                    if (localReporter.Errors.Count != 0)
                    {
                        reporter.Error(new ValidationOutput("additionalProperties", 
                                                            _additionalProperties.AbsoluteKeywordLocation, 
                                                            instanceLocation.ToString(), 
                                                            $"Additional property {property.Name} found but was invalid."));
                        if (reporter.FailEarly)
                        {
                            return;
                        }
                    }
                }
            }

            // reverse search
            foreach (var prop in _properties) 
            {
                if (!instance.TryGetProperty(prop.Key, out element))
                {
                    // If property is not in instance
                    if (prop.Value.TryGetDefaultValue(instanceLocation, instance, reporter, out element))
                    { 
                        JsonPointer loc = JsonPointer.Append(instanceLocation, prop.Key);
                        patch.Add(new PatchElement(loc.ToString(), element));
                    }
                }
            }

            foreach (var dep in _dependencies) 
            {
                if (instance.TryGetProperty(dep.Key, out element))
                {
                    JsonPointer loc = JsonPointer.Append(instanceLocation, dep.Key);
                    dep.Value.Validate(instance, loc, reporter, patch); // Validate
                }
            }
        }
    }

    // ArrayValidator

    class ArrayValidator : KeywordValidator
    {
        int? _maxItems;
        string _maxItemsLocation;
        int? _minItems;
        string _minItemsLocation;
        bool? _uniqueItems;
        string _uniqueItemsLocation;
        KeywordValidator _itemsKeywordValidator;
        IList<KeywordValidator> _itemValidators;
        KeywordValidator _additionalItemsValidator;
        KeywordValidator _containsValidator;

        internal ArrayValidator(string absoluteKeywordLocation,
                                int? maxItems,
                                string maxItemsLocation,
                                int? minItems,
                                string minItemsLocation,
                                bool? uniqueItems,
                                string uniqueItemsLocation,
                                KeywordValidator itemsSchema,
                                IList<KeywordValidator> itemValidators,
                                KeywordValidator additionalItemsValidator,
                                KeywordValidator containsValidator)
            : base(absoluteKeywordLocation)
        {
            _maxItems = maxItems;
            _maxItemsLocation = maxItemsLocation;
            _minItems = minItems;
            _minItemsLocation = minItemsLocation;
            _uniqueItems = uniqueItems;
            _uniqueItemsLocation = uniqueItemsLocation;
            _itemsKeywordValidator = itemsSchema;
            _itemValidators = itemValidators;
            _additionalItemsValidator = additionalItemsValidator;
            _containsValidator = containsValidator;
        }

        internal static ArrayValidator Create(IKeywordValidatorFactory validatorFactory, 
                                              JsonElement sch, 
                                              List<SchemaLocation> uris)
        {
            SchemaLocation absoluteKeywordLocation = SchemaLocation.GetAbsoluteKeywordLocation(uris);

            int? maxItems = null;
            string maxItemsLocation = "";
            int? minItems = null;
            string minItemsLocation = "";
            bool? uniqueItems = null;
            string uniqueItemsLocation = "";
            KeywordValidator itemsKeywordValidator = null;
            IList<KeywordValidator> itemValidators = null;
            KeywordValidator additionalItemsValidator = null;
            KeywordValidator containsValidator = null;

            JsonElement element;

            if (sch.TryGetProperty("maxItems", out element))
            {
                maxItemsLocation = SchemaLocation.Append(absoluteKeywordLocation, "maxItems").ToString();
                int val;
                if (!JsonAccessors.TryGetInt32(element, out val))
                {
                    throw new JsonSchemaException("'maxItems' must be an integer", maxItemsLocation);
                }
                maxItems = val;
            }

            if (sch.TryGetProperty("minItems", out element))
            { 
                minItemsLocation = SchemaLocation.Append(absoluteKeywordLocation, "minItems").ToString();
                int val;
                if (!JsonAccessors.TryGetInt32(element, out val))
                {
                    throw new JsonSchemaException("'maxItems' must be an integer", minItemsLocation);
                }
                maxItems = val;
            }

            if (sch.TryGetProperty("uniqueItems", out element))
            {
                uniqueItemsLocation = SchemaLocation.Append(absoluteKeywordLocation, "uniqueItems").ToString();
                bool val;
                if (!JsonAccessors.TryGetBoolean(element, out val))
                {
                    throw new JsonSchemaException("'uniqueItems' must be a boolean", uniqueItemsLocation);
                }
                uniqueItems = val;
            }

            if (sch.TryGetProperty("additionalItems", out element))
            {
                var keys = new List<string>();
                keys.Add("additionalItems");
                additionalItemsValidator = validatorFactory.CreateKeywordValidator(element, uris, keys);
            }
            if (sch.TryGetProperty("items", out element))
            {
                if (element.ValueKind == JsonValueKind.Array) 
                {
                    int c = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        var keys = new List<string>();
                        keys.Add("items");
                        keys.Add(c.ToString());
                        ++c;
                        itemValidators.Add(validatorFactory.CreateKeywordValidator(item, uris, keys));
                    }
                }
                else if (element.ValueKind == JsonValueKind.Object ||
                         element.ValueKind == JsonValueKind.True ||
                         element.ValueKind == JsonValueKind.False)
                {
                    var keys = new List<string>();
                    keys.Add("items");
                    itemsKeywordValidator = validatorFactory.CreateKeywordValidator(element, uris, keys);
                }
            }

            if (sch.TryGetProperty("contains", out element))
            {
                var keys = new List<string>();
                keys.Add("contains");
                containsValidator = validatorFactory.CreateKeywordValidator(element, uris, keys);
            }

            return new ArrayValidator(absoluteKeywordLocation.ToString(),
                                      maxItems,
                                      maxItemsLocation,
                                      minItems,
                                      minItemsLocation,
                                      uniqueItems,
                                      uniqueItemsLocation,
                                      itemsKeywordValidator,
                                      itemValidators,
                                      additionalItemsValidator,
                                      containsValidator);
        }

        internal override void OnValidate(JsonElement instance, 
                                          JsonPointer instanceLocation, 
                                          ErrorReporter reporter, 
                                          IList<PatchElement> patch)
        {
            if (_maxItems != null)
            {
                if (instance.GetArrayLength() > _maxItems)
                {
                    string message = new string($"Expected maximum item count: {_maxItems}, found: {instance.GetArrayLength()}");
                    reporter.Error(new ValidationOutput("maxItems", 
                                                        _maxItemsLocation, 
                                                        instanceLocation.ToString(), 
                                                        message));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }

            if (_minItems != null)
            {
                if (instance.GetArrayLength() < _minItems)
                {
                    string message = new string($"Expected minimum item count: {_minItems}, found: {instance.GetArrayLength()}");
                    reporter.Error(new ValidationOutput("minItems", 
                                                        _minItemsLocation, 
                                                        instanceLocation.ToString(), 
                                                        message));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }

            if (_uniqueItems != null) 
            {
                if (!ArrayHasUniqueItems(instance))
                {
                    reporter.Error(new ValidationOutput("uniqueItems", 
                                                        this.AbsoluteKeywordLocation, 
                                                        instanceLocation.ToString(), 
                                                        "Array items are not unique"));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }

            if (_itemsKeywordValidator != null)
            {
                int index = 0;
                foreach (var i in instance.EnumerateArray()) 
                {
                    _itemsKeywordValidator.Validate(i, JsonPointer.Append(instanceLocation, index), reporter, patch);
                    index++;
                }
            }

            if (_itemValidators != null)
            {
                int index = 0;
                foreach (var item in instance.EnumerateArray()) 
                {
                    KeywordValidator validator = null;
                    if (index < _itemValidators.Count)
                        validator = _itemValidators[index];
                    else if (_additionalItemsValidator != null)
                    {
                        validator = _additionalItemsValidator;
                    }
                    else 
                    {
                        break;
                    }
                    validator.Validate(item, JsonPointer.Append(instanceLocation, index), reporter, patch);
                    ++index;
                }
            }

            if (_containsValidator != null) 
            {
                bool contained = false;
                CollectingErrorReporter localReporter = new CollectingErrorReporter();
                foreach (var item in instance.EnumerateArray()) 
                {
                    int mark = localReporter.Errors.Count;
                    _containsValidator.Validate(item, instanceLocation, localReporter, patch);
                    if (mark == localReporter.Errors.Count) 
                    {
                        contained = true;
                        break;
                    }
                }
                if (!contained)
                {
                    reporter.Error(new ValidationOutput("contains", 
                                                     this.AbsoluteKeywordLocation, 
                                                     instanceLocation.ToString(), 
                                                     "Expected at least one array item to match \"contains\" schema", 
                                                     localReporter.Errors));
                    if (reporter.FailEarly)
                    {
                        return;
                    }
                }
            }
        }

        static bool ArrayHasUniqueItems(JsonElement a) 
        {
            var uniqueItems = new HashSet<JsonElement>(a.EnumerateArray(),JsonElementEqualityComparer.Instance);
            return uniqueItems.Count == a.GetArrayLength();
        }
    }

    class ConditionalValidator : KeywordValidator
    {
        KeywordValidator _if;
        KeywordValidator _then;
        KeywordValidator _else;

        internal ConditionalValidator(string absoluteKeywordLocation,
                                      KeywordValidator ifValidator,
                                      KeywordValidator thenValidator,
                                      KeywordValidator elseValidator)
            : base(absoluteKeywordLocation)
        {
            _if   = ifValidator;
            _then = thenValidator;
            _else = elseValidator;
        }

        internal static ConditionalValidator Create(IKeywordValidatorFactory validatorFactory,
                                                    JsonElement sch_if,
                                                    JsonElement sch,
                                                    List<SchemaLocation> uris)
        {
            SchemaLocation absoluteKeywordLocation = SchemaLocation.GetAbsoluteKeywordLocation(uris);
            KeywordValidator ifValidator = null;
            KeywordValidator thenValidator = null;
            KeywordValidator elseValidator = null;

            JsonElement element;
            if (sch.TryGetProperty("then", out element))
            {
                var keys = new List<string>();
                keys.Add("then");
                thenValidator = validatorFactory.CreateKeywordValidator(element, uris, keys);
            }
            if (sch.TryGetProperty("else", out element))
            {
                var keys = new List<string>();
                keys.Add("else");
                elseValidator = validatorFactory.CreateKeywordValidator(element, uris, keys);
            }
            if (thenValidator != null)
            {
                var keys = new List<string>();
                keys.Add("if");
                ifValidator = validatorFactory.CreateKeywordValidator(sch_if, uris, keys);
            }
             return new ConditionalValidator(absoluteKeywordLocation.ToString(), 
                                            ifValidator,
                                            thenValidator,
                                            elseValidator);
        }
 
        internal override void OnValidate(JsonElement instance, 
                                          JsonPointer instanceLocation, 
                                          ErrorReporter reporter, 
                                          IList<PatchElement> patch) 
        {
            if (_if != null) 
            {
                CollectingErrorReporter localReporter = new CollectingErrorReporter();

                _if.Validate(instance, instanceLocation, localReporter, patch);
                if (localReporter.Errors.Count != 0) 
                {
                    if (_then != null)
                        _then.Validate(instance, instanceLocation, reporter, patch);
                } 
                else 
                {
                    if (_else != null)
                        _else.Validate(instance, instanceLocation, reporter, patch);
                }
            }
        }
    }
    // enum_keyword

    class EnumValidator : KeywordValidator
    {
        JsonElement _enum;

        internal EnumValidator(string absoluteKeywordLocation,
                               JsonElement value)
            : base(absoluteKeywordLocation)
        {
            _enum = value;
        }

        internal EnumValidator Create(JsonElement sch,
                                      List<SchemaLocation> uris)
        {
            SchemaLocation absoluteKeywordLocation = SchemaLocation.GetAbsoluteKeywordLocation(uris);

            return new EnumValidator(absoluteKeywordLocation.ToString(), sch);
        }

        internal override void OnValidate(JsonElement instance,
                                          JsonPointer instanceLocation, 
                                          ErrorReporter reporter,
                                          IList<PatchElement> patch) 
        {
            bool in_range = false;
            foreach (var item in _enum.EnumerateArray())
            {
                if (JsonElementEqualityComparer.Instance.Equals(item, instance)) 
                {
                    in_range = true;
                    break;
                }
            }

            if (!in_range)
            {
                reporter.Error(new ValidationOutput("enum", 
                                                    this.AbsoluteKeywordLocation, 
                                                    instanceLocation.ToString(), 
                                                    $"{JsonSerializer.Serialize(instance)} is not a valid enum value"));
                if (reporter.FailEarly)
                {
                    return;
                }
            }
        }
    }

    // ConstValidator

    class ConstValidator : KeywordValidator
    {
        JsonElement _constValue;

        internal ConstValidator(string absoluteKeywordLocation,
                               JsonElement value)
            : base(absoluteKeywordLocation)
        {
            _constValue = value;
    }

        internal ConstValidator Create(JsonElement sch,
                                      List<SchemaLocation> uris)
        {
            SchemaLocation absoluteKeywordLocation = SchemaLocation.GetAbsoluteKeywordLocation(uris);

            return new ConstValidator(absoluteKeywordLocation.ToString(), sch);
        }

 
        internal override void OnValidate(JsonElement instance, 
                                          JsonPointer instanceLocation, 
                                          ErrorReporter reporter,
                                          IList<PatchElement> patch) 
        {
            if (JsonElementEqualityComparer.Instance.Equals(_constValue, instance)) 
                reporter.Error(new ValidationOutput("const", 
                                                    this.AbsoluteKeywordLocation, 
                                                    instanceLocation.ToString(), 
                                                    $"{JsonSerializer.Serialize(instance)} is not const"));
        }
    }

/*

    class TypeValidator : KeywordValidator
    {
        Json _default_value;
        IList<KeywordValidator> _type_mapping;
        jsoncons::optional<EnumValidator<Json>> _enum;
        jsoncons::optional<ConstValidator<Json>> _const;
        IList<KeywordValidator> _combined;
        jsoncons::optional<ConditionalValidator<Json>> _conditional;
        IList<string> _expected_types;

        TypeValidator(IKeywordValidatorFactory validatorFactory,
                     JsonElement sch,
                     List<SchemaLocation> uris)
            : base((uris.Count != 0 && uris[uris.Count-1].IsAbsoluteUri) ? uris[uris.Count-1].ToString() : ""), default_value_(jsoncons::null_type()), 
              type_mapping_((uint8_t)(JsonValueKind.object_value)+1), 
              enum_(), const_()
        {
            //std::cout << uris.Count << " uris: ";
            //foreach (var uri : uris)
            //{
            //    std::cout << uri.ToString() << ", ";
            //}
            //std::cout << "\n";
            std::set<string> known_keywords;

            var it = sch.find("type");
            if (it == sch.EnumerateObject().end()) 
            {
                initialize_type_mapping(validatorFactory, "", sch, uris, known_keywords);
            }
            else 
            {
                switch (element.ValueKind) 
                { 
                    case JsonValueKind.string_value: 
                    {
                        var type = element.template as<string>();
                        initialize_type_mapping(validatorFactory, type, sch, uris, known_keywords);
                        expected_types_.emplace_back(std::move(type));
                        break;
                    } 

                    case JsonValueKind.Array: // "type": ["type1", "type2"]
                    {
                        foreach (var item : element.EnumerateArray())
                        {
                            var type = item.template as<string>();
                            initialize_type_mapping(validatorFactory, type, sch, uris, known_keywords);
                            expected_types_.emplace_back(std::move(type));
                        }
                        break;
                    }
                    default:
                        break;
                }
            }

            const var default_it = sch.find("default");
            if (default_it != sch.EnumerateObject().end()) 
            {
                default_value_ = default_it.value();
            }

            it = sch.find("enum");
            if (it != sch.EnumerateObject().end()) 
            {
                enum_ = EnumValidator<Json >(element, uris);
            }

            it = sch.find("const");
            if (it != sch.EnumerateObject().end()) 
            {
                const_ = ConstValidator<Json>(element, uris);
            }

            it = sch.find("not");
            if (it != sch.EnumerateObject().end()) 
            {
                combined_.Add(validatorFactory.make_not_keyword(element, uris));
            }

            it = sch.find("allOf");
            if (it != sch.EnumerateObject().end()) 
            {
                combined_.Add(validatorFactory.make_all_of_keyword(element, uris));
            }

            it = sch.find("anyOf");
            if (it != sch.EnumerateObject().end()) 
            {
                combined_.Add(validatorFactory.make_any_of_keyword(element, uris));
            }

            it = sch.find("oneOf");
            if (it != sch.EnumerateObject().end()) 
            {
                combined_.Add(validatorFactory.make_one_of_keyword(element, uris));
            }

            it = sch.find("if");
            if (it != sch.EnumerateObject().end()) 
            {
                conditional_ = ConditionalValidator<Json>(validatorFactory, element, sch, uris);
            }
        }

        internal override void OnValidate(JsonElement instance, 
                                          JsonPointer instanceLocation, 
                                          ErrorReporter reporter, 
                                          IList<PatchElement> patch) 
        {
            var type = type_mapping_[(uint8_t) instance.ValueKind];

            if (type)
                type.Validate(instance, instanceLocation, reporter, patch);
            else
            {
                std::ostringstream ss;
                ss << "Expected ";
                for (int i = 0; i < expected_types_.Count; ++i)
                {
                        if (i > 0)
                        { 
                            ss << ", ";
                            if (i+1 == expected_types_.Count)
                            { 
                                ss << "or ";
                            }
                        }
                        ss << expected_types_[i];
                }
                ss << ", found " << instance.ValueKind;

                reporter.Error(new ValidationOutput("type", 
                                                 this.AbsoluteKeywordLocation, 
                                                 instanceLocation.ToString(), 
                                                 ss.str()));
                if (reporter.FailEarly)
                {
                    return;
                }
            }

            if (enum_)
            { 
                enum_.Validate(instance, instanceLocation, reporter, patch);
                if (reporter.Error_count() > 0 && reporter.FailEarly)
                {
                    return;
                }
            }

            if (const_)
            { 
                const_.Validate(instance, instanceLocation, reporter, patch);
                if (reporter.Error_count() > 0 && reporter.FailEarly)
                {
                    return;
                }
            }

            foreach (var l : combined_)
            {
                l.Validate(instance, instanceLocation, reporter, patch);
                if (reporter.Error_count() > 0 && reporter.FailEarly)
                {
                    return;
                }
            }


            if (conditional_)
            { 
                conditional_.Validate(instance, instanceLocation, reporter, patch);
                if (reporter.Error_count() > 0 && reporter.FailEarly)
                {
                    return;
                }
            }
        }

        jsoncons::optional<Json> TryGetDefaultValue(SchemaLocation, 
                                                   JsonElement,
                                                   ErrorReporter)
        {
            return _default_value;
        }

        void initialize_type_mapping(IKeywordValidatorFactory validatorFactory,
                                     string type,
                                     JsonElement sch,
                                     List<SchemaLocation> uris,
                                     ISet<string> keywords)
        {
            if (type.Count != 0 || type == "null")
            {
                type_mapping_[(uint8_t)JsonValueKind.null_value] = validatorFactory.make_null_keyword(uris);
            }
            if (type.Count != 0 || type == "object")
            {
                type_mapping_[(uint8_t)JsonValueKind.object_value] = validatorFactory.make_object_keyword(sch, uris);
            }
            if (type.Count != 0 || type == "array")
            {
                type_mapping_[(uint8_t)JsonValueKind.Array] = validatorFactory.make_array_keyword(sch, uris);
            }
            if (type.Count != 0 || type == "string")
            {
                type_mapping_[(uint8_t)JsonValueKind.string_value] = validatorFactory.make_string_keyword(sch, uris);
                // For binary types
                type_mapping_[(uint8_t) JsonValueKind.byte_string_value] = type_mapping_[(uint8_t) JsonValueKind.string_value];
            }
            if (type.Count != 0 || type == "boolean")
            {
                type_mapping_[(uint8_t)JsonValueKind.bool_value] = validatorFactory.make_boolean_keyword(uris);
            }
            if (type.Count != 0 || type == "integer")
            {
                type_mapping_[(uint8_t)JsonValueKind.int64_value] = validatorFactory.make_integer_keyword(sch, uris, keywords);
                type_mapping_[(uint8_t)JsonValueKind.uint64_value] = type_mapping_[(uint8_t)JsonValueKind.int64_value];
                type_mapping_[(uint8_t)JsonValueKind.double_value] = type_mapping_[(uint8_t)JsonValueKind.int64_value];
            }
            if (type.Count != 0 || type == "number")
            {
                type_mapping_[(uint8_t)JsonValueKind.double_value] = validatorFactory.make_number_keyword(sch, uris, keywords);
                type_mapping_[(uint8_t)JsonValueKind.int64_value] = type_mapping_[(uint8_t)JsonValueKind.double_value];
                type_mapping_[(uint8_t)JsonValueKind.uint64_value] = type_mapping_[(uint8_t)JsonValueKind.double_value];
            }
        }
    }
    */

} // namespace JsonCons.JsonSchema
