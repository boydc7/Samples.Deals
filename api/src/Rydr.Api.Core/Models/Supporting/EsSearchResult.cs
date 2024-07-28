namespace Rydr.Api.Core.Models.Supporting;

public class EsSearchResult<T>
{
    public List<T> Results { get; set; }
    public long TotalHits { get; set; }
    public long TookMs { get; set; }
    public bool TimedOut { get; set; }
    public bool Successful { get; set; }

    public string Request { get; set; }
    public string Response { get; set; }
}
