using System.Globalization;
using Rydr.Api.Core.Extensions;

namespace Rydr.Api.Core.Models;

public class FileMetaData : ICloneable, IEquatable<FileMetaData>
{
#region ICloneable

    object ICloneable.Clone() => Clone();

    public FileMetaData Clone()
    {
        var cloned = new FileMetaData(FullName, Convert.ToChar(DirectorySeparatorCharacter));

        return cloned;
    }

#endregion ICloneable

    public const char WindowsDirectorySeparator = '\\';
    public const char UnixDirectorySeparator = '/';

    // This class is not stored anywhere, simply used to send/retrieve/etc. to/from an IFileStorageProvider

    private string _fileExtension;
    private Dictionary<string, string> _tags;

    public FileMetaData()
    {
        // ReSharper disable once ImpureMethodCallOnReadonlyValueField
        DirectorySeparatorCharacter = Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture);
    }

    public FileMetaData(string path, string fileName, char? dirSeparatorCharacter = null) : this(Path.Combine(path ?? string.Empty, fileName), dirSeparatorCharacter) { }

    public FileMetaData(string filePathAndName, char? dirSeparatorCharacter = null)
    {
        var index = 0;
        var slashPartIndex = -1;
        var backslashPartIndex = -1;
        var slashCount = 0;
        var backslashCount = 0;
        var dirSeparator = string.Empty;

        if (dirSeparatorCharacter.HasValue)
        {
            dirSeparator = dirSeparatorCharacter.Value.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            // Figure out if there are mixed directory markers and adjust to the appropriate one across the board - If we have
            // both path separators, use only the first. If we have only one, use that
            foreach (var sc in filePathAndName)
            {
                if (sc.Equals('/'))
                {
                    if (slashPartIndex < 0)
                    {
                        slashPartIndex = index;
                    }

                    slashCount++;
                }

                if (sc.Equals('\\'))
                {
                    if (backslashPartIndex < 0)
                    {
                        backslashPartIndex = index;
                    }

                    backslashCount++;
                }

                index++;
            }

            var useBackslashDirectorySeparator = slashPartIndex >= 0 && backslashPartIndex >= 0 // Have both separators?
                                                     ? slashCount == backslashCount // YES - Same # of occurrences?
                                                           ? backslashPartIndex < slashPartIndex // YES - Which came first
                                                           : backslashCount > slashCount // NO - Which has most
                                                     : backslashPartIndex >= 0; // NO - Which do we have?

            // Set the character separator to use
            dirSeparator = useBackslashDirectorySeparator
                               ? "\\"
                               : "/";
        }

        DirectorySeparatorCharacter = dirSeparator;
        Init(filePathAndName);
    }

    public FileMetaData(string filePathAndName, char dirSeparatorCharacter)
    {
        DirectorySeparatorCharacter = dirSeparatorCharacter.ToString(CultureInfo.InvariantCulture);
        Init(filePathAndName);
    }

    private void Init(string filePathAndName)
    {
        if (filePathAndName.EndsWithOrdinalCi("\\") || filePathAndName.EndsWithOrdinalCi("/"))
        { // Ends with a path marker - check to see if this is actually a FILE ending with a terminator - if so, remove it
            var isFileTestPath = filePathAndName.TrimEnd('\\', '/');

            var isFileTestExtension = Path.GetExtension(isFileTestPath);

            if (isFileTestExtension.HasValue() &&
                isFileTestPath.EndsWith(isFileTestExtension, StringComparison.OrdinalIgnoreCase))
            {
                filePathAndName = isFileTestPath;
            }
        }

        string pathScrubber(string f) => f.Replace(DirectorySeparatorCharacter.Equals("\\", StringComparison.OrdinalIgnoreCase)
                                                       ? "/"
                                                       : "\\", DirectorySeparatorCharacter.Equals("\\", StringComparison.OrdinalIgnoreCase)
                                                                   ? "\\"
                                                                   : "/");

        FileName = pathScrubber(Path.GetFileNameWithoutExtension(filePathAndName));

        FolderName = Path.GetDirectoryName(filePathAndName) == null
                         ? filePathAndName.EqualsOrdinalCi(Path.GetPathRoot(filePathAndName))
                               ? filePathAndName
                               : string.Empty
                         : pathScrubber(Path.GetDirectoryName(filePathAndName) ?? string.Empty);

        var fileExtension = pathScrubber(Path.GetExtension(filePathAndName)).Trim();

        FileExtension = fileExtension.StartsWith(".", StringComparison.OrdinalIgnoreCase)
                            ? fileExtension.Substring(1)
                            : fileExtension;
    }

    public string FileName { get; private set; }
    public string DisplayName { get; set; }
    public string FolderName { get; set; }
    public byte[] Bytes { get; set; }
    public Stream Stream { get; set; }
    public IDictionary<string, string> User { get; set; }
    public Dictionary<string, string> Tags => _tags ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string FileExtension
    {
        get => _fileExtension;
        set => _fileExtension = value == null
                                    ? string.Empty
                                    : value.StartsWithOrdinalCi(".")
                                        ? value.Substring(1)
                                        : value;
    }

    public string Combine(params string[] paths)
    {
        var appendSeparator = false;
        var returnVal = string.Empty;

        foreach (var path in paths)
        {
            if (!path.HasValue())
            {
                continue;
            }

            returnVal = string.Concat(returnVal,
                                      appendSeparator
                                          ? DirectorySeparatorCharacter
                                          : string.Empty,
                                      path);

            appendSeparator = !path.EndsWith(DirectorySeparatorCharacter, StringComparison.OrdinalIgnoreCase);
        }

        return returnVal;
    }

    public string DirectorySeparatorCharacter { get; }

    public string FullName => Combine(FolderName.HasValue()
                                          ? FolderName
                                          : string.Empty, FileNameAndExtension);

    public string FileNameAndExtension => FileExtension.HasValue()
                                              ? string.Concat(FileName, ".", FileExtension)
                                              : FileName;

    public override bool Equals(object obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj is FileMetaData bpkObj && Equals(bpkObj);
    }

    public bool Equals(FileMetaData other) => other != null && ToString().EqualsOrdinalCi(other.ToString());

    public override string ToString() => FullName;

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(ToString());
}
