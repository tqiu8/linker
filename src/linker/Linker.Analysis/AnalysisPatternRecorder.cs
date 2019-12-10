using Mono.Cecil;
using System;
using System.Collections.Generic;

namespace Mono.Linker.Analysis
{
	class AnalysisPatternRecorder : IPatternRecorder
	{
		public List<MethodDefinition> UnanalyzedMethods { get; private set; } = new List<MethodDefinition> ();

		public AnalysisPatternRecorder()
		{
		}

		public void RecognizedReflectionEventAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, EventDefinition accessedEvent)
		{
			Console.WriteLine ($"Recognized reflection access to event {accessedEvent} in {sourceMethod.FullName} via {reflectionMethod}");
		}

		public void RecognizedReflectionFieldAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, FieldDefinition accessedField)
		{
			Console.WriteLine ($"Recognized reflection access to field {accessedField} in {sourceMethod.FullName} via {reflectionMethod}");
		}

		public void RecognizedReflectionMethodAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, MethodDefinition accessedMethod)
		{
			Console.WriteLine ($"Recognized reflection access to method {accessedMethod} in {sourceMethod.FullName} via {reflectionMethod}");
		}

		public void RecognizedReflectionPropertyAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, PropertyDefinition accessedProperty)
		{
			Console.WriteLine ($"Recognized reflection access to property {accessedProperty} in {sourceMethod.FullName} via {reflectionMethod}");
		}

		public void RecognizedReflectionTypeAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, TypeDefinition accessedType)
		{
			Console.WriteLine ($"Recognized reflection access to type {accessedType} in {sourceMethod.FullName} via {reflectionMethod}");
		}

		public void UnrecognizedReflectionCallPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, string message)
		{
			UnanalyzedMethods.Add (sourceMethod);
		}
	}
}
