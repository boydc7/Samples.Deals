using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.Sockets;
using Rydr.Api.Core.Models;
using Rydr.Api.Core.Services.Internal;
using Rydr.Api.Dto;
using ServiceStack;

namespace Rydr.Api.Core.Extensions;

public static class WebServiceExtensions
{
    public const string RydrPublisherAccountIdHeaderName = "X-Rydr-PublisherAccountId";
    public const string RydrWorkspaceIdName = "X-Rydr-WorkspaceId";

    public static OnlyResultResponse<T> AsOnlyResultResponse<T>(this T result)
        where T : class
        => new()
           {
               Result = result
           };

    public static async Task<OnlyResultResponse<T>> AsOnlyResultResponseAsync<T>(this Task<T> asyncResult)
        where T : class
    {
        var result = await asyncResult;

        return new OnlyResultResponse<T>
               {
                   Result = result
               };
    }

    public static OnlyResultsResponse<T> AsOnlyResultsResponse<T>(this IEnumerable<T> results, long? totalCount = null)
        where T : class
        => new()
           {
               Results = results?.AsListReadOnly(),
               TotalCount = totalCount.NullIfNotPositive()
           };

    public static async Task<OnlyResultsResponse<T>> AsOnlyResultsResponseAsync<T>(this IAsyncEnumerable<T> asyncResults)
        where T : class
        => new()
           {
               Results = asyncResults == null
                             ? null
                             : await asyncResults.ToListReadOnly()
           };

    public static Image GetImage(this string fromUrl)
    {
        if (fromUrl.IsNullOrEmpty())
        {
            return null;
        }

        try
        {
            using(var webClient = new WebClient())
            using(var stream = webClient.OpenRead(fromUrl))
            {
                return Image.FromStream(stream);
            }
        }
        catch(HttpRequestException) { }
        catch(WebException) { }
        catch(SocketException) { }
        catch(IOException) { }

        return null;
    }

    public static async Task DownloadFromUrl(this string fromUrl, FileMetaData toLocalFileMeta)
    {
        if (fromUrl.IsNullOrEmpty())
        {
            return;
        }

        try
        {
            PathHelper.Create(toLocalFileMeta.FolderName);

            using(var webClient = new WebClient())
            {
                await webClient.DownloadFileTaskAsync(fromUrl, toLocalFileMeta.FullName);
            }
        }
        catch(HttpRequestException) { }
        catch(WebException) { }
        catch(SocketException) { }
        catch(IOException) { }
    }

    public static byte[] ToBytes(this Image image, ImageFormat format = null)
    {
        if (image == null)
        {
            return null;
        }

        using(var memStream = new MemoryStream())
        {
            image.Save(memStream, format ?? image.RawFormat);

            return memStream.ToArray();
        }
    }
}
