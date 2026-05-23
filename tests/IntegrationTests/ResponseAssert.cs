namespace IntegrationTests;

internal class ResponseAssert
{
    public static async Task ContentContains(HttpResponseMessage response, string expectedContent)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.IsTrue(content.Contains(expectedContent), $"Response content does not contain expected value. Expected to find: {expectedContent}. Actual content: {content}");
    }

    public static void HasErrorReason(HttpResponseMessage response, string expectedErrorReason)
    {
        Assert.IsTrue(response.Headers.Contains("ErrorReason"), "ErrorReason header is missing.");
        Assert.AreEqual(expectedErrorReason, response.Headers.GetValues("ErrorReason").FirstOrDefault());
    }
}
