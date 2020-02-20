using System;
using System.Windows.Markup;
using System.Windows.Data;
using System.Windows;

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
        { }

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
            // Get the binding source for all targets affected by this MarkupExtension
            // whether set directly on an element or object, or when applied via a style
            var dynamicResource = new DynamicResourceExtension(ResourceKey);
            ProxyBinder = new BindingProxy(dynamicResource.ProvideValue(null)); // Pass 'null' here

            // Override base properties to setup the proxy binder as source:
            base.Source = ProxyBinder;
            base.Path = new PropertyPath(BindingProxy.DataProperty);
            base.Mode = BindingMode.OneWay;

            // Let base create binding then return result:
            var result = base.ProvideValue(serviceProvider);

            return result;
        }
    }
}
