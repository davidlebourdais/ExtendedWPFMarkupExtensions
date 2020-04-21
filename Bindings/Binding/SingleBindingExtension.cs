﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using EMA.ExtendedWPFMarkupExtensions.Utils;

namespace EMA.ExtendedWPFMarkupExtensions
{
    /// <summary>
    /// Markup extension base to build custom bindings upon.
    /// </summary>
    /// <remarks> Useful as the binding class is more or less sealed in the framework. We still 
    /// use and return a binding but we can manipulate its properties before being set.</remarks>
    [MarkupExtensionReturnType(typeof(BindingExpression))]
    public abstract class SingleBindingExtension : MarkupExtension
    {
        private WeakReference generatedBinding;  // stores the generated binding as a weak reference.

        /// <summary>
        /// Stores an empty binding to be used as default value in some use cases.
        /// </summary>
        protected static Binding EmptyBinding { get; } = new Binding();

        /// <summary>
        /// Gets a value indicating if the extension context must persist
        /// after <see cref="ProvideValue(IServiceProvider)"/> is invoked.
        /// </summary>
        /// <remarks>To be overriden by derivating type.</remarks>
        protected virtual bool IsExtensionPersistent { get; } = false;

        /// <summary>
        /// Stores the <see cref="Binding"/> generated by this 
        /// extension as a weak reference.
        /// </summary>
        protected Binding GeneratedBinding => generatedBinding?.IsAlive == true ? generatedBinding.Target as Binding : null;

        /// <summary>
        /// Stores the target of the binding, if any existing.
        /// </summary>
        protected DependencyObject TargetObject { get; set; }

        /// <summary>
        /// Stores the target property of the binding, if any existing.
        /// </summary>
        protected DependencyProperty TargetProperty { get; set; }

        /// <summary>
        /// Initiates a new instance of <see cref="SingleBindingExtension"/>.
        /// </summary>
        protected SingleBindingExtension() : base()
        {
            ValidationRules = new Collection<ValidationRule>();
        }

        /// <summary>
        /// Initiates a new instance of <see cref="SingleBindingExtension"/>.
        /// </summary>
        /// <param name="path">The property path to be set in the binding.</param>
        protected SingleBindingExtension(PropertyPath path) : this()
        {
            Path = path;
        }

        /// <summary>
        /// Provides a values to be used by the framework to set a given target's object target property binding.
        /// </summary>
        /// <param name="serviceProvider">Service provider offered by the framework.</param>
        /// <returns>A <see cref="BindingExpression"/> for the targeted object and property.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // Build context:
            var pvt = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            var targetObject = pvt?.TargetObject != null ? pvt.TargetObject as DependencyObject : null;
            TargetObject = targetObject as DependencyObject;
            if (pvt?.TargetObject?.GetType().Name == "SharedDp")
                return pvt.TargetObject;  // template usage, see https://stackoverflow.com/questions/26877676/how-do-i-get-a-bindingexpression-from-a-binding-object.

            // In source of BindingBase classes, we should return the current instance (so this extension) as a result
            // when encountering a CLR property. However, framework would allow only BindingBase derivatives as a result,
            // and we cannot functionnaly derive from them (sealed + internal dependencies). 
            // Thus, throw exceptions to prevent developpers from using this extension in some specific cases:
            if (pvt?.TargetObject is SetterBase)  // we cannot get target for a CLI setter object
                throw new NotSupportedException(nameof(SingleBindingExtension) + " cannot be used with a " + nameof(SetterBase) + ".");
            else if (pvt?.TargetObject is TriggerBase)  // same for triggers
                throw new NotSupportedException(nameof(SingleBindingExtension) + " cannot be used with a " + nameof(TriggerBase) + ".");
            else if (pvt?.TargetObject is Condition)  // same for datatrigger conditions
                throw new NotSupportedException(nameof(SingleBindingExtension) + " cannot be used with a " + nameof(Condition) + ".");

            var targetProperty = TargetProperty = pvt?.TargetProperty != null ? pvt.TargetProperty as DependencyProperty : null;

            // Create binding:
            var binding = createBinding(serviceProvider);
            generatedBinding = new WeakReference(binding);

