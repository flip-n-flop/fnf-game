using System;
using System.Collections.Generic;
using System.Reflection;
using FlipNflop.Commons.Contract;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FlipNflop.Gla.Serialization
{
    /// <summary>
    /// Newtonsoft.Json contract resolver for the types of the shared library FlipNflopCommons.
    /// For every property of a contract type (<see cref="ContractNames.ForType"/>), it sets the
    /// transport name from <see cref="ContractNames"/>, makes the field required
    /// (<see cref="Required.AllowNull"/> when the property admits <c>null</c>, otherwise
    /// <see cref="Required.Always"/>), writes it even when it is <c>null</c>, and reads its scalar
    /// values and items with <see cref="StrictValueConverter"/>. Other types keep the default behavior.
    /// Used by <see cref="GlaJson"/>.
    /// </summary>
    internal sealed class GlaContractResolver : DefaultContractResolver
    {
        #region Constants

        private const string NullableAttributeName = "System.Runtime.CompilerServices.NullableAttribute";
        private const string NullableContextAttributeName = "System.Runtime.CompilerServices.NullableContextAttribute";
        private const byte NotAnnotatedFlag = 1;

        #endregion

        #region Contract Creation

        /// <summary>
        /// Rejects a contract type whose transport names do not match its properties, which means
        /// that FlipNflopCommons and this resolver are out of sync. Interacts with <see cref="ContractNames.ForType"/>.
        /// </summary>
        /// <param name="type">The type being resolved.</param>
        /// <param name="memberSerialization">The member serialization mode.</param>
        /// <returns>The properties of the type.</returns>
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            IList<JsonProperty> properties = base.CreateProperties(type, memberSerialization);
            IReadOnlyDictionary<string, string> names = ContractNames.ForType(type);
            if (names != null && properties.Count != names.Count)
            {
                throw new JsonSerializationException(
                    $"Contract type {type.Name} has {properties.Count} serializable properties but {names.Count} transport names in ContractNames.");
            }

            return properties;
        }

        /// <summary>
        /// Applies the transport name, the presence rule and the strict reading to a property of a
        /// contract type. Interacts with <see cref="ContractNames.ForType"/> and <see cref="StrictValueConverter"/>.
        /// </summary>
        /// <param name="member">The property.</param>
        /// <param name="memberSerialization">The member serialization mode.</param>
        /// <returns>The configured property.</returns>
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty property = base.CreateProperty(member, memberSerialization);
            IReadOnlyDictionary<string, string> names = member.DeclaringType != null ? ContractNames.ForType(member.DeclaringType) : null;
            if (names == null) return property;

            if (!names.TryGetValue(member.Name, out string transportName))
            {
                throw new JsonSerializationException(
                    $"Property {member.DeclaringType.Name}.{member.Name} has no transport name in ContractNames.");
            }

            property.PropertyName = transportName;
            property.Required = AdmitsNull(member, property.PropertyType) ? Required.AllowNull : Required.Always;
            property.NullValueHandling = NullValueHandling.Include;
            property.DefaultValueHandling = DefaultValueHandling.Include;

            if (StrictValueConverter.Instance.CanConvert(property.PropertyType))
                property.Converter = StrictValueConverter.Instance;

            Type itemType = ItemTypeOf(property.PropertyType);
            if (itemType != null && StrictValueConverter.Instance.CanConvert(itemType))
                property.ItemConverter = StrictValueConverter.Instance;

            return property;
        }

        #endregion

        #region Nullability

        /// <summary>
        /// Whether a property of a contract type admits <c>null</c>: a <see cref="Nullable{T}"/>
        /// value type, or a reference type annotated as nullable in the compiled FlipNflopCommons
        /// (the C# nullable metadata, read from the attribute instances by name). A reference type
        /// without nullable metadata is treated as admitting <c>null</c>, so that writing never fails
        /// when a build strips that metadata.
        /// </summary>
        /// <param name="member">The property.</param>
        /// <param name="propertyType">The type of the property.</param>
        /// <returns><c>true</c> when the field may be <c>null</c>.</returns>
        private static bool AdmitsNull(MemberInfo member, Type propertyType)
        {
            if (propertyType.IsValueType) return Nullable.GetUnderlyingType(propertyType) != null;

            byte flag = NullableFlagOf(member);
            return flag != NotAnnotatedFlag;
        }

        /// <summary>
        /// Reads the nullable flag of a member: its own <c>NullableAttribute</c> (first flag), or the
        /// <c>NullableContextAttribute</c> of the closest declaring type; 0 (oblivious) when there is none.
        /// </summary>
        /// <param name="member">The member.</param>
        /// <returns>0 (oblivious), 1 (not annotated) or 2 (annotated).</returns>
        private static byte NullableFlagOf(MemberInfo member)
        {
            foreach (object attribute in member.GetCustomAttributes(false))
            {
                if (attribute.GetType().FullName != NullableAttributeName) continue;

                object flags = attribute.GetType().GetField("NullableFlags")?.GetValue(attribute);
                if (flags is byte[] bytes && bytes.Length > 0) return bytes[0];
            }

            for (Type type = member.DeclaringType; type != null; type = type.DeclaringType)
            {
                foreach (object attribute in type.GetCustomAttributes(false))
                {
                    if (attribute.GetType().FullName != NullableContextAttributeName) continue;

                    object flag = attribute.GetType().GetField("Flag")?.GetValue(attribute);
                    if (flag is byte value) return value;
                }
            }

            return 0;
        }

        /// <summary>
        /// The item type of an array or of a generic <see cref="List{T}"/>, or <c>null</c> for other types.
        /// </summary>
        /// <param name="type">The property type.</param>
        /// <returns>The item type, or <c>null</c>.</returns>
        private static Type ItemTypeOf(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return type.GetGenericArguments()[0];
            return null;
        }

        #endregion
    }
}
