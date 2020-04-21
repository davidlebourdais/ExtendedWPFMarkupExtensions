using System;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows;
using System.Reflection;

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
        private bool initialized;   // indicates if an instance of this class has been initialized.

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

            if (result is BindingExpression) // process only if a binding was issued.
                initialize(getBindingSource(GeneratedBinding, serviceProvider));

            return result;
        }

        /// <summary>
        /// Called when the target object is initialized.
        /// </summary>
        /// <param name="target">The binding target object.</param>
        protected override void OnTargetInitialized(FrameworkElement target)
        {
            if (target == null) return;

            // Retry class initialization at framework element init:
            if (!initialized)
                initialize(getBindingSource(GeneratedBinding, TargetObject));
        }

        /// <summary>
        /// Called when the target object is loaded.
        /// </summary>
        /// <param name="target">The binding target object.</param>
        protected override void OnTargetLoaded(FrameworkElement target)
        {
            if (target == null) return;

            // Retry initialization at loaded:
            if (!initialized && GeneratedBinding != null)
                initialize(getBindingSource(GeneratedBinding, TargetObject), true);
        }

        /// <summary>
        /// Prepares this class to filter data based on resolved (or unresolved source).
        /// </summary>
        /// <param name="source">The source object to assess.</param>
        /// <param name="loading">Indicates if the functin is called in the loading context of the target.</param>
        private void initialize(object source, bool loading = false)
        {
            if (initialized) return;

            // If source is resolved then set binding up:
            if (source != null)
            {
                toggleBinding(source);
                initialized = true;

                // Check if source is a datacontext, in which case subscribe to datacontext changed event:
                if (DatacontextTarget.Item1 != null)
                    DatacontextTarget.Item2.DataContextChanged += DatacontextTarget_DataContextChanged;
            }
            // else clear current binding since we cannot know if source type is correct:
            else if (TargetObject != null)  // but only if target object is identified.
            {
                // Clear current binding while source isn't resolved, since it could be of another
                // type as the accepted one:
                if (BindingOperations.GetBindingBase(TargetObject, TargetProperty) != null)
                    BindingOperations.ClearBinding(TargetObject, TargetProperty);
            }
        }

        /// <summary>
        /// Called when the target object is unloaded.
        /// </summary>
        /// <param name="target">The binding target object.</param>
        protected override void OnTargetUnloaded(FrameworkElement target)
        {
            // Free up event handlers:
            target.DataContextChanged -= DatacontextTarget_DataContextChanged;
        }

        /// <summary>
        /// Called whenever the datacontext of one of the inner source binding changed.
        /// </summary>
        /// <param name="sender">A framework element for which the property changed.</param>
        /// <param name="e">Information about the datacontext changed event.</param>
        private void DatacontextTarget_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (TargetObject == sender && TargetProperty != null)
                toggleBinding(getBindingSource(GeneratedBinding, TargetObject));
            else if (sender is FrameworkElement casted)
                casted.DataContextChanged -= DatacontextTarget_DataContextChanged;
        }

        /// <summary>
        /// Sets or unsets the inner binding regarding to the passed source element value.
        /// </summary>
        /// <param name="sourceElement">The binding source element that must be assessed.</param>
        private void toggleBinding(object sourceElement)
        {
            if (TargetObject != null && TargetProperty != null)
            {
                // If has binding while should not have, clear binding:
                if (BindingOperations.GetBindingBase(TargetObject, TargetProperty) != null && (sourceElement == null
                    || sourceElement?.GetType() != SourceType && !sourceElement.GetType().GetTypeInfo().IsSubclassOf(SourceType)))
                {
                    BindingOperations.ClearBinding(TargetObject, TargetProperty);
                }
                // If has no binding while should have, set binding:
                else if (sourceElement != null 
                        && BindingOperations.GetBindingBase(TargetObject, TargetProperty) == null 
                        && (sourceElement?.GetType() == SourceType || sourceElement.GetType().GetTypeInfo().IsSubclassOf(SourceType)))
                {
                    BindingOperations.SetBinding(TargetObject, TargetProperty, GeneratedBinding);
                }
            }
        }
    }
}
