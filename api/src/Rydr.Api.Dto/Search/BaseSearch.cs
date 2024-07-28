namespace Rydr.Api.Dto.Search;

public abstract class BaseSearch : RequestBase
{
    public string Query { get; set; }
    public int Take { get; set; } = 50;
}
