# Examples of marking higher-level APIs for "Linker safety"

## Lazy<T>
Framework method [`System.LazyHelper.CreateViaDefaultConstructor<T>`](https://github.com/dotnet/runtime/blob/f5957b1bae3028fd3854230164d9f18b40c7193f/src/libraries/System.Private.CoreLib/src/System/Lazy.cs#L156) calls `Activator.CreateInstance<T>()`. Currently this is a pattern which linker can't resolve on its own. It means that if the code actually reaches this method at runtime the `T` while existing (since it's a generic parameter) it may not have any `ctor` (linker could trim it as unreachable) and thus `CreateInstance` may fail on it.

Detailed analysis of `Lazy<T>` and its call tree shows, that the `CreateViaDefaultConstructor` can only be called if there's no value factory specified. This in turn means that if the `Lazy<T>` is instantiated via some constructors it will never reach the `CreateViaDefaultConstructor`. Linker on its own can't resolve this, it will see that there's a direct path from `Lazy<T>.Value` to `CreateViaDefaultConstructor` and thus mark pretty much all usages of `Lazy<T>` as unsafe.

To solve this we can add annotations which tell linker about the different constructor. First we mark the constructors which lead to `CreateViaDefaultConstructor` as unsafe. Currently this is done in `ApiFilter.cs` in `GetInterestingReasonFromAnnotation`. This piece of code does that:

``` C#
    // 5. Lazy<T> usage of Activator.CreateInstance
    if (method.DeclaringType.FullName == "System.Lazy`1" &&
        method.Name == ".ctor") {
        if (method.Parameters.Count == 0 ||
            (method.Parameters.Count == 1 && method.Parameters [0].ParameterType.FullName == "System.Boolean") ||
            (method.Parameters.Count == 1 && method.Parameters [0].ParameterType.FullName == "System.Threading.LazyThreadSafetyMode")) {
            // Lazy<T>.ctor()
            // Lazy<T>.ctor(bool)
            // Lazy<T>.ctor(LazyThreadSafetyMode)
            // all these ctors create Lazy<T> which will end up calling Activator.CreateInstance<T> which is currently unanalyzable.
            // Since we marked the internal helper which actually calls the CreateInstance as safe (to avoid reporting cases
            // where safe ctors are called), we need to mark the dangerous ctors as unsafe.
            return InterestingReason.LazyOfT;
        }
    }
```

It marks `ctor()`, `ctor(bool)` and `ctor(LazyThreadSafetyMode)` as unsafe - for now we mark it with a specific reason `LazyOfT`. The thinking is that it would get annotation something like 
``` C#
[LinkerUnsafe("Usage of this constructor is unsafe. Solve this by either using constructors which accept value factory Func or by marking the T.ctor via PreserveDependency")]
```

This will now nicely mark the unsafe constructors, but linker would still report the `CreateViaDefaultConstructor` as unsafe. So we need to mark it as safe now:
``` C#
    if (method.DeclaringType.FullName == "System.LazyHelper" &&
        method.Name == "CreateViaDefaultConstructor") {
        // Lazy<T> has a set of ctors which create the object based on a Func, those are perfectly safe
        // as the creation of the instance is done by the specified Func (and thus analyzable by the linker).
        // The other set of ctors lead to an object which will create the instance by calling Activator.CreateInstance<T>()
        // this one is theoretically analyzable (as we should be able to deduce T), but linker currently doesn't have the ability
        // to fully analyze generics. So in the meantime these ctors will be marked as unsafe.
        // Unfortunately the switch between the two cases is based on internal variable and thus linker sees the unsafe
        // Activator.CreateInstance for all cases. So mark the internal LazyHelper.CreateViaDefaultConstructor
        // which calls the Activator.CreateInstance as safe and instead rely on the "unsafe" ctors only.
        return InterestingReason.AnnotatedLinkerFriendly;
    }
```

This will tell linker to ignore the unsafe calls inside the `CreateViaDefaultConstructor`.

The end result is that code using the safe ctors of `Lazy<T>` will not get any warnings, while those calling the unsafe ctors will get a warning with helpful message.