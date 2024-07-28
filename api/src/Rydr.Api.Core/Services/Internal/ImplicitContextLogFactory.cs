using Rydr.Api.Core.Extensions;
using ServiceStack.Logging;

namespace Rydr.Api.Core.Services.Internal;

public class ImplicitContextLogFactory : ILogFactory
{
    private readonly ILogFactory _logFactory;

    public ImplicitContextLogFactory(ILogFactory logFactory)
    {
        _logFactory = logFactory;
    }

    public ILog GetLogger(Type type) => new ImplicitContextLog(_logFactory.GetLogger(type));
    public ILog GetLogger(string typeName) => new ImplicitContextLog(_logFactory.GetLogger(typeName));
}

public class ImplicitContextLog : ILog
{
    private readonly ILog _logger;

    public ImplicitContextLog(ILog logger)
    {
        _logger = logger;
    }

    private string EnrichMessage(object message)
    {
        var m = ContextStack.CurrentStackLogInfo;

        return string.Concat(m,
                             string.IsNullOrEmpty(m)
                                 ? "00000000000000000000000000000000|0|0|0|GET|0|"
                                 : "|",
                             message);
    }

    public void Debug(object message)
    {
        // Just a simple perf optimization so we don't enrich things we aren't going to log
        if (_logger.IsDebugEnabled)
        {
            _logger.Debug(EnrichMessage(message));
        }
    }

    public void Debug(object message, Exception exception)
    {
        // Just a simple perf optimization so we don't enrich things we aren't going to log
        if (_logger.IsDebugEnabled)
        {
            _logger.Debug(EnrichMessage(message), exception);
        }
    }

    public void DebugFormat(string format, params object[] args)
    {
        // Just a simple perf optimization so we don't enrich things we aren't going to log
        if (_logger.IsDebugEnabled)
        {
            _logger.DebugFormat(EnrichMessage(format), args);
        }
    }

    public void Error(object message) => _logger.Error(EnrichMessage(message));

    public void Error(object message, Exception exception)
    {
        var msgString = message?.ToString();

        var msg = msgString.StartsWithOrdinalCi("!!! ")
                      ? msgString
                      : exception?.ToLogMessage(true, msgString) ?? msgString;

        _logger.Error(EnrichMessage(msg), exception);
    }

    public void ErrorFormat(string format, params object[] args) => _logger.ErrorFormat(EnrichMessage(format), args);

    public void Fatal(object message) => _logger.Fatal(EnrichMessage(message));

    public void Fatal(object message, Exception exception)
    {
        var msg = exception?.ToLogMessage(true, message.ToString()) ?? message?.ToString();
        _logger.Fatal(EnrichMessage(msg), exception);
    }

    public void FatalFormat(string format, params object[] args) => _logger.FatalFormat(EnrichMessage(format), args);

    public void Info(object message) => _logger.Info(EnrichMessage(message));

    public void Info(object message, Exception exception) => _logger.Info(EnrichMessage(message), exception);

    public void InfoFormat(string format, params object[] args) => _logger.InfoFormat(EnrichMessage(format), args);

    public void Warn(object message) => _logger.Warn(EnrichMessage(message));

    public void Warn(object message, Exception exception)
    {
        var msg = exception?.ToLogMessage(true, message.ToString()) ?? message?.ToString();
        _logger.Warn(EnrichMessage(msg), exception);
    }

    public void WarnFormat(string format, params object[] args) => _logger.WarnFormat(EnrichMessage(format), args);

    public bool IsDebugEnabled => _logger.IsDebugEnabled;
}
