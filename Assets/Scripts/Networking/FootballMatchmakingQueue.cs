using System.Collections.Generic;
using Mirror;

public sealed class FootballMatchmakingQueue
{
    private readonly Queue<NetworkConnectionToClient> _connections = new Queue<NetworkConnectionToClient>();
    private readonly HashSet<NetworkConnectionToClient> _membership = new HashSet<NetworkConnectionToClient>();

    public int Count => _membership.Count;

    public bool Enqueue(NetworkConnectionToClient connection)
    {
        if (connection == null || !_membership.Add(connection))
            return false;

        _connections.Enqueue(connection);
        return true;
    }

    public bool Remove(NetworkConnectionToClient connection)
    {
        return connection != null && _membership.Remove(connection);
    }

    public bool TryDequeuePair(out NetworkConnectionToClient first, out NetworkConnectionToClient second)
    {
        first = DequeueValid();
        second = DequeueValid();

        if (first != null && second != null)
            return true;

        if (first != null)
            Enqueue(first);

        first = null;
        second = null;
        return false;
    }

    public IEnumerable<NetworkConnectionToClient> GetConnections()
    {
        foreach (NetworkConnectionToClient connection in _connections)
        {
            if (_membership.Contains(connection))
                yield return connection;
        }
    }

    public void Clear()
    {
        _connections.Clear();
        _membership.Clear();
    }

    private NetworkConnectionToClient DequeueValid()
    {
        while (_connections.Count > 0)
        {
            NetworkConnectionToClient connection = _connections.Dequeue();

            if (!_membership.Remove(connection))
                continue;

            if (connection != null && connection.isAuthenticated)
                return connection;
        }

        return null;
    }
}

