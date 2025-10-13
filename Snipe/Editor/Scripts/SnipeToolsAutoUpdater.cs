#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

using Debug = UnityEngine.Debug;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace MiniIT.Snipe.Unity.Editor
{
#if !UNITY_CLOUD_BUILD
	[InitializeOnLoad]
#endif
	public static class SnipeToolsAutoUpdater
	{
		private const string GIT_API_URL = "https://api.github.com/repos/Mini-IT/SnipeToolsUnityPackage/";

		private static bool s_processing = false;
		private static List<string> s_packageVersions;
		private static int s_currentPackageVersionIndex = -1;
		private static ListRequest s_packageListRequest;

		//[MenuItem("Snipe/Check for SnipeTools Update")]
		public static void CheckUpdateAvailable()
		{
			CheckUpdateAvailable(null);
		}

		public static async void CheckUpdateAvailable(PackageCollection intalledPackages)
		{
#if UNITY_CLOUD_BUILD
			return;
#endif

			if (s_processing)
				return;
			s_processing = true;

			Debug.Log("[SnipeToolsAutoUpdater] CheckUpdateAvailable");

			await FetchVersionsList();

			if (s_packageVersions != null && s_packageVersions.Count > 0)
			{
				string currentVersionCode = s_currentPackageVersionIndex >= 0 ?
					s_packageVersions[s_currentPackageVersionIndex] :
					"unknown";

				Debug.Log($"[SnipeToolsAutoUpdater] Current version (detected): {currentVersionCode}");

				if (SnipeAutoUpdater.TryParseVersion(currentVersionCode, out int[] version))
				{
					string newerVersionCode = null;
					int[] newerVersion = null;

					for (int i = 0; i < s_packageVersions.Count; i++)
					{
						if (i == s_currentPackageVersionIndex)
							continue;

						string verName = s_packageVersions[i];
						if (SnipeAutoUpdater.TryParseVersion(verName, out int[] ver) && SnipeAutoUpdater.CheckVersionGreater(newerVersion ?? version, ver))
						{
							newerVersionCode = verName;
							newerVersion = ver;
						}
					}

					if (!string.IsNullOrEmpty(newerVersionCode))
					{
						Debug.Log($"[SnipeToolsAutoUpdater] A newer version found: {newerVersionCode}");

						if (EditorUtility.DisplayDialog("Snipe Tools Auto Updater",
							$"Snipe Tools {newerVersionCode}\n\nNewer version of Snipe Tools found\n(Installed version is {currentVersionCode})",
							"Update now", "Dismiss"))
						{
							SnipeUpdater.InstallSnipeToolsPackage();
						}
					}
				}
			}

			s_processing = false;
		}

		private static async Task FetchVersionsList(PackageCollection intalledPackages = null)
		{
			Debug.Log("[SnipeToolsAutoUpdater] FetchVersionsList - GetBranchesList - start");

			var branches = await SnipeUpdater.RequestList<GitHubBranchesListWrapper>(GIT_API_URL, "branches");
			var tags = await SnipeUpdater.RequestList<GitHubTagsListWrapper>(GIT_API_URL, "tags");

			int itemsCount = (branches?.items?.Count ?? 0) + (tags?.items?.Count ?? 0);
			s_packageVersions = new List<string>(itemsCount);

			if (branches?.items != null)
			{
				foreach (var item in branches.items)
				{
					s_packageVersions.Add(item.name);
				}
			}

			if (tags?.items != null)
			{
				foreach (var item in tags.items)
				{
					s_packageVersions.Add(item.name);
				}
			}

			Debug.Log("SnipeToolsAutoUpdater] FetchVersionsList - GetBranchesList - done");

			Debug.Log("SnipeToolsAutoUpdater] FetchVersionsList - Check installed packages");

			if (intalledPackages != null)
			{
				RefreshCurrentPackageVersionIndex(intalledPackages);
			}
			else
			{
				s_packageListRequest = UnityEditor.PackageManager.Client.List(false, false);
				EditorApplication.update -= OnEditorUpdate;
				EditorApplication.update += OnEditorUpdate;

				while (s_packageListRequest != null)
				{
					await Task.Delay(100);
				}
			}
		}

		private static void OnEditorUpdate()
		{
			if (s_packageListRequest == null)
			{
				EditorApplication.update -= OnEditorUpdate;
				return;
			}

			if (!s_packageListRequest.IsCompleted)
			{
				return;
			}

			if (s_packageListRequest.Status == StatusCode.Success)
			{
				RefreshCurrentPackageVersionIndex(s_packageListRequest.Result);
			}
			else if (s_packageListRequest.Status >= StatusCode.Failure)
			{
				Debug.Log($"[SnipeToolsAutoUpdater] Search failed : {s_packageListRequest.Error.message}");
			}

			s_packageListRequest = null;
			EditorApplication.update -= OnEditorUpdate;
		}

		private static UnityEditor.PackageManager.PackageInfo GetPackageInfo(PackageCollection intalledPackages, string packageName)
		{
			foreach (var item in intalledPackages)
			{
				if (item.name == packageName)
				{
					return item;
				}
			}

			return null;
		}

		private static void RefreshCurrentPackageVersionIndex(PackageCollection intalledPackages)
		{
			var package = GetPackageInfo(intalledPackages, Packages.SnipeTools.Name);
			if (package != null)
			{
				Debug.Log($"[SnipeToolsAutoUpdater] found package: {package.name} {package.version} {package.packageId}");

				int index = package.packageId.LastIndexOf(".git#", StringComparison.Ordinal);
				if (index > 0)
				{
					string packageVersion = package.packageId.Substring(index + ".git#".Length);
					s_currentPackageVersionIndex = s_packageVersions.IndexOf(packageVersion);
				}
				else
				{
					s_currentPackageVersionIndex = s_packageVersions.IndexOf(package.version);
				}
			}
		}
	}

}
#endif
