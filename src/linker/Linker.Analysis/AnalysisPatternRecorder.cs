using Mono.Cecil;
using System.Collections.Generic;

namespace Mono.Linker.Analysis
{
	class AnalysisPatternRecorder : IPatternRecorder
	{
		public List<MethodDefinition> UnanalyzedMethods { get; private set; } = new List<MethodDefinition> ();
		public Dictionary<MethodDefinition, HashSet<MethodDefinition>> ResolvedReflectionCalls { get; private set; } =
			new Dictionary<MethodDefinition, HashSet<MethodDefinition>> ();

		public AnalysisPatternRecorder()
		{
		}

		public void RecognizedReflectionEventAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, EventDefinition accessedEvent)
		{
			AddResolvedReflectionCall (sourceMethod, reflectionMethod);
		}

		public void RecognizedReflectionFieldAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, FieldDefinition accessedField)
		{
			AddResolvedReflectionCall (sourceMethod, reflectionMethod);
		}

		public void RecognizedReflectionMethodAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, MethodDefinition accessedMethod)
		{
			AddResolvedReflectionCall (sourceMethod, reflectionMethod);
		}

		public void RecognizedReflectionPropertyAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, PropertyDefinition accessedProperty)
		{
			AddResolvedReflectionCall (sourceMethod, reflectionMethod);
		}

		public void RecognizedReflectionTypeAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, TypeDefinition accessedType)
		{
			AddResolvedReflectionCall (sourceMethod, reflectionMethod);
		}

		public void UnrecognizedReflectionCallPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, string message)
		{
			UnanalyzedMethods.Add (sourceMethod);
		}

		private void AddResolvedReflectionCall(MethodDefinition caller, MethodDefinition callee)
		{
			if (!ResolvedReflectionCalls.TryGetValue(callee, out HashSet<MethodDefinition> callers)) {
				callers = new HashSet<MethodDefinition> ();
				ResolvedReflectionCalls.Add (callee, callers);
			}

			callers.Add (caller);
		}
	}
}
