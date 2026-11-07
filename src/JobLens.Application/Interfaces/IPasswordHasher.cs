namespace JobLens.Application.Interfaces;

public interface IPasswordHasher
{
    string Hash(string email, string secret);

    bool Verify(string email, string passwordHash, string secret);
}
