using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

namespace Mono.Linker.Analysis
{

	public class CallGraph : ICallGraph<MethodDefinition>
	{

		// interface implementation
		public ICollection<MethodDefinition> Methods => methods;

		public ICollection<(MethodDefinition, MethodDefinition)> Calls => calls;

		// TODO: track virtual calls properly.
		// for now, they're approximately handled in the Analyzer
		public ICollection<(MethodDefinition, MethodDefinition)> Overrides => null;

		public bool IsEntry (MethodDefinition m)
		{
			return apiFilter.IsEntryMethod (m);
		}

		public bool IsInteresting (MethodDefinition m)
		{
			return apiFilter.IsInterestingMethod (m);
		}

		public ApiFilter apiFilter;
		// currently only used when taking edges in as strings

		HashSet<MethodDefinition> methods;
		HashSet<(MethodDefinition, MethodDefinition)> calls;

		public CallGraph (
			List<(MethodDefinition, MethodDefinition)> edges,
			ApiFilter apiFilter)
		{
			this.apiFilter = apiFilter;
			Initialize (edges);
		}

		public void Initialize (List<(MethodDefinition, MethodDefinition)> edges)
		{
			calls = edges.ToHashSet (); ;
			methods = new HashSet<MethodDefinition> ();
			foreach (var e in edges) {
				var (from, to) = e;
				methods.Add (from);
				methods.Add (to);
			}
		}
	}
}
