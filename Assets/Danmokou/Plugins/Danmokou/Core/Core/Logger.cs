using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using BagoumLib;
using BagoumLib.Events;
using BagoumLib.Expressions;
using UnityEngine;
using Debug = UnityEngine.Debug;
using static Danmokou.Core.LogUtils;
using Logger = BagoumLib.Logger;

namespace Danmokou.Core {
public static class Logs {
    private const LogLevel MIN_LEVEL = LogLevel.DEBUG1;
    private const LogLevel BUILD_MIN = LogLevel.DEBUG2;

    public static readonly Logger DMKLogs = new();

    public static readonly string? logFile;
    private static StreamWriter? fileStream;
    private const string LOGDIR = "DMK_Logs/";
    private static readonly List<IDisposable> listeners = new List<IDisposable>();

    public static bool Verbose { get; set; } = false;

    static Logs() {
#if UNITY_EDITOR
        if (!Application.isPlaying) return;
#endif
        var d = DateTime.Now;
        logFile = $"{LOGDIR}log_{d.Year}-{d.Month}-{d.Day}-{d.Hour}-{d.Minute}-{DateTime.Now.Second}.log";
        FileUtils.CheckPath(ref logFile);
        fileStream = new StreamWriter(logFile);
        listeners.Add(Logging.Logs.RegisterListener(DMKLogs));
        listeners.Add(DMKLogs.RegisterListener(LogToUnityAndFileImpl));
        Log($"Opened log file {logFile}.");
    }

#if UNITY_EDITOR
    public static void SetupTestMode() {
        listeners.Add(Logging.Logs.RegisterListener(DMKLogs));
        listeners.Add(DMKLogs.RegisterListener(LogToUnityAndFileImpl));
    }
#endif

    public static void CloseLog() {
        if (fileStream != null) {
            Log($"Closing log file {logFile}.");
            fileStream.Close();
            fileStream = null;
        }
        listeners.DisposeAll();
    }
    
    public static void Log(string msg, bool? stackTrace = null, LogLevel level = LogLevel.INFO) =>
        DMKLogs.Log(msg, level, stackTrace);

    public static void LogException(Exception e) => 
        DMKLogs.Error(e);

    public static void UnityError(string msg) {
        Log(msg, true, LogLevel.ERROR);
    }

    private static readonly LogImpl LogToUnityAndFileImpl = new();
    private class LogImpl : ILogListener {
        public void OnCompleted() { }

        public void OnError(Exception error) { }

        public void OnNext(LogMessage lm) {
            if (CanSkipMessage(lm.Level, lm.Exception)) return;
            var useStacktrace = Verbose || (lm.ShowStackTrace ?? (lm.Exception != null));
            var msg = (lm.Exception == null) ? lm.Message : PrintException(lm.Exception, lm.Message);
            msg = $"Frame {ETime.FrameNumber}: {msg}";
            if (useStacktrace)
                msg = $"{msg}\n{GenerateStackTrace()}";    
            fileStream?.WriteLine(msg);
            Debug.LogFormat(lm.Level switch {
                LogLevel.ERROR => LogType.Error,
                LogLevel.WARNING => LogType.Warning,
                _ => LogType.Log
            }, LogOption.NoStacktrace, null, msg.Replace("{", "{{").Replace("}", "}}"));
        }

        public bool CanSkipMessage(LogLevel level, Exception? exc) {
#if UNITY_EDITOR
            return !Verbose && level < MIN_LEVEL && exc == null;
#else
            return !Verbose && level < BUILD_MIN && exc == null;
#endif
        }
    }
}

//Avoid static instantiation of Logs class when using for language server
public static class LogUtils {
    public static string PrintException(Exception e, string prefixMsg="") =>
        (string.IsNullOrWhiteSpace(prefixMsg) ? "" : $"{prefixMsg}\n") +
        Exceptions.PrintNestedExceptionInverted(e);

    private static readonly CSharpTypePrinter TypePrinter = new();
    private static readonly CSharpTypePrinter NSTypePrinter = new() { PrintTypeNamespace = _ => true };
    public static string GenerateStackTrace(int skipFrames = 5) {
        var sb = new StringBuilder();
        var st = new StackTrace(skipFrames, true);
        for (int ii = 0; ii < st.FrameCount; ++ii) {
            var frame = st.GetFrame(ii);
            var mi = frame.GetMethod();
            if (mi.DeclaringType == null)
                continue;
            sb.Append(NSTypePrinter.Print(mi.DeclaringType));
            sb.Append('.');
            sb.Append(mi.Name);
            sb.Append('(');
            var prms = mi.GetParameters();
            for (int jj = 0; jj < prms.Length; ++jj) {
                if (jj > 0)
                    sb.Append(", ");
                sb.Append(TypePrinter.Print(prms[jj].ParameterType));
            }
            sb.Append(')');
#if UNITY_EDITOR
            sb.AppendFormat(" (at {0})\n", ToFileLink(frame.GetFileName(), frame.GetFileLineNumber()));
#else
            sb.AppendFormat(" (at {0}:{1})\n", frame.GetFileName(), frame.GetFileLineNumber());
#endif
        }
        return sb.ToString();
    }

    public static string ToFileLink(string? filename, int line, string? content = null) =>
        $"<a href=\"{filename}\" line=\"{line}\">{content ?? $"{filename}:{line}"}</a>";
}


}
