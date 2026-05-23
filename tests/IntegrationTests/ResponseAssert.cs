namespace IntegrationTests;

internal class ResponseAssert
{
    public static void HasContent(HttpResponseMessage response, string expectedContent)
    {
        var content = response.Content.ReadAsStringAsync().Result;
        Assert.IsTrue(content.Contains(expectedContent), $"Response content does not contain expected value. Expected to find: {expectedContent}. Actual content: {content}");
    }

    public static void HasErrorReason(HttpResponseMessage response, string expectedErrorReason)
    {
        var errorReason = response.Headers.GetValues("ErrorReason").FirstOrDefault();
        Assert.IsNotNull(errorReason, "ErrorReason header is missing.");
        Assert.AreEqual(expectedErrorReason, errorReason);
    }
}
