using JobLens.Application.Dtos;
using JobLens.Domain.Entities;

namespace JobLens.Application.Interfaces;

public interface IUserRepository
{
    Task<UserAccount?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<int> CreateAsync(UserAccount user, CancellationToken cancellationToken);
}
