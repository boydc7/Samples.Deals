namespace Rydr.Api.Core.Enums;

public class FileStorageOptions
{
    public FileStorageClass StorageClass { get; set; }
    public bool Encrypt { get; set; }
    public string ContentType { get; set; }
    public string ContentEncoding { get; set; }
    public FileStorageContentDisposition ContentDisposition { get; set; }
}

public enum FileStorageClass
{
    Standard = 0,
    Intelligent = 1,
    InfrequentAccess = 2,
    Archive = 3
}

public enum FileStorageContentDisposition
{
    Inline = 0,
    Attachment
}

public enum FileStorageTag
{
    None,
    Lifecycle,
    Privacy
}

public static class FileStorageTags
{
    public const string LifecyclePurge = "purge";
    public const string LifecycleKeep = "keep";
    public const string PrivacyPrivate = "private";
    public const string PrivacyPublic = "public";
}
