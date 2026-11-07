using JobLens.Web.Chat;
using Xunit;

namespace JobLens.Tests;

public sealed class ChatDemoMatchTests
{
    [Theory]
    [InlineData("How much money was requested?", false)]
    [InlineData("Reports involving EY", true)]
    [InlineData("Check metadata for the domain", false)]
    [InlineData("Any Meta impersonation reports?", true)]
    [InlineData("Was the campaign targeted?", false)]
    [InlineData("Reports that impersonate Target", true)]
    public void Whole_word_company_match_avoids_substring_false_positives(string question, bool expectCompany)
    {
        var args = ChatOrchestrator.SearchArguments(question);
        if (expectCompany)
        {
            Assert.True(args.ContainsKey("companyName"));
        }
        else
        {
            Assert.False(args.ContainsKey("companyName"));
        }
    }

    [Fact]
    public void Whole_word_match_finds_scam_type_and_channel()
    {
        var args = ChatOrchestrator.SearchArguments("Approved Fake check reports by Email for Amazon");
        Assert.Equal("Fake check", args["scamType"]);
        Assert.Equal("Email", args["channel"]);
        Assert.Equal("Amazon", args["companyName"]);
        Assert.Equal("Approved", args["status"]);
    }

    [Fact]
    public void ContainsWholePhrase_is_case_insensitive_with_word_boundaries()
    {
        Assert.True(ChatOrchestrator.ContainsWholePhrase("see Deloitte reports", "Deloitte"));
        Assert.False(ChatOrchestrator.ContainsWholePhrase("metadata leak", "Meta"));
        Assert.False(ChatOrchestrator.ContainsWholePhrase("money trail", "EY"));
        Assert.False(ChatOrchestrator.ContainsWholePhrase("targeted ads", "Target"));
    }
}
