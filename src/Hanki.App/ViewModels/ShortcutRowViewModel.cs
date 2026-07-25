using Hanki.Core.Models;

namespace Hanki.App.ViewModels;

public sealed class ShortcutRowViewModel(ShortcutItem model)
{
    public ShortcutItem Model { get; } = model;
    public string Id => Model.Id;
    public string Title => string.IsNullOrWhiteSpace(Model.Title) ? "제목 없음" : Model.Title;
    public string TriggerText => Model.TriggerText;
    public string ReplacementText => Model.ReplacementText;
    public bool IsFavorite => Model.IsFavorite;
    public long UsageCount => Model.UsageCount;
}
