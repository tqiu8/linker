using Mono.Cecil;
using Mono.Linker.Analysis;
using System.IO;

namespace Mono.Linker.Steps
{
	public class AnalysisStep : BaseStep
	{
		private LinkContext context;
		private CallgraphDependencyRecorder callgraphDependencyRecorder;
		private AnalysisPatternRecorder patternRecorder;
		private AnalysisEntryPointsStep entryPointsStep;

		public AnalysisStep(LinkContext context, AnalysisEntryPointsStep entryPointsStep)
		{
			this.context = context;
			this.entryPointsStep = entryPointsStep;
			
			callgraphDependencyRecorder = new CallgraphDependencyRecorder ();
			context.Tracer.AddRecorder (callgraphDependencyRecorder);

			patternRecorder = new AnalysisPatternRecorder ();
			context.PatternRecorder = patternRecorder;
		}

		protected override void Process ()
		{
			var apiFilter = new ApiFilter (patternRecorder.UnanalyzedMethods, entryPointsStep.EntryPoints);
			var cg = new CallGraph (callgraphDependencyRecorder.Dependencies, apiFilter);

			string jsonFile = Path.Combine (context.OutputDirectory, "trimanalysis.json");
			using (StreamWriter sw = new StreamWriter (jsonFile)) {
				(IntCallGraph intCallGraph, IntMapping<MethodDefinition> mapping) = IntCallGraph.CreateFrom (cg);
				var formatter = new Formatter (cg, mapping, json: true, sw);
				var analyzer = new Analyzer (cg, intCallGraph, mapping, apiFilter, formatter: formatter, Grouping.Callee);
				analyzer.Analyze ();
			}
		}
	}
}
