using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Veyra.UI.Infrastructure;

namespace Veyra.UI.ViewModels;

public abstract class PageViewModel : ObservableObject
{
    public abstract string Title { get; }
    public abstract string Subtitle { get; }
    public virtual string Glyph => string.Empty;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        protected set => Set(ref _isBusy, value);
    }

    public ObservableCollection<string> Warnings { get; } = new();

    public virtual Task LoadAsync() => Task.CompletedTask;

    protected void ReplaceWarnings(params string[] warnings)
    {
        Warnings.Clear();
        foreach (var warning in warnings) Warnings.Add(warning);
    }
}
