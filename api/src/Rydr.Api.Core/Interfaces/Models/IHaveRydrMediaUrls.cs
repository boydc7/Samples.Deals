using Rydr.Api.Dto.Enums;
using Rydr.Api.Dto.Interfaces;

namespace Rydr.Api.Core.Interfaces.Models;

public interface IHaveMediaUrls
{
    string Caption { get; }
    string MediaUrl { get; set; }
    string ThumbnailUrl { get; set; }
    PublisherContentType ContentType { get; }
}

public interface IGenerateMediaUrls : IHaveMediaUrls, IHasPublisherAccountId
{
    long UrlsGeneratedOn { get; set; }
    long RydrMediaId { get; }
    bool IsPermanentMedia { get; }
}

public interface IGenerateFileMediaUrls : IGenerateMediaUrls
{
    long MediaFileId { get; }
}
