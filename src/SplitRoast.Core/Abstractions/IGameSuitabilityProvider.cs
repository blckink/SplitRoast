using System.Threading;
using System.Threading.Tasks;
using SplitRoast.Core.Models;

namespace SplitRoast.Core.Abstractions;

/// <summary>
/// Resolves whether a game is a good candidate for SplitRoast's split-screen
/// approach (based on its co-op / multiplayer categories). Implementations may use
/// the network and should cache results; any failure must degrade gracefully to
/// <see cref="GameCoopInfo.Unknown"/> rather than throw, so the UI never breaks.
/// </summary>
public interface IGameSuitabilityProvider
{
    Task<GameCoopInfo> GetAsync(SteamGame game, CancellationToken cancellationToken = default);
}
