using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using EMA.ExtendedWPFMarkupExtensions.Utils;

namespace EMA.ExtendedWPFMarkupExtensions
{
    /// <summary>
    /// A multibinding that updates when a bound <see cref="INotifyCollectionChanged"/> 
    /// property source item show any (or predefined) update.
    /// </summary>
    [MarkupExtensionReturnType(typeof(MultiBindingExpression))]
    [ContentProperty(nameof(Bindings))]
    public class ICollectionChangedMultiBindingExtension : MultiBindingExtension, IWeakEventListener
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
        /// Stores every unresolved child bindings at startup.
        /// </summary>
        private Dictionary<Binding, FrameworkElement> UnresolvedBindings { get; } = new Dictionary<Binding, FrameworkElement>();

        /// <summary>
        /// Stores child bindings on which source is a datacontext. 
        /// </summary>
        private Dictionary<Binding, FrameworkElement> DataContextBindings { get; } = new Dictionary<Binding, FrameworkElement>();

        /// <summary>
        /// Initiates a new <see cref="ICollectionChangedMultiBindingExtension"/>.
        /// </summary>
        public ICollectionChangedMultiBindingExtension() : base()
        {   }

        /// <summary>
        /// Effectively generates the multibinding expression.
        /// </summary>
        /// <param name="serviceProvider">Service provider given by the framework.</param>
        /// <returns>A new <see cref="MultiBindingExpression"/> object.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var result = base.ProvideValue(serviceProvider);

            // Check each item property source:
            foreach (var item in Bindings)
                if (getBindingSourcePropertyValue(item as Binding, serviceProvider) is INotifyCollectionChanged collection)
                    CollectionChangedEventManager.AddListener(collection, this);  // add this as listener to update collection when it changes

            // For all target element related to an 'unresolved' binding during the 
            // previous method call (normaly only one), register to loaded event so we will
            // add the listener once the visual tree is constructed:
            foreach (var unresolvedtarget in UnresolvedBindings.Values.Distinct())
                unresolvedtarget.Loaded += Unresolvedtarget_Loaded; 

            foreach (var datacontextTarget in DataContextBindings.Values.Distinct())
                datacontextTarget.DataContextChanged += DatacontextTarget_DataContextChanged;

            return result;
        }

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
                        if (!DataContextBindings.ContainsKey(binding))
                            DataContextBindings.Add(binding, targetAsFE);
                        return binding.Source;
                    }
                    // Else we cannot know now ,so report assessment when target will be loaded by setting
                    // the current binding as 'unresolved':
                    else if (!targetAsFE.IsLoaded)
                    {
                        UnresolvedBindings.Add(binding, targetAsFE);
                    }
                }
                else
                    return binding.Source;
            }

            // -- Case where element name is provided, seek source in xaml: --
            else if (!string.IsNullOrWhiteSpace(binding.ElementName) && serviceProvider != null)
                return BindingHelpers.GetSourceFromElementName(binding.ElementName, serviceProvider);

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
                        if (!DataContextBindings.ContainsKey(binding))
                            DataContextBindings.Add(binding, targetAsFE);
                        return binding.Source;
                    }
                    // Else we cannot know now so report assessment at target loading:
                    else if (!targetAsFE.IsLoaded)
                    {
                        UnresolvedBindings.Add(binding, targetAsFE);
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
                    var relative = BindingHelpers.GetSourceFromRelativeSource(binding.RelativeSource, target);
                    if (relative == null && targetAsFE != null && !targetAsFE.IsLoaded)
                        if (!UnresolvedBindings.ContainsKey(binding))
                            UnresolvedBindings.Add(binding, targetAsFE);
                }

                // -- Case where no source is given at all: --
                else if (binding.Source == null && binding.RelativeSource == null
                    && string.IsNullOrWhiteSpace(binding.ElementName) && targetAsFE != null)
                {
                    if (targetAsFE.IsLoaded)
                    {
                        if (!DataContextBindings.ContainsKey(binding))
                            DataContextBindings.Add(binding, targetAsFE);
                        return targetAsFE.DataContext;
                    }
                    else if (!UnresolvedBindings.ContainsKey(binding))
                        UnresolvedBindings.Add(binding, targetAsFE);
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
                return BindingHelpers.ResolvePathValue(sourceElement, binding.Path);
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
                return BindingHelpers.ResolvePathValue(sourceElement, binding.Path);
            else
                return null;
        }
        #endregion

        /// <summary>
        /// Executed when the targeted framework element is loaded.
        /// </summary>
        /// <param name="sender">The framework element that sent the event.</param>
        /// <param name="e">Information about the loaded event.</param>
        private void Unresolvedtarget_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement casted)
            {
                casted.Loaded -= Unresolvedtarget_Loaded;  // unsubscribe.

                // Applies only if extension is still alive:
                if (IsExtensionValid)
                {
                    // Check for each unresolved binding:
                    foreach (var item in Bindings)
                    {
                        // If they lie in the unresolved list, and the FE that sent the event is registered for that purpose:
                        if (item is Binding binding && UnresolvedBindings.ContainsKey(binding) && UnresolvedBindings[binding] == casted)
                        {
                            // Get source (should be ok now that visual tree is constructed):
                            if (getBindingSourcePropertyValue(item as Binding, casted) is INotifyCollectionChanged collection)
                                CollectionChangedEventManager.AddListener(collection, this);  // add this as listener to update collection when it changes
                        }
                    }

                    // Get the opportunity to register to datacontext change event
                    // in case we just processed a value that was unresolved and now marked in the 
                    // list of the datacontext bindings to follow:
                    if (DataContextBindings.ContainsValue(casted))
                    {
                        foreach (Binding binding in Bindings)
                        {
                            if (UnresolvedBindings.ContainsKey(binding))  // was unresolved
                            {
                                if (DataContextBindings[binding] == casted)  // and is in the datacontext list to follow
                                    casted.DataContextChanged += DatacontextTarget_DataContextChanged;
                            }
                        }
                    }

                    // Since resolved, remove all related unresolved 
                    // bindings for the current framework element:
                    foreach (Binding binding in Bindings)
                    {
                        if (UnresolvedBindings.ContainsKey(binding) && UnresolvedBindings[binding] == casted)
                            UnresolvedBindings.Remove(binding);
                    }
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
                    // Loop through every bindings set on datacontext that we registered,
                    // and hook to collection change event for the new datacontext value:
                    foreach (var binding in DataContextBindings.Keys)
                        if (DataContextBindings[binding] == casted)
                            if (BindingHelpers.ResolvePathValue(casted.DataContext, binding.Path) is INotifyCollectionChanged collection)
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
                        (GeneratedMultibindingExpression.Target as MultiBindingExpression).UpdateTarget();
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
