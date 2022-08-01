using System;
using System.Runtime.CompilerServices;
using System.Text;
using Rydr.Api.Core.Configuration;
using Rydr.FbSdk.Extensions;
using ServiceStack;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Extensions
{
    public static class LogExtensions
    {
        private static readonly bool _isContainerizedEnvironment = RydrEnvironment.GetAppSetting("Environment.Containerized", false);
        private static readonly int _requestLengthToLog = RydrEnvironment.IsLocalEnvironment
                                                              ? 250000
                                                              : RydrEnvironment.GetAppSetting("Logging.RequestLength", 1000);

        public static bool IsTraceEnabled { get; } = RydrEnvironment.GetAppSetting("Logging.TraceEnabled", false);
        public static bool IsDebugInfoEnabled { get; } = RydrEnvironment.GetAppSetting("Logging.DebugInfo", false);

        public static void Exception(this ILog log, Exception ex, string baseMsg = "", bool wasHandled = false)
        {
            if (ex == null)
            {
                ex = new ApplicationException("Unknown Exception Object Reference");
            }

            var msg = ToLogMessage(ex, wasHandled, baseMsg);

            log.Error(msg, ex);
        }

        public static string ToLogMessage(this Exception ex, bool wasHandled = false, string customMessage = null)
        {
            if (ex == null)
            {
                return null;
            }

            string stack = null;

            if (_isContainerizedEnvironment)
            {
                stack = ex.StackTrace.ToNullIfEmpty() ?? ex.InnerException?.StackTrace;

                var stackParts = stack.IsNullOrEmpty()
                                     ? null
                                     : stack.Split('\n');

                if (!stackParts.IsNullOrEmpty())
                {
                    var stackBuilder = new StringBuilder(stackParts.Length * 4);

                    for(var i = 0; i < stackParts.Length; i++)
                    {
                        stackBuilder.Append(" |--");
                        stackBuilder.Append((i + 1).ToStringInvariant().PadLeft(3, '0'));
                        stackBuilder.Append("--) ");
                        stackBuilder.Append(stackParts[i].Trim());

                        if (stackBuilder.Length >= 1500)
                        {
                            break;
                        }
                    }

                    stack = stackBuilder.ToString();
                }
            }
            else
            {
                stack = ex.ToString();
            }

            var msg = string.Concat("!!! ",
                                    wasHandled
                                        ? "HANDLED"
                                        : "EXCEPTION", " !!! ",
                                    customMessage.HasValue()
                                        ? customMessage
                                        : "N/A",
                                    " :: Type [", ex.GetType(),
                                    "] :: Message [", ex.Message,
                                    "] :: InnerType [", ex.InnerException?.GetType(),
                                    "] :: InnerMsg [", ex.InnerException?.Message,
                                    "] :: Stack [", stack.Left(1500), "]");

            return msg;
        }

        public static bool LogExceptionReturnFalse(this ILog log, Exception x, string message = null)
        {
            Exception(log, x, message);

            return false;
        }

        public static bool LogExceptionReturnTrue(this ILog log, Exception x, string message = null)
        {
            Exception(log, x, message);

            return true;
        }

        public static void LogRequestStart(this ILog log, string typeName, string ipAddress, object request, [CallerMemberName] string methodName = null)
        {
            const string startRequestFormatMsg = "### START REQUEST ### :: Source [{0}.{1}] :: IP [{2}] :: Request [{3}]";

            LogStart(log, typeName, startRequestFormatMsg, ipAddress, request, true, methodName);
        }

        public static void LogOperationStart(this ILog log, string typeName, object request, [CallerMemberName] string methodName = null)
        {
            const string startOperationFormatMsg = "### START OPERATION ### :: Source [{0}.{1}] :: Request [{3}]";
            LogStart(log, typeName, startOperationFormatMsg, string.Empty, request, methodName: methodName);
        }

        private static void LogStart(this ILog log, string typeName, string fmtMsg, string ipAddress,
                                     object request, bool infoLevel = false, [CallerMemberName] string methodName = null)
        {
            if (infoLevel)
            {
                log.InfoFormat(fmtMsg, typeName, methodName, ipAddress, request.ToJsv().Left(_requestLengthToLog));
            }
            else if (IsDebugInfoEnabled)
            {
                log.DebugInfoFormat(fmtMsg, typeName, methodName, ipAddress, request.ToJsv().Left(_requestLengthToLog));
            }
            else if (log.IsDebugEnabled)
            {
                log.DebugFormat(fmtMsg, typeName, methodName, ipAddress, request.ToJsv().Left(_requestLengthToLog));
            }
        }

        public static void LogRequestEnd(this ILog log, string typeName,
                                         Exception ex = null, bool wasHandled = false,
                                         string extraInfo = null,
                                         [CallerMemberName] string methodName = null)
        {
            const string endRequestFormatMsg = "### END REQUEST ### :: Source [{0}.{1}] :: Status [{2}]{3}";

            LogEnd(log, typeName, endRequestFormatMsg, ex, wasHandled, extraInfo, true, methodName);
        }

        public static void LogOperationEnd(this ILog log, string typeName,
                                           Exception ex = null, bool wasHandled = false,
                                           string extraInfo = null,
                                           [CallerMemberName] string methodName = null)
        {
            const string endOperationFormatMsg = "### END OPERATION ### :: Source [{0}.{1}] :: Status [{2}]{3}";
            LogEnd(log, typeName, endOperationFormatMsg, ex, wasHandled, extraInfo, methodName: methodName);
        }

        private static void LogEnd(this ILog log, string typeName, string formatMsg,
                                   Exception ex = null, bool wasHandled = false,
                                   string extraInfo = null, bool infoLevel = false,
                                   [CallerMemberName] string methodName = null)
        {
            if (ex != null)
            {
                var statusCode = ex.ToStatusCode();

                log.Exception(ex,
                              $"Source [{typeName ?? "Unknown"}.{methodName ?? "Unknown"}] :: StatusCode [{statusCode}]",
                              wasHandled: wasHandled);
            }

            if (infoLevel)
            {
                log.InfoFormat(formatMsg, typeName, methodName,
                               ex != null
                                   ? "FAILED"
                                   : "SUCCESS",
                               extraInfo.HasValue()
                                   ? string.Concat(" -- ", extraInfo)
                                   : string.Empty);
            }
            else if (IsDebugInfoEnabled)
            {
                log.DebugInfoFormat(formatMsg, typeName, methodName,
                                    ex != null
                                        ? "FAILED"
                                        : "SUCCESS",
                                    extraInfo.HasValue()
                                        ? string.Concat(" -- ", extraInfo)
                                        : string.Empty);
            }
            else
            {
                log.DebugFormat(formatMsg, typeName, methodName,
                                ex != null
                                    ? "FAILED"
                                    : "SUCCESS",
                                extraInfo.HasValue()
                                    ? string.Concat(" -- ", extraInfo)
                                    : string.Empty);
            }
        }

        public static void DebugInfo(this ILog log, string msg)
        {
            if (IsDebugInfoEnabled)
            {
                log.Info(msg);
            }
            else
            {
                log.Debug(msg);
            }
        }

        public static void DebugInfoFormat(this ILog log, string format, params object[] args)
        {
            if (IsDebugInfoEnabled)
            {
                log.InfoFormat(format, args);
            }
            else
            {
                log.DebugFormat(format, args);
            }
        }

        public static void TraceInfo(this ILog log, string msg)
        {
            if (!IsTraceEnabled)
            {
                return;
            }

            log.Info(msg);
        }

        public static void TraceInfoFormat(this ILog log, string format, params object[] args)
        {
            if (!IsTraceEnabled)
            {
                return;
            }

            log.InfoFormat(format, args);
        }
    }
}
