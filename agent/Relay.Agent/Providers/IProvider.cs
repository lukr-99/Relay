using System.Text.Json;

namespace Relay.Agent.Providers;

/// <summary>
/// A capability the deck can drive (os, obs, micforge, …). Adding a provider adds verbs without
/// touching the wire protocol or the layout model.
/// </summary>
public interface IProvider
{
    /// <summary>Stable id referenced by <c>action.provider</c> (e.g. "os").</summary>
    string Id { get; }

    /// <summary>Execute a verb with provider-scoped params. Throws on failure.</summary>
    Task InvokeAsync(string verb, JsonElement @params, CancellationToken ct = default);
}
