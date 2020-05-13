using System;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows;
using System.Reflection;
using EMA.ExtendedWPFMarkupExtensions.Utils;

namespace EMA.ExtendedWPFMarkupExtensions
{
    /// <summary>
    /// Markup extension for binding that would only work when 
    /// its source type matches a parameterized source type value. 
    /// </summary>
    /// <remarks>This type of binding can be used to avoid 'property not found errors' when toggling typest
    ///  over a content control or when the datacontext naturally changes during a control initialization.</remarks>
    [MarkupExtensionReturnType(typeof(Binding))]
    public class TypeFilteredBindingExtension : SingleBindingExtension
    {
        private bool initialized;            // indicates if initialization is over.
        private bool init_to_empty = true;   // indicates if, after very first initialization, returned provided value shall be empty or plain binding.
        private Binding innerBinding;        // Keeps a hard copy of the base inner binding;
        private Binding selfPropertyBinding; // an identity binding (binding that returns target object's target property's value to self).

        /// <summary>
        /// Gets or sets the type of the binding source which will
        /// validate the binding. Source must be of this type otherwise
        /// binding will not be set.
        /// </summary>
        public Type SourceType { get; set; }

        /// <summary>
        /// Gets a value indicating if the extension context must persist
        /// after <see cref="ProvideValue(IServiceProvider)"/> is invoked.
        /// </summary>
        protected override bool IsExtensionPersistent { get; } = true;

        /// <summary>
        /// Initiates a new instance of <see cref="TypeFilteredBindingExtension"/>.
        /// </summary>
        public TypeFilteredBindingExtension()
        {   }

        /// <summary>
        /// Initiates a new instance of <see cref="TypeFilteredBindingExtension"/>.
        /// </summary>
        /// <param name="path">The property path to be set in the binding.</param>
        public TypeFilteredBindingExtension(PropertyPath path) : base(path)
        {   }

        /// <summary>
        /// Provides a values to be used by the framework to set a given target's object target property binding.
        /// </summary>
        /// <param name="serviceProvider">Service provider offered by the framework.</param>
        /// <returns>A <see cref="Binding"/> for the targeted object and property.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var result = base.ProvideValue(serviceProvider);

            // 'Normal' binding is no filter source type is provided:
            if (SourceType == null)
                initialized = true;
            // else process only if a binding was issued and source type is not set:
            else if (result is BindingExpression)
            {
                // Build self binding which will be our "null" value to set:
                selfPropertyBinding = new Binding()
                {
                    Path = new PropertyPath(TargetProperty.Name),
                    RelativeSource = new RelativeSource(RelativeSourceMode.Self)
                };

                // Try init:
                Initialize(GetInnerBindingSource(serviceProvider));
                if (!initialized || init_to_empty)
                {
                    innerBinding = InnerBinding;  // keep a copy for later usage.
                    return selfPropertyBinding.ProvideValue(serviceProvider);  // return empty binding we do not know source type or if inited as it.
                }
            }

            return result;  // return if full binding is required, or SharedDp or whatever was issued.
        }

        /// <summary>
        /// Called when the target object is initialized.
        /// </summary>
        /// <param name="target">The binding target object.</param>
        protected override void OnTargetInitialized(FrameworkElement target) => Initialize(); // retry class initialization at framework element init.

        /// <summary>
        /// Called when the target object is loaded.
        /// </summary>
        /// <param name="target">The binding target object.</param>
        protected override void OnTargetLoaded(FrameworkElement target) => Initialize(); // retry class initialization at loaded.

        /// <summary>
        /// Prepares this class to filter data based on resolved (or unresolved source).
        /// </summary>
        private void Initialize()
        {
            if (initialized) return;

            // Find source value here if not provided:
            var source = BindingHelpers.GetBindingSource(innerBinding, TargetObject, out bool resolved, out bool _);
            if (resolved)
                Initialize(source);
        }

        /// <summary>
        /// Prepares this class to filter data based on resolved (or unresolved source).
        /// </summary>
        /// <param name="source">A source object to assess.</param>
        private void Initialize(object source)
        {
            if (initialized) return;

            // If source is resolved then set binding up:
            if (source != null)
            {
                ToggleBinding(source);
                initialized = true;

                // Check if source is a datacontext, in which case subscribe to datacontext changed event:
                if (IsInnerSourceDatacontext && TargetObject != null)
                    (TargetObject as FrameworkElement).DataContextChanged += DatacontextTarget_DataContextChanged;
            }
            // else clear current binding since we cannot know if source type is correct:
            else if (TargetObject != null)  // but only if target object is identified.
            {
                // Try to clear current binding while source isn't resolved, since it could be of another
                // type as the accepted one and to free the identity binding processing:
                if (BindingOperations.GetBindingBase(TargetObject, TargetProperty) != null)
                    BindingOperations.ClearBinding(TargetObject, TargetProperty);
            }
        }

        /// <summary>
        /// Called when the target object is unloaded.
        /// </summary>
        /// <param name="target">The binding target object.</param>
        protected override void OnTargetUnloaded(FrameworkElement target)
            => target.DataContextChanged -= DatacontextTarget_DataContextChanged;  // free up event handlers.

        /// <summary>
        /// Called whenever the datacontext of one of the inner source binding changed.
        /// </summary>
        /// <param name="sender">A framework element for which the property changed.</param>
        /// <param name="e">Information about the datacontext changed event.</param>
        private void DatacontextTarget_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (TargetObject == sender && TargetProperty != null)
                ToggleBinding(BindingHelpers.GetBindingSource(innerBinding, TargetObject, out bool _, out bool _));
            else if (sender is FrameworkElement casted)
                casted.DataContextChanged -= DatacontextTarget_DataContextChanged;
        }

        /// <summary>
        /// Sets or unsets the inner binding regarding to the passed source element value.
        /// </summary>
        /// <param name="sourceElement">The binding source element that must be assessed.</param>
        private void ToggleBinding(object sourceElement)
        {
            if (TargetObject != null && TargetProperty != null && SourceType != null)
            {
                // If has binding while should not have, clear binding:
                var currentBinding = BindingOperations.GetBindingBase(TargetObject, TargetProperty);
                if (currentBinding != null && (sourceElement == null || SourceType == null 
                    || sourceElement?.GetType() != SourceType && !sourceElement.GetType().GetTypeInfo().IsSubclassOf(SourceType)))
                {
                    BindingOperations.ClearBinding(TargetObject, TargetProperty);
                    if (BindingOperations.IsDataBound(TargetObject, TargetProperty))  // clearing may fail happen when called from datatemplate
                        BindingOperations.SetBinding(TargetObject, TargetProperty, selfPropertyBinding);  // set set "null" value.
                    init_to_empty = true;
                }
                // If has no binding while should have, set binding:
                else if (sourceElement != null && (currentBinding == null || ((currentBinding as Binding)?.IsEquivalentTo(selfPropertyBinding) == true))
                        && (sourceElement.GetType() == SourceType || sourceElement.GetType().GetTypeInfo().IsSubclassOf(SourceType)))
                {
                    BindingOperations.SetBinding(TargetObject, TargetProperty, innerBinding);
                    init_to_empty = false;
                }
            }
        }
    }
}
