using UnityEngine;
using UnityEditor.PackageManager;

namespace MiniIT.Snipe.Editor
{
	public static class AdvertisingIdFetcherInstaller
	{
		public static void CheckAndInstall(PackageCollection installedPackages)
		{
			Debug.Log("[AdvertisingIdFetcherInstaller] Checking package installed");

			foreach (var item in installedPackages)
			{
				if (item.name == Packages.AdvertisingIdFetcher.Name)
				{
					Debug.Log("[AdvertisingIdFetcherInstaller] Found");
					return;
				}
			}

			Debug.Log("[AdvertisingIdFetcherInstaller] Installing AdvertisingIdFetcher package");
			Client.Add($"{Packages.AdvertisingIdFetcher.Url}");
		}
	}
}