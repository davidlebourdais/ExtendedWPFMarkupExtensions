using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace EMA.ExtendedWPFMarkupExtensions.Utils
{
    /// <summary>
    /// Provides extension methods for the <see cref="Binding"/> class.
    /// </summary>
    public static class BindingExtensions
    {
        /// <summary>
        /// Clones a <see cref="Binding"/> object.
        /// </summary>
        /// <param name="binding">The binding to be cloned.</param>
        /// <returns>A new instance with properties initialized to object-to-be-clone's ones.</returns>
        public static Binding Clone(this Binding binding)
        {
            if (binding == null) return null;

            var toReturn = new Binding()
            {
                FallbackValue = binding.FallbackValue,
                StringFormat = binding.StringFormat,
                TargetNullValue = binding.TargetNullValue,
                BindingGroupName = binding.BindingGroupName,
                Delay = binding.Delay,
                UpdateSourceTrigger = binding.UpdateSourceTrigger,
                NotifyOnSourceUpdated = binding.NotifyOnSourceUpdated,
                NotifyOnValidationError = binding.NotifyOnValidationError,
                Converter = binding.Converter,
                ConverterParameter = binding.ConverterParameter,
                ConverterCulture = binding.ConverterCulture,
                IsAsync = binding.IsAsync,
                XPath = binding.XPath,
                ValidatesOnDataErrors = binding.ValidatesOnDataErrors,
                ValidatesOnNotifyDataErrors = binding.ValidatesOnNotifyDataErrors,
                BindsDirectlyToSource = binding.BindsDirectlyToSource,
                ValidatesOnExceptions = binding.ValidatesOnExceptions,
                Path = binding.Path,
                UpdateSourceExceptionFilter = binding.UpdateSourceExceptionFilter,

                Mode = binding.Mode
            };
            if (binding.ValidationRules != null)
                foreach (var rule in binding.ValidationRules)
                    toReturn.ValidationRules.Add(rule);

            // Do not set properties even if null as it will trigger exception in the
            // Binding class implementation + cheat a bit by giving source type priority 
            // orders (otherwise exception is triggered too):
            if (binding.Source != null)
                toReturn.Source = binding.Source;
            else if (binding.ElementName != null)
                toReturn.ElementName = binding.ElementName;
            else if (binding.RelativeSource != null)
                toReturn.RelativeSource = binding.RelativeSource;


            return toReturn;
        }

        #region Equivalency extension
        /// <summary>
        /// Determines if a <see cref="Binding"/> object has the same properties
        /// as another one.
        /// </summary>
        /// <param name="binding">A binding to be checked.</param>
        /// <param name="reference">A binding used as reference for comparison.</param>
        /// <returns>True if all properties are equal.</returns>
        public static bool IsEquivalentTo(this Binding binding, Binding reference)
        {
            if (binding == null && reference != null) return false;
            if (binding != null && reference == null) return false;

            return
                   binding.Source == reference.Source
                && binding.ElementName == reference.ElementName
                && AreRelativeSourceEquivalent(binding.RelativeSource, reference.RelativeSource)
                && ArePropertyPathEquivalent(binding.Path, reference.Path)
                && binding.Mode == reference.Mode
                && binding.Converter == reference.Converter
                && binding.ConverterParameter == reference.ConverterParameter
                && AreCulutureInfoEquivalent(binding.ConverterCulture, reference.ConverterCulture)
                && binding.UpdateSourceTrigger == reference.UpdateSourceTrigger
                && binding.FallbackValue == reference.FallbackValue
                && binding.TargetNullValue == reference.TargetNullValue
                && binding.ValidatesOnDataErrors == reference.ValidatesOnDataErrors
                && binding.ValidatesOnNotifyDataErrors == reference.ValidatesOnNotifyDataErrors
                && binding.ValidatesOnExceptions == reference.ValidatesOnExceptions
                && binding.StringFormat == reference.StringFormat
                && binding.Delay == reference.Delay
                && binding.BindingGroupName == reference.BindingGroupName
                && binding.BindsDirectlyToSource == reference.BindsDirectlyToSource
                && binding.NotifyOnSourceUpdated == reference.NotifyOnSourceUpdated
                && binding.NotifyOnValidationError == reference.NotifyOnValidationError
                && binding.IsAsync == reference.IsAsync
                && binding.XPath == reference.XPath
                && binding.UpdateSourceExceptionFilter == reference.UpdateSourceExceptionFilter
                && (binding.ValidationRules == null && reference.ValidationRules == null
                    || (binding.ValidationRules != null && reference.ValidationRules != null && !binding.ValidationRules.Except(reference.ValidationRules).Any()));
        }

        /// <summary>
        /// Determines if two <see cref="RelativeSource"/> are equivalent.
        /// </summary>
        /// <param name="relativesource">First object to be compared.</param>
        /// <param name="reference">Reference object.</param>
        /// <returns>True is the two objects are the same or have the same property values.</returns>
        private static bool AreRelativeSourceEquivalent(RelativeSource relativesource, RelativeSource reference)
            => relativesource == reference || (relativesource != null && reference != null && relativesource.Mode == reference.Mode
                && (relativesource.Mode != RelativeSourceMode.FindAncestor || (relativesource.AncestorType == reference.AncestorType && relativesource.AncestorLevel == reference.AncestorLevel)));

        /// <summary>
        /// Determines if two <see cref="PropertyPath"/> are equivalent.
        /// </summary>
        /// <param name="propertypath">First object to be compared.</param>
        /// <param name="reference">Reference object.</param>
        /// <returns>True is the two objects are the same or have the same property values.</returns>
        private static bool ArePropertyPathEquivalent(PropertyPath propertypath, PropertyPath reference)
            => propertypath == reference || (propertypath != null && reference != null && propertypath.Path == reference.Path
            && (propertypath.PathParameters == reference.PathParameters || !propertypath.PathParameters.Except(reference.PathParameters).Any()));

        /// <summary>
        /// Determines if two <see cref="CultureInfo"/> are equivalent.
        /// </summary>
        /// <param name="cultureinfo">First object to be compared.</param>
        /// <param name="reference">Reference object.</param>
        /// <returns>True is the two objects are the same or have the same property values.</returns>
        private static bool AreCulutureInfoEquivalent(CultureInfo cultureinfo, CultureInfo reference)
            => cultureinfo == reference || (cultureinfo != null && reference != null && cultureinfo.Equals(reference));
        #endregion

        #region Value resolver extensions
        /// <summary>
        /// Resolves and returns a binding result on a given target object.
        /// </summary>
        ///  <param name="binding">The binding to be evaluated.</param>
        /// <param name="target">The object on which the binding must be used.</param>
        /// <returns>The result of the binding operation on the target.</returns>
        public static object ResolveValue(this Binding binding, DependencyObject target)
            => BindingHelpers.ResolveBindingValue(binding, target);

        /// <summary>
        /// Resolves and returns a binding result on a given target object.
        /// </summary>
        ///  <param name="binding">The binding to be evaluated.</param>
        /// <param name="serviceProvider">Service provider provided by the framework.</param>
        /// <returns>The result of the binding operation on the target.</returns>
        public static object ResolveValue(this Binding binding, IServiceProvider serviceProvider)
            => BindingHelpers.ResolveBindingValue(binding, serviceProvider);
        #endregion
    }
}
