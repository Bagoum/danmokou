using Danmokou.Core;
using Danmokou.DMath;
using Scriptor;

namespace Danmokou.SM {
[Reflect]
public static class HelperStateMachines {
    private static StateMachine? WaitForPhaseSM;
    
    /// <summary>
    /// Load a StateMachine from the provided file.
    /// </summary>
    [BDSL2Only]
    public static StateMachine File(string filename) => 
        StateMachineManager.FromName(filename) ?? throw new CompileException($"Couldn't load StateMachine from file {filename}");
    
    /// <summary>
    /// A null StateMachine.
    /// </summary>
    [BDSL2Only]
    public static StateMachine Null() => null!;
    
    /// <summary>
    /// A StateMachine that waits infinitely.
    /// </summary>
    [BDSL2Only]
    public static StateMachine Stall() => 
        WaitForPhaseSM ??= SMReflection.Wait(Synchronization.Time(_ => M.IntFloatMax));
    
}
}