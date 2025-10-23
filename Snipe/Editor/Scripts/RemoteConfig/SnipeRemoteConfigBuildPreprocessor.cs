using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MiniIT.Snipe.Unity.Editor
{
	public class SnipeRemoteConfigBuildPreprocessor : IPreprocessBuildWithReport
	{
		public int callbackOrder => 11;

		public void OnPreprocessBuild(BuildReport report)
		{
			Debug.Log($"[{nameof(SnipeRemoteConfigBuildPreprocessor)}] OnPreprocessBuild - Started");

			SnipeToolsConfig.Load();

			if (!SnipeToolsConfig.LoadDefaultConfigOnBuild)
			{
				Debug.Log($"[{nameof(SnipeRemoteConfigBuildPreprocessor)}] Default config autoloading disabled");
				return;
			}

			Debug.Log($"[{nameof(SnipeRemoteConfigBuildPreprocessor)}] Downloading default config...");

			string targetPlatform = GetPlatformString(report.summary.platform);
			var task = Task.Run(async () => await SnipeRemoteConfigDownloadWindow.DownloadAndSaveDefaultConfig(targetPlatform));
			task.Wait();

			if (!task.IsCompletedSuccessfully || string.IsNullOrEmpty(task.Result))
			{
				Debug.LogError($"[{nameof(SnipeRemoteConfigBuildPreprocessor)}] OnPreprocessBuild - FAILED");

#if UNITY_CLOUD_BUILD
				throw new BuildFailedException($"[{nameof(SnipeRemoteConfigBuildPreprocessor)}] Failed to download default config");
#endif
			}

			Debug.Log($"[{nameof(SnipeRemoteConfigBuildPreprocessor)}] OnPreprocessBuild - Finished");
		}

		private static string GetPlatformString(BuildTarget targetPlatform)
		{
#if STEAM || MINIIT_STEAM || UNITY_STEAM
			return "steam";
#endif

			switch (targetPlatform)
			{
				case BuildTarget.Android:
#if AMAZON_STORE
					return "amazon";
#elif RUSTORE
					return "rustore";
#elif NUTAKU
					return "androidNutaku";
#elif HUAWEI
					return "huawei";
#else
					return "android";
#endif

				case BuildTarget.iOS:
					return "ios";

				case BuildTarget.StandaloneLinux64:
					return "linux";

				case BuildTarget.StandaloneOSX:
					return "macos";

				case BuildTarget.StandaloneWindows:
				case BuildTarget.StandaloneWindows64:
				case BuildTarget.WSAPlayer:
					return "windows";

				case BuildTarget.WebGL:
#if YANDEX
					return "webglYandex";
#elif NUTAKU
					return "webglNutaku";
#else
					return "webgl";
#endif

				case BuildTarget.PS4:
					return "ps4";
				case BuildTarget.PS5:
					return "ps5";
				case BuildTarget.Switch:
					return "switch";

				case BuildTarget.XboxOne:
				case BuildTarget.GameCoreXboxOne:
				case BuildTarget.GameCoreXboxSeries:
					return "xboxone";

				default:
					return targetPlatform.ToString().ToLowerInvariant();
			}
		}
	}
}
