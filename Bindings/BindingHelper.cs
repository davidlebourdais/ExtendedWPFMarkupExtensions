using System;
using System.Windows;
using System.Windows.Data;
using System.Xaml;
using EMA.ExtendedWPFVisualTreeHelper;

namespace EMA.ExtendedWPFMarkupExtensions.Utils
{
    /// <summary>
    /// Provides some common methods for binding processings.
    /// </summary>
    public static class BindingHelpers
    {
        /// <summary>
        /// Gets a binding source from a name defined in the XAML scope.
        /// </summary>
        /// <param name="element_name">The name of the element to find.</param>
        /// <param name="serviceProvider">A service provider from which a <see cref="IXamlNameResolver"/> can be retrieved.</param>
        /// <returns>The nammed source object if any found, null otherwise.</returns>
        public static object getSourceFromElementName(string element_name, IServiceProvider serviceProvider)
        {
            if (!string.IsNullOrWhiteSpace(element_name) && serviceProvider != null)
            {
                // Try to find element name in current xaml namescope:
                if (serviceProvider.GetService(typeof(IXamlNameResolver)) is IXamlNameResolver xnr)
                    return xnr.Resolve(element_name);
            }
            return null;
        }

        /// <summary>
        /// Gets a binding source as a <see cref="RelativeSource"/> of a <see cref="DependencyObject"/>.
        /// </summary>
        /// <param name="relativeSource"><see cref="RelativeSource"/> value for seeking.</param>
        /// <param name="root">Root reference object.</param>
        /// <returns>The source object that is relatated to the reference one, if any found, null otherwise.</returns>
        public static object getSourceFromRelativeSource(RelativeSource relativeSource, DependencyObject root)
        {
            if (relativeSource == null) return root;

            if (relativeSource.Mode == RelativeSourceMode.FindAncestor)
            {
                if (relativeSource.AncestorType != null)
                    return WPFVisualFinders.FindParentByTypeExtended(root, relativeSource.AncestorType);
                else if (relativeSource.AncestorLevel > 0)
                    return WPFVisualFinders.FindParentByLevel(root, relativeSource.AncestorLevel);
            }
            else if (relativeSource.Mode == RelativeSourceMode.Self)
            {
                return root;
            }
            else if (relativeSource.Mode == RelativeSourceMode.PreviousData)
            {
                //TODO.
                throw new NotSupportedException(nameof(RelativeSourceMode.PreviousData) + " is currently not supported.");
            }
            else if (relativeSource.Mode == RelativeSourceMode.TemplatedParent)
            {
                //TODO.
                throw new NotSupportedException(nameof(RelativeSourceMode.TemplatedParent) + " is currently not supported.");
            }
            return root;
        }

        /// <summary>
        /// Gets a binding source property value.
        /// </summary>
        /// <param name="source">A binding source object on which the property value will 
        /// be retrieved (passed property path must be resolvable on this object's type).</param>
        /// <param name="path">Path of the property where to find the value.</param>
        /// <returns>The value of the property on the source object if any, null otherwise.</returns>
        public static object GetBindingSourcePropertyValue(object source, PropertyPath path)
        {
            if (source == null) return null;

            // Create a dummy watcher to resolve binding and get source property value:
            using (var dummy = new DependencyPropertyWatcher<object>(source, path))
            {
                if (dummy != null)
                    return dummy.Value;
            }
            return null;
        }
    }
}
