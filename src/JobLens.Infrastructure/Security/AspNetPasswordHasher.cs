using JobLens.Application.Interfaces;

namespace JobLens.Infrastructure.Security;

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    public string Hash(string email, string secret) => UserPassword.Hash(email, secret);

    public bool Verify(string email, string passwordHash, string secret) =>
        UserPassword.Verify(email, passwordHash, secret);
}
