# Linker analyzer

We have been prototyping a callgraph-based analyzer for the linker, that reports "linker-unsafe" usage in the IL reachable from some set of entry points (Main, or all public surface area). It simulates attributes that influence how unsafe patterns get reported.

# Instructions

To give it a try:

## Clone and build
```
> git clone https://github.com/vitek-karas/linker -b Analysis
> cd linker
linker> ./eng/dotnet.{sh/ps1} build illink.sln
```

## Run

Run the linker specifying the desired set of roots, optimizations, etc. A summary of the analysis will be written to stdout, and the "callstacks" will be written to `trimanalysis.json` in the linker output directory.
For example: 

```
linker> dotnet ./src/ILLink.Tasks/bin/Debug/netcoreapp2.0/illink.dll -a console -c link -u link -d testapps/console/out/ --skip-unresolved true --verbose --used-attrs-only true -out testapps/console/linked --analyze-for-trim
```

Here, the command-line flags have the following meaning.
```
-a console:                    Roots console.dll. The linker will see that it is an application, and root only the entry point (not all public APIs)
-c link -u link:               Tells the linker to trim all application (-u) and framework (-c) libraries unless otherwise specified.
-d testapps/console/out:       Tells the linker where to find the dependencies of the application.
--skip-unresolved:             Continue when encountering unresolved dependencies.
--verbose:                     Output extra info to stdout, such as the action taken per assembly.
--used-attrs-only true:        Only keep custom attributes when the attribute type is referenced. (Otherwise this keeps all custom attributes on kept types).
--out testapps/console/linked: Output the linked app into the specified directory.
```
All of the above are standard linker flags. To enable the analyzer:
```
--analyze-for-trim:            Output a summary of the analysis to console, and write trimanalysis.json into the linker output (--out) directory.
```

# How it works

This analyzer maintains a set of "interesting" methods - where "interesting" means either that the linker noticed something special about the method, or we added a (simulated) attribute on it to mark it as interesting explicitly.

The tool finds callers of the interesting method and reports partial "call stacks" to give context on how the linker thought each interesting method was reachable. There are a number of challenges in reporting this information in a useful way, and we will continue iterating on it. For now, it behaves as follows:

The callstacks stop at public APIs or virtual methods. Stopping at virtual methods avoid lots of redundancy in cases where a very common virtual method (like `ToString`) has an override that is unsafe - which would otherwise show up everywhere that `ToString` is called.

The linker already attempts to understand some simple reflection patterns. When it sees a pattern that it doesn't recognize, the containing method is marked as `LinkerUnanalyzed`. Cases where the linker *does* understand a relfection pattern are not yet taken into account. Because we have defined simulated attributes for reflection APIs, most of understood ones will show up as calls to reflection APIs with the `KnownReflection` attribute to indicate that it is not generally safe.

Methods that look "unsafe" but are actually safe can be attributed with a specific attribute to track the feature area, or with `AnnotatedLinkerFriendly` which will prevent the method from showing up in "unsafe" callstacks. The tool may still report callstacks that only end up in `AnnotatedLinkerFriendly` methods (for now), though the plan is for these not to show up in the output.

The plan is likely for `PreserveDependencyAttribute` on a method to mark it as linker-safe for the analysis, but this isn't yet taken into account.

# Adding attributes

The simulated attributes (other than `LinkerUnanalyzed`) are currently defined in C# sources. `ReflectionAPIs.cs` defines the "base" list of APIs we are considering unsafe. `ApiFilter.cs` has some attempt to add attributes to track unsafe usage in higher-level APIs, but is fairly ad-hoc. Ultimately we probably want to mark public APIs as linker-unsafe or linker-safe so that the attributes can show up in ref assemblies.

# Known challenges

As described earlier, virtual methods pose a problem. The current thinking is that we will "blame the constructors", so that the constructor of any type with unsafe members is marked unsafe, and any callers of this constructor get marked.