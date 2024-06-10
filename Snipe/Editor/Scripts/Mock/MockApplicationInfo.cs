using UnityEngine;

namespace MiniIT.Snipe.Unity.Editor
{
	public class MockApplicationInfo : IApplicationInfo
	{
		public string ApplicationIdentifier { get; set; }
		public string ApplicationVersion { get; set; }
		public string ApplicationPlatform { get; set; }
		public string DeviceIdentifier { get; set; }
		public string PersistentDataPath { get; set; }
		public string StreamingAssetsPath { get; set; }

		public MockApplicationInfo()
		{
			ApplicationIdentifier = Application.identifier;
			ApplicationVersion = Application.version;

#if AMAZON_STORE
			ApplicationPlatform = Application.platform.ToString() + "Amazon";
#else
			ApplicationPlatform = Application.platform.ToString();
#endif

			DeviceIdentifier = SystemInfo.deviceUniqueIdentifier;
			PersistentDataPath = Application.persistentDataPath;
			StreamingAssetsPath = Application.streamingAssetsPath;
		}
	}
}
