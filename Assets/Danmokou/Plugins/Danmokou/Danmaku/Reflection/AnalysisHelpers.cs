using BagoumLib;
using BagoumLib.Reflection;
using JetBrains.Annotations;
using LanguageServer.VsCode.Contracts;
using Mizuhashi;
using Scriptor.Reflection;
using Position = LanguageServer.VsCode.Contracts.Position;

namespace Danmokou.Reflection {
/// <summary>
/// Helpers consumed by the language server project.
/// </summary>
[PublicAPI]
public static class AnalysisHelpers {
    public static Reflector.ParamFeatures[]? ParamFeatures(this IMethodSignature sig) => 
        TypeMemberFeatures.Features(sig.Member);
    
    public static Reflector.ParamFeatures? FeaturesAt(this IMethodSignature sig, int ii) => 
        sig.ParamFeatures()?.Try(ii);
}
}