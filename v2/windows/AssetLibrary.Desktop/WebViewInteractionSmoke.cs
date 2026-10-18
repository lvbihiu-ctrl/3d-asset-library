using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;

namespace AssetLibrary.Desktop;

internal static class WebViewInteractionSmoke
{
    private static readonly TimeSpan LibraryTimeout = TimeSpan.FromSeconds(30);

    public static async Task VerifyLargeLibraryAsync(WebView2 webView, Library.LibraryCatalog catalog)
    {
        if (catalog is null || catalog.Assets.Count <= 80) throw new InvalidDataException("Large-library smoke needs an existing catalog over 80 records.");
        var uniqueNames = catalog.Assets.GroupBy(asset => asset.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1).Select(group => group.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var target = catalog.Assets.Skip(80).Last(asset => uniqueNames.Contains(asset.Name));
        var expectedPreviews = catalog.Assets.Count(asset => asset.HasCover || asset.HasReferencePreview);
        var script = """
            (() => {
              document.querySelector('#showAll').click();
              const cards = () => [...document.querySelectorAll('article.asset')];
              const firstPage = cards().length;
              const firstId = cards()[0].dataset.id;
              document.querySelector('#pageNext').click();
              const nextPage = cards().length;
              const nextId = cards()[0].dataset.id;
              const search = document.querySelector('#search');
              search.value = QUERY; search.dispatchEvent(new Event('input',{bubbles:true}));
              const foundGlobal = cards().some(card=>card.dataset.id === TARGET);
              document.querySelector('#showAll').click();
              document.querySelector('[data-preview="available"]').click();
              const available = Number(document.querySelector('#assets').dataset.totalResults);
              document.querySelector('[data-preview="missing"]').click();
              const missing = Number(document.querySelector('#assets').dataset.totalResults);
              const missingPageSize = cards().length;
              document.querySelector('#showAll').click();
              const compactPaths = cards().every(card=>getComputedStyle(card.querySelector('.origin')).whiteSpace==='nowrap');
              return {firstPage,nextPage,different:firstId!==nextId,foundGlobal,available,missing,missingPageSize,compactPaths};
            })()
            """;
        script = script.Replace("QUERY", JsonSerializer.Serialize(target.Name), StringComparison.Ordinal)
            .Replace("TARGET", JsonSerializer.Serialize(target.Id), StringComparison.Ordinal);
        using var result = JsonDocument.Parse(await webView.CoreWebView2.ExecuteScriptAsync(script));
        var value = result.RootElement;
        if (value.GetProperty("firstPage").GetInt32() != 80 || value.GetProperty("nextPage").GetInt32() > 80
            || !value.GetProperty("different").GetBoolean() || !value.GetProperty("foundGlobal").GetBoolean()
            || value.GetProperty("available").GetInt32() != expectedPreviews
            || value.GetProperty("missing").GetInt32() != catalog.Assets.Count - expectedPreviews
            || value.GetProperty("missingPageSize").GetInt32() > 80 || !value.GetProperty("compactPaths").GetBoolean())
            throw new InvalidDataException("Large-library UI verification failed: " + result.RootElement.GetRawText());
        Console.WriteLine($"LARGE LIBRARY PASS: {catalog.Assets.Count} records, 80 cards/page, global search and preview filters; model covers={catalog.Assets.Count(asset=>asset.HasCover)}, project references={catalog.Assets.Count(asset=>!asset.HasCover && asset.HasReferencePreview)}, without previews={catalog.Assets.Count-expectedPreviews}.");
    }

    public static async Task VerifyAsync(WebView2 webView)
    {
        const string script = """
            (() => {
              const fireInput = element => element.dispatchEvent(new Event('input', { bubbles: true }));
              const search = document.querySelector('#search');
              search.value = '几何球';
              fireInput(search);
              const searchCount = document.querySelectorAll('#assets article.asset').length;

              document.querySelector('#showAll').click();
              document.querySelector('[data-filter="cat:工业设计"]').click();
              const categoryCount = document.querySelectorAll('#assets article.asset').length;

              document.querySelector('#theme').click();
              const dark = document.body.classList.contains('dark')
                && document.querySelector('#theme').getAttribute('aria-pressed') === 'true';

              document.querySelector('#listBtn').click();
              const list = document.querySelector('#app').classList.contains('listmode');
              document.querySelector('#boardBtn').click();
              const board = document.querySelector('#app').classList.contains('boardmode');

              const cards = document.querySelectorAll('#assets article.asset');
              const selectedCard = cards[1];
              const expectedName = selectedCard.querySelector('.aname').textContent;
              selectedCard.click();
              const selected = document.querySelector('#assets article.asset[aria-current="true"] .aname')?.textContent;
              const detail = document.querySelector('#detail .detailName')?.textContent;

              document.querySelector('#showAll').click();
              const scrollRegions = ['.nav', '.main', '.inspector'].map(selector => {
                const element = document.querySelector(selector);
                element.scrollTop = Math.min(120, element.scrollHeight - element.clientHeight);
                return {
                  selector,
                  scrollTop: element.scrollTop,
                  overflowY: getComputedStyle(element).overflowY
                };
              });
              const scrolling = scrollRegions.every(region =>
                region.scrollTop > 0 && ['auto', 'scroll'].includes(region.overflowY));

              return { searchCount, categoryCount, dark, list, board, selected, detail, expectedName, scrolling };
            })()
            """;

        var rawResult = await webView.CoreWebView2.ExecuteScriptAsync(script);
        var result = JsonSerializer.Deserialize<InteractionResult>(rawResult, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        })
            ?? throw new InvalidDataException("WebView2 UI interaction result was empty.");

        if (result.SearchCount != 2
            || result.CategoryCount != 3
            || !result.Dark
            || !result.List
            || !result.Board
            || !result.Scrolling
            || string.IsNullOrWhiteSpace(result.ExpectedName)
            || result.Selected != result.ExpectedName
            || result.Detail != result.ExpectedName)
        {
            throw new InvalidDataException($"WebView2 UI interaction verification failed: {rawResult}");
        }
    }

