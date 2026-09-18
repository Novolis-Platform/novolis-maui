using Novolis.Maui.WebView;

namespace Novolis.Maui.Unit.WebView;

public sealed class HtmlContentSecurityPolicyTests
{
    [Test]
    public async Task Apply_InsertsCspMetaAfterHead()
    {
        var html = HtmlContentSecurityPolicy.Apply("<html><head></head><body></body></html>");

        await Assert.That(html).Contains("default-src 'none'");
        await Assert.That(html).Contains("img-src data:");
        await Assert.That(html).Contains("<head><meta http-equiv=\"Content-Security-Policy\"");
    }

    [Test]
    public async Task Apply_ThrowsWhenHeadIsMissing()
    {
        var act = () => HtmlContentSecurityPolicy.Apply("<html><body></body></html>");
        await Assert.That(act).Throws<InvalidDataException>();
    }
}
