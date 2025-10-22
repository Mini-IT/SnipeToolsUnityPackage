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

		public string DeviceManufacturer { get; }
		public string OperatingSystemFamily { get; }
		public string OperatingSystemVersion { get; }

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

			var info = SystemInformationExtractor.Instance.GetSystemInfo();
			DeviceManufacturer = info.DeviceManufacturer;
			OperatingSystemFamily = info.OperatingSystemFamily;
			OperatingSystemVersion = info.OperatingSystemVersion;
		}
	}
}
