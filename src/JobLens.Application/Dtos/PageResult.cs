namespace JobLens.Application.Dtos;

public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int From => Total == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int To => Total == 0 ? 0 : From + Items.Count - 1;

    public int TotalPages => Total == 0 ? 1 : (int)Math.Ceiling(Total / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}
