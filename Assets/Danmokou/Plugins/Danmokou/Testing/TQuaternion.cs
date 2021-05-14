using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

namespace Danmokou.Testing {
public static class TQuaternion {
    public static void TestQuaternion() {
        Profiler.BeginSample("Quaternion TEST");
        Vector3 vi = new Vector3(1f, 0f, 0f);
        Vector3 vj = new Vector3(0f, 1f, 0f);
        Vector3 vk = new Vector3(0f, 0f, 1f);
        Quaternion q = Quaternion.Euler(0f, 20f, 45f);
        Vector3 vir = q * vi;
        Vector3 vjr = q * vj;
        Vector3 vkr = q * vk;
        
        Profiler.EndSample();
        Debug.Log(vir * 100f);
        Debug.Log(vjr * 100f);
        Debug.Log(vkr * 100f);
    }
}
}