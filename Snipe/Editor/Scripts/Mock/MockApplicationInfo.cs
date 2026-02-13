#if SNIPE_8_0_OR_NEWER

#if SNIPE_8_1_OR_NEWER || (SNIPE_7_5_OR_NEWER && !SNIPE_8_0_OR_NEWER)
#define SYSTEM_INFO
#endif

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

#if SYSTEM_INFO
		public string DeviceManufacturer { get; }
		public string OperatingSystemFamily { get; }
		public string OperatingSystemVersion { get; }
#endif

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

#if SYSTEM_INFO
			DeviceManufacturer = SystemInfo.deviceModel;
			OperatingSystemFamily = SystemInfo.operatingSystemFamily.ToString();
			OperatingSystemVersion = SystemInfo.operatingSystem;
#endif
		}
	}
}

#endif
