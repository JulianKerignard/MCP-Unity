using McpUnity.Server;

namespace McpUnity.Models
{
    /// <summary>
    /// Represents a message queued for processing on the main thread
    /// Used for thread-safe communication between WebSocket and Unity main thread
    /// </summary>
    // SEC-#443: read-only after construction. These instances move from the WebSocket
    // thread to the Unity main thread, so mutability would require additional
    // synchronization that the producer/consumer pattern doesn't currently provide.
    public class QueuedMessage
    {
        /// <summary>The JSON-RPC message content.</summary>
        public string Message { get; }

        /// <summary>WebSocket behavior that sent this message — used to route the response back.</summary>
        public McpBehavior Sender { get; }

        public QueuedMessage(string message, McpBehavior sender)
        {
            Message = message;
            Sender = sender;
        }
    }
}
