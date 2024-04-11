using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace ArchieVBetterWeight
{
    internal static class DevLogger
    {
        /// <summary>
        /// Logs message if DevMode is on
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void DevMessage(object message)
        {
            if (BetterWeight.DevMode)
            {
                DevMessage(message);
            }
        }

        /// <summary>
        /// Logs warning if DevMode is on
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void DevWarning(object message)
        {
            if (BetterWeight.DevMode)
            {
                Log.Warning(message.ToString());
            }
        }

        /// <summary>
        /// Logs error if DevMode is on
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void DevError(object message)
        {
            if (BetterWeight.DevMode)
            {
                Log.Error(message.ToString());
            }
        }
    }
}
