using System.Windows;

namespace EMA.ExtendedWPFMarkupExtensions
{
    /// <summary>
    /// A class used to bind DynamicResources on objects that normaly do not allow them.
    /// </summary>
    /// <remarks>Adapted from: https://www.thomaslevesque.com/2011/03/21/wpf-how-to-bind-to-data-when-the-datacontext-is-not-inherited/
    /// </remarks>
    public class BindingProxy : Freezable
    {
        /// <summary>
        /// Initiates a new instance of <see cref="BindingProxy"/>.
        /// </summary>
        public BindingProxy()
        {   }

        /// <summary>
        /// Initiates a new instance of <see cref="BindingProxy"/>.
        /// </summary>
        /// <param name="value">The value to bind.</param>
        public BindingProxy(object value)
        {
            Data = value;
        }

        /// <summary>
        /// Creates a new instance of <see cref="BindingProxy"/>.
        /// </summary>
        /// <returns>A new instance of the class</returns>
        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }

        /// <summary>
        /// Gets or sets the data associated to this binding proxy.
        /// </summary>
        public object Data
        {
            get { return (object)GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }

        /// <summary>
        /// Registers <see cref="Data"/> as a dependency property.
        /// </summary>
        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new FrameworkPropertyMetadata(null));
    }
}