            // We might be in a multibinding or a template setter, just return current binding. 
            if (targetObject == null || targetProperty == null)
                return binding.ProvideValue(serviceProvider);

            if (targetObject is FrameworkElement targetAsFE && !targetAsFE.IsLoaded)
            {
                targetAsFE.Initialized += TargetAsFE_Initialized;
            }

            return binding.ProvideValue(serviceProvider); // binding.ProvideValue(serviceProvider);  // provide a framework binding.
        }

        /// <summary>
        /// Occurs when the binding target is initialized.
        /// </summary>
        /// <param name="sender">Should be the binding target as an initializable <see cref="FrameworkElement"/>.</param>
        /// <param name="e">Unused event info.</param>
        private void TargetAsFE_Initialized(object sender, EventArgs e)
        {
            if (sender is FrameworkElement senderAsFE)
            {
                senderAsFE.Initialized -= TargetAsFE_Initialized;
                OnTargetInitialized(senderAsFE);
                senderAsFE.Loaded += TargetAsFE_Loaded;
            }
        }

        /// <summary>
        /// Called when the target object is initialized.
        /// </summary>
        /// <param name="target">The binding target object.</param>
        protected virtual void OnTargetInitialized(FrameworkElement target)
        {   }

        /// <summary>
        /// Occurs when the binding target is loaded.
        /// </summary>
        /// <param name="sender">Should be the binding target as an loadable <see cref="FrameworkElement"/>.</param>
        /// <param name="e">Unused event info.</param>
        private void TargetAsFE_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement senderAsFE)
            {
                senderAsFE.Loaded -= TargetAsFE_Loaded;
                OnTargetLoaded(senderAsFE);
                if (IsExtensionPersistent)  // subscribe to unloaded event to make this extension persist.
                    senderAsFE.Unloaded += TargetAsFE_Unloaded;
            }
        }

        /// <summary>
        /// Called when the target object is loaded.
        /// </summary>
        /// <param name="target">The binding target object.</param>
        protected virtual void OnTargetLoaded(FrameworkElement target)
        {   }

        /// <summary>
        /// Invoked when the target object is unloaded.
        /// </summary>
        /// <param name="sender">Should be our target object.</param>
        /// <param name="e">Routed event information.</param>
        private void TargetAsFE_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement senderAsFE)
            {
                senderAsFE.Unloaded -= TargetAsFE_Unloaded;
                OnTargetUnloaded(senderAsFE);
            }
        }

        /// <summary>
        /// Called when the target object is unloaded.
        /// </summary>
        /// <param name="target">The binding target object.</param>
        protected virtual void OnTargetUnloaded(FrameworkElement target)
        {   }

        /// <summary>
        /// Effectively generates the binding expression.
        /// </summary>
        /// <param name="serviceProvider">Service provider given by the framework.</param>
        /// <returns>A new <see cref="Binding"/> object.</returns>
        protected Binding createBinding(IServiceProvider serviceProvider)
        {
            // Retrieve object over which the extension is applied
            var pvt = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            if (pvt == null) return EmptyBinding;

            var targetObject = pvt.TargetObject as DependencyObject;
            //if (targetObject == null) return EmptyBinding;  // continue even if target object is not set.

            var targetProperty = pvt.TargetProperty as DependencyProperty;
            //if (targetProperty == null) return EmptyBinding; // continue even if target property is not given.

            var modeToSet = this.Mode;

            // Correct mode if not compatible:
            if (modeToSet == BindingMode.TwoWay && targetProperty != null && targetObject != null)
            {
                var frameworktargetproperty = targetProperty.GetMetadata(targetObject.GetType()) as FrameworkPropertyMetadata;
                if (frameworktargetproperty != null && !frameworktargetproperty.BindsTwoWayByDefault)
                    modeToSet = BindingMode.OneWay;

                // Change also if dp is clearly set as readonly:
                if (targetProperty.ReadOnly)
                    modeToSet = BindingMode.OneWay;
            }

            // Create binding:
            var binding = new Binding()
            {
                FallbackValue = this.FallbackValue,
                StringFormat = this.StringFormat,
                TargetNullValue = this.TargetNullValue,
                BindingGroupName = this.BindingGroupName,
                Delay = this.Delay,
                UpdateSourceTrigger = this.UpdateSourceTrigger,
                NotifyOnSourceUpdated = this.NotifyOnSourceUpdated,
                NotifyOnValidationError = this.NotifyOnValidationError,
                Converter = this.Converter,
                ConverterParameter = this.ConverterParameter,
                ConverterCulture = this.ConverterCulture,
                IsAsync = this.IsAsync,
                XPath = this.XPath,
                ValidatesOnDataErrors = this.ValidatesOnDataErrors,
                ValidatesOnNotifyDataErrors = this.ValidatesOnNotifyDataErrors,
                BindsDirectlyToSource = this.BindsDirectlyToSource,
                ValidatesOnExceptions = this.ValidatesOnExceptions,
                Path = this.Path,
                UpdateSourceExceptionFilter = this.UpdateSourceExceptionFilter,

                Mode = modeToSet
            };
            if (binding.ValidationRules != null)
                foreach (var rule in this.ValidationRules)
                    binding.ValidationRules.Add(rule);

            // Bindings do not support having two elements among ElementName, RelativeSource and Source
            // to be set at the same time so might generate a normal exception from here.
            if (ElementName != null)
                binding.ElementName = ElementName;
            if (RelativeSource != null)
                binding.RelativeSource = RelativeSource;
            if (Source != null)
                binding.Source = Source;

            // We manualy set the datacontext here is none currently set:
            if (Source == null && RelativeSource == null
                && string.IsNullOrWhiteSpace(ElementName) && targetObject is FrameworkElement targetAsFE)
            {
                var datacontext = targetAsFE.GetValue(FrameworkElement.DataContextProperty);
                if (datacontext != null)
                    binding.Source = datacontext;
            }

            return binding;
        }

        /// <summary>
        /// Stores target object that could lead to a source source resolution
        /// when the binding source is unresolved.
        /// </summary>
        protected static (Binding, FrameworkElement) UnresolvedSource;

        /// <summary>
        /// Stores a target object for which the datacontext was detected to be 
        /// the source of our inner binding.
        /// </summary>
        protected static (Binding, FrameworkElement) DatacontextTarget;

        #region Binding source providers
        /// <summary>
        /// Gets the binding source object of a binding.
        /// </summary>
        /// <param name="binding">The binding that indicates the source.</param>
        /// <param name="serviceProvider">Service provider provided by the framework.</param>
        /// <returns>The source object that is described by the binding.</returns>
        protected object getBindingSource(Binding binding, IServiceProvider serviceProvider)
        {
            if (binding == null)
                return null;

            // Optionaly get the target object:
            var pvt = serviceProvider?.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            var targetObject = pvt?.TargetObject != null ? pvt.TargetObject as DependencyObject : null;

            // -- Case where source is explicitely provided: --
            if (binding.Source != null)
            {
                // Get target object as FE if provided:
                if (targetObject is FrameworkElement targetAsFE)
                {
                    // And if the binding source is the datacontext of the target object, then add 
                    // the object the to list of datacontexts to be followed:
                    if (targetAsFE.IsLoaded && targetAsFE.DataContext == binding.Source)
                    {
                        DatacontextTarget = (binding, targetAsFE);
                        return binding.Source;
                    }
                    // Else we cannot know now ,so report assessment when target will be loaded by setting
                    // the current binding as 'unresolved':
                    else if (!targetAsFE.IsLoaded)
                    {
                        UnresolvedSource = (binding, targetAsFE);
                    }
                }
                else
                    return binding.Source;
            }

            // -- Case where element name is provided, seek source in xaml: --
            else if (!string.IsNullOrWhiteSpace(binding.ElementName) && serviceProvider != null)
                return BindingHelper.getSourceFromElementName(binding.ElementName, serviceProvider);

            // -- All other case where we have a target to provide: --
            else if (targetObject != null)
                return getBindingSource(binding, targetObject);  // call the other method without service provider.

            return null;
        }

        /// <summary>
        /// Gets the binding source object of a binding.
        /// </summary>
        /// <param name="binding">The binding that indicates the source.</param>
        /// <param name="target">A target object where the source might be found.</param>
        /// <returns>The source object that is described by the binding.</returns>
        protected object getBindingSource(Binding binding, DependencyObject target)
        {
            if (binding == null)
                return null;

            var targetAsFE = target as FrameworkElement;

            // -- Case where source is explicitely provided: --
            if (binding.Source != null)
            {
                // If a target object is provided:
                if (targetAsFE != null)
                {
                    // And if the binding source is the datacontext of the target object, then add 
                    // the object the to list of datacontexts to be followed:
                    if (targetAsFE.IsLoaded && targetAsFE.DataContext == binding.Source)
                    {
                        DatacontextTarget = (binding, targetAsFE);
                        return binding.Source;
                    }
                    // Else we cannot know now so report assessment at target loading:
                    else if (!targetAsFE.IsLoaded)
                    {
                        UnresolvedSource = (binding, targetAsFE);
                    }
                }
                else
                    return binding.Source;
            }

            // -- In other cases that depends on datacontext or visual tree --
            else if (target != null)
            {
                // -- Case where relative source is provided: --
                if (binding.RelativeSource != null)
                {
                    var relative = BindingHelper.getSourceFromRelativeSource(binding.RelativeSource, target);
                    if (relative == null && targetAsFE != null && !targetAsFE.IsLoaded)
                        UnresolvedSource = (binding, targetAsFE);
                }

                // -- Case where no source is given at all: --
                else if (binding.Source == null && binding.RelativeSource == null
                    && string.IsNullOrWhiteSpace(binding.ElementName) && targetAsFE != null)
                {
                    if (targetAsFE.IsLoaded || targetAsFE.DataContext != null)
                    {
                        DatacontextTarget = (binding, targetAsFE);
                        return targetAsFE.DataContext;
                    }
                    else UnresolvedSource = (binding, targetAsFE);
                }
            }

            return null;
        }
        #endregion

        #region Binding source property value providers
        /// <summary>
        /// Gets the targeted bound property value from a given binding.
        /// </summary>
        /// <param name="binding">The binding that indicates the source.</param>
        /// <param name="serviceProvider">Service provider that should be offered by the framework.</param>
        /// <returns>The property value that is described by the binding.</returns>
        protected object getBindingSourcePropertyValue(Binding binding, IServiceProvider serviceProvider)
        {
            var sourceElement = getBindingSource(binding, serviceProvider);

            // Process source element to retrieve target property:
            if (sourceElement != null)
                return BindingHelper.GetBindingSourcePropertyValue(sourceElement, binding.Path);
            else
                return null;
        }

        /// <summary>
        /// Gets the current value of the binding targeted property.
        /// </summary>
        /// <param name="binding">The binding description indicating where to find the source and the value.</param>
        /// <param name="target">A target object where the source might be found.</param>
        /// <returns>The property value that is described by the binding.</returns>
        protected object getBindingSourcePropertyValue(Binding binding, FrameworkElement target)
        {
            var sourceElement = getBindingSource(binding, target);

            // Process source element to retrieve target property:
            if (sourceElement != null)
                return BindingHelper.GetBindingSourcePropertyValue(sourceElement, binding.Path);
            else
                return null;
        }
        #endregion

        #region Binding properties, reproduced here to be directly transmitted at binding creation
        /// <summary>
        /// Gets or sets the value to use when the binding is unable to return a value.
        /// </summary>
        public object FallbackValue { get; set; }
        /// <summary>
        /// Gets or sets a string that specifies how to format the binding if it displays the bound value as a string.
        /// </summary>
        [DefaultValue(null)]
        public string StringFormat { get; set; }
        /// <summary>
        /// Gets or sets the value that is used in the target when the value of the source is null.
        /// </summary>
        public object TargetNullValue { get; set; }
        /// <summary>
        ///  Gets or sets the name of the BindingGroup to which this binding belongs.
        /// </summary>
        [DefaultValue("")]
        public string BindingGroupName { get; set; }
        /// <summary>
        /// Gets or sets the amount of time, in milliseconds, to wait before updating the binding source after the value on the target changes.
        /// </summary>
        [DefaultValue(0)]
        public int Delay { get; set; }
        /// <summary>
        /// Gets or sets a value that determines the timing of binding source updates.
        /// </summary>
        [DefaultValue(UpdateSourceTrigger.Default)]
        public UpdateSourceTrigger UpdateSourceTrigger { get; set; }
        /// <summary>
        /// Gets or sets a value that indicates whether to raise the SourceUpdated event when a value is transferred from the binding target to the binding source.
        /// </summary>
        [DefaultValue(false)]
        public bool NotifyOnSourceUpdated { get; set; }
        /// <summary>
        /// Gets or sets a value that indicates whether to raise the TargetUpdated event when a value is transferred from the binding source to the binding target.
        /// </summary>
        [DefaultValue(false)]
        public bool NotifyOnTargetUpdated { get; set; }
        /// <summary>
        /// Gets or sets a value that indicates whether to raise the Error attached event on the bound object.
        /// </summary>
        [DefaultValue(false)]
        public bool NotifyOnValidationError { get; set; }
        /// <summary>
        /// Gets or sets the converter to use.
        /// </summary>
        [DefaultValue(null)]
        public IValueConverter Converter { get; set; }
        /// <summary>
        /// Gets or sets the parameter to pass to the Converter.
        /// </summary>
        [DefaultValue(null)]
        public object ConverterParameter { get; set; }
        /// <summary>
        /// Gets or sets the culture in which to evaluate the converter.
        /// </summary>
        [DefaultValue(null)]
        [TypeConverter(typeof(CultureInfoIetfLanguageTagConverter))]
        public CultureInfo ConverterCulture { get; set; }
        /// <summary>
        /// Gets or sets the object to use as the binding source.
        /// </summary>
        public object Source { get; set; }
        /// <summary>
        /// Gets or sets the binding source by specifying its location relative to the position of the binding target.
        /// </summary>
        [DefaultValue(null)]
        public RelativeSource RelativeSource { get; set; }
        /// <summary>
        /// Gets or sets the name of the element to use as the binding source object.
        /// </summary>
        public string ElementName { get; set; }
        /// <summary>
        /// Gets or sets a value that indicates whether the Binding should get and set values asynchronously.
        /// </summary>
        [DefaultValue(false)]
        public bool IsAsync { get; set; }
        /// <summary>
        /// Gets or sets opaque data passed to the asynchronous data dispatcher.
        /// </summary>
        [DefaultValue(null)]
        public object AsyncState { get; set; }
        /// <summary>
        /// Gets or sets a value that indicates the direction of the data flow in the binding.
        /// </summary>
        [DefaultValue(BindingMode.Default)]
        public BindingMode Mode { get; set; }
        /// <summary>
        /// Gets or sets an XPath query that returns the value on the XML binding source to use.
        /// </summary>
        [DefaultValue(null)]
        public string XPath { get; set; }
        /// <summary>
        /// Gets or sets a value that indicates whether to include the DataErrorValidationRule.
        /// </summary>
        [DefaultValue(false)]
        public bool ValidatesOnDataErrors { get; set; }
        /// <summary>
        /// Gets or sets a value that indicates whether to include the NotifyDataErrorValidationRule.
        /// </summary>
        [DefaultValue(true)]
        public bool ValidatesOnNotifyDataErrors { get; set; }
        /// <summary>
        /// Gets or sets a value that indicates whether to evaluate the Path relative to the data item or the DataSourceProvider object.
        /// </summary>
        [DefaultValue(false)]
        public bool BindsDirectlyToSource { get; set; }
        /// <summary>
        /// Gets or sets a value that indicates whether to include the ExceptionValidationRule.
        /// </summary>
        [DefaultValue(false)]
        public bool ValidatesOnExceptions { get; set; }
        /// <summary>
        /// Gets a collection of rules that check the validity of the user input.
        /// </summary>
        public Collection<ValidationRule> ValidationRules { get; }
        /// <summary>
        /// Gets or sets the path to the binding source property.
        /// </summary>
        [ConstructorArgument(nameof(Path))]
        public PropertyPath Path { get; set; }
        /// <summary>
        /// Gets or sets a handler you can use to provide custom logic for handling exceptions that the binding engine encounters during 
        /// the update of the binding source value. This is only applicable if you have associated an ExceptionValidationRule with your binding.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public UpdateSourceExceptionFilterCallback UpdateSourceExceptionFilter { get; set; }
        #endregion
    }
}
