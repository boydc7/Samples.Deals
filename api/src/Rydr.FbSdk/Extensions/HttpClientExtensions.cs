using System.Net.Http;
using System.Threading.Tasks;
using ServiceStack;

namespace Rydr.FbSdk.Extensions
{
    public static class HttpClientExtensions
    {
        public static async Task<T> GetAsAsync<T>(this HttpClient client, string fromUrl)
        {
            var bytes = await client.GetByteArrayAsync(fromUrl);

            var json = bytes.ToStringEncoded();

            var model = json.FromJson<T>();

            return model;
        }
    }
}
