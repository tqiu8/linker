//
// ConsoleDependencyGraph.cs: text output related code for dependency graph
//
// Author:
//   Radek Doulik (rodo@xamarin.com)
//
// Copyright 2015 Xamarin Inc (http://www.xamarin.com).
//
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using LinkerAnalyzer.Core;
using System.Text.Json;
using System.Linq;
using System.IO;
using System.IO.Compression;

namespace LinkerAnalyzer
{
	public class ConsoleDependencyGraph : DependencyGraph
	{
		public bool Tree;
		public bool FlatDeps;
		private Utf8JsonWriter depJsonWriter = null;
		private string depJsonOutputFileName = null;
		private Utf8JsonWriter indexJsonWriter = null;
		private string indexJsonOutputFileName = null;
		private string wheelJsonOutputFileName = null;
		private List<VertexData> limitVertices = null;
		private Dictionary<string, int> limitIndexes = null;

		public void ShowDependencies (string raw, List<VertexData> verticesList, string searchString)
		{
			VertexData vertex = Vertex (raw);
			if (vertex == null) {
				Regex regex = new Regex (searchString);
				int count = 0;

				foreach (var v in verticesList) {
					if (regex.Match (v.value) != Match.Empty) {
						ShowDependencies (v);
						count++;
					}
				}

				if (count == 0)
					Console.WriteLine ("\nUnable to find vertex: {0}", raw);
				else
					Console.WriteLine ("\nFound {0} matches", count);
			} else
				ShowDependencies (vertex);
		}

		void ShowFlatDependencies (VertexData vertex)
		{
			bool first = true;
			var flatDeps = GetAllDependencies (vertex);

			Console.WriteLine ();

			foreach (var d in flatDeps) {
				var dSize = SpaceAnalyzer == null ? 0 : SpaceAnalyzer.GetSize (d.Item1);
				if (first) {
					var sizeStr = dSize > 0 ? $" [size: {dSize}]" : "";
					Console.WriteLine ($"Distance | {d.Item1.value} [total deps: {flatDeps.Count}]{sizeStr}");
					Line ();
					first = false;
					continue;
				}
				var sizeStr2 = dSize > 0 ? $" [size: {dSize}]" : "";
				Console.WriteLine ($"{string.Format ("{0,8}", d.Item2)} | {d.Item1.value}{d.Item1.DepsCount}{sizeStr2}");
			}
		}

		string SizeString (VertexData vertex)
		{
			return SpaceAnalyzer == null ?
				"" : string.Format (" size: {0}", SpaceAnalyzer.GetSize (vertex));
		}

		public void WriteVertexDeps(VertexData vertex)
		{
			depJsonWriter?.Flush ();
			depJsonWriter?.WriteStartObject ();
			depJsonWriter?.WriteNumber ("index", vertex.index);
			depJsonWriter?.WriteString("type", vertex.type);
			depJsonWriter?.WriteNumber ("deps", vertex.DepsNumber);
		}

		public void ShowDependencies (VertexData vertex, bool useSize = false)
		{
			if (FlatDeps) {
				ShowFlatDependencies (vertex);

				return;
			}

			Header ("{0} dependencies", vertex.value);
			WriteVertexDeps(vertex);

			if (vertex.parentIndexes == null) {
				Console.WriteLine ("Root dependency");
			} else {
				
				depJsonWriter?.WritePropertyName ("children");
				depJsonWriter?.WriteStartArray ();
				int i = 0, size = 0, totalSize = 0;
				
				foreach (int index in vertex.parentIndexes) {
					Console.WriteLine ("Dependency #{0}", ++i);
					Console.WriteLine ($"\t{vertex.value}{SizeString (vertex)}");
					var childVertex = Vertex (index);
					Console.WriteLine ("\t| {0}{1}", childVertex.value, childVertex.DepsCount);
					
					// writeVertexDeps (childVertex);
					depJsonWriter?.Flush ();
					depJsonWriter?.WriteStartObject ();
					depJsonWriter?.WriteNumber ("index", childVertex.index);
					depJsonWriter?.WriteString("type", childVertex.type);
					depJsonWriter?.WriteNumber ("deps", childVertex.DepsNumber);
					depJsonWriter?.WritePropertyName("children");
					depJsonWriter?.WriteStartArray ();

					int depSize = 0;
					int childSize = SpaceAnalyzer.GetSize( childVertex );

					while (childVertex.parentIndexes != null) {
						childVertex = Vertex (childVertex.parentIndexes [0]);
						Console.WriteLine ("\t| {0}{1}", childVertex.value, childVertex.DepsCount);

						WriteVertexDeps(childVertex);
						
						int parentSize = 0;

						if (childVertex.parentDeps != null) {
							depJsonWriter?.WritePropertyName ("children");
							depJsonWriter?.WriteStartArray ();
							// depJsonWriter?.WriteStartObject();
							foreach (VertexData parent in childVertex.parentDeps) {
								// writeVertexDeps (parent);
								size = SpaceAnalyzer.GetSize ( parent );
								// depJsonWriter?.WriteNumber ("size", size);
								depJsonWriter?.WriteNumberValue (parent.index);
								parentSize += size;
								// depJsonWriter?.WriteNull ("children");
								// depJsonWriter?.WriteEndObject ();
							}
							// depJsonWriter?.WriteEndObject();
							depJsonWriter?.WriteEndArray ();
						} else {
							depJsonWriter?.WriteNull ("children");
						}

						// SHOULD ADD SIZES OF DEPS TO VERTEX SIZE?
						size = SpaceAnalyzer.GetSize ( childVertex ) + parentSize;
						if (useSize) depJsonWriter?.WriteNumber ("size", size);
						depJsonWriter?.WriteEndObject ();
						depSize += size;
					}

					depJsonWriter?.WriteEndArray ();

					size = depSize + childSize;
					if (useSize) depJsonWriter?.WriteNumber ("size", size);
					depJsonWriter?.WriteEndObject ();
					totalSize += size;
					if (Tree)
						break;
				}
				
				depJsonWriter?.WriteEndArray ();
				if (useSize) depJsonWriter?.WriteNumber("size", totalSize);
				
			}
			depJsonWriter?.WriteEndObject();
			
		}

