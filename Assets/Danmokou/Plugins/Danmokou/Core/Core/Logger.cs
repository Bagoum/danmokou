using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text;
using BagoumLib;
using BagoumLib.Events;
using UnityEngine;

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
#else
        if ((int) lm.Level < BUILD_MIN) return;
#endif
        var msg = (lm.Exception == null) ? lm.Message : PrintException(lm.Exception, lm.Message);
        msg = $"Frame {ETime.FrameNumber}: {msg}";
        
        LogOption lo = (lm.ShowStackTrace == false) ? LogOption.NoStacktrace : LogOption.None;
        LogType unityLT = LogType.Log;
        fileStream?.WriteLine(msg);
        if (lm.Level == LogLevel.WARNING) 
            unityLT = LogType.Warning;
        if (lm.Level == LogLevel.ERROR) {
            //unityLT = LogType.Error;
            Debug.LogError(msg);
        } else {
            Debug.LogFormat(unityLT, lo, null, msg.Replace("{", "{{").Replace("}", "}}"));
        }
    }

}
}
