using JobLensChat.Services;
using Xunit;

namespace JobLensChat.Tests;

public sealed class RdapParserTests
{
    private const string GoogleRdap = """
        {
          "ldhName": "GOOGLE.COM",
          "events": [
            { "eventAction": "registration", "eventDate": "1997-09-15T04:00:00Z" },
            { "eventAction": "expiration", "eventDate": "2028-09-14T04:00:00Z" },
            { "eventAction": "last changed", "eventDate": "2019-09-09T15:39:04Z" },
            { "eventAction": "last update of RDAP database", "eventDate": "2026-11-06T12:00:00Z" }
          ],
          "entities": [
            {
              "roles": ["registrar"],
              "vcardArray": ["vcard", [
                ["version", {}, "text", "4.0"],
                ["fn", {}, "text", "MarkMonitor Inc."]
              ]],
              "entities": [
                {
                  "roles": ["abuse"],
                  "vcardArray": ["vcard", [
                    ["version", {}, "text", "4.0"],
                    ["fn", {}, "text", ""]
                  ]]
                }
              ]
            }
          ],
          "nameservers": [
            { "ldhName": "NS1.GOOGLE.COM" },
            { "ldhName": "NS2.GOOGLE.COM" },
            { "ldhName": "NS3.GOOGLE.COM" },
            { "ldhName": "NS4.GOOGLE.COM" }
          ]
        }
        """;

    [Fact]
    public void Parses_google_registration_and_ignores_nested_abuse_contact()
    {
        var result = RdapService.ParseForTests("google.com", GoogleRdap);

        Assert.True(result.Found);
        Assert.Equal("1997-09-15", result.RegistrationDate);
        Assert.Equal("2028-09-14", result.ExpirationDate);
        Assert.Equal("2019-09-09", result.LastChangedDate);
        Assert.Equal("MarkMonitor Inc.", result.Registrar);
        Assert.Equal(
            ["NS1.GOOGLE.COM", "NS2.GOOGLE.COM", "NS3.GOOGLE.COM", "NS4.GOOGLE.COM"],
            result.Nameservers);
    }
}
