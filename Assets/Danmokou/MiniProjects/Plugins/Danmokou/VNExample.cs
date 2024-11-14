using System.Reactive;
using System.Threading.Tasks;
using Danmokou.Core;
using Danmokou.VN;
using Scriptor;
using Suzunoya;
using Suzunoya.ControlFlow;
using SuzunoyaUnity.Rendering;
using static SuzunoyaUnity.Helpers;

namespace MiniProjects.VN {
[Reflect]
public static class VNExample {
    public static Task ExampleDialogue(DMKVNState vn) => new BoundedContext<Unit>(vn, "test", async () => {
        vn.DefaultRenderGroup.Alpha = 0;
        _ = vn.DefaultRenderGroup.FadeTo(1, 0.4f).Task;
        using var md = vn.Add(new ADVDialogueBox());
        using var reimu = vn.Add(new Reimu());
        using var marisa = vn.Add(new Marisa());
        reimu.LocalLocation.Value = new(-3f, 0, 0);
        marisa.LocalLocation.Value = new(4f, 0, 0);
        marisa.Alpha = 0;
        await reimu.SayC("hello world");
        await marisa.FadeTo(1f, 1f).And(marisa.MoveBy(V3(-1f, 0), 1f));
        await marisa.ESayC("happy", "foo bar");
        await vn.DefaultRenderGroup.FadeTo(0f, 1f);
        return default;
    }).Execute();
}
}