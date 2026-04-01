namespace DropShot.Services;

public class LayoutState
{
    public bool IsFullscreen { get; private set; }
    public event Action? OnChange;

    public void SetFullscreen(bool fullscreen)
    {
        IsFullscreen = fullscreen;
        OnChange?.Invoke();
    }
}
