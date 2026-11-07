using JobLensChat.Services;
using Xunit;

namespace JobLensChat.Tests;

public sealed class RdapRegistryUrlTests
{
    [Theory]
    [InlineData(
        "google.com",
        "https://rdap.verisign.com/com/v1/domain/google.com",
        "https://rdap.org/domain/google.com")]
    [InlineData(
        "amaz0n-careers.net",
        "https://rdap.verisign.com/net/v1/domain/amaz0n-careers.net",
        "https://rdap.org/domain/amaz0n-careers.net")]
    [InlineData(
        "example.org",
        "https://rdap.publicinterestregistry.org/rdap/domain/example.org",
        "https://rdap.org/domain/example.org")]
    public void Official_registry_is_tried_before_rdap_org(string domain, string primary, string fallback)
    {
        Assert.Equal([primary, fallback], RdapService.RegistryUrls(domain));
    }

    [Fact]
    public void Other_tlds_use_rdap_org_only()
    {
        Assert.Equal(
            ["https://rdap.org/domain/example.io"],
            RdapService.RegistryUrls("example.io"));
    }
}
