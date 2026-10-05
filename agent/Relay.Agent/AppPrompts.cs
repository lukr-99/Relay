using System.Windows;
using DotNetLib.Tray;
using Relay.Agent.Theming;

namespace Relay.Agent;

/// <summary>The agent's message, question and text dialogs: the kit's dialogs in the current theme.</summary>
public sealed class AppPrompts(AgentTheme theme)
{
    private const string Title = "Relay";

    /// <summary>Shows a message with OK.</summary>
    public void Inform(string message) => TrayMessageWindow.Inform(Title, message, theme.Attach);

    /// <summary>Asks before a destructive action. True only when the user chose <paramref name="confirmText"/>.</summary>
    public bool ConfirmDanger(string title, string message, string confirmText, Window? owner = null) =>
        TrayConfirmWindow.Ask(new TrayConfirmation(title, message, confirmText, "Cancel"), owner, theme.Attach);

    /// <summary>Asks for one line of text. Null when cancelled.</summary>
    public string? AskLine(string title, string prompt, string initial) =>
        TrayTextWindow.AskLine(title, prompt, initial, theme.Attach);
}
