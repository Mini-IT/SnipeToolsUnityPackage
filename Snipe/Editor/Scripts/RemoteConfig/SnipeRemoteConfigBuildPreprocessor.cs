using System.Threading.Tasks;
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

			var task = Task.Run(SnipeRemoteConfigDownloader.DownloadAndSaveDefaultConfig);
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
	}
}