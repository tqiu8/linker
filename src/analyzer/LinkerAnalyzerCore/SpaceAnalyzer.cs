// SpaceAnalyzer.cs
//
// Author:
//  Radek Doulik <radou@microsoft.com>
//
// Copyright (C) 2018 Microsoft Corporation (http://www.microsoft.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Mono.Cecil;

namespace LinkerAnalyzer.Core
{
	public class SpaceAnalyzer
	{
		private readonly string assembliesDirectory;
		private readonly List<AssemblyDefinition> assemblies = new List<AssemblyDefinition> ();
		private readonly Dictionary<string, int> sizes = new Dictionary<string, int> ();
		private string sizeJsonOutputFileName = null;
		private Utf8JsonWriter sizeJsonWriter = null;

		public SpaceAnalyzer (string assembliesDirectory)
		{
			this.assembliesDirectory = assembliesDirectory;
		}

		public void OutputSizeJson(string fileName)
		{
			sizeJsonOutputFileName = fileName;
		}

		static bool IsAssemblyBound (TypeDefinition td)
		{
			do {
				if (td.IsNestedPrivate || td.IsNestedAssembly || td.IsNestedFamilyAndAssembly)
					return true;

				td = td.DeclaringType;
			} while (td != null);

			return false;
		}

		string GetTypeKey (TypeDefinition td)
		{
			if (td == null)
				return "";

			var addAssembly = td.IsNotPublic || IsAssemblyBound (td);

			var addition = addAssembly ? $":{td.Module}" : "";
			return $"{td.MetadataToken.TokenType}:{td}{addition}";
		}

		string GetKey (IMetadataTokenProvider provider)
		{
			return $"{provider.MetadataToken.TokenType}:{provider}";
		}

		int GetMethodSize (MethodDefinition method)
		{
			sizeJsonWriter?.Flush ();

			sizeJsonWriter?.WriteStartObject ();
			sizeJsonWriter?.WriteString ("type", "method");
			sizeJsonWriter?.WriteString ("name", method.ToString ());
			sizeJsonWriter?.WriteNull ("children");

			var key = GetKey (method);

			int msize;
			if (sizes.ContainsKey (key)) {
				msize = sizes [key];
			} else {
				msize = method.Body.CodeSize;
				msize += method.Name.Length;

				sizes.Add (key, msize);
			}

			sizeJsonWriter?.WriteNumber ("size", msize);
			sizeJsonWriter?.WriteEndObject ();

			sizeJsonWriter?.Flush ();

			return msize;
		}

		int ProcessType (TypeDefinition type)
		{
			sizeJsonWriter?.WriteStartObject ();
			sizeJsonWriter?.WriteString ("type", "class");
			sizeJsonWriter?.WriteString ("name", type.ToString());
			sizeJsonWriter?.WritePropertyName ("children");
			sizeJsonWriter?.WriteStartArray ();

			sizeJsonWriter?.Flush ();

			int size = type.Name.Length;

			foreach (var field in type.Fields)
				size += field.Name.Length;

			foreach (var method in type.Methods) {
				method.Resolve ();
				if (method.Body != null)
					size += GetMethodSize (method);
			}

			try {
				sizes.Add (GetTypeKey (type), size);
			} catch (ArgumentException e) {
				Console.WriteLine ($"\nWarning: duplicated type '{type}' scope '{type.Scope}'\n{e}");
			}

			sizeJsonWriter?.WriteEndArray ();
			sizeJsonWriter?.WriteNumber ("size", size);
			sizeJsonWriter?.WriteEndObject ();

			return size;
		}

		public void LoadAssemblies (bool verbose = true)
		{
			if (verbose) {
				ConsoleDependencyGraph.Header ("Space analyzer");
				Console.WriteLine ("Load assemblies from {0}", assembliesDirectory);
			} else
				Console.Write ("Analyzing assemblies .");

			var resolver = new DefaultAssemblyResolver ();
			resolver.AddSearchDirectory (assembliesDirectory);

			sizeJsonWriter = null;
			if (sizeJsonOutputFileName != null) {
				sizeJsonWriter = new Utf8JsonWriter (new FileStream(sizeJsonOutputFileName, FileMode.Create, FileAccess.Write, FileShare.Read));
			}

			using (sizeJsonWriter) {
				sizeJsonWriter?.WriteStartArray ();

				int totalSize = 0;
				foreach (var file in System.IO.Directory.GetFiles (assembliesDirectory, "*.dll")) {
					if (verbose)
						Console.WriteLine ($"Analyzing {file}");
					else
						Console.Write (".");

					ReaderParameters parameters = new ReaderParameters () { ReadingMode = ReadingMode.Immediate, AssemblyResolver = resolver };
					var assembly = AssemblyDefinition.ReadAssembly (file, parameters);
					assemblies.Add (assembly);

					sizeJsonWriter?.WriteStartObject ();
					sizeJsonWriter?.WriteString ("type", "assembly");
					sizeJsonWriter?.WriteString ("name", assembly.Name.Name);

					sizeJsonWriter?.WritePropertyName ("children");
					sizeJsonWriter?.WriteStartArray ();
					int assemblySize = 0;
					foreach (var module in assembly.Modules) {
						foreach (var type in module.Types) {
							assemblySize += ProcessType (type);
							foreach (var child in type.NestedTypes)
								assemblySize += ProcessType (child);
						}
					}

					sizeJsonWriter?.WriteEndArray ();

					sizeJsonWriter?.WriteNumber ("size", assemblySize);
					sizeJsonWriter?.WriteEndObject ();
					totalSize += assemblySize;
				}

				if (verbose)
					Console.WriteLine ("Total known size: {0}", totalSize);
				else
					Console.WriteLine ();

				sizeJsonWriter?.WriteEndArray ();
			}
		}

		public int GetSize (VertexData vertex)
		{
			if (sizes.ContainsKey (vertex.value))
				return sizes [vertex.value];
			return 0;
		}
	}
}
