using System;
using System.Collections.Generic;
using System.Text;

namespace dmuka3.CS.Simple.Queuing
{
    /// <summary>
    /// Protocol messages.
    /// </summary>
    public class QueuingMessages
    {
        #region Variables
        internal const string SERVER_HI = "HI";
        internal const string SERVER_NOT_AUTHORIZED = "NOT_AUTHORIZED";
        internal const string SERVER_OK = "OK";
        internal const string SERVER_END = "END";
        internal const string SERVER_ERROR = "ERROR";

        internal const string CLIENT_HI = "HI";
        internal const string CLIENT_ENQUEUE = "ENQUEUE";
        internal const string CLIENT_DEQUEUE = "DEQUEUE";
        internal const string CLIENT_DEQUEUE_COMPLETED = "DEQUEUE_COMPLETED";
        internal const string CLIENT_CLOSE = "CLOSE";
        #endregion
    }
}