		public void GetDependencyArray (VertexData vertex) {

			depJsonWriter?.WriteStartObject ();
			depJsonWriter?.WriteString ("name", vertex.name);
			if (vertex.parentIndexes != null) {
				depJsonWriter?.WritePropertyName ("dependencies");
				depJsonWriter?.WriteStartArray ();
				JsonSerializer.Serialize (depJsonWriter, vertex.parentIndexes);
				depJsonWriter?.WriteEndArray ();
			}
			depJsonWriter?.WriteEndObject ();
		}

		public void ShowAllDependencies ()
		{
			depJsonWriter = null;

			Header ("All dependencies");
			Console.WriteLine ("Types count: {0}", vertices.Count);
			if (depJsonOutputFileName != null) {
				depJsonWriter = new Utf8JsonWriter (new FileStream(depJsonOutputFileName, FileMode.Create, FileAccess.Write, FileShare.Read));
			}

			using (depJsonWriter) {
				depJsonWriter?.WriteStartArray ();

				foreach (var vertex in vertices)
					ShowDependencies (vertex);

				depJsonWriter?.WriteEndArray ();
			}


			if (indexJsonOutputFileName != null) {
				indexJsonWriter = new Utf8JsonWriter (new FileStream(indexJsonOutputFileName, FileMode.Create, FileAccess.Write, FileShare.Read));
				JsonSerializer.Serialize(indexJsonWriter, indexes); 
			}

			DependencyWheel();
		}

		public void DependencyWheel() 
		{
			depJsonWriter = null;

			if (depJsonOutputFileName != null) {
				depJsonWriter = new Utf8JsonWriter (new FileStream(wheelJsonOutputFileName, FileMode.Create, FileAccess.Write, FileShare.Read));
			}

			using (depJsonWriter) {
				// depJsonWriter?.WritePropertyName ("packageNames");
				// depJsonWriter?.WriteStartArray ();
				// JsonSerializer.Serialize(depJsonWriter, indexes.Keys.ToList());
				// // foreach (var key in indexes.Keys.ToList()) 
				// // 	depJsonWriter?.WriteStringValue(key);
				// depJsonWriter?.WriteEndArray ();

				depJsonWriter?.WriteStartArray ();

				// limitVertices = vertices.Where((v => (v.parentIndexes != null) && (v.parentIndexes.Count > 5))).ToList();
				// limitIndexes = limitVertices.Select((v, index) => new {v.value, index})
				// 							.ToDictionary(x => x.value, x => x.index);

				foreach (var vertex in vertices)
					GetDependencyArray (vertex);


				depJsonWriter?.WriteEndArray (); 

			}
		}

		public void OutputDepJson(string fileName) 
		{		
			depJsonOutputFileName = fileName + "dependency.json";
			indexJsonOutputFileName = fileName + "index.json";
			wheelJsonOutputFileName = fileName + "wheel.json";
		}

		public void ShowTypesDependencies ()
		{
			Header ("All types dependencies");
			Console.WriteLine ("Deps count: {0}", Types.Count);
			foreach (var type in Types)
				ShowDependencies (type);
		}

		string Tabs (string key)
		{
			int count = Math.Max (1, 2 - key.Length / 8);

			if (count == 1)
				return "\t";
			else
				return "\t\t";
		}

		public void ShowStat (bool verbose = false)
		{
			Header ("Statistics");
			if (verbose) {
				foreach (var key in counts.Keys)
					Console.WriteLine ("Vertex type:\t{0}{1}count:{2}", key, Tabs (key), counts [key]);
			} else {
				Console.WriteLine ("Assemblies:\t{0}", counts ["Assembly"]);
				Console.WriteLine ("Modules:\t{0}", counts ["Module"]);
				Console.WriteLine ("Types:\t\t{0}", counts ["TypeDef"]);
				Console.WriteLine ("Fields:\t\t{0}", counts ["Field"]);
				Console.WriteLine ("Methods:\t{0}", counts ["Method"]);
			}

			Console.WriteLine ();
			Console.WriteLine ("Total vertices: {0}", vertices.Count);
		}

		public void ShowRoots ()
		{
			Header ("Root vertices");

			int count = 0;
			foreach (var vertex in vertices) {
				if (vertex.parentIndexes == null) {
					Console.WriteLine ("{0}", vertex.value);
					count++;
				}
			}

			Console.WriteLine ();
			Console.WriteLine ("Total root vertices: {0}", count);
		}

		public void ShowRawDependencies (string raw)
		{
			Header ("Raw dependencies: '{0}'", raw);
			ShowDependencies (raw, vertices, raw);
		}

		public void ShowTypeDependencies (string raw)
		{
			Header ("Type dependencies: '{0}'", raw);
			ShowDependencies ("TypeDef:" + raw, Types, raw);
		}

		static readonly string line = new string ('-', 72);

		void Line ()
		{
			Console.Write (line);
			Console.WriteLine ();
		}

		static public void Header (string header, params object[] values)
		{
			string formatted = string.Format (header, values);
			Console.WriteLine ();
			Console.WriteLine ($"--- {formatted} {new string ('-', Math.Max (3, 67 - formatted.Length))}");
		}
	}
}
