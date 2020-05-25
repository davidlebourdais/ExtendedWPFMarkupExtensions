using System;
using System.Windows;
using System.Windows.Data;

namespace EMA.ExtendedWPFMarkupExtensions.Utils
{
    /// <summary>
    /// A watcher for dependency property changes.
    /// </summary>
    /// <typeparam name="T">Type of the object to watch.</typeparam>
    /// <remarks>From: https://www.engineeringsolutions.de/how-to-implement-a-dependencypropertywatcher/ </remarks>
    public class DependencyPropertyWatcher<T> : DependencyObject, IDisposable
    {
        /// <summary>
        /// Value of Value Property
        /// </summary>
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(object), typeof(DependencyPropertyWatcher<T>), new PropertyMetadata(null, OnPropertyChanged));
        /// <summary>
        /// Called when Property Changes.
        /// </summary>
        public event EventHandler PropertyChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyPropertyWatcher{T}"/> class.
        /// </summary>
        /// <param name="relativesource">Databind relative source info</param>
        /// <param name="propertyPath">Property path.</param>
        public DependencyPropertyWatcher(RelativeSource relativesource, PropertyPath propertyPath)
        {
            Source = relativesource;
            BindingOperations.SetBinding(this, ValueProperty, new Binding() { RelativeSource = relativesource, Path = propertyPath, Mode = BindingMode.OneWay });
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyPropertyWatcher{T}"/> class.
        /// </summary>
        /// <param name="relativesource">Databind relative source info</param>
        /// <param name="property_path">Path of Property</param>
        public DependencyPropertyWatcher(RelativeSource relativesource, string property_path) : this(relativesource, new PropertyPath(property_path)) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyPropertyWatcher{T}"/> class.
        /// </summary>
        /// <param name="source">Databind source object</param>
        /// <param name="propertyPath">Property path.</param>
        public DependencyPropertyWatcher(object source, PropertyPath propertyPath)
        {
            Source = source;
            BindingOperations.SetBinding(this, ValueProperty, new Binding() { Source = source, Path = propertyPath, Mode = BindingMode.OneWay });
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyPropertyWatcher{T}"/> class.
        /// </summary>
        /// <param name="source">Databind source object</param>
        /// <param name="property_path">Path of Property</param>
        public DependencyPropertyWatcher(object source, string property_path) : this(source, new PropertyPath(property_path)) { }

        /// <summary>
        /// Gets the source object used.
        /// </summary>
        public object Source { get; private set; }

        /// <summary>
        /// Gets the current Value.
        /// </summary>
        public T Value => (T)GetValue(ValueProperty);

        /// <summary>
        /// Gets the current Value.
        /// </summary>
        public string PropertyPath => BindingOperations.GetBindingExpression(this, ValueProperty).ResolvedSourcePropertyName; 

        /// <summary>
        /// Called when the Property is updated
        /// </summary>
        /// <param name="sender">Source</param>
        /// <param name="args">Args</param>
        public static void OnPropertyChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            var source = (DependencyPropertyWatcher<T>)sender;
            source.PropertyChanged?.Invoke(source, EventArgs.Empty);
        }

        /// <summary>
        /// Called when object is being disposed.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Called when object is being disposed.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                Application.Current.Dispatcher.Invoke(() => ClearValue(ValueProperty));
        }
    }
}
