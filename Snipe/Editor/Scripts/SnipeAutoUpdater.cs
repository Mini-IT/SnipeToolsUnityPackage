#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace MiniIT.Snipe.Unity.Editor
{
#if !UNITY_CLOUD_BUILD
	[InitializeOnLoad]
#endif
	public static class SnipeAutoUpdater
	{
		private const string PREF_AUTO_UPDATE_ENABLED = "Snipe.AutoUpdateEnabled";
		private const string PREF_LAST_UPDATE_CHECK_ID = "Snipe.LastUpdateCheckId";
		private const string PREF_LAST_UPDATE_CHECK_TS = "Snipe.LastUpdateCheckTS";

		private const string MENU_AUTO_UPDATE_ENABLED = "Snipe/Check for Updates Automatically";

		private static bool s_processing = false;
		private static PackageCollection s_installedPackages;

		public static bool AutoUpdateEnabled
		{
			get => EditorPrefs.GetBool(PREF_AUTO_UPDATE_ENABLED, true);
			set => EditorPrefs.SetBool(PREF_AUTO_UPDATE_ENABLED, value);
		}

		[MenuItem(MENU_AUTO_UPDATE_ENABLED, false)]
		static void SnipeAutoUpdaterCheckMenu()
		{
			AutoUpdateEnabled = !AutoUpdateEnabled;
			Menu.SetChecked(MENU_AUTO_UPDATE_ENABLED, AutoUpdateEnabled);

			ShowNotificationOrLog(AutoUpdateEnabled ? "Snipe auto update enabled" : "Snipe auto update disabled");

			if (AutoUpdateEnabled)
			{
				CheckUpdateAvailable();
			}
		}

		// The menu won't be gray out, we use this validate method for update check state
		[MenuItem(MENU_AUTO_UPDATE_ENABLED, true)]
		static bool SnipeAutoUpdaterCheckMenuValidate()
		{
			Menu.SetChecked(MENU_AUTO_UPDATE_ENABLED, AutoUpdateEnabled);
			return true;
		}

		static SnipeAutoUpdater()
		{
#if !UNITY_CLOUD_BUILD
			Run();
#endif
		}

		//[MenuItem("Snipe/Run Autoupdater")]
		public static void Run()
		{
			if (AutoUpdateEnabled)
			{
				bool checkNeeded = EditorPrefs.GetInt(PREF_LAST_UPDATE_CHECK_ID, 0) != (int)EditorAnalyticsSessionInfo.id;
				if (!checkNeeded)
				{
					var checkTs = EditorPrefs.GetInt(PREF_LAST_UPDATE_CHECK_TS, 0);
					var passed = DateTime.UtcNow - DateTimeOffset.FromUnixTimeSeconds(checkTs).UtcDateTime;
					checkNeeded = passed.TotalHours >= 12;
				}

				if (checkNeeded)
				{
					CheckUpdateAvailable();
				}
			}
		}

		private static void ShowNotificationOrLog(string msg)
		{
			if (Resources.FindObjectsOfTypeAll<SceneView>().Length > 0)
				EditorWindow.GetWindow<SceneView>().ShowNotification(new GUIContent(msg));
			else
				Debug.Log($"[SnipeAutoUpdater] {msg}"); // When there's no scene view opened, we just print a log
		}

		public static async void CheckUpdateAvailable()
		{
			if (s_processing)
				return;
			s_processing = true;

			Debug.Log("[SnipeAutoUpdater] CheckUpdateAvailable");

			SnipeUpdater.InstalledPackagesListFetched -= OnInstalledPackagesListFetched;
			SnipeUpdater.InstalledPackagesListFetched += OnInstalledPackagesListFetched;
			await SnipeUpdater.FetchVersionsList();

			if (SnipeUpdater.SnipePackageVersions != null && SnipeUpdater.SnipePackageVersions.Length > 0)
			{
				string currentVersionCode = SnipeUpdater.CurrentSnipePackageVersionIndex >= 0 ?
					SnipeUpdater.SnipePackageVersions[SnipeUpdater.CurrentSnipePackageVersionIndex] :
					"unknown";

				Debug.Log($"[SnipeAutoUpdater] Current version (detected): {currentVersionCode}");

				if (TryParseVersion(currentVersionCode, out int[] version))
				{
					string newerVersionCode = null;
					int[] newerVersion = null;

					for (int i = 0; i < SnipeUpdater.SnipePackageVersions.Length; i++)
					{
						if (i == SnipeUpdater.CurrentSnipePackageVersionIndex)
							continue;

						string verName = SnipeUpdater.SnipePackageVersions[i];
						if (TryParseVersion(verName, out int[] ver) && CheckVersionGreater(newerVersion ?? version, ver))
						{
							newerVersionCode = verName;
							newerVersion = ver;
						}
					}

					if (!string.IsNullOrEmpty(newerVersionCode))
					{
						Debug.Log($"[SnipeAutoUpdater] A newer version found: {newerVersionCode}");

						if (EditorUtility.DisplayDialog("Snipe Auto Updater",
							$"Snipe {newerVersionCode}\n\nNewer version found.\n(Installed version is {currentVersionCode})",
							"Update now", "Dismiss"))
						{
							SnipeUpdater.InstallSnipePackage(newerVersionCode);
						}
					}
				}
			}

			EditorPrefs.SetInt(PREF_LAST_UPDATE_CHECK_ID, (int)EditorAnalyticsSessionInfo.id);
			EditorPrefs.SetInt(PREF_LAST_UPDATE_CHECK_TS, (int)new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds());

			s_processing = false;

			SnipeToolsAutoUpdater.CheckUpdateAvailable(s_installedPackages);
#if UNITY_2020_1_OR_NEWER
			AdvertisingIdFetcherInstaller.CheckAndInstall(s_installedPackages);
#endif
		}

		private static void OnInstalledPackagesListFetched(PackageCollection installedPackages)
		{
			SnipeUpdater.InstalledPackagesListFetched -= OnInstalledPackagesListFetched;
			s_installedPackages = installedPackages;
		}

		internal static bool TryParseVersion(string versionString, out int[] version)
		{
			string[] versionCode = versionString.Split('.');
			if (versionCode != null && versionCode.Length == 3)
			{
				version = new int[versionCode.Length];
				bool parsingFailed = false;
				for (int i = 0; i < versionCode.Length; i++)
				{
					if (!int.TryParse(versionCode[i], out version[i]))
					{
						parsingFailed = true;
						break;
					}
				}

				if (!parsingFailed)
				{
					return true;
				}
			}

			version = null;
			return false;
		}

		internal static bool CheckVersionGreater(int[] currentVersion, int[] checkVersion)
		{
			if (checkVersion[0] < currentVersion[0])
				return false;
			if (checkVersion[0] > currentVersion[0])
				return true;
			if (checkVersion[1] < currentVersion[1])
				return false;
			if (checkVersion[1] > currentVersion[1])
				return true;
			if (checkVersion[2] < currentVersion[2])
				return false;
			return (checkVersion[2] > currentVersion[2]);
		}
	}

}
#endif
