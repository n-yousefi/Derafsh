using Derafsh.SettingsSample.SqlServer.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Derafsh.SettingsSample.SqlServer.Pages;

public sealed class SettingsModel(StoreSettingsStore store) : PageModel
{
    [BindProperty]
    public StoreSettingsDto Settings { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Settings = await store.LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await store.SaveAsync(Settings, cancellationToken);
        StatusMessage = $"Saved. {result.InsertedCount} inserted, {result.UpdatedCount} updated, {result.DeletedCount} deleted.";
        return RedirectToPage();
    }
}
