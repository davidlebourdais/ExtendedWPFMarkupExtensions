# Extended WPF Markup Extensions

This repo contains a set of XAML Markup extensions to be used with the WPF framework. For now, there are only a few of them related to binding extensions.

## SingleBinding and/or MultiBinding ICollectionChanged extensions

**Attention:** Cannot be used in style and templates. Bypass the framework on this points is tricky as BindingBase classes cannot be derived and WPF does not handle other classes than BindingBase (or DynamicResources) on CLR properties. Since this is what are Setters, Triggers, and Conditions, we are stuck on this specific point.

### TypeFilteredBinding
Set binding only if the source object matches a specific predefined type value.

### ICollectionChangedBinding
When used, offers binding notifications when a source collection is updated. Works as single Binding and MultiBinding. In the latter case, any update of a bound source Binding item will trigger the udpate.

ActionsToNotify is a combinable NotifyCollectionChangedAction property value (set to all by default) that will be assesses to determined if the collection change can lead to the whole binding notification.


Note: does not support PriorityBindings, and bindings in Setters and Triggers.