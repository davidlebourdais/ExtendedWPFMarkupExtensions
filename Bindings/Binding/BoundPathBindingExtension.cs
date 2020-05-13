using System;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows;
using EMA.ExtendedWPFMarkupExtensions.Utils;

namespace EMA.ExtendedWPFMarkupExtensions
{
    /// <summary>
    /// A binding that accepts its path to be bound and resolved through
    /// the <see cref="PathValueBinding"/> property.
    /// </summary>
    /// <remarks> Useful to bind to user defined property names on a type through CustomType descriptors.</remarks>
    [MarkupExtensionReturnType(typeof(Binding))]
    public class BoundPathBindingExtension : SingleBindingExtension
    {
        private bool providing_value;        // true when the ProvideValue method is executed.
        private bool initialized;            // set to true once parth value binding is properly used.
        private Binding selfPropertyBinding; // an identity binding (binding that returns target object's target property's value to self).
        private Binding boundPathBinding;    // core binding to be provided to target once path is resolved.
        private object boundPathSource;      // stores resolved source of the bound path binding.
        private DependencyPropertyWatcher<object> boundPathBindingWatcher; // watches updates of the bound path binding once resolved.
        private bool no_target_update;       // disables reentrancy when binding is applied in this class.

        /// <summary>
        /// Gets or sets the binding to be used for 
        /// the binding <see cref="Binding.Path"/> property.
        /// </summary>
        public Binding PathValueBinding { get; set; }

        /// <summary>
        /// Indicates if the path property must be overriden 
        /// or combined with resolved path value.
        /// </summary>
        public bool OverridePath { get; set; }
         
        /// <summary>
        /// Gets a value indicating if the extension context must persist
        /// after <see cref="ProvideValue(IServiceProvider)"/> is invoked.
        /// </summary>
        protected override bool IsExtensionPersistent { get; } = true;

        /// <summary>
        /// Initiates a new instance of <see cref="BoundPathBindingExtension"/>.
        /// </summary>
        public BoundPathBindingExtension()
        {   }

        /// <summary>
        /// Initiates a new instance of <see cref="BoundPathBindingExtension"/>.
        /// </summary>
        /// <param name="path">The property path to be set in the binding.</param>
        public BoundPathBindingExtension(PropertyPath path) : base(path)
        {   }

        /// <summary>
        /// Called when the instance if garbage collected.
        /// </summary>
        ~BoundPathBindingExtension()
        {
            StopTrackingPathBindingUpdates(); // dispose tracking resources.
        }

        /// <summary>
        /// Provides a values to be used by the framework to set a given target's object target property binding.
        /// </summary>
        /// <param name="serviceProvider">Service provider offered by the framework.</param>
        /// <returns>A <see cref="Binding"/> for the targeted object and property.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // Format will be: 
            // <MyDependencyObject MyProperty={BoundPathBinding PathValueBinding={Binding somePropertyProvider}} />

            // Build binding in base:
            var result = base.ProvideValue(serviceProvider);

            // 'Normal' binding is no path is provided:
            if (PathValueBinding == null)
                initialized = true;
            // Else try to setup binding from here (else will be redone at init and loading):
            else if (result is BindingExpression)
            {
                // As we won't return it as result to framework, it will be GCed. Keep a copy now:
                boundPathBinding = InnerBinding?.Clone();
                if (boundPathBinding != null)
                {
                    // Try to init now:
                    providing_value = true;
                    Initialize(serviceProvider);
                    providing_value = false;

                    // If success, then return bound path binding immediately:
                    if (initialized)
                        return boundPathBinding.ProvideValue(serviceProvider);
                    else // Else return empty binding:
                    {
                        // Build self binding which will be our "null" value to set:
                        selfPropertyBinding = BindingHelpers.GetIdentityBinding(TargetProperty);

                        return selfPropertyBinding.ProvideValue(serviceProvider);  // and return an empty binding while path is unresolved.
                    }
                }
                else initialized = true;
            }

            return result;
        }

        /// <summary>
        /// Called when the target object is initialized.
        /// </summary>
        /// <param name="target">The binding target object.</param>
        protected override void OnTargetInitialized(FrameworkElement target) => Initialize();

        /// <summary>
        /// Called when the target object is loaded.
        /// </summary>
        /// <param name="target">The binding target object.</param>
        protected override void OnTargetLoaded(FrameworkElement target) => Initialize();

        /// <summary>
        /// Tries to resolve path binding and to set whole binding accordingly.
        /// </summary>
        /// <param name="serviceProvider">Service provider given by the framework.</param>
        private void Initialize(IServiceProvider serviceProvider = null)
        {
            if (initialized) return;

            if (ResolveBoundPathBinding(serviceProvider))
            {
                if (!providing_value)  // set binding only if we are not called by the ProvideValue method.
                {
                    no_target_update = true;
                    BindingOperations.SetBinding(TargetObject, TargetProperty, boundPathBinding);
                    no_target_update = false;
                }

                initialized = true;
            }
            else if (!providing_value && TargetObject != null)  // if unresolved and target object is identified, try to clear property binding.
                if (BindingOperations.GetBindingBase(TargetObject, TargetProperty) != null)
                {
                    no_target_update = true;
                    BindingOperations.ClearBinding(TargetObject, TargetProperty); // may fail in datatemplates, but self binding is at least maintained.
                    no_target_update = false;
                }
        }

