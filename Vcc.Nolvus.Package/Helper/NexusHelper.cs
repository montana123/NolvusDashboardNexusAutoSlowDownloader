using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Vcc.Nolvus.Core.Services;

namespace Vcc.Nolvus.Package.Helper
{
    public static class NexusHelper
    {
        private static string session_cookie = "fwroute=1735909542.332.410.251510|b295758090068ae543818c1ba2aeea3e; nexusmods_session=16bf9b4ebb6f96532d85d98fe8e973e0; nexusmods_session_refresh=1735987515; cf_clearance=P0wP64cuQAcz29JgInt7ePoithWAHh2mXDtHKWaiaUw-1735991697-1.2.1.1-O835YGVfqh7r__Yo6DYrT1zZppxFY97bF59pHQiW7fEPB344xPQj5xCrBYOdFBGEz5R.MkIHmI4VOmoX7fow8dLoUoNEC39QQ7MlGX7gV6x0yvOi3ZL6rHcTW9JUBfQElcQLrYmy.lRcKqswVNXs0spAcFf4h8nV38hc7H9rerdZ0ofzDIhfCynbXPnDTR0UFdZyTt9rGQ3jrbm9KhKoCc9T1vhYKWs7Mkh2mGeRhHGEeW4ia326Z9iSO.G.4hpzJewjn_LypNBsXf5RYgnruURhYjwSycJdZnHNCssQ5o0B6jfposYcvM99wx.XGWiy7jRSoqzW9ypFGJMAVKIWUc9vD1W0_tXuDy1kGImjYB_2h_hUhMBimOBoId.zzy20";
        private static string skyrim_game_id = "1704";
        private static string nexus_download_url = "https://www.nexusmods.com/Core/Libs/Common/Managers/Downloads?GenerateDownloadUrl";
        
        public static async Task<string> MakePostRequest(string refererUrl, string fileId)
        {
            string downloadUrl = null;
            HttpClientHandler handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            ServiceSingleton.Logger.Log("FileId: " + fileId);
            ServiceSingleton.Logger.Log("DownloadUrl: " + refererUrl);

            var request = (HttpWebRequest)WebRequest.Create(nexus_download_url);
            request.Accept = "*/*";
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

            request.Headers.Add("Accept-Encoding", "gzip, deflate, br, zstd");
            request.Headers.Add("Accept-Language", "en,de;q=0.9,de-DE;q=0.8,en-US;q=0.7");
            request.Headers.Add("Cache-Control", "no-cache");
            request.Headers.Add("Origin", "https://www.nexusmods.com");
            request.Referer = refererUrl;
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            request.Headers.Add("Cookie", session_cookie);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            var postData = "fid="+ fileId;
            postData += "&game_id=" + skyrim_game_id;
            var data = Encoding.ASCII.GetBytes(postData);

            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
            request.ContentLength = data.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }
            try
            {
                var response = (HttpWebResponse)request.GetResponse();

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ServiceSingleton.Logger.Log($"Request successful to {refererUrl}");
                    string responseBody = new StreamReader(response.GetResponseStream()).ReadToEnd();

                    try
                    {
                        ServiceSingleton.Logger.Log("Response from download: " + responseBody);
                        var responseData = JsonConvert.DeserializeObject<dynamic>(responseBody);

                        downloadUrl = responseData?.url;

                        if (string.IsNullOrEmpty(downloadUrl))
                        {
                            ServiceSingleton.Logger.Log("No download URL found in the response.");
                        }
                    }
                    catch (JsonException)
                    {
                        ServiceSingleton.Logger.Log($"Failed to parse the response as JSON from {refererUrl}");
                    }
                }
                else
                {
                    ServiceSingleton.Logger.Log($"Request failed for {refererUrl} with status code {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                ServiceSingleton.Logger.Log($"Error during POST request: {ex.Message}");
            }

            ServiceSingleton.Logger.Log("Post DownloadUrl: " + downloadUrl);

            return downloadUrl;
        }

    }
}
