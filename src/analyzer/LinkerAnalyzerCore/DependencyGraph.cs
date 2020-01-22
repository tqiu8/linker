//
// DependencyGraph.cs: linker dependencies graph
//
// Author:
//   Radek Doulik (rodo@xamarin.com)
//
// Copyright 2015 Xamarin Inc (http://www.xamarin.com).
//
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;

namespace LinkerAnalyzer.Core
{
	public class VertexData {
		public string value;
		public string type;
		public string name;
		public List<int> parentIndexes;
		public int index;
		public List<VertexData> parentDeps;

		public string DepsCount {
			get {
				if (parentIndexes == null || parentIndexes.Count < 1)
					return "";
				return string.Format (" [{0} deps]", parentIndexes.Count);
			}
		}

		public int DepsNumber {
			get {
				if (parentIndexes == null || parentIndexes.Count < 1)
					return 0;
				return parentIndexes.Count;
			}
		}
	}

	public class DependencyGraph
	{
		protected List<VertexData> vertices = new List<VertexData> ();
		public List<VertexData> Types = new List<VertexData> ();
		public Dictionary<string, int> indexes = new Dictionary<string, int> ();
		protected Dictionary<string, int> counts = new Dictionary<string, int> ();
		internal SpaceAnalyzer SpaceAnalyzer { get; set; }

		public void Load (string filename)
		{
			Console.WriteLine ("Loading dependency tree from: {0}", filename);

			try {
				using (var fileStream = File.OpenRead (filename))
				using (var zipStream = new GZipStream (fileStream, CompressionMode.Decompress)) {
					Load (zipStream);
				}
			} catch (Exception) {
				Console.WriteLine ("Unable to open and read the dependencies.");
				Environment.Exit (1);
			}
		}

		void Load (GZipStream zipStream) {
			using (XmlReader reader = XmlReader.Create (zipStream)) {
				while (reader.Read ()) {
					switch (reader.NodeType) {
					case XmlNodeType.Element:
						//Console.WriteLine (reader.Name);
						if (reader.Name == "edge" && reader.IsStartElement ()) {
							string b = reader.GetAttribute ("b");
							string e = reader.GetAttribute ("e");
							// Console.WriteLine ("edge value " + b + "  -->  " + e);

							if (e != b) {
								VertexData begin = Vertex (b, true);
								VertexData end = Vertex (e, true);
								
								if (end.parentIndexes == null) {
									end.parentIndexes = new List<int> ();
									end.parentDeps = new List<VertexData> ();
								}
								
								if (!end.parentIndexes.Contains (begin.index)) {
									end.parentDeps.Add(begin);
									end.parentIndexes.Add (begin.index);
									// Console.WriteLine (" end parent index: {0}", end.parentIndexes);
								}
							}
						}
						break;
					default:
						//Console.WriteLine ("node: " + reader.NodeType);
						break;
					}
				}
			}
		}

		public VertexData Vertex (string vertexName, bool create = false)
		{
			if (indexes.TryGetValue (vertexName, out int index)) {
				return vertices [index];
			} else {
				if (create) {
					index = vertices.Count;
					string prefix = vertexName.Substring (0, vertexName.IndexOf (':'));
					
					VertexData vertex = new VertexData () { value = vertexName, 
															index = index, 
															type = prefix,
															name = VertexName(vertexName) };
					
					vertices.Add (vertex);
					indexes.Add (vertexName, index);
					if (counts.ContainsKey (prefix))
						counts [prefix]++;
					else
						counts [prefix] = 1;
					//Console.WriteLine ("prefix " + prefix + " count " + counts[prefix]);
					if (prefix == "TypeDef") {
						Types.Add (vertex);
					}

					return vertex;
				} else {
					return null;
				}
			}
		}

		public VertexData Vertex (int index)
		{
			return vertices [index];
		}

		public string VertexName (string vertexName)
		{
			return vertexName.Substring(vertexName.IndexOf(':') + 1);
		}

		IEnumerable<Tuple<VertexData, int>> AddDependencies (VertexData vertex, HashSet<int> reachedVertices, int depth)
		{
			reachedVertices.Add (vertex.index);
			yield return new Tuple<VertexData, int> (vertex, depth);

			if (vertex.parentIndexes == null)
				yield break;

			foreach (var pi in vertex.parentIndexes) {
				var parent = Vertex (pi);
				if (reachedVertices.Contains (parent.index))
					continue;

				foreach (var d in AddDependencies (parent, reachedVertices, depth + 1))
					yield return d;
			}
		}

		public List<Tuple<VertexData, int>> GetAllDependencies (VertexData vertex)
		{
			return new List<Tuple<VertexData, int>> (AddDependencies (vertex, new HashSet<int> (), 0));
		}
	}
}

