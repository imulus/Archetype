using System.Collections.Generic;
using System.Linq;
using Archetype.Extensions;
using Newtonsoft.Json;
using Umbraco.Core;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web;

namespace Archetype.Models
{
    /// <summary>
    /// Model that represents a stored property in Archetype.
    /// </summary>
    public class ArchetypePropertyModel
    {
        [JsonProperty("alias")]
        public string Alias { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonProperty("propertyEditorAlias")]
        public string PropertyEditorAlias { get; internal set; }

        [JsonProperty("dataTypeId")]
        public int DataTypeId { get; internal set; }

        [JsonProperty("dataTypeGuid")]
        internal string DataTypeGuid { get; set; }

        // container for temporary editor state from the Umbraco backend
        [JsonProperty("editorState")]
        internal UmbracoEditorState EditorState { get; set; }

        [JsonProperty("hostContentType")]
        internal PublishedContentType HostContentType { get; set; }

        /// <summary>
        /// Gets the value based on the type given.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetValue<T>()
        {
            // Try Umbraco's PropertyValueConverters
            var converters = UmbracoContext.Current != null ? PropertyValueConvertersResolver.Current.Converters : Enumerable.Empty<IPropertyValueConverter>();
            if (!string.IsNullOrWhiteSpace(this.PropertyEditorAlias) && converters.Any())
            {
                var convertedAttempt = TryConvertWithPropertyValueConverters<T>(Value, converters);
                if (convertedAttempt.Success)
                    return convertedAttempt.Result;
            }

            // If the value is of type T, just return it
            if (Value is T)
                return (T)Value;

            // No PropertyValueConverters matched, so try a regular type conversion
            var convertAttempt2 = Value.TryConvertTo<T>();
            if (convertAttempt2.Success)
                return convertAttempt2.Result;

            return default(T);
        }

        /// <summary>
        /// Tries to convert the value with property value converters.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <param name="converters">The converters.</param>
        /// <returns></returns>
        private Attempt<T> TryConvertWithPropertyValueConverters<T>(object value, IEnumerable<IPropertyValueConverter> converters)
        {
            var properyType = this.CreateDummyPropertyType();

            // In umbraco, there are default value converters that try to convert the 
            // value if all else fails. The problem is, they are also in the list of
            // converters, and the means for filtering these out is internal, so
            // we currently have to try ALL converters to see if they can convert
            // rather than just finding the most appropreate. If the ability to filter
            // out default value converters becomes public, the following logic could
            // and probably should be changed.
            foreach (var converter in converters.Where(x => x.IsConverter(properyType)))
            {
                // Convert the type using a found value converter
                var value2 = converter.ConvertDataToSource(properyType, value, false);

                // If the value is of type T, just return it
                if (value2 is T)
                    return Attempt<T>.Succeed((T)value2);

                // If ConvertDataToSource failed try ConvertSourceToObject.
                var value3 = converter.ConvertSourceToObject(properyType, value2, false);

                // If the value is of type T, just return it
                if (value3 is T)
                    return Attempt<T>.Succeed((T)value3);

                // Value is not final value type, so try a regular type conversion aswell
                var convertAttempt = value2.TryConvertTo<T>();
                if (convertAttempt.Success)
                    return Attempt<T>.Succeed(convertAttempt.Result);
            }

            return Attempt<T>.Fail();
        }

        /// <summary>
        /// Model that represents the current UmbracoEditorState
        /// </summary>
        internal class UmbracoEditorState
        {
            // container for the names of any files selected for a property in the Umbraco backend
            [JsonProperty("fileNames")]
            public IEnumerable<string> FileNames;
        }
    }
}
