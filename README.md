# Extended WPF Markup Extensions

This repo contains a set of XAML Markup extensions to be used with the WPF framework. They extend, in a way, capabilities of the framework by providing new easy-to-implement bindings. 

> **Warning:** These extensions are quite powerful for interface development. However they are not unit-tested so they must be integrated with a careful post-analysis.


## Reference

> **Important notice:** Bindings below cannot be used in styles and templates. Bypass of the framework with these items is tricky as BindingBase classes cannot be derived and WPF does not handle other classes than BindingBase (or DynamicResources) on CLR properties. Since this is what are Setters, Triggers, and Conditions, we are stuck on this specific point.
> 
### BoundPathBinding
A binding for which the *Path* property can be bound like a dependency property through the usage of the *PathValueBinding* property ().

*Path* is actually combined with *PathValueBinding* when *OverridePath* is set to false (default), otherwise it is discarded.

**Attention:** when binding to the *DataContext* property: inherited value is used as default to determine the *PathValueBinding* if no *Source*, *RelativeSource*, or *ElementName* is explicitly set. Note that inherited *DataContext* will be the default value as long the path is not resolved. In fact, and this is true for any usage: any other binding properties that you might set will not be used to modify the default value while path is not resolved. 

### DelayBinding
A binding that updates the target property after a specified *DelayMS* in ms (unlike *Delay* which specifies source update delays).

When the *HasDelayCondition* is set to true (default is false), a condition reference is set by *DelayCondition* compared to the current binding value. There is no delay (target gets immediately updated) when the *DelayCondition* is not met while the *HasDelayCondition* is true.

### DynamicResourceBinding
A binding that can connect a dynamic resource pointed by *ResourceKey* to a property and updates the latter the dynamic resource changes.

### ICollectionChangedBinding
Offers binding notifications when a source collection is updated. 

Works as single *Binding* or as *MultiBinding*. In the latter case, any update of a bound source *Binding* item will trigger the update.

A *ActionsToNotify* property allows you to specify the kind of collection changes that can trigger notifications (default set to any change, enum supports combinations). 

### TypeFilteredBinding
Set binding only if the source object type matches the predefined *SourceType* one.


## License
This work is licensed under the [MIT License](LICENSE.md).