using System;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows;
using System.Xaml;

namespace EMA.ExtendedWPFMarkupExtensions
{
    /// <summary>
    /// Markup extension for bindings with a dynamic resource source.
    /// </summary>
    /// <remarks>Partly inspired from: https://stackoverflow.com/questions/33816511/how-can-you-bind-to-a-dynamicresource-so-you-can-use-a-converter-or-stringformat </remarks>
    [MarkupExtensionReturnType(typeof(Binding))]
    public class DynamicResourceBindingExtension : SingleBindingExtension
    {
        /// <summary>
        /// The resource key of the binding.
        /// </summary>
        public object ResourceKey { get; set; }

        /// <summary>
        /// Stores the binding proxy that carries dynamic resource binding.
        /// </summary>
        private BindingProxy ProxyBinder { get; set; }

        /// <summary>
        /// Initiates a new instance of <see cref="DynamicResourceBindingExtension"/>.
        /// </summary>
        public DynamicResourceBindingExtension() : base()
        {   }

        /// <summary>
        /// Initiates a new instance of <see cref="DynamicResourceBindingExtension"/>.
        /// </summary>
        /// <param name="path">The property path to be set in the binding.</param>
        public DynamicResourceBindingExtension(PropertyPath path) : base(path)
        {   }

        /// <summary>
        /// Initiates a new instance of <see cref="DynamicResourceBindingExtension"/>.
        /// </summary>
        /// <param name="resourceKey">The dynamic resource key to look at.</param>
        public DynamicResourceBindingExtension(object resourceKey) : base()
        {
            ResourceKey = resourceKey ?? throw new ArgumentNullException(nameof(resourceKey));
        }
        
        /// <summary>
        /// Provides a values to be used by the framework to set a given target's object target property binding.
        /// </summary>
        /// <param name="serviceProvider">Service provider offered by the framework.</param>
        /// <returns>A <see cref="Binding"/> for the targeted object and property.</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // Build context:
            var pvt = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            var targetObject = pvt?.TargetObject != null ? pvt.TargetObject as DependencyObject : null;
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

            var solvedTarget = pvt?.TargetObject ?? null;

            if (solvedTarget == null || !(solvedTarget is DependencyObject))
            {
                // Try to reach root element in xaml namescope to later setup our dynamic resource:
                var xnr = serviceProvider.GetService(typeof(IRootObjectProvider)) as IRootObjectProvider;
                if (xnr != null)
                    solvedTarget = xnr.RootObject;
            }

            // Build what will be our proxy binder:
            var dynamicResource = new DynamicResourceExtension(ResourceKey);
            ProxyBinder = new BindingProxy(dynamicResource.ProvideValue(null));

            // Override base properties to setup the proxy binder as source:
            base.Source = ProxyBinder;
            base.Path = new PropertyPath(BindingProxy.DataProperty);
            base.Mode = BindingMode.OneWay;

            // Let base create binding then return result:
            var result = base.ProvideValue(serviceProvider);

            // Now our binding proxy must be set in the resource dictionary of our
            // target object to be in its scope:
            // Check if binding source was not resolved, in which case postpone processing
            // when target element is loaded:
            if (solvedTarget is FrameworkElement solvedTargetAsFE)
            {
                if (!solvedTargetAsFE.IsLoaded)
                    solvedTargetAsFE.Loaded += Target_Loaded;
                else if (!solvedTargetAsFE.Resources.Contains(ProxyBinder))
                    solvedTargetAsFE.Resources[ProxyBinder] = ProxyBinder;
            }

            return result;  // return the result.
        }


        /// <summary>
        /// Occurs when the target object is loaded and thus visual tree is constructed. 
        /// Try to find binding source if not resolved at construction.
        /// </summary>
        /// <param name="sender">The framework element that was loaded.</param>
        /// <param name="e">Information about the event.</param>
        private void Target_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement casted)
            {
                casted.Loaded -= Target_Loaded;  // unsubscribe.

                // Add binding proxy in the object resources:
                if (!casted.Resources.Contains(ProxyBinder))
                    casted.Resources[ProxyBinder] = ProxyBinder;

                // Unset unresolved as now processed:
                UnresolvedSource = (null, null);
            }
        }
    }
}