        /// <summary>
        /// Resolves path binding and prepares the inner custom binding with resolved path value.
        /// </summary>
        /// <param name="serviceProvider">Service provider given by the framework.</param>
        /// <returns>True if successfully prepared path binding, false if cannot resolve its value.</returns>
        private bool ResolveBoundPathBinding(IServiceProvider serviceProvider = null)
        {
            if (PathValueBinding == null) return false;

            // If source is not already resolved:
            if (boundPathSource == null)
            {
                // Try to get the source of value for the path value binding using base mechanisms:
                boundPathSource = serviceProvider != null ? BindingHelpers.GetBindingSource(PathValueBinding, serviceProvider, out bool source_is_resolved, out bool source_is_datacontext)
                    : BindingHelpers.GetBindingSource(PathValueBinding, TargetObject, out source_is_resolved, out source_is_datacontext);

                if (!source_is_resolved || boundPathSource == null) return false;

                // If source is datacontext then track it for future changes:
                if (source_is_datacontext)
                    (TargetObject as FrameworkElement).DataContextChanged += TargetObject_DataContextChanged;
            }

            // Try to resolve path binding value on the source: 
            var value = BindingHelpers.ResolvePathValue(boundPathSource, PathValueBinding.Path);

            // If not given a proper property path format:
            if (!(value is string) && !(value is PropertyPath))
            {
                StopTrackingPathBindingUpdates(); // as we did not resolved binding, stop tracking changes on current bind if any.
                return false;
            }

            // Update bound path binding by getting a copy of the precedent value:
            if (!providing_value)
            {
                boundPathBinding = boundPathBinding?.Clone();
                if (boundPathBinding == null) return false;
            }

            // Build new inner binding path based on resolved value + OverridePath property:
            if (value is string as_string)
            {
                if (!OverridePath && Path != null)
                    boundPathBinding.Path = new PropertyPath(Path.Path + "." + as_string);  // TODO: path parameters?
                else boundPathBinding.Path = new PropertyPath(as_string);
            }
            else if (value is PropertyPath asPropertyPath)
            {
                if (!OverridePath && Path != null)
                    boundPathBinding.Path = new PropertyPath(Path.Path + "." + asPropertyPath.Path, asPropertyPath.PathParameters);
                else boundPathBinding.Path = asPropertyPath;
            }

            // As we resolved binding, track any further changes:
            StartTrackingPathBindingUpdates();

            return true;
        }

        /// <summary>
        /// Occurs whenever the datacontext of the target object changes. 
        /// We subscribed to this event in case the source of the path value binding is 
        /// the datacontext of the target object.
        /// </summary>
        /// <param name="sender">Should be our target object.</param>
        /// <param name="e">Event information.</param>
        private void TargetObject_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender == TargetObject && !no_target_update)
            {
                no_target_update = true;
                StopTrackingPathBindingUpdates();
                (TargetObject as FrameworkElement).DataContextChanged -= TargetObject_DataContextChanged;
                boundPathSource = null;
                ClearBinding();
                if (ResolveBoundPathBinding())
                    BindingOperations.SetBinding(TargetObject, TargetProperty, boundPathBinding);
                no_target_update = false;
            }
        }

        /// <summary>
        /// Initiates tracking of the bound path binding value to update Path 
        /// when the binding updates the target object (this class).
        /// </summary>
        private void StartTrackingPathBindingUpdates()
        {
            if (boundPathBindingWatcher == null && boundPathSource != null)
            {
                boundPathBindingWatcher = new DependencyPropertyWatcher<object>(boundPathSource, PathValueBinding.Path);
                boundPathBindingWatcher.PropertyChanged += BoundPathBindingWatcher_PropertyChanged;
            }
        }

        /// <summary>
        /// Stops tracking path binding updates and release related resources.
        /// </summary>
        private void StopTrackingPathBindingUpdates()
        {
            if (boundPathBindingWatcher != null)
            {
                boundPathBindingWatcher.PropertyChanged -= BoundPathBindingWatcher_PropertyChanged;
                boundPathBindingWatcher.Dispose();
                boundPathBindingWatcher = null;
            }
        }

        /// <summary>
        /// Occures whenever the bound path binding value updates.
        /// </summary>
        /// <param name="sender">The property watcher that sent the event.</param>
        /// <param name="e">Event information.</param>
        private void BoundPathBindingWatcher_PropertyChanged(object sender, EventArgs e)
        {
            if (sender == boundPathBindingWatcher && !no_target_update)
            {
                no_target_update = true;

                // Update binding with new path value:
                if (ResolveBoundPathBinding())
                    BindingOperations.SetBinding(TargetObject, TargetProperty, boundPathBinding);
                // or clear new binding:
                else ClearBinding();

                no_target_update = false;
            }
        }

        /// <summary>
        /// Clears binding on the target object.
        /// </summary>
        private void ClearBinding()
        {
            if (TargetObject != null && TargetProperty != null)
            {
                if (BindingOperations.IsDataBound(TargetObject, TargetProperty))
                {
                    BindingOperations.ClearBinding(TargetObject, TargetProperty);
                    if (BindingOperations.IsDataBound(TargetObject, TargetProperty))  // clearing may fail happen when called from datatemplate
                        BindingOperations.SetBinding(TargetObject, TargetProperty, selfPropertyBinding);  // set set "null" value.
                }
            }
        }
    }
}
