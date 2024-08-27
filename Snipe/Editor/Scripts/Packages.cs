
namespace MiniIT.Snipe.Unity.Editor
{
	public class GitPackageInfo
	{
		public string Name;
		public string Url;
	}

	public static class Packages
	{
		public static readonly GitPackageInfo SnipeClient = new GitPackageInfo()
		{
			Name = "com.miniit.snipe.client",
			Url = "https://github.com/Mini-IT/SnipeUnityPackage.git",
		};
		
		public static readonly GitPackageInfo SnipeTools = new GitPackageInfo()
		{
			Name = "com.miniit.snipe.tools",
			Url = "https://github.com/Mini-IT/SnipeToolsUnityPackage.git",
		};
		
		public static readonly GitPackageInfo AdvertisingIdFetcher = new GitPackageInfo()
		{
			Name = "com.miniit.advertising-id",
			Url = "https://github.com/Mini-IT/AdvertisingIdentifierFetcher.git",
		};
	}
}