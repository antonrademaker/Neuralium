using System.Diagnostics;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Neuralium.Worker.Services;

/// <summary>
/// Fetches and extracts readable content from web articles.
/// Uses HtmlAgilityPack for parsing and heuristics for content extraction.
/// </summary>
public sealed partial class ArticleFetcherService(
    IHttpClientFactory httpClientFactory,
    ILogger<ArticleFetcherService> logger) : IArticleFetcherService
{
    private static readonly ActivitySource s_activitySource = new("Neuralium.Worker.ArticleFetcherService");

    // Minimum content length to consider sufficient (characters)
    private const int MinimumContentLength = 150;

    // Domains known to have paywall/access issues - skip fetching
    private static readonly HashSet<string> SkipDomains =
    [
        "wsj.com",
        "ft.com",
        "bloomberg.com",
        "nytimes.com"
    ];

    public bool IsInsufficientContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return true;

        // Check if content is just HTML links (common in HN RSS)
        var htmlLinkPattern = @"^<a\s+href=[""'].*?[""']>.*?</a>$";
        if (Regex.IsMatch(content.Trim(), htmlLinkPattern, RegexOptions.IgnoreCase))
        {
            LogDetectedLinkOnlyContent();
            return true;
        }

        // Check if content is very short (likely just a snippet)
        var strippedContent = Regex.Replace(content, @"<[^>]+>", "").Trim();
        if (strippedContent.Length < MinimumContentLength)
        {
            LogDetectedShortContent(strippedContent.Length, MinimumContentLength);
            return true;
        }

        return false;
    }

    public async Task<string?> FetchArticleContentAsync(string url, CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity("Fetch article content");
        activity?.SetTag("url", url);

        try
        {
            // Check if domain should be skipped
            var uri = new Uri(url);
            if (SkipDomains.Any(d => uri.Host.Contains(d, StringComparison.OrdinalIgnoreCase)))
            {
                LogSkippingPaywallDomain(uri.Host);
                return null;
            }

            var httpClient = httpClientFactory.CreateClient("ArticleFetcher");
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; NeuraliumBot/1.0)");

            var html = await httpClient.GetStringAsync(url, cancellationToken);

            var extractedContent = ExtractMainContent(html);

            if (!string.IsNullOrWhiteSpace(extractedContent))
            {
                activity?.SetTag("content.length", extractedContent.Length);
                LogContentExtracted(url, extractedContent.Length);
                return extractedContent;
            }

            LogExtractionFailed(url);
            return null;
        }
        catch (HttpRequestException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogFetchError(url, ex.Message);
            return null;
        }
        catch (TaskCanceledException)
        {
            LogFetchTimeout(url);
            return null;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogFetchError(url, ex.Message);
            return null;
        }
    }

    private static string? ExtractMainContent(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove script, style, nav, footer, and other non-content elements
        var nodesToRemove = doc.DocumentNode.SelectNodes(
            "//script | //style | //nav | //footer | //header | //aside | //iframe | //noscript");
        if (nodesToRemove != null)
        {
            foreach (var node in nodesToRemove)
            {
                node.Remove();
            }
        }

        // Try common article content selectors
        var selectors = new[]
        {
            "//article",
            "//*[@class='article-content']",
            "//*[@class='post-content']",
            "//*[@class='entry-content']",
            "//*[@class='content']",
            "//*[@id='content']",
            "//main",
            "//body"
        };

        foreach (var selector in selectors)
        {
            var contentNode = doc.DocumentNode.SelectSingleNode(selector);
            if (contentNode != null)
            {
                var paragraphs = contentNode.SelectNodes(".//p");
                if (paragraphs != null && paragraphs.Count > 0)
                {
                    var text = string.Join("\n\n", paragraphs.Select(p => p.InnerText.Trim()));

                    // Clean up whitespace
                    text = Regex.Replace(text, @"\s+", " ");
                    text = Regex.Replace(text, @"\n\s*\n", "\n\n");

                    if (text.Length > MinimumContentLength)
                    {
                        // Limit to reasonable length for LLM processing (first ~2000 chars)
                        return text.Length > 2000 ? text[..2000] + "..." : text;
                    }
                }
            }
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "ArticleFetcher: Detected link-only content")]
    private partial void LogDetectedLinkOnlyContent();

    [LoggerMessage(Level = LogLevel.Debug, Message = "ArticleFetcher: Detected short content ({Length} chars < {MinLength} required)")]
    private partial void LogDetectedShortContent(int length, int minLength);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ArticleFetcher: Skipping paywall domain: {Domain}")]
    private partial void LogSkippingPaywallDomain(string domain);

    [LoggerMessage(Level = LogLevel.Information, Message = "ArticleFetcher: Extracted {Length} characters from {Url}")]
    private partial void LogContentExtracted(string url, int length);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ArticleFetcher: Failed to extract content from {Url}")]
    private partial void LogExtractionFailed(string url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ArticleFetcher: Fetch error for {Url}: {Error}")]
    private partial void LogFetchError(string url, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ArticleFetcher: Timeout fetching {Url}")]
    private partial void LogFetchTimeout(string url);
}
