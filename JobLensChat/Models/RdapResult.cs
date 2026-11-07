using System.Text.Json.Serialization;

namespace JobLensChat.Models;

public sealed record RdapResult(
    string Domain,
    bool Found,
    string? RegistrationDate,
    string? ExpirationDate,
    string? LastChangedDate,
    string? Registrar,
    int? DomainAgeDays,
    IReadOnlyList<string> Status,
    IReadOnlyList<string> Nameservers,
    string Message,
    string Source,
    DateTimeOffset RetrievedAtUtc,
    [property: JsonIgnore] bool FromCache = false);
