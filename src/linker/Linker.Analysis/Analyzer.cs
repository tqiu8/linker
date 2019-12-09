using Mono.Cecil;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Mono.Linker.Analysis
{

    public class Analyzer
    {
        public Dictionary<string, HashSet<TypeDefinition>> hitTypesPerNS;
        public List<AnalyzedStacktrace> analyzedStacktraces;
        public Dictionary<InterestingReason, HashSet<AnalyzedStacktrace>> stacktracesPerReason;
        ICallGraph<MethodDefinition> callGraph;
        ApiFilter apiFilter;

        Formatter formatter;

        bool[] isVirtualMethod;
        bool[] isAnnotatedSafeMethod;
        InterestingReason[] interestingReasons;
        int numInterestingMethods;
        int numEntryMethods;

        public IntCallGraph intCallGraph;
        IntMapping<MethodDefinition> mapping;

        // track methods for each interesting reason
        public Dictionary<InterestingReason, HashSet<MethodDefinition>> methodsPerReason;

        void TrackInterestingReason(InterestingReason reason, MethodDefinition method) {
            if (methodsPerReason == null) {
                methodsPerReason = new Dictionary<InterestingReason, HashSet<MethodDefinition>>();
            }

            if (!methodsPerReason.TryGetValue(reason, out HashSet<MethodDefinition> methods)) {
                methods = new HashSet<MethodDefinition>();
                methodsPerReason[reason] = methods;
            }
            methods.Add(method);
        }

        public Analyzer(CallGraph callGraph,
                        IntCallGraph intCallGraph,
                        IntMapping<MethodDefinition> mapping,
                        ApiFilter apiFilter,
						Formatter formatter = null) {
            this.callGraph = callGraph;
            this.mapping = mapping;
            this.intCallGraph = intCallGraph;
            this.apiFilter = apiFilter;
            this.formatter = formatter;
        }

        void ReportMethodReasons() {
			if (methodsPerReason is null)
				return;

            var sortedReasons = methodsPerReason.OrderByDescending(e => e.Value.Count);
            Console.WriteLine("summary: found " + numInterestingMethods + " interesting methods");
            foreach (var e in sortedReasons) {
                var reason = e.Key;
                var methods = e.Value;
                Console.WriteLine(methods.Count + " methods are " + reason);
            }
            var allReasons = (InterestingReason[])Enum.GetValues(typeof(InterestingReason));
            foreach (var reason in allReasons) {
                if (reason == InterestingReason.None) {
                    continue;
                }
                if (methodsPerReason.ContainsKey(reason)) {
                    var methods = methodsPerReason[reason];
                    Debug.Assert(methods != null && methods.Count > 0);
                } else {
                    Console.WriteLine("0 methods are " + reason);
                }
            }
        }

        // for each interesting reason, give a count, and a few samples.
        void ReportBuckets() {
            if (stacktracesPerReason == null)
            {
                return;
            }

            var sortedReasons = stacktracesPerReason.OrderByDescending(e => e.Value.Count);

            // give a sample of some stacktraces for each category. disabled.
            // int count = 10;
            // foreach (var e in sortedReasons) {
            //     var reason = e.Key;
            //     var sts = e.Value;
            //     Console.WriteLine(sts.Count + " stacktraces are " + reason + ":");
            //     foreach (var st in sts) {
            //         if (count == 0) {
            //             break;
            //         }
            //         Program.PrintStacktrace(st.stacktrace);
            //         Console.WriteLine();
            //         count--;
            //     }
            // }
            Console.WriteLine("summary: found " + stacktrace_count + " stacktraces");
            foreach (var e in sortedReasons) {
                var reason = e.Key;
                var sts = e.Value;
                Console.WriteLine(sts.Count + " stacktraces are " + reason);
            }
            var allReasons = (InterestingReason[])Enum.GetValues(typeof(InterestingReason));
            foreach (var reason in allReasons) {
                if (reason == InterestingReason.None) {
                    continue;
                }
                if (stacktracesPerReason.ContainsKey(reason)) {
                    var sts = stacktracesPerReason[reason];
                    Debug.Assert(sts != null && sts.Count > 0);
                } else {
                    Console.WriteLine("0 stacktraces are " + reason);
                }
            }
        }

        // HashSet<List<string>>[] buckets;
        // void ReportStacktraceBuckets(AnalyzedStacktrace res) {
        //     int reportCount = 3;
        //     string prefix = "  ";
        //     var c = (int)res.category;
        //     buckets[c].Add(res.stacktrace);
        //     if (buckets[c].Count == reportCount) {
        //         Console.WriteLine("-----------------------------");
        //         Console.WriteLine("a few examples of stack traces that are " + res.category);
        //         foreach (var stacktrace in buckets[c]) {
        // 
        //             string methodString;
        //             MethodDefinition methodDef;
        //             if (usingStringInput) {
        //                 methodString = res.stacktrace[0];
        //                 methodDef = GetCecilMethod(methodString);
        //                 if (methodDef == null) {
        //                     Console.WriteLine(prefix + "---------- (???)");
        //                 } else {
        //                     Console.WriteLine(prefix + "---------- (" + GetInterestingReason(methodDef).ToString() + ")");
        //                 }
        //             } else {
        //                 throw new Exception("not supported");
        //             }
        // 
        //             foreach (var m in stacktrace) {
        //                 Console.WriteLine(prefix + m);
        //             }
        //         }
        //     }
        // }

        public void Analyze() {
            numInterestingMethods = 0;

            interestingReasons = new InterestingReason[intCallGraph.numMethods];
            isVirtualMethod = new bool[callGraph.Methods.Count];
			isAnnotatedSafeMethod = new bool [callGraph.Methods.Count];
            var isPublicOrVirtual = new bool[callGraph.Methods.Count];
            for (int i = 0; i < intCallGraph.numMethods; i++) {
                var cecilMethod = mapping.intToMethod[i];
                if (cecilMethod == null) {
                    continue;
                }
                if (intCallGraph.isInteresting[i]) {
                    numInterestingMethods++;
                    var reason = apiFilter.GetInterestingReason(cecilMethod);
                    interestingReasons[i] = reason;
                    TrackInterestingReason(reason, cecilMethod);
                }
                if (intCallGraph.isEntry[i]) {
                    numEntryMethods++;
                }
                if (cecilMethod.IsVirtual) {
                    isVirtualMethod[i] = true;
                }
                if (intCallGraph.isEntry[i] || isVirtualMethod[i]) {
                    isPublicOrVirtual[i] = true;
                }
                if (apiFilter.IsAnnotatedLinkerFriendlyApi(cecilMethod)) {
                    isAnnotatedSafeMethod[i] = true;
                }
            }

            Console.WriteLine("found " + intCallGraph.numMethods + " methods");
            Console.WriteLine("found " + numEntryMethods + " entry methods");
            Console.WriteLine("found " + numInterestingMethods + " \"interesting\" methods");

            Console.WriteLine("built call graph...");

            hitTypesPerNS = new Dictionary<string, HashSet<TypeDefinition>>();
            // concurrently print out call stacks as we find them
            var cq = new ConcurrentQueue<AnalyzedStacktrace>();

            var buckets = new Dictionary<string, int>();
            Action reportAction = () =>
            {
                // int numCategories = (int)Category.NumCategories;
                // Debug.Assert(numCategories == Enum.GetNames(typeof(Category)).Length - 1);
                // buckets = new HashSet<List<string>>[numCategories];
                // int i;
                // for (i = 0; i < numCategories; i++) {
                //     buckets[i] = new HashSet<List<string>>();
                // }

                while (true) {
                    if (cq.TryDequeue(out AnalyzedStacktrace res)) {
                        if (res.source == -2) {
                            // signals completion
                            break;
                        }
                        if (formatter != null)
                            formatter.WriteStacktrace(res);
                        CountStacktrace(res);
                        // ReportStacktraceBuckets(res);
                        if (!buckets.ContainsKey(res.category)) {
                            buckets[res.category] = 1;
                        } else {
                            buckets[res.category]++;
                        }

                        // var c = (int)res.category;

                        // if ((completion_counter % 100) == 0) {
                        //     Console.WriteLine(completion_counter + "/" + numMethods);
                        // }
                    }
                }
                // for (i = 0; i < numCategories; i++) {
                //     Console.WriteLine(buckets[i].Count + " stack traces are " + (Category)i);
                // }
            };
            var reportTask = Task.Run(reportAction);

            int num_stacktraces = 0;


            IntBFS.AllPairsBFS(
                neighbors: intCallGraph.callers,                 // search bottom-up (callees to callers).
                isSource: intCallGraph.isInteresting,                   // look for a shortest path from each "interesting" method...
                isDestination: isPublicOrVirtual,                // ...to each public or virtual method.
                numMethods: intCallGraph.numMethods,
                excludePathsToSources: true,                     // ...that don't go through any other "interesting" methods.
                ignoreEdgesTo: isAnnotatedSafeMethod,            // ignore calls from annotated safe methods (edges to safe methods in the bottom-up case)
                ignoreEdgesFrom: isVirtualMethod,                // ignore calls to virtual methods (edges from virtual methods in the bottom-up case)

                // don't report paths that go through another public or virtual method
                //   this is ensured by the BFS algorithm - it won't consider edges from a destination node. (calls to a public/virtual)
                // also don't report paths that go through another interesting method
                //   this is ensured by the isSource check.

                resultAction: (IntBFSResult r) => {
                    var publicOrVirtuals = r.destinations;
                    var sourceInterestingMethod = r.source;
                    if (publicOrVirtuals.Count > 0) { // if a public or virtual method was reachable from a reflection method...
                        // only print the first one for now.
                        foreach (var destination in r.destinations) {
                            FormattedStacktrace f;
                            if (formatter != null) {
                                f = formatter.FormatStacktrace(r, destination: destination, reverse: true);
                            } else {
                                throw new Exception("unable to format!");
                            }
                            // queue it for printing
                            var res = new AnalyzedStacktrace {
                                source = sourceInterestingMethod,
                                stacktrace = f,
                                category = CategorizeStacktraceWithCecil(f.asMethods),
                                reason = interestingReasons[sourceInterestingMethod]
                            };
                            cq.Enqueue(res);
                            Interlocked.Add(ref num_stacktraces, 1);
                        }
                    }
                });
            cq.Enqueue(new AnalyzedStacktrace { source = -2 }); // signal completion

            // record the shortest reverse path from each "dangerous" API to each public or virtual method
            //var result = Parallel.For(0, numInterestingMethods, (iInteresting, state) =>
            //{

            // non-virtual call graph.
            // all shortest non-loopy paths from public/virtual (that isn't itself "dangerous") to first "dangerous" API
            // want: all ways that public surface area reaches dangerous methods.
            // when it reaches one dangerous method, don't need to continue to find all dangerous callees of the dangerous method
            // but DO need to show paths to different dangerous methods that don't overlay
            // and if there are multiple paths to same dangerous method?
            //   only need to report one. but if one of the paths goes through a different dangerous method, need to report both.
            //   never report cycles.
            //
            // scan from "interesting" methods to callers.
            // stop when we see a method that is public or virtual
            // what if we see an "interesting" method?
            // don't report lone "interesting" methods that are public APIs - want at least one caller.
            // ignore virtual calls, and stop at public APIs or virtual methods.
            // don't consider calls from a "safe" method
            // don't consider calls from an "unsafe" method (since we don't want these to show up as extra paths with the unsafe API in the middle)
            // for a bottom-up search, starting with the dangerous APIs, find callers.

            // record the shortest path from each source to an interesting method
            //    var result = Parallel.For(0, numEntryMethods, (iEntry, state) =>
            //    // for (int iEntry = 0; iEntry < numEntryMethods; iEntry++)
            //    {
            //        int source = entryMethods[iEntry];
            //        var r = IntBFS(source, neighborsList, isInterestingMethod);
            //    
            //        int interestingMethod = r.interestingMethod;
            //        int i = interestingMethod;
            //        // string prefix = String.Format("{0,-6}", source) + ": ";
            //        if (i != -1) { // if an interesting method was reachable from source...
            //            // format a call stack
            //            var f = FormatCallStack(r);
            //            stacktraces[source] = f.asList;
            //            // queue the stacktrace for printing
            //            var res = new AnalyzedStacktrace {
            //                stacktrace = f.stacktrace,
            //                formattedStacktrace = f.asString,
            //                category = CategorizeStacktrace(f.stacktrace)
            //            };
            //            cq.Enqueue(res);
            //            Interlocked.Add(ref num_stacktraces, 1);
            //        }
            //        Interlocked.Add(ref completion_counter, 1);
            //    });
            // }
            reportTask.Wait();
            Console.WriteLine("-------------");
            Console.WriteLine("found " + num_stacktraces + " stack traces");
            var items = new List<(string, int)>();
            foreach (var item in buckets) {
                var category = item.Key;
                var hitCount = item.Value;
                items.Add((category, hitCount));
            }
            items.Sort((x, y) => ((int)x.Item2).CompareTo((int)y.Item2));
            items.Reverse();
            foreach (var item in items) {
                var (category, hitCount) = item;
                Console.WriteLine(hitCount + " stack traces are " + category);
            }
            //foreach (var ns in CecilAdapter.namespaces) {
            //    if (!buckets.ContainsKey(ns)) {
            //        Console.WriteLine(0 + " stack traces are " + ns);
            //    }
            //}
            Console.WriteLine("----------");
            ReportBuckets();
            Console.WriteLine("----------");
            ReportTypeCounts();
            Console.WriteLine("----------");
            ReportMethodReasons();
            // ReportRandomSample();
        }


        public int stacktrace_count = 0;
        public void CountStacktrace(AnalyzedStacktrace st) {
            stacktrace_count++;
            // bucketize based on interesting reason
            if (stacktracesPerReason == null) {
                stacktracesPerReason = new Dictionary<InterestingReason, HashSet<AnalyzedStacktrace>>();
            }
            if (!stacktracesPerReason.TryGetValue(st.reason, out HashSet<AnalyzedStacktrace> sts)) {
                sts = new HashSet<AnalyzedStacktrace>();
                stacktracesPerReason[st.reason] = sts;
            }
            sts.Add(st);


			var method = st.stacktrace.asMethods.Last ();
			if (method == null) {
				Console.WriteLine ("TODO!");
				return;
			}

			if (analyzedStacktraces == null) {
				analyzedStacktraces = new List<AnalyzedStacktrace> ();
			}
			analyzedStacktraces.Add (st);

			// find outermost declaring type? no. count nested types.
			var type = method.DeclaringType;
			var nsType = type;
			while (nsType.DeclaringType != null) {
				nsType = nsType.DeclaringType;
			}
			var ns = nsType.Namespace;
			if (String.IsNullOrEmpty (ns)) {
				return;
			}
			if (!hitTypesPerNS.TryGetValue (ns, out HashSet<TypeDefinition> types)) {
				types = new HashSet<TypeDefinition> ();
				hitTypesPerNS [ns] = types;
			}
			types.Add (type);
		}

        public void ReportRandomSample() {
            int sample = 100;
            int total = analyzedStacktraces.Count;
            var random = new Random();
            // without replacement.
            var hs = new HashSet<int>();
            for (int i = 0; i < sample; i++) {
                while (true) {
                    var r = random.Next(0, total);
                    if (hs.Add(r)) {
                        break;
                    }
                }
            }
            random.Next(0, total);

            Console.WriteLine("A random sample of " + sample + " stacktraces:");
            foreach (var r in hs) {
                var analyzedStacktrace = analyzedStacktraces[r];
                Console.WriteLine(analyzedStacktrace.stacktrace.asString);
            }
        }

        public void ReportTypeCounts() {
			var ordered = hitTypesPerNS.OrderByDescending (e => e.Value.Count);

			HashSet<TypeDefinition> allTypes = callGraph.Methods.Select (m => m.DeclaringType).ToHashSet ();
			HashSet<string> allNamespaces = allTypes.Select (t => t.Namespace).Where(ns => !string.IsNullOrEmpty(ns)).ToHashSet ();

			var totalTypesPerNS = new Dictionary<string, int> ();
			foreach (var t in allTypes) {
				var type = t;
				while (type.DeclaringType != null) {
					type = type.DeclaringType;
				}
				var ns = type.Namespace;
				if (!String.IsNullOrEmpty (ns)) {
					if (!totalTypesPerNS.ContainsKey (ns)) {
						totalTypesPerNS [ns] = 1;
					} else {
						totalTypesPerNS [ns]++;
					}
				}
			}

			// output the actual data
			foreach (var e in ordered) {
				var ns = e.Key;
				var types = e.Value;
				Console.WriteLine (ns + ": " + types.Count + " / " + totalTypesPerNS [ns]);
			}

			// output namespaces with zero hits
			var zeroNamespaces = allNamespaces.Where (ns => !hitTypesPerNS.ContainsKey (ns)).OrderBy (ns => ns);
			foreach (var ns in zeroNamespaces) {
				Console.WriteLine (ns + ": 0 / " + totalTypesPerNS [ns]);
			}
		}


        // pass in a specific destination to print,
        // otherwise it's assumed the result only has a single destination.



        // not used
        public enum Category {
            AnonymousGetHashCode,
            ObjectToStringMoveNext,
            ObjectEqualsDynamicBinding,
            ResourceManagerGetStringMoveNext,
            MulticastDelegateGetMethodBase,
            XmlSchemaMoveNext,
            ResourceEnumeratorDeserializeObject,
            IEnumeratorMoveNextReflection,
            ComponentModelEqualityComparer,
            PublicReflectionApi,
            DefaultEqualityComparer,
            NetEventSourceFormatTypeName,
            ExceptionToStringMemberInfo,
            Uncategorized,
            NumCategories
        }


        public string CategorizeStacktraceWithCecil(List<MethodDefinition> stacktrace) {
            var sourceMethod = stacktrace.Last();
            if (sourceMethod == null) {
                return "resolution failure";
            }
            if (stacktrace.Any(frame => frame.ToString().Contains("System.Diagnostics.Tracing.EventSource::GetCustomAttributeHelper"))) {
                return "eventsource_customattributehelper";
            }
            if (stacktrace.Any(frame => frame.ToString ().Contains("EventSource::WriteEventWithRelatedActivityIdCore")) &&
                stacktrace.Any(frame => frame.ToString ().Contains("TraceLoggingEventTypes::.ctor(System.String"))) {
                return "eventsource_traceloggingeventtypes";
            }
            var ns = sourceMethod.DeclaringType.Namespace;
            if (!String.IsNullOrEmpty(ns)) {
                return ns;
            }
            TypeDefinition type = sourceMethod.DeclaringType;
            while (type.DeclaringType != null) {
                type = type.DeclaringType;
            }
            ns = type.Namespace;
            if (!String.IsNullOrEmpty(ns)) {
                return ns;
            }
            return sourceMethod.DeclaringType.FullName;
            // return "uncategorized";

            // Category category = Category.Uncategorized;
            // if (stacktrace.Any(m => m.Contains("System.Object::GetHashCode()")) &&
            //     stacktrace.Any(m => m.Contains("<>f__AnonymousType0`1"))) {
            //     SetCategory(ref category, Category.AnonymousGetHashCode);
            // }
            // if (stacktrace.Any(m => m.Contains("System.Collections.IEnumerator::MoveNext"))) {
            //     SetCategory(ref category, Category.IEnumeratorMoveNextReflection);
            // }
            // // if (stacktrace.Any(m => m.Contains("System.Object::ToString()")) &&
            // //     stacktrace.Any(m => m.Contains("System.Collections.IEnumerator::MoveNext"))) {
            // //     SetCategory(ref category, Category.ObjectToStringMoveNext);
            // // }
            // if (stacktrace.Any(m => m.Contains("System.Object::Equals")) &&
            //     stacktrace.Any(m => m.Contains("System.Dynamic.BindingRestrictions/TypeRestriction::Equals"))) {
            //     SetCategory(ref category, Category.ObjectEqualsDynamicBinding);
            // }
            // // if (stacktrace.Any(m => m.Contains("System.Resources.ResourceManager::GetString")) &&
            // //     stacktrace.Any(m => m.Contains("System.Collections.IEnumerator::MoveNext"))) {
            // //     SetCategory(ref category, Category.ResourceManagerGetStringMoveNext);
            // // }
            // if (stacktrace.Any(m => m.Contains("System.MulticastDelegate::GetMethodImpl")) &&
            //     stacktrace.Any(m => m.Contains("System.Type::IsSubclassOf"))) {
            //     SetCategory(ref category, Category.MulticastDelegateGetMethodBase);
            // }
            // // if (stacktrace.Any(m => m.Contains("System.Xml.Schema.XmlSchemaCollection")) &&
            // //     stacktrace.Any(m => m.Contains("System.Collections.IEnumerator::MoveNext")) &&
            // //     stacktrace.All(m => !m.Contains("System.Object::ToString()"))) {
            // //     SetCategory(ref category, Category.XmlSchemaMoveNext);
            // // }
            // if (stacktrace.Any(m => m.Contains("IDictionaryEnumerator::get_Value")) &&
            //     stacktrace.Any(m => m.Contains("System.Resources.ResourceReader::DeserializeObject"))) {
            //     SetCategory(ref category, Category.ResourceEnumeratorDeserializeObject);
            // }
            // if (stacktrace.Any(m => m.Contains("System.Collections.Generic.ComparerHelpers::CreateDefaultEqualityComparer"))) {
            //     SetCategory(ref category, Category.DefaultEqualityComparer);
            // }
            // if (stacktrace.Any(m => m.Contains("System.ComponentModel.Design.ServiceContainer/ServiceCollection`1/EmbeddedTypeAwareTypeComparer"))) {
            //     SetCategory(ref category, Category.ComponentModelEqualityComparer);
            // }
            // if (new Regex("System.Reflection.PropertyInfo::.*.et.*Value").IsMatch(stacktrace[stacktrace.Count-1])) {
            //     SetCategory(ref category, Category.PublicReflectionApi);
            // }
            // if (stacktrace.Any(m => m.Contains("System.Net.Http.HttpRequestMessage::.ctor")) &&
            //     stacktrace.Any(m => m.Contains("System.Net.NetEventSource::Format"))) {
            //     SetCategory(ref category, Category.NetEventSourceFormatTypeName);
            // }
            // if (stacktrace.Any(m => m.Contains("System.Exception::ToString")) &&
            //     stacktrace.Any(m => m.Contains("System.Reflection.MemberInfo::IsDefined"))) {
            //     SetCategory(ref category, Category.ExceptionToStringMemberInfo);
            // }
            // 
            // return category;
        }

        HashSet<(Category, Category)> duplicateCategoryErrors;
        void SetCategory(ref Category existing, Category newCategory) {
            if (duplicateCategoryErrors == null) {
                duplicateCategoryErrors = new HashSet<(Category, Category)>();
            }
            if (existing != Category.Uncategorized &&
                !duplicateCategoryErrors.Contains((newCategory, existing))) {
                Console.WriteLine(newCategory + " already categorized as " + existing);
                duplicateCategoryErrors.Add((newCategory, existing));
            }
            existing = newCategory;
        }

    }

    public struct AnalyzedStacktrace {
        public int source;
        public FormattedStacktrace stacktrace;
        public InterestingReason reason;
        public string category;
    }

}
