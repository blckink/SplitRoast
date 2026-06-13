using System.Threading;
using System.Threading.Tasks;
using SplitRoast.Core.Models;

namespace SplitRoast.Core.Abstractions;

/// <summary>
/// Searches the public Steam store for games matching a co-op / genre / sort
/// filter, so the user can discover titles that suit SplitRoast beyond the ones
/// they already have installed. Implementations may use the network; any failure
/// must degrade gracefully to <see cref="StoreSearchResult.Empty"/> rather than
/// throw, so the UI never breaks.
/// </summary>
public interface IStoreDiscoveryProvider
{
    Task<StoreSearchResult> SearchAsync(StoreSearchQuery query, CancellationToken cancellationToken = default);
}
