namespace JobLens.Application.Interfaces;

public interface IDatabaseAdmin
{
    string ResolvePath();
    Task ResetAsync(CancellationToken cancellationToken);
}
