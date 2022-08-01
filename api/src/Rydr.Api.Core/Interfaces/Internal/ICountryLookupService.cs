namespace Rydr.Api.Core.Interfaces.Internal
{
    public interface ICountryLookupService
    {
        string GetCountryNameFromTwoLetterIso(string twoLetterCode);
    }
}
