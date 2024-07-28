using ISO3166;
using Rydr.Api.Core.Interfaces.Internal;

namespace Rydr.Api.Core.Services.Internal;

public class InMemoryCountryLookupService : ICountryLookupService
{
    private static readonly Dictionary<string, string> _twoLetterIsoMap = Country.List.ToDictionary(c => c.TwoLetterCode, c => c.Name, StringComparer.OrdinalIgnoreCase);

    private InMemoryCountryLookupService() { }

    public static InMemoryCountryLookupService Instance { get; } = new();

    public string GetCountryNameFromTwoLetterIso(string twoLetterCode)
        => _twoLetterIsoMap.ContainsKey(twoLetterCode)
               ? _twoLetterIsoMap[twoLetterCode]
               : null;
}
