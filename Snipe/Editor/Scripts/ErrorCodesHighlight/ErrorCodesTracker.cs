using System.Collections.Generic;
using MiniIT.Snipe.Debugging;
using Snipe.Services.Analytics;

namespace MiniIT.Snipe.Unity.Editor
{
	public class ErrorCodesTracker : ISnipeErrorsTracker, ITestSnipeErrorTracker
	{
		internal List<IDictionary<string, object>> Items { get; } = new ();

		public void TrackNotOk(IDictionary<string, object> properties)
		{
			Items.Add(properties);
		}

		public void Clear()
		{
			Items.Clear();
		}

		public IReadOnlyList<IDictionary<string, object>> GetItems()
		{
			return Items;
		}
	}
}
