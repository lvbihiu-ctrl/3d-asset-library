using System.Text.Json;
using Microsoft.Web.WebView2.WinForms;

namespace AssetLibrary.Desktop;

internal static class WebViewInteractionSmoke
{
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
}
