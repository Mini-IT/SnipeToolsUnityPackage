using System.Collections.Generic;
using MiniIT.Snipe.Debugging;

namespace MiniIT.Snipe.Unity.Editor
{
	public class ErrorCodesTracker : ISnipeErrorsTracker
	{
		public List<IDictionary<string, object>> Items { get; } = new ();

		public void TrackNotOk(IDictionary<string, object> properties)
		{
			Items.Add(properties);
		}

		public void Clear()
		{
			Items.Clear();
		}
	}
}
