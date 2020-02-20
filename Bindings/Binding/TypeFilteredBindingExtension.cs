using System;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows;
using System.Reflection;

namespace EMA.ExtendedWPFMarkupExtensions
{
    /// <summary>
    /// Markup extension for binding that would only work when 
    /// its source type matched a passed filtered type. 
    /// </summary>
    /// <remarks>This type of binding can be used to avoid 'property not found errors' when toggling types over a content control.</remarks>
    [MarkupExtensionReturnType(typeof(Binding))]
    public class TypeFilteredBindingExtension : SingleBindingExtension
    {
        /// <summary>
        /// Gets or sets the type of the binding source which will
        /// validate the binding.
        /// </summary>
        public Type FilteringType { get; set; }

        /// <summary>
        /// Gets a value indicating if the extension context must persist
        /// after <see cref="ProvideValue(IServiceProvider)"/> is invoked.
        /// </summary>
        protected override bool IsExtensionPersistent { get; } = true;

        /// <summary>
        /// Initiates a new instance of <see cref="TypeFilteredBindingExtension"/>.
        /// </summary>
        public TypeFilteredBindingExtension()
        { }

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

            // Get binding that was generated:
            if (GeneratedBindingExpression != null && GeneratedBindingExpression.IsAlive)
            {
                var expression = GeneratedBindingExpression.Target as BindingExpression;
                toggleBinding(getBindingSource(expression.ParentBinding, serviceProvider));

                // Check if binding was not resolved, in which case postpone processing when target element is loaded:
                if (UnresolvedSource.Item1 != null)
                    UnresolvedSource.Item2.Loaded += UnresolvedSource_Loaded;

                // Check if source is a datacontext, in which case subscribe to datacontext changed event:
                if (DatacontextTarget.Item1 != null)
                    DatacontextTarget.Item2.DataContextChanged += DatacontextTarget_DataContextChanged;
            }

            return result;
        }

        /// <summary>
        /// Occurs when the target object is loaded and thus visual tree is contructed. 
        /// Try to find binding source if not resolved at construction.
        /// </summary>
        /// <param name="sender">The framework element that was loaded.</param>
        /// <param name="e">Information about the event.</param>
        private void UnresolvedSource_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement casted)
            {
                casted.Loaded -= UnresolvedSource_Loaded;  // unsubscribe.

                // Applies only if extension is still alive:
                if (IsExtensionValid)
                {
                    var expression = GeneratedBindingExpression.Target as BindingExpression;
                    var binding = expression.ParentBinding;

                    if (binding != null && UnresolvedSource.Item2 == casted)  // should be unresolved.
                    {
                        // Get source (should be ok now that visual tree is constructed):
                        toggleBinding(getBindingSource(expression.ParentBinding, casted));
                    }

                    // Check if source is a datacontext, in which case subscribe to datacontext changed event:
                    if (DatacontextTarget.Item1 != null)
                        DatacontextTarget.Item2.DataContextChanged += DatacontextTarget_DataContextChanged;

                    // Unset unresolved as now processed:
                    UnresolvedSource = (null, null);
                }
            }
        }

        /// <summary>
        /// Called whenever the datacontext of one of the inner source binding changed.
        /// </summary>
        /// <param name="sender">A framework element for which the property changed.</param>
        /// <param name="e">Information about the datacontext changed event.</param>
        private void DatacontextTarget_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is FrameworkElement casted)
            {
                // Only if extension is still alive:
                if (IsExtensionValid)
                {
                    var expression = GeneratedBindingExpression.Target as BindingExpression;
                    var binding = expression.ParentBinding;

                    if (binding != null)
                        toggleBinding(getBindingSource(expression.ParentBinding, casted));
                }
                else
                {
                    casted.DataContextChanged -= DatacontextTarget_DataContextChanged;
                }
            }
        }

        /// <summary>
        /// Sets or unsets the inner binding regarding to the passed source element value.
        /// </summary>
        /// <param name="sourceElement">The binding source element that must be assessed.</param>
        private void toggleBinding(object sourceElement)
        {
            if (GeneratedBindingExpression.IsAlive)
            {
                var expression = GeneratedBindingExpression.Target as BindingExpression;
                var binding = expression?.ParentBinding;
                if (binding != null)
                {
                    // If has binding while should not have, clear binding:
                    if (BindingOperations.GetBindingBase(expression.Target, expression.TargetProperty) != null
                        && sourceElement?.GetType() != FilteringType && !sourceElement.GetType().GetTypeInfo().IsSubclassOf(FilteringType))
                        BindingOperations.ClearBinding(expression.Target, expression.TargetProperty);
                    // If has no binding while should have, set binding:
                    else if (BindingOperations.GetBindingBase(expression.Target, expression.TargetProperty) == null)
                        BindingOperations.SetBinding(expression.Target, expression.TargetProperty, binding);
                }
            }
        }
    }
}
