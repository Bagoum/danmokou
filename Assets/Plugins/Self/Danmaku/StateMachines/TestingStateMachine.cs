using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Danmaku;
using UnityEngine;

namespace SM {
public class DebugLASM : LineActionSM {
    private readonly string msg;
    public DebugLASM(string msg) {
        this.msg = msg;
    }
    public override Task Start(SMHandoff smh) {
        Log.Unity(msg);
        return Task.CompletedTask;
    }
}
public class ErrorLASM : LineActionSM {
    private readonly string msg;
    public ErrorLASM(string msg) {
        this.msg = msg;
    }
    public override Task Start(SMHandoff smh) {
        throw new Exception(msg);
    }
}

}