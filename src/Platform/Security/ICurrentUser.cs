namespace OpenDealer360.Platform.Security;

/// <summary>
/// The authenticated caller for the current request. Resolved once, before any
/// endpoint runs, from the session (doc 06 §2). Endpoints and services never
/// take a user id as a parameter from the client — that would let a caller act
/// as someone else.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The caller's id. Throws when the request is unauthenticated.</summary>
    Guid Id { get; }

    void Set(Guid userId);
}

/// <summary>Scoped, write-once holder of the caller for the current request.</summary>
public sealed class CurrentUser : ICurrentUser
{
    private Guid? _id;

    public bool IsAuthenticated => _id is not null;

    public Guid Id => _id
        ?? throw new InvalidOperationException(
            "No user is resolved for this request. An authorized operation ran outside authentication.");

    public void Set(Guid userId)
    {
        if (_id is not null)
        {
            throw new InvalidOperationException("The user for this request is already resolved.");
        }

        _id = userId;
    }
}
