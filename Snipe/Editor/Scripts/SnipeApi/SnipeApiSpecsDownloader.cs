using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace MiniIT.Snipe.Unity.Editor
{
	/// <summary>
	/// Downloads Snipe API specs JSON from the server.
	/// </summary>
	public static class SnipeApiSpecsDownloader
	{
		/// <summary>
		/// Downloads the API specs JSON for the current project.
		/// </summary>
		/// <returns>The specs JSON string, or null if download failed.</returns>
		public static async Task<string> DownloadSpecsAsync(int projectId, string authKey)
		{
			SnipeToolsConfig.Load();

			using (var client = new HttpClient())
			{
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authKey);

				string url = $"https://edit.snipe.dev/api/v1/project/{projectId}/code/meta";

				Debug.Log($"SnipeApiSpecsDownloader.DownloadSpecsAsync - downloading specs from {url}");

				var response = await client.GetAsync(url);

				if (!response.IsSuccessStatusCode)
				{
					Debug.LogError(
						$"SnipeApiSpecsDownloader.DownloadSpecsAsync - FAILED to download Specs; HTTP status: {(int)response.StatusCode} - {response.StatusCode}");
					return null;
				}

				string specsJson = await response.Content.ReadAsStringAsync();
				Debug.Log($"SnipeApiSpecsDownloader.DownloadSpecsAsync - successfully downloaded specs ({specsJson.Length} characters)");
				return specsJson;
			}
		}
	}
}

