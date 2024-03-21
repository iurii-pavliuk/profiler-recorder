using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking.PlayerConnection;

public class PlayerConnectionManager : MonoBehaviour
{
    private PlayerConnection _playerConnection;

    private void Start()
    {
        _playerConnection = PlayerConnection.instance; 
        _playerConnection.Register(Guid.NewGuid(), OnMessageReceived);
    }

    private static void OnMessageReceived(MessageEventArgs message)
    {
        var messageString = Encoding.ASCII.GetString(message.data);
        Debug.Log($"Message received from the editor:\n{messageString}");
    }
}

public abstract class MessageTypes
{
    public static readonly Guid PerformanceStatistics = new Guid("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
}
