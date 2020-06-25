using System;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows;
using System.ComponentModel;
using System.Collections.Specialized;

namespace EMA.ExtendedWPFMarkupExtensions
{
    /// <summary>
    /// Markup extension for binding of <see cref="INotifyCollectionChanged"/> objects. 
    /// Will fire <see cref="INotifyPropertyChanged"/> when the collection updates, allowing to 
    /// trigger a things like an <see cref="IValueConverter"/>.
    /// </summary>
    [MarkupExtensionReturnType(typeof(BindingExpression))]
    public class ICollectionChangedBindingExtension : SingleBindingExtension, IWeakEventListener
    {
        private bool initialized;           // indicates if source was resolved and processing is ready.
        private Action updateTargetAction;  // stores action to be called for target update.

        /// <summary>
        /// Gets or set the <see cref="NotifyCollectionChangedAction"/> the binding should 
        /// be alerted on. Multiple actions are possible (ex: Add|Remove). Default is set to all possible actions.
        /// </summary>
        public NotifyCollectionChangedAction ActionsToNotify { get; set; } =
              NotifyCollectionChangedAction.Add
            | NotifyCollectionChangedAction.Remove
            | NotifyCollectionChangedAction.Replace
            | NotifyCollectionChangedAction.Reset
            | NotifyCollectionChangedAction.Move;

        /// <summary>
        /// Gets a value indicating if the extension context must persist
        /// after <see cref="ProvideValue(IServiceProvider)"/> is invoked.
        /// </summary>
        /// <remarks>To be overriden by derivating type.</remarks>
        protected override bool IsExtensionPersistent { get; } = true;  // true here, we want to stay alive to keep listening to weak collection changed events while control is alive.

        /// <summary>
        /// Initiates a new instance of <see cref="ICollectionChangedBindingExtension"/>.
        /// </summary>
        public ICollectionChangedBindingExtension() : base()
        {   }

        /// <summary>
        /// Initiates a new instance of <see cref="ICollectionChangedBindingExtension"/>.
        /// </summary>
        /// <param name="path">The property path to be set in the binding.</param>
        public ICollectionChangedBindingExtension(PropertyPath path) : base(path)
        {   }

        /// <summary>
        /// Provides a values to be used by the framework to set a given target's object target property binding.
        /// </summary>
        /// <param name="serviceProvider">Service provider offered by the framework.</param>
        /// <returns>A <see cref="Binding"/> for the targeted object and property.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var result = base.ProvideValue(serviceProvider);

            // Try to init if providing a binding:
            if (result is BindingExpression)
                Initialize(GetInnerBindingSource(serviceProvider));

            return result;
        }

        /// <summary>
        /// Prepares this class to listen to source collection is any.
        /// </summary>
        private void Initialize()
        {
            if (initialized || InnerBinding == null) return;

            // Find source value here if not provided:
            Initialize(GetInnerBindingSource());
        }

        /// <summary>
        /// Called when the target object is loaded.
        /// </summary>
        /// <param name="target">The binding target object.</param>
        protected override void OnTargetLoaded(FrameworkElement target) => Initialize(); // retry class initialization at loaded.

        /// <summary>
        /// Prepares this class to listen to source collection is any.
        /// </summary>
        /// <param name="source">A source object to assess.</param>
        private void Initialize(object source)
        {
            // If source is resolved then set binding up:
            if (!initialized && source != null)
            {
                // Prepare an expression updater:
                var expression = BindingOperations.GetBindingExpression(TargetObject, TargetProperty);
                if (expression != null)
                    updateTargetAction = () => expression?.UpdateTarget();

                // Get source (should be ok now that visual tree is constructed):
                if (ResolveInnerBindingValue() is INotifyCollectionChanged collection)
                    CollectionChangedEventManager.AddListener(collection, this);  // add this as listener to update collection when it changes

                // Check if source is a datacontext, in which case subscribe to datacontext changed event:
                if (IsInnerSourceDatacontext)
                    (TargetObject as FrameworkElement).DataContextChanged += DatacontextTarget_DataContextChanged;

                initialized = true;
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
            if (sender is FrameworkElement casted)
                if (sender == TargetObject)
                {
                    if (ResolveInnerBindingValue() is INotifyCollectionChanged collection)
                        CollectionChangedEventManager.AddListener(collection, this);
                }
                else
                {
                    casted.DataContextChanged -= DatacontextTarget_DataContextChanged;
                }
        }

        /// <summary>
        /// Called whenever one of the inner collection changed.
        /// </summary>
        /// <param name="managerType">Type of the manager we subscribed to.</param>
        /// <param name="sender">The collection that sent the event.</param>
        /// <param name="e">Information about the event.</param>
        /// <returns>True if was able to perform the required operation.</returns>
        /// <remarks><see cref="IWeakEventListener"/> implementation.</remarks>
        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            if (TargetObject != null && TargetProperty != null)
            {
                if (managerType == typeof(CollectionChangedEventManager))
                {
                    // Update only if collection change event args matches the allowed actions for notification:
                    if (e is NotifyCollectionChangedEventArgs collectionargs && ActionsToNotify.HasFlag(collectionargs.Action))
                    {
                        // Update binding target when collection changed:
                        if (updateTargetAction != null)
                        {
                            if (Application.Current.Dispatcher.CheckAccess())
                                updateTargetAction.Invoke();
                            else Application.Current.Dispatcher.BeginInvoke(updateTargetAction);
                        }
                    }
                }
            }
            else if (sender is INotifyCollectionChanged collection)
            {
                CollectionChangedEventManager.RemoveListener(collection, this);  // our binding expression is not used anymore, we can shut listening down.
            }
            return true;  // always return true otherwise ugly exception happen in the framework core.
        }
    }
}
