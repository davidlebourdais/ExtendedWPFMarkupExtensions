﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace EMA.ExtendedWPFMarkupExtensions
{
    /// <summary>
    /// Markup extension base for custom multibindings.
    /// </summary>
    [MarkupExtensionReturnType(typeof(MultiBindingExpression))]
    [ContentProperty(nameof(Bindings))]
    public class MultiBindingExtension : MarkupExtension, IAddChild
    {
        /// <summary>
        /// Stores an empty multibinding to be used as default value in some use cases.
        /// </summary>
        protected static MultiBinding EmptyMultiBinding = new MultiBinding();

        /// <summary>
        /// Gets a value indicating if the extension context must persist
        /// after <see cref="ProvideValue(IServiceProvider)"/> is invoked.
        /// </summary>
        protected virtual bool IsExtensionPersistent { get; } = false;

        /// <summary>
        /// Stores the <see cref="MultiBindingExpression"/> generated by this extension
        /// as a weak reference.
        /// </summary>
        protected WeakReference GeneratedMultibindingExpression { get; set; }

        /// <summary>
        /// Gets a value indicating if the current extension is valid.
        /// </summary>
        protected bool IsExtensionValid => GeneratedMultibindingExpression.IsAlive;

        /// <summary>
        /// Initiates a new <see cref="MultiBindingExtension"/>.
        /// </summary>
        public MultiBindingExtension() : base()
        {
            // Init collection properties:
            Bindings = new Collection<BindingBase>();
            ValidationRules = new Collection<ValidationRule>();
        }

        /// <summary>
        /// Provides a values to be used by the framework.
        /// </summary>
        /// <param name="serviceProvider">Service provider offered by the framework.</param>
        /// <returns>A <see cref="MultiBindingExpression"/> for the targeted object and property.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (!(serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget pvt)) 
                return new MultiBinding().ProvideValue(serviceProvider);

            if (!(pvt.TargetObject is DependencyObject targetObject))
                return new MultiBinding().ProvideValue(serviceProvider);

            // Template usage case, see https://stackoverflow.com/questions/26877676/how-do-i-get-a-bindingexpression-from-a-binding-object.
            if (pvt?.TargetObject?.GetType().Name == "SharedDp")
                return pvt.TargetObject;

            // In source of BindingBase classes, we should return the current instance (so this extension) as a result
            // when encountering a CLR property. However, framework would allow only BindingBase derivatives as a result,
            // and we cannot functionnaly derive from them (sealed + internal dependencies). 
            // Thus, throw exceptions to prevent developpers from using this extension in some specific cases:
            if (pvt?.TargetObject is SetterBase)  // we cannot get target for a CLI setter object
                throw new NotSupportedException(nameof(MultiBindingExtension) + " cannot be used with a " + nameof(SetterBase) + ".");
            else if (pvt?.TargetObject is TriggerBase)  // same for triggers
                throw new NotSupportedException(nameof(MultiBindingExtension) + " cannot be used with a " + nameof(TriggerBase) + ".");
            else if (pvt?.TargetObject is Condition)  // same for datatrigger conditions
                throw new NotSupportedException(nameof(MultiBindingExtension) + " cannot be used with a " + nameof(Condition) + ".");

            if (!(pvt.TargetProperty is DependencyProperty targetProperty)) 
                return new MultiBinding().ProvideValue(serviceProvider);

            // Create multibinding:
            var multibinding = createMultiBinding(serviceProvider);

            // Manualy set the binding here:
            // /!\ Very important: this class sets the binding, not the held multibinding underlying class:
            var expression = BindingOperations.SetBinding(targetObject, targetProperty, multibinding);

            if (expression is MultiBindingExpression multibindingexpression)
            {
                if (IsExtensionPersistent && targetObject is FrameworkElement targetAsFE)
                {
                    // Important note: it's a bad idea to add timers or reference the targeted object in anyway as 
                    // this will result in a memory leak. This is probably due to bad handling from the Framework, but hey, we're 
                    // not supposed to create extensions that live after they  are consummed anyway so let's play a minimum by the 
                    // rules and only survive through a strong event reference.
                    //TargetObjectAsFE = new WeakReference<FrameworkElement>(targetAsFE);  // so do not use.
                    targetAsFE.Unloaded += TargetAsFE_Unloaded;  // registering to this event will help us to be kept alive until target object is unloaded by the framework.
                }
                GeneratedMultibindingExpression = new WeakReference(multibindingexpression);
            }

            return expression; //multibinding.ProvideValue(serviceProvider);  // provide a framework multibinding expression as a result of the extension.
        }

        /// <summary>
        /// Invoked when the target object is unloaded.
        /// </summary>
        /// <param name="sender">Should be our target object.</param>
        /// <param name="e">Routed event information.</param>
        private void TargetAsFE_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement casted)
                casted.Unloaded -= TargetAsFE_Unloaded;
        }

        /// <summary>
        /// Effectively generates the multibinding expression.
        /// </summary>
        /// <param name="serviceProvider">Service provider given by the framework.</param>
        /// <returns>A new <see cref="MultiBinding"/> object.</returns>
        protected MultiBinding createMultiBinding(IServiceProvider serviceProvider)
        {
            // Retrieve object over which the extension is applied
            if (!(serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget pvt))
                return EmptyMultiBinding;

            if (!(pvt.TargetObject is DependencyObject targetObject))
                return EmptyMultiBinding;

            if (!(pvt.TargetProperty is DependencyProperty targetProperty)) 
                return EmptyMultiBinding;

            var modeToSet = this.Mode;
            // Correct mode if not compatible:
            if (modeToSet == BindingMode.TwoWay)
            {
                if (targetProperty.GetMetadata(targetObject.GetType()) is FrameworkPropertyMetadata frameworktargetproperty 
                    && !frameworktargetproperty.BindsTwoWayByDefault)
                    modeToSet = BindingMode.OneWay;

                // Change also if dp is clearly set as readonly:
                if (targetProperty.ReadOnly)
                    modeToSet = BindingMode.OneWay;
            }

            // Create binding:
            var multibinding = new MultiBinding
            {
                FallbackValue = this.FallbackValue,
                StringFormat = this.StringFormat,
                TargetNullValue = this.TargetNullValue,
                BindingGroupName = this.BindingGroupName,
                Delay = this.Delay,
                ValidatesOnExceptions = this.ValidatesOnExceptions,
                UpdateSourceExceptionFilter = this.UpdateSourceExceptionFilter,
                ConverterCulture = this.ConverterCulture,
                ConverterParameter = this.ConverterParameter,
                Converter = this.Converter,
                ValidatesOnDataErrors = this.ValidatesOnDataErrors,
                NotifyOnValidationError = this.NotifyOnValidationError,
                NotifyOnSourceUpdated = this.NotifyOnSourceUpdated,
                UpdateSourceTrigger = this.UpdateSourceTrigger,
                Mode = modeToSet,
                NotifyOnTargetUpdated = this.NotifyOnTargetUpdated,
                ValidatesOnNotifyDataErrors = this.ValidatesOnNotifyDataErrors
            };

            if (multibinding.ValidationRules != null)
                foreach (var rule in this.ValidationRules)
                    multibinding.ValidationRules.Add(rule);
            if (multibinding.Bindings != null)
                foreach (var binding in this.Bindings)
                    multibinding.Bindings.Add(binding);

            return multibinding;
        }

        #region IAddChild implementation
        /// <summary>
        /// Appends the supplied object to the end of the array.
        /// </summary>
        /// <param name="value">The value to be appended.</param>
        public void AddChild(object value)
        {
            if (value is BindingBase binding)
                Bindings.Add(binding);
        }

        /// <summary>
        /// Adds the text content of a node to the object.
        /// </summary>
        /// <param name="text">The text content to be added.</param>
        public void AddText(string text)
        {
            // Reproduced code of <see cref="MultiBinding"/> here:
            if (text != null)
                for (int i = 0; i < text.Length; i++)
                    if (!Char.IsWhiteSpace(text[i]))
                        throw new ArgumentException("Non-empty string", text);
        }
        #endregion

        #region Multibinding properties, reproduced here to be directly transmitted at multibinding creation
        /// <summary>
        /// Gets or sets the value to use when the binding is unable to return a value.
        /// </summary>
        public object FallbackValue { get; set; } = DependencyProperty.UnsetValue;
        /// <summary>
        /// Gets or sets a string that specifies how to format the binding if it displays the bound value as a string.
        /// </summary>
        [DefaultValue(null)]
        public string StringFormat { get; set; }
        /// <summary>
        /// Gets or sets the value that is used in the target when the value of the source is null.
        /// </summary>
        public object TargetNullValue { get; set; } = DependencyProperty.UnsetValue;
        /// <summary>
        ///  Gets or sets the name of the BindingGroup to which this binding belongs.
        /// </summary>
        [DefaultValue("")]
        public string BindingGroupName { get; set; } = "";
        /// <summary>
        /// Gets or sets the amount of time, in milliseconds, to wait before updating the binding source after the value on the target changes.
        /// </summary>
        [DefaultValue(0)]
        public int Delay { get; set; }
        /// <summary>
        /// Gets or sets a value that indicates whether to include the ExceptionValidationRule.
        /// </summary>
        [DefaultValue(false)]
        public bool ValidatesOnExceptions { get; set; }
        /// <summary>
        /// Gets or sets a handler you can use to provide custom logic for handling exceptions that 
        /// the binding engine encounters during the update of the binding source value. This is only 
        /// applicable if you have associated the ExceptionValidationRule with your MultiBinding object.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public UpdateSourceExceptionFilterCallback UpdateSourceExceptionFilter { get; set; }
        /// <summary>
        /// Gets the collection of ValidationRule objects for this instance of MultiBinding.
        /// </summary>
        public Collection<ValidationRule> ValidationRules { get; }
        /// <summary>
        /// Gets or sets the CultureInfo object that applies to any converter assigned to bindings wrapped by the MultiBinding or on the MultiBinding itself.
        /// </summary>
        [DefaultValue(null)]
        [TypeConverter(typeof(CultureInfoIetfLanguageTagConverter))]
        public CultureInfo ConverterCulture { get; set; }
        /// <summary>
        /// Gets or sets an optional parameter to pass to a converter as additional information.
        /// </summary>
        [DefaultValue(null)]
        public object ConverterParameter { get; set; }
        /// <summary>
        /// Gets or sets the converter to use to convert the source values to or from the target value.
        /// </summary>
        [DefaultValue(null)]
        public IMultiValueConverter Converter { get; set; }
        /// <summary>
        /// Gets or sets a value that indicates whether to include the DataErrorValidationRule.
        /// </summary>
        [DefaultValue(false)]
        public bool ValidatesOnDataErrors { get; set; }
        /// <summary>
        /// Gets or sets a value that indicates whether to raise the Error attached event on the bound element.
        /// </summary>
        [DefaultValue(false)]
        public bool NotifyOnValidationError { get; set; }
        /// <summary>
        /// Gets or sets a value that indicates whether to raise the SourceUpdated event when a value is transferred from the binding target to the binding source.
        /// </summary>
        [DefaultValue(false)]
        public bool NotifyOnSourceUpdated { get; set; }
        /// <summary>
        /// Gets or sets a value that determines the timing of binding source updates.
        /// </summary>
        [DefaultValue(UpdateSourceTrigger.PropertyChanged)]
        public UpdateSourceTrigger UpdateSourceTrigger { get; set; } = UpdateSourceTrigger.PropertyChanged;
        /// <summary>
        /// Gets or sets a value that indicates the direction of the data flow of this binding.
        /// </summary>
        [DefaultValue(BindingMode.Default)]
        public BindingMode Mode { get; set; } = BindingMode.Default;
        /// <summary>
        /// Gets the collection of Binding objects within this MultiBinding instance.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public Collection<BindingBase> Bindings { get; }
        /// <summary>
        /// Gets or sets a value that indicates whether to raise the TargetUpdated event when a value is transferred from the binding source to the binding target.
        /// </summary>
        [DefaultValue(false)]
        public bool NotifyOnTargetUpdated { get; set; }
        /// <summary>
        /// Gets or sets a value that indicates whether to include the NotifyDataErrorValidationRule.
        /// </summary>
        [DefaultValue(true)]
        public bool ValidatesOnNotifyDataErrors { get; set; } = true;
        #endregion
    }
}
