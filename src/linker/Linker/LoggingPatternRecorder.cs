//
// LoggingPatternRecorder.cs
//
// Copyright (C) 2017 Microsoft Corporation (http://www.microsoft.com)
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

using Mono.Cecil;

namespace Mono.Linker
{
	class LoggingPatternRecorder : IPatternRecorder
	{
		private LinkContext _context;

		public LoggingPatternRecorder(LinkContext context)
		{
			_context = context;
		}

		public void RecognizedReflectionEventAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, EventDefinition accessedEvent)
		{
			// Do nothing - there's no logging for successfully recognized patterns
		}

		public void RecognizedReflectionFieldAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, FieldDefinition accessedField)
		{
			// Do nothing - there's no logging for successfully recognized patterns
		}

		public void RecognizedReflectionMethodAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, MethodDefinition accessedMethod)
		{
			// Do nothing - there's no logging for successfully recognized patterns
		}

		public void RecognizedReflectionPropertyAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, PropertyDefinition accessedProperty)
		{
			// Do nothing - there's no logging for successfully recognized patterns
		}

		public void RecognizedReflectionTypeAccessPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, TypeDefinition accessedType)
		{
			// Do nothing - there's no logging for successfully recognized patterns
		}

		public void UnrecognizedReflectionCallPattern (MethodDefinition sourceMethod, MethodDefinition reflectionMethod, string message)
		{
			_context.LogMessage (MessageImportance.Low, message);
		}
	}
}
