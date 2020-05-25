using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Xaml;
using EMA.ExtendedWPFVisualTreeHelper;

namespace EMA.ExtendedWPFMarkupExtensions.Utils
{
    /// <summary>
    /// Provides some common methods for binding processings.
    /// </summary>
    public static class BindingHelpers
    {
        #region Value resolvers
        /// <summary>
        /// Resolves the value of a binding of a binding on a target object.
        /// </summary>
        /// <param name="binding">A binding description that targets one of the target object's properties.</param>
        /// <param name="target">A target object to resolve the binding against.</param>
        /// <returns>The bound value is any found and if binding was resolved, null otherwise.</returns>
        public static object ResolveBindingValue(Binding binding, DependencyObject target)
        {
            var sourceElement = GetBindingSource(binding, target, out bool is_resolved, out _);
            return is_resolved ? ResolvePathValue(sourceElement, binding.Path) : null;
        }

        /// <summary>
        /// Resolves the value of a binding of a binding on a target object.
        /// </summary>
        /// <param name="binding">A binding description that targets one of the target object's properties.</param>
        /// <param name="serviceProvider">Service provider provided by the framework.</param>
        /// <returns>The bound value is any found and if binding was resolved, null otherwise.</returns>
        public static object ResolveBindingValue(Binding binding, IServiceProvider serviceProvider)
        {
            var sourceElement = GetBindingSource(binding, serviceProvider, out bool is_resolved, out _);
            return is_resolved ? ResolvePathValue(sourceElement, binding.Path) : null;
        }

        /// <summary>
        /// Retrieves the value pointed by a path on a source object.
        /// </summary>
        /// <param name="source">A source object on which the property value will 
        /// be retrieved (passed property path must be resolvable on this object's type).</param>
        /// <param name="path">Path of the property where to find the value.</param>
        /// <returns>The value of the property on the source object if any, null otherwise.</returns>
        public static object ResolvePathValue(object source, PropertyPath path)
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
        #endregion

        #region Source resolvers
        /// <summary>
        /// Gets the source object of a binding.
        /// </summary>
        /// <param name="binding">The binding that indicates the source.</param>
        /// <param name="serviceProvider">Service provider provided by the framework.</param>
        /// <param name="source_is_resolved">Indicates if the returned object value exists, so that returned null value is the effective value.</param>
        /// <param name="source_is_datacontext">Indicates if returned source object if the datacontext of the targeted object.</param>
        /// <returns>The source object that is described by the binding.</returns>
        public static object GetBindingSource(Binding binding, IServiceProvider serviceProvider, out bool source_is_resolved, out bool source_is_datacontext)
        {
            source_is_resolved = true;  // will be set to false later when unresolved.
            source_is_datacontext = false;

            if (binding == null)
            {
                source_is_resolved = false;
                return null;
            }

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
                        source_is_datacontext = true;
                        return binding.Source;
                    }
                    // Else we cannot know now ,so report assessment when target will be loaded by setting
                    // the current binding as 'unresolved':
                    else if (!targetAsFE.IsLoaded)
                    {
                        source_is_resolved = false;
                    }
                }
                else
                    return binding.Source;
            }

            // -- Case where element name is provided, seek source in xaml: --
            else if (!string.IsNullOrWhiteSpace(binding.ElementName) && serviceProvider != null)
                return GetSourceFromElementName(binding.ElementName, serviceProvider);

            // -- All other case where we have a target to provide: --
            else if (targetObject != null)
                return GetBindingSource(binding, targetObject, out source_is_resolved, out source_is_datacontext);  // call other method without service provider.

            return null;
        }

        /// <summary>
        /// Gets the source object of a binding.
        /// </summary>
        /// <param name="binding">The binding that indicates the source.</param>
        /// <param name="target">A target object where the source might be found.</param>
        /// <param name="source_is_resolved">Indicates if the returned object value exists, so that returned null value is the effective value.</param>
        /// <param name="source_is_datacontext">Indicates if returned source object if the datacontext of the targeted object.</param>
        /// <returns>The source object that is described by the binding.</returns>
        public static object GetBindingSource(Binding binding, DependencyObject target, out bool source_is_resolved, out bool source_is_datacontext)
        {
            source_is_resolved = true;  // will be set to false later when unresolved.
            source_is_datacontext = false;

            if (binding == null)
            {
                source_is_resolved = false;
                return null;
            }

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
                        source_is_datacontext = true;
                        return binding.Source;
                    }
                    // If taget is ready by still not same datacontext, then ok
                    // and return current value:
                    else if (targetAsFE.IsLoaded)
                    {
                        return binding.Source;
                    }
                    // Else we cannot know now so report assessment at target loading:
                    else if (!targetAsFE.IsLoaded)
                    {
                        source_is_resolved = false;
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
                        source_is_resolved = false;
                    else return relative;
                }

                // -- Case where no source is given at all: --
                else if (binding.Source == null && binding.RelativeSource == null
                    && string.IsNullOrWhiteSpace(binding.ElementName) && targetAsFE != null)
                {
                    if (targetAsFE.IsLoaded || targetAsFE.DataContext != null)
                    {
                        source_is_datacontext = true;
                        return targetAsFE.DataContext;
                    }
                    else source_is_resolved = false;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a binding source from a name defined in the XAML scope.
        /// </summary>
        /// <param name="element_name">The name of the element to find.</param>
        /// <param name="serviceProvider">A service provider from which a <see cref="IXamlNameResolver"/> can be retrieved.</param>
        /// <returns>The nammed source object if any found, null otherwise.</returns>
        internal static object GetSourceFromElementName(string element_name, IServiceProvider serviceProvider)
        {
            if (!string.IsNullOrWhiteSpace(element_name) && serviceProvider != null)
            {
                // Try to find element name in current xaml namescope:
                if (serviceProvider.GetService(typeof(IXamlNameResolver)) is IXamlNameResolver xnr)
                {
                    // Try using resolve:
                    if (xnr.Resolve(element_name) is object result)
                        return result;
                    else // try by tracking names:
                        foreach (var item in xnr.GetAllNamesAndValuesInScope())
                            if (item.Key == element_name)
                                return item.Value;

                    return null;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets a binding source as a <see cref="RelativeSource"/> of a <see cref="DependencyObject"/>.
        /// </summary>
        /// <param name="relativeSource"><see cref="RelativeSource"/> value for seeking.</param>
        /// <param name="root">Root reference object.</param>
        /// <returns>The source object that is relatated to the reference one, if any found, null otherwise.</returns>
        internal static object GetSourceFromRelativeSource(RelativeSource relativeSource, DependencyObject root)
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
        #endregion

        #region Identity bindings
        /// <summary>
        /// Builds a binding towards self to maintain a value over a property.
        /// </summary>
        /// <param name="targetProperty">Optional dependency property. Identity binding content could be more adapted regarding to passed value.</param>
        /// <returns>A binding that returns a value mimicking the target property value.</returns>
        public static Binding GetIdentityBinding(DependencyProperty targetProperty = null)
        {

            if (targetProperty == FrameworkElement.DataContextProperty)
                return new Binding()
                {
                    Path = new PropertyPath(FrameworkElement.DataContextProperty),
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(FrameworkElement), 2)
                };
            else 
                return new Binding()
                {
                    Path = new PropertyPath(targetProperty.Name),
                    RelativeSource = new RelativeSource(RelativeSourceMode.Self)
                };
        }
        #endregion
    }
}
