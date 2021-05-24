using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Text;
using BagoumLib;
using UnityEngine;

namespace Danmokou.Core {
public static class Log {

    private const int MIN_LEVEL = (int) LogLevel.DEBUG1;
    private const int BUILD_MIN = (int) LogLevel.DEBUG2;


    private static StreamWriter? file;
    private const string LOGDIR = "DMK_Logs/";
    private static IDisposable? listener;

    static Log() {
        var d = DateTime.Now;
        var log = $"{LOGDIR}log_{d.Year}-{d.Month}-{d.Day}-{d.Hour}-{d.Minute}-{DateTime.Now.Second}.log";
        FileUtils.CheckDirectory(log);
        file = new StreamWriter(log);
        listener = Logging.Logs.Subscribe(lm => {
            if (lm.Exception != null)
                UnityException(lm.Exception, lm.Message);
            else
                Unity(lm.Message, lm.ShowStackTrace ?? true, lm.Level);
        });
    }

    public static void CloseLog() {
        file?.Close();
        file = null;
        listener?.Dispose();
        listener = null;
    }

    public static void Unity(string msg, bool stackTrace = true, LogLevel level = LogLevel.INFO) {
        if ((int) level < MIN_LEVEL) return;
#if UNITY_EDITOR
#else
        if ((int) level < BUILD_MIN) return;
#endif
        msg = $"Frame {ETime.FrameNumber}: {msg}";
        LogOption lo = stackTrace ? LogOption.None : LogOption.NoStacktrace;
        LogType unityLT = LogType.Log;
        file?.WriteLine(msg);
        if (level == LogLevel.WARNING) unityLT = LogType.Warning;
        if (level == LogLevel.ERROR) {
            unityLT = LogType.Error;
            Debug.LogError(msg);
        } else {
            Debug.LogFormat(unityLT, lo, null, msg.Replace("{", "{{").Replace("}", "}}"));
        }
    }

    public static void UnityError(string msg) {
        Unity(msg, true, LogLevel.ERROR);
    }

    public static void UnityException(Exception e, string prefixMsg="") => Log.UnityError(
        (string.IsNullOrWhiteSpace(prefixMsg) ? "" : $"{prefixMsg}\n") +
        Exceptions.PrintNestedException(e));

}
}
