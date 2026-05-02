namespace Rexo.Tui;

public sealed class TuiNavigationService
{
    private string _current = "home";

    public string Current => _current;

    public string? Parameter { get; private set; }

    public event EventHandler? NavigationRequested;

    public void Navigate(string page, string? parameter = null)
    {
        _current = page;
        Parameter = parameter;
        NavigationRequested?.Invoke(this, EventArgs.Empty);
    }
}
