using System;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows;
using System.ComponentModel;
using System.Collections.Specialized;
using EMA.ExtendedWPFMarkupExtensions.Utils;

namespace EMA.ExtendedWPFMarkupExtensions
{
    /// <summary>
    /// Markup extension for binding of <see cref="INotifyCollectionChanged"/> objects. 
    /// Will fire <see cref="INotifyPropertyChanged"/> when the collection updates, allowing to 
    /// use trigger <see cref="IValueConverter"/> for instance.
    /// </summary>
    [MarkupExtensionReturnType(typeof(BindingExpression))]
    public class ICollectionChangedBindingExtension : SingleBindingExtension, IWeakEventListener
    {
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
        protected override bool IsExtensionPersistent { get; } = true;  // true here, we want to stay alive to keep listening to weak collection changed events.

        /// <summary>
        /// Initiates a new instance of <see cref="ICollectionChangedBindingExtension"/>.
        /// </summary>
        public ICollectionChangedBindingExtension() : base()
        { }

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

            // Get binding that was generated:
            if (GeneratedBindingExpression != null && GeneratedBindingExpression.IsAlive)
            {
                var expression = GeneratedBindingExpression.Target as BindingExpression;
                if (getBindingSourcePropertyValue(expression.ParentBinding, serviceProvider) is INotifyCollectionChanged collection)
                    CollectionChangedEventManager.AddListener(collection, this);  // add this as listener to update collection when it changes

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
                        if (getBindingSourcePropertyValue(binding, casted) is INotifyCollectionChanged collection)
                            CollectionChangedEventManager.AddListener(collection, this);  // add this as listener to update collection when it changes
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
                        if (BindingHelper.GetBindingSourcePropertyValue(casted.DataContext, binding.Path) is INotifyCollectionChanged collection)
                            CollectionChangedEventManager.AddListener(collection, this);
                }
                else
                {
                    casted.DataContextChanged -= DatacontextTarget_DataContextChanged;
                }
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
            if (IsExtensionValid)
            {
                if (managerType == typeof(CollectionChangedEventManager))
                {
                    // Update only if collection change event args matches the allowed actions for notification:
                    if (e is NotifyCollectionChangedEventArgs collectionargs && ActionsToNotify.HasFlag(collectionargs.Action))
                    {
                        // Update binding target when collection changed:
                        (GeneratedBindingExpression.Target as BindingExpression).UpdateTarget();
                        return true;
                    }
                    else return false;
                }
            }
            else if (sender is INotifyCollectionChanged collection)
            {
                CollectionChangedEventManager.RemoveListener(collection, this);  // our binding expression is not used anymore, we can shut listening down.
            }
            return false;
        }
    }
}
