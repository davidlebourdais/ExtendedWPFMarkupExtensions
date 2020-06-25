using System;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows;
using System.ComponentModel;
using System.Windows.Threading;
using EMA.ExtendedWPFMarkupExtensions.Utils;

namespace EMA.ExtendedWPFMarkupExtensions
{
    /// <summary>
    /// Markup extension for binding that updates its target after a certain delay. 
    /// </summary>
    /// <remarks>Provides delay from source to target through <see cref="DelayMS"/> which is the exact opposite of 
    /// <see cref="BindingBase.Delay"/> that should still be used to apply a binding from target to source.</remarks>
    [MarkupExtensionReturnType(typeof(Binding))]
    public class DelayBindingExtension : SingleBindingExtension
    {
        private bool initialized;                                       // indicates if initialization is over.
        private DispatcherTimer delayTimer;                             // the timer to count the delay applied on target property changes.
        private DependencyPropertyWatcher<object> bindingWatcher;       // an inner fake object that will read binding updates.
        private DependencyPropertyDescriptor bindingWatcherDescriptor;

        /// <summary>
        /// Gets or sets the dispatcher that will run the delay timer.
        /// </summary>
        /// <remarks>Defaults to application's one if existing.</remarks>
        public Dispatcher Dispatcher { get; set; } = Application.Current?.Dispatcher;

        /// <summary>
        /// Gets or sets the number of milliseconds after which a binding
        /// target update is really applied.
        /// </summary>
        public int DelayMS { get; set; }

        /// <summary>
        /// Gets a sets wether delay must be applied when source value matches
        /// a given condition (if set and condition is not matched, delay is not
        /// set and value is immediately updated).
        /// </summary>
        public bool HasDelayCondition { get; set; }

        /// <summary>
        /// Gets or set the condition that source property value must match to trigger a delayed target update.
        /// Used only if <see cref="HasDelayCondition"/> is set.
        /// </summary>
        public object DelayCondition { get; set; }

        /// <summary>
        /// Gets a value indicating if the extension context must persist
        /// after <see cref="ProvideValue(IServiceProvider)"/> is invoked.
        /// </summary>
        protected override bool IsExtensionPersistent { get; } = true;

        /// <summary>
        /// Initiates a new instance of <see cref="DelayBindingExtension"/>.
        /// </summary>
        public DelayBindingExtension()
        {   }

        /// <summary>
        /// Initiates a new instance of <see cref="DelayBindingExtension"/>.
        /// </summary>
        /// <param name="path">The property path to be set in the binding.</param>
        public DelayBindingExtension(PropertyPath path) : base(path)
        {   }

        /// <summary>
        /// Provides a values to be used by the framework to set a given target's object target property binding.
        /// </summary>
        /// <param name="serviceProvider">Service provider offered by the framework.</param>
        /// <returns>A <see cref="Binding"/> for the targeted object and property.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var result = base.ProvideValue(serviceProvider);

            // If we have a valid result:
            if (DelayMS > 0 && result is BindingExpression && TargetObject != null && TargetProperty != null 
                && InnerBinding.Mode != BindingMode.OneTime && InnerBinding.Mode != BindingMode.OneWayToSource)
            {
                // Reprepare binding but with a mode that won't update on every property changes:
                var previousMode = Mode = BindingMode.OneTime;
                Mode = BindingMode.OneTime;
                result = base.ProvideValue(serviceProvider);
                Mode = previousMode; 

                // Try init:
                Initialize(GetInnerBindingSource(serviceProvider));
            }
            // Else 'normal' binding if no delay is provided, some items are not valid or 
            // if binding mode does not match this class utility:
            else
                initialized = true;

            return result;  // return whatever binding result we constructed.
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
        /// Prepares this class to apply delays base on source property value changes.
        /// </summary>
        private void Initialize()
        {
            if (initialized) return;

            // Find source value here if not provided:
            var source = BindingHelpers.GetBindingSource(InnerBinding, TargetObject, out bool resolved, out bool _);
            if (resolved)
                Initialize(source);
        }

