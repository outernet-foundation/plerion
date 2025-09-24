using System;
using FofX;

namespace Plerion
{
    public class GroupLogger : ILogger
    {
        public LogGroup logGroup;

        public GroupLogger(LogGroup logGroup)
        {
            this.logGroup = logGroup;
        }

        public void Debug(string message)
        {
            Log.Debug(logGroup, message);
        }

        public void Error(string message)
        {
            Log.Error(logGroup, message);
        }

        public void Error(Exception exception)
        {
            Log.Error(logGroup, exception);
        }

        public void Error(string message, Exception exception)
        {
            Log.Error(logGroup, message, exception);
        }

        public void Generic(LogLevel logLevel, string message = null, Exception exception = null)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                    Trace(message);
                    break;

                case LogLevel.Debug:
                    Debug(message);
                    break;

                case LogLevel.Info:
                    Info(message);
                    break;

                case LogLevel.Warn:
                    Warning(message);
                    break;

                case LogLevel.Error:
                case LogLevel.Fatal:
                    Error(message, exception);
                    break;
            }
        }

        public void Info(string message)
        {
            Log.Info(logGroup, message);
        }

        public bool LevelEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Trace(string message)
        {
            Log.Trace(logGroup, message);
        }

        public void Warning(string message)
        {
            Log.Warn(logGroup, message);
        }
    }
}