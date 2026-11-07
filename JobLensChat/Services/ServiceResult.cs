namespace JobLensChat.Services;

public sealed record ServiceResult(bool Success, string? ErrorMessage, int? Id)
{
    public static ServiceResult Ok(int? id = null) => new(true, null, id);

    public static ServiceResult Fail(string errorMessage) => new(false, errorMessage, null);
}
