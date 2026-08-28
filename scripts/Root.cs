using Godot;

/// <summary>
/// Racine de l'application : gère la bascule entre le menu d'accueil et la
/// partie. Le menu choisit la graine ; « Nouvelle partie » lance une Game avec
/// cette graine. Une partie déjà en cours est remplacée.
/// </summary>
public partial class Root : Node
{
    private MainMenu _menu;
    private Game _game;

    public override void _Ready() => ShowMenu();

    private void ShowMenu()
    {
        if (_game != null) { _game.QueueFree(); _game = null; }

        _menu = new MainMenu();
        _menu.StartRequested += StartGame;
        AddChild(_menu);
    }

    private void StartGame(ulong seed)
    {
        if (_menu != null) { _menu.QueueFree(); _menu = null; }

        _game = new Game { Seed = seed };
        AddChild(_game);
    }
}
