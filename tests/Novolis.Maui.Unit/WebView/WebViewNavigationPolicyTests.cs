using Novolis.Maui.WebView;

namespace Novolis.Maui.Unit.WebView;

public sealed class WebViewNavigationPolicyTests
{
    [Test]
    [Arguments(null, WebViewNavigationDecision.Allow)]
    [Arguments("", WebViewNavigationDecision.Allow)]
    [Arguments("about:blank", WebViewNavigationDecision.Allow)]
    [Arguments("https://example.com/doc", WebViewNavigationDecision.OpenExternally)]
    [Arguments("http://example.com", WebViewNavigationDecision.OpenExternally)]
    [Arguments("mailto:a@b.c", WebViewNavigationDecision.OpenExternally)]
    [Arguments("file:///tmp/x.md", WebViewNavigationDecision.Cancel)]
    [Arguments("javascript:alert(1)", WebViewNavigationDecision.Cancel)]
    [Arguments("not a url", WebViewNavigationDecision.Cancel)]
    public async Task Decide_ClassifiesUrls(string? url, WebViewNavigationDecision expected) =>
        await Assert.That(WebViewNavigationPolicy.Decide(url)).IsEqualTo(expected);
}
