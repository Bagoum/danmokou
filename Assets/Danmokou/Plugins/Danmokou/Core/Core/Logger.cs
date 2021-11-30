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

namespace Danmokou.Core {
public static class Logs {
    private const int MIN_LEVEL = (int) LogLevel.DEBUG1;
    private const int BUILD_MIN = (int) LogLevel.DEBUG2;

    public static readonly ISubject<LogMessage> DMKLogs = new Event<LogMessage>();

    private static readonly string? logFile;
    private static StreamWriter? fileStream;
    private const string LOGDIR = "DMK_Logs/";
    private static readonly List<IDisposable> listeners = new List<IDisposable>();

    static Logs() {
        if (!Application.isPlaying) return;
        var d = DateTime.Now;
        logFile = $"{LOGDIR}log_{d.Year}-{d.Month}-{d.Day}-{d.Hour}-{d.Minute}-{DateTime.Now.Second}.log";
        FileUtils.CheckDirectory(logFile);
        fileStream = new StreamWriter(logFile);
        listeners.Add(Logging.Logs.Subscribe(PrintToUnityLog));
        listeners.Add(DMKLogs.Subscribe(PrintToUnityLog));
        Log($"Opened log file {logFile}.");
    }

    public static void CloseLog() {
        Log($"Closing log file {logFile}.");
        fileStream?.Close();
        fileStream = null;
        foreach (var t in listeners)
            t.Dispose();
        listeners.Clear();
    }

    private const bool DEFAULT_USE_STACKTRACE =
#if UNITY_EDITOR
        true;
#else
        false;
#endif
    
    public static void Log(string msg, bool stackTrace = DEFAULT_USE_STACKTRACE, LogLevel level = LogLevel.INFO) =>
        DMKLogs.OnNext(new LogMessage(msg, level, null, stackTrace));

    public static void LogException(Exception e) => 
        DMKLogs.OnNext(new LogMessage("", LogLevel.ERROR, e, true));

    public static void UnityError(string msg) {
        Log(msg, true, LogLevel.ERROR);
    }

    private static string PrintException(Exception e, string prefixMsg="") => 
        (string.IsNullOrWhiteSpace(prefixMsg) ? "" : $"{prefixMsg}\n") +
        Exceptions.PrintNestedException(e);

    private static void PrintToUnityLog(LogMessage lm) {
        if ((int) lm.Level < MIN_LEVEL) return;
#if UNITY_EDITOR
        var useStacktrace = true;
#else
        if ((int) lm.Level < BUILD_MIN) return;
        var useStacktrace = lm.ShowStackTrace ?? (lm.Exception != null);
#endif
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

    private static readonly CSharpTypePrinter TypePrinter = new();
    private static readonly CSharpTypePrinter NSTypePrinter = new() { PrintTypeNamespace = _ => true };
    private static string GenerateStackTrace(int skipFrames = 5) {
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
            sb.AppendFormat(" (at <a href=\"{0}\" line=\"{1}\">{0}:{1}</a>)\n", 
                frame.GetFileName(), frame.GetFileLineNumber());
#else
            sb.AppendFormat(" (at {0}:{1})\n", frame.GetFileName(), frame.GetFileLineNumber());
#endif
        }


        return sb.ToString();
    }

}
}