        /// <summary>
        /// Prepares this class to apply delays base on source property value changes.
        /// </summary>
        /// <param name="source">A source object to assess.</param>
        private void Initialize(object source)
        {
            if (initialized) return;

            // If source is resolved then set binding up:
            if (source != null)
            {
                // Build a fake dependency object to receive binding notifications:
                bindingWatcher = new DependencyPropertyWatcher<object>(source, InnerBinding.Path);

                bindingWatcherDescriptor = DependencyPropertyDescriptor.FromProperty(DependencyPropertyWatcher<object>.ValueProperty, typeof(DependencyPropertyWatcher<object>));
                bindingWatcherDescriptor.AddValueChanged(bindingWatcher, BindingWatcher_SourcePropertyChanged);

                initialized = true;

                // Check if source is a datacontext, in which case subscribe to datacontext changed event:
                if (IsInnerSourceDatacontext && TargetObject != null)
                    (TargetObject as FrameworkElement).DataContextChanged += DatacontextTarget_DataContextChanged;
            }
        }

        /// <summary>
        /// Called when the target object is unloaded.
        /// </summary>
        /// <param name="target">The binding target object.</param>
        protected override void OnTargetUnloaded(FrameworkElement target)
        {
            target.DataContextChanged -= DatacontextTarget_DataContextChanged;  // free up event handlers.
            delayTimer?.Stop();
            if (bindingWatcher != null)
            {
                bindingWatcherDescriptor.RemoveValueChanged(bindingWatcher, BindingWatcher_SourcePropertyChanged);
                bindingWatcher.Dispose();
                bindingWatcher = null;
            }         
        }

        /// <summary>
        /// Called whenever the datacontext of target object changed.
        /// </summary>
        /// <param name="sender">A framework element for which the property changed.</param>
        /// <param name="e">Information about the datacontext changed event.</param>
        private void DatacontextTarget_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (TargetObject == sender && TargetProperty != null)
            {
                if (bindingWatcher != null)
                {
                    bindingWatcherDescriptor.RemoveValueChanged(bindingWatcher, BindingWatcher_SourcePropertyChanged);
                    bindingWatcher.Dispose();
                }
                bindingWatcher = new DependencyPropertyWatcher<object>(BindingHelpers.GetBindingSource(InnerBinding, TargetObject, out bool _, out bool _), InnerBinding.Path);
                bindingWatcherDescriptor = bindingWatcherDescriptor = DependencyPropertyDescriptor.FromProperty(DependencyPropertyWatcher<object>.ValueProperty, typeof(DependencyPropertyWatcher<object>));
                bindingWatcherDescriptor.AddValueChanged(bindingWatcher, BindingWatcher_SourcePropertyChanged);
            }
            else if (sender is FrameworkElement casted)
                casted.DataContextChanged -= DatacontextTarget_DataContextChanged;
        }

        /// <summary>
        /// Triggered whenever the target property of the watcher is updated, meaning our source
        /// object is pushing a new value.
        /// </summary>
        /// <param name="sender">Should be our binding watcher object.</param>
        /// <param name="args">Property changed event information.</param>
        private void BindingWatcher_SourcePropertyChanged(object sender, EventArgs args)
        {
            // If should respect delay condition and this delay is not respected then 
            // immediately update value:
            if (HasDelayCondition && !Equals(DelayCondition, bindingWatcher?.Value))
            {
                OnDelayTimerEllapsed(null, null);
                return;
            }

            if (Dispatcher == null) return;  // note: won't update if not dispatcher is here to ensure delay.

            // Otherwise, apply delay before target update:
            if (delayTimer == null)
                delayTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(DelayMS), DispatcherPriority.DataBind, OnDelayTimerEllapsed, Dispatcher);
            delayTimer.Stop();
            delayTimer.Start();
        }

        /// <summary>
        /// Called when the delay timer is ellapsed.
        /// </summary>
        /// <param name="sender">Our delay timer.</param>
        /// <param name="args">Ellapsed timer event information.</param>
        private void OnDelayTimerEllapsed(object sender, EventArgs args)
        {
            delayTimer?.Stop();

            var expression = BindingOperations.GetBindingExpression(TargetObject, TargetProperty);
            if (expression != null)
            {
                if (Application.Current.Dispatcher.CheckAccess())
                    expression.UpdateTarget();
                else Application.Current.Dispatcher.BeginInvoke(new Action(() => expression.UpdateTarget()));
            }
        }
    }
}
