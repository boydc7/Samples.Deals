namespace Rydr.Api.Dto.Interfaces;

public interface IHasSkipTake
{
    int Skip { get; set; }
    int Take { get; set; }
}
