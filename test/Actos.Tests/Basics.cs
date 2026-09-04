namespace Actos.Tests;

public class Basics
{
    [Fact]
    public void Namespace_Is_Actos_Expected()
    {
        // Smoke: test infrastructure + nullability/latest-lang compile sanity.
        string[] expectedResources =
        {
            "Auth", "Actors", "Posts", "Comments", "Inbox", "Feed",
            "Search", "Tags", "Votes", "Saves", "Uploads", "Reports", "Admin", "Meta",
        };
        Assert.Equal(14, expectedResources.Length);
    }
}