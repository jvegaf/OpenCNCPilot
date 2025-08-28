using System.Threading.Tasks;

namespace OpenCNCPilot.UI.Services;

public interface IDialogService
{
    Task<string[]?> OpenFilesAsync(string title, string? initialDirectory = null, string[]? filters = null, bool allowMultiple = false);
    Task<string?> SaveFileAsync(string title, string? initialDirectory = null, string? defaultFileName = null, string[]? filters = null);
    Task<string?> PickFolderAsync(string title, string? initialDirectory = null);
    Task AlertAsync(string title, string message, string okText = "OK");
    Task<bool> ConfirmAsync(string title, string message, string confirmText = "OK", string cancelText = "Cancel");
    Task<double?> PromptNumberAsync(string title, string message, double? defaultValue = null, double? min = null, double? max = null, int decimals = 3);
}