    public static async Task VerifyCardZoomAsync(WebView2 webView)
    {
        const string readWidth = "document.querySelector('article.asset').getBoundingClientRect().width";
        var desired = JsonSerializer.Deserialize<double>(await webView.CoreWebView2.ExecuteScriptAsync("parseFloat(document.querySelector('#app').style.getPropertyValue('--card-size'))"));
        await WaitForCardWidthAsync(webView, desired);
        var before = JsonSerializer.Deserialize<double>(await webView.CoreWebView2.ExecuteScriptAsync(readWidth));
        var sidebarBefore = await webView.CoreWebView2.ExecuteScriptAsync("document.querySelector('.nav').getBoundingClientRect().width");
        var direction = desired >= 456 ? 120 : -120;
        await webView.CoreWebView2.ExecuteScriptAsync($"document.querySelector('.main').dispatchEvent(new WheelEvent('wheel',{{ctrlKey:true,deltaY:{direction},bubbles:true,cancelable:true}})); true");
        // Background/occluded WebView2 windows can throttle requestAnimationFrame.
        // Wait for the real layout change rather than assuming a 100 ms paint deadline.
        await WaitForCardWidthAsync(webView, before + (direction < 0 ? 24 : -24));
        var enlarged = JsonSerializer.Deserialize<double>(await webView.CoreWebView2.ExecuteScriptAsync(readWidth));
        var sidebarAfter = await webView.CoreWebView2.ExecuteScriptAsync("document.querySelector('.nav').getBoundingClientRect().width");
        if (enlarged == before || sidebarBefore != sidebarAfter || webView.ZoomFactor != 1 || webView.CoreWebView2.Settings.IsZoomControlEnabled)
            throw new InvalidDataException($"Card zoom verification failed: before={before}, enlarged={enlarged}, sidebar={sidebarBefore}/{sidebarAfter}, zoom={webView.ZoomFactor}, pageZoomEnabled={webView.CoreWebView2.Settings.IsZoomControlEnabled}.");

        var reloaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnReload(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args) => reloaded.TrySetResult();
        webView.CoreWebView2.NavigationCompleted += OnReload;
        try
        {
            webView.CoreWebView2.Reload();
            await reloaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                var ready = await webView.CoreWebView2.ExecuteScriptAsync("document.documentElement.dataset.libraryReady === 'true' && !!document.querySelector('article.asset')");
                if (ready == "true") break;
                await Task.Delay(100);
            }
            await WaitForCardWidthAsync(webView, enlarged);
            var restored = JsonSerializer.Deserialize<double>(await webView.CoreWebView2.ExecuteScriptAsync(readWidth));
            if (restored != enlarged) throw new InvalidDataException("Card size did not survive page reload.");
            await webView.CoreWebView2.ExecuteScriptAsync($"document.querySelector('.main').dispatchEvent(new WheelEvent('wheel',{{ctrlKey:true,deltaY:{-direction},bubbles:true,cancelable:true}})); true");
            await WaitForCardWidthAsync(webView, before);
            var reduced = JsonSerializer.Deserialize<double>(await webView.CoreWebView2.ExecuteScriptAsync(readWidth));
            if (reduced != before) throw new InvalidDataException("Ctrl+wheel down did not restore the smaller card size.");
            Console.WriteLine("CARD ZOOM PASS: Ctrl+wheel handler enlarged/reduced cards, sidebar/page scale unchanged, saved size survived WebView2 reload.");
        }
        finally { webView.CoreWebView2.NavigationCompleted -= OnReload; }
    }

    private static async Task WaitForCardWidthAsync(WebView2 webView, double expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var raw = await webView.CoreWebView2.ExecuteScriptAsync("document.querySelector('article.asset')?.getBoundingClientRect().width ?? 0");
            if (Math.Abs(JsonSerializer.Deserialize<double>(raw) - expected) < .5) return;
            await Task.Delay(100);
        }
        throw new TimeoutException($"Card layout did not reach the expected width {expected}.");
    }

    public static async Task VerifyRealLibraryAsync(
        WebView2 webView,
        Func<int> expectedCount,
        Func<string?> locatedAssetId)
    {
        await webView.CoreWebView2.ExecuteScriptAsync(
            "document.querySelector('#importFolder').click(); true");

        var deadline = DateTime.UtcNow + LibraryTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var ready = await webView.CoreWebView2.ExecuteScriptAsync(
                "document.querySelector('#assets')?.getAttribute('aria-label') === '真实素材'");
            if (ready == "true")
            {
                break;
            }

            await Task.Delay(100).ConfigureAwait(true);
        }

        const string script = """
            (() => {
              const cards = [...document.querySelectorAll('#assets article.asset')];
              if (!cards.length) return { count: 0 };
              const firstId = cards[0].dataset.id || '';
              const firstName = cards[0].querySelector('.aname')?.textContent || '';
              const search = document.querySelector('#search');
              search.value = firstName;
              search.dispatchEvent(new Event('input', { bubbles: true }));
              const filtered = document.querySelectorAll('#assets article.asset').length;
              document.querySelector('#showAll').click();
              return {
                count: cards.length,
                filtered,
                firstName,
                selected: document.querySelector('#detail .detailName')?.textContent || '',
                realLabel: document.querySelector('#assets')?.getAttribute('aria-label') || '',
                pageCount: document.querySelector('#pageCount')?.textContent || '',
                hint: document.querySelector('#hintline')?.textContent || '',
                locateEnabled: !document.querySelector('#locateFile')?.disabled,
                firstId,
                modelState: cards[0].querySelector('.model-state')?.textContent || ''
              };
            })()
            """;

        var rawResult = await webView.CoreWebView2.ExecuteScriptAsync(script);
        var result = JsonSerializer.Deserialize<RealLibraryResult>(rawResult, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidDataException("Real library UI interaction result was empty.");

        var nativeCount = expectedCount();
        if (result.Count < 1
            || result.Count != nativeCount
            || result.Filtered < 1
            || result.RealLabel != "真实素材"
            || result.PageCount.Contains("合成", StringComparison.Ordinal)
            || !result.Hint.Contains("真实素材只读索引", StringComparison.Ordinal)
            || !result.LocateEnabled
            || !result.ModelState.Contains("原模型只读", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(result.FirstName)
            || result.Selected != result.FirstName)
        {
            throw new InvalidDataException($"Real library WebView2 verification failed: {rawResult}");
        }

        var coverDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        var coversLoaded = false;
        while (DateTime.UtcNow < coverDeadline)
        {
            var coverState = await webView.CoreWebView2.ExecuteScriptAsync(
                "[...document.querySelectorAll('img[src^=\"https://assetlibrary-covers.local/\"]')]"
                + ".every(image => image.complete && image.naturalWidth > 0)");
            if (coverState == "true")
            {
                coversLoaded = true;
                break;
            }
            await Task.Delay(100).ConfigureAwait(true);
        }
        if (!coversLoaded)
        {
            throw new InvalidDataException("Matched cover files did not render in the real WebView2 process.");
        }

        await webView.CoreWebView2.ExecuteScriptAsync(
            "document.querySelector('#locateFile').click(); true");
        var locateDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (locatedAssetId() != result.FirstId && DateTime.UtcNow < locateDeadline)
        {
            await Task.Delay(50).ConfigureAwait(true);
        }

        if (locatedAssetId() != result.FirstId)
        {
            throw new InvalidDataException("Real library locate button did not reach the trusted native locator.");
        }
    }

    private sealed record InteractionResult(
        int SearchCount,
        int CategoryCount,
        bool Dark,
        bool List,
        bool Board,
        bool Scrolling,
        string? Selected,
        string? Detail,
        string? ExpectedName);

    private sealed record RealLibraryResult(
        int Count,
        int Filtered,
        string FirstName,
        string Selected,
        string RealLabel,
        string PageCount,
        string Hint,
        bool LocateEnabled,
        string FirstId,
        string ModelState);
}
