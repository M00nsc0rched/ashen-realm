using System;

namespace AshenRealm.Core
{
    /// <summary>
    /// Minimal global message channel for short player-facing notifications (rewards, lore, events).
    /// Subscribers must unsubscribe in OnDisable so the channel stays clean when domain reload is disabled.
    /// </summary>
    public static class GameMessages
    {
        public static event Action<string> Notification;

        public static void Notify(string message)
        {
            if (!string.IsNullOrEmpty(message))
                Notification?.Invoke(message);
        }
    }
}
