using System.Collections.Generic;

namespace Mono.Linker.Analysis
{

	public interface ICallGraph<T>
	{
		ICollection<T> Methods { get; }
		ICollection<(T, T)> Calls { get; }
		ICollection<(T, T)> Overrides { get; }
		bool IsEntry (T t);
		bool IsInteresting (T t);
	}
}
