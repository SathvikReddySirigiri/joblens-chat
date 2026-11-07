namespace JobLens.Domain.Entities;

public sealed record Company(int CompanyId, string Name, string OfficialDomain, string Industry, string HqState);
