using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

namespace Mono.Linker.Steps
{
	public class AnalysisEntryPointsStep : BaseStep
	{
		public HashSet<MethodDefinition> EntryPoints { get;  private set; }

		public AnalysisEntryPointsStep ()
		{
		}

		protected override void Process ()
		{
			EntryPoints = new HashSet<MethodDefinition> (Annotations.GetMarked ().OfType<MethodDefinition> ());
		}
	}
}
