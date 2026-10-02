using Novolis.Maui.WebView;

namespace Novolis.Maui.Unit.WebView;

public sealed class WebViewNavigationPolicyTests
{
    [Test]
    [Arguments(null, WebViewNavigationDecision.Allow)]
    [Arguments("", WebViewNavigationDecision.Allow)]
    [Arguments("about:blank", WebViewNavigationDecision.Allow)]
    [Arguments("about:srcdoc", WebViewNavigationDecision.Allow)]
    [Arguments("data:text/html,<h1>Hi</h1>", WebViewNavigationDecision.Allow)]
    [Arguments("https://example.com/doc", WebViewNavigationDecision.OpenExternally)]
    [Arguments("http://example.com", WebViewNavigationDecision.OpenExternally)]
    [Arguments("mailto:a@b.c", WebViewNavigationDecision.OpenExternally)]
    [Arguments("novolis-md://copy/0", WebViewNavigationDecision.HandleInternally)]
    [Arguments("novolis-md://preview/2", WebViewNavigationDecision.HandleInternally)]
    [Arguments("about:novolis-md/preview/0", WebViewNavigationDecision.HandleInternally)]
    [Arguments("about:novolis-md/copy/3", WebViewNavigationDecision.HandleInternally)]
    [Arguments("https://novolis.md/preview/1", WebViewNavigationDecision.HandleInternally)]
    [Arguments("file:///tmp/x.md", WebViewNavigationDecision.Cancel)]
    [Arguments("javascript:alert(1)", WebViewNavigationDecision.Cancel)]
    [Arguments("not a url", WebViewNavigationDecision.Cancel)]
    public async Task Decide_ClassifiesUrls(string? url, WebViewNavigationDecision expected) =>
        await Assert.That(WebViewNavigationPolicy.Decide(url)).IsEqualTo(expected);
}
