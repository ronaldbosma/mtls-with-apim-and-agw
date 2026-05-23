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
        Assert.IsTrue(response.Headers.Any(h => h.Key == "ErrorReason"), "ErrorReason header is missing.");
        Assert.AreEqual(expectedErrorReason, response.Headers.GetValues("ErrorReason").FirstOrDefault());
    }
}
