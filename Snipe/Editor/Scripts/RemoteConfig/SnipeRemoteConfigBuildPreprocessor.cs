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
				Debug.Log($"[{nameof(SnipeRemoteConfigBuildPreprocessor)}] Default config - Auto load disabled!");
				return;
			}

			Debug.Log($"[{nameof(SnipeRemoteConfigBuildPreprocessor)}] Downloading default config...");
			
			SnipeRemoteConfigDownloader.DownloadAndSaveDefaultConfig()
				.ContinueWith((task) =>
				{
#if UNITY_CLOUD_BUILD
					if (!task.IsCompleted || string.IsNullOrEmpty(task.Result))
					{
						throw new BuildFailedException($"[{nameof(SnipeRemoteConfigBuildPreprocessor)}] Failed to download default config");
					}
#endif
					Debug.Log($"[{nameof(SnipeRemoteConfigBuildPreprocessor)}] OnPreprocessBuild - Finished");
				});
		}
	}
}