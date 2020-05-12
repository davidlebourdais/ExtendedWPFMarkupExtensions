using System.Linq;
using System.Windows.Data;

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
                && binding.RelativeSource == reference.RelativeSource
                && binding.Path == reference.Path
                && binding.Mode == reference.Mode
                && binding.Converter == reference.Converter
                && binding.ConverterParameter == reference.ConverterParameter
                && binding.ConverterCulture == reference.ConverterCulture
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
    }
}
