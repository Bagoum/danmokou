using BagoumLib.Expressions;
using Danmokou.Core;
using Danmokou.Expressions;
using Ex = System.Linq.Expressions.Expression;
using static Danmokou.Expressions.ExMHelpers;
using tfloat = Danmokou.Expressions.TEx<float>;
using tbool = Danmokou.Expressions.TEx<bool>;
using static Danmokou.Services.GameManagement;

namespace Danmokou.DMath.Functions {
/// <summary>
/// See <see cref="ExM"/>. This class contains functions related to variables external to the game instance, such as the state of save data. Note that these functions are not safe for use in replays.
/// </summary>
[Reflect]
public static class ExMExternal {
    private static readonly ExFunction campaignCompleted =
        ExFunction.Wrap<SaveData.Record>("CampaignCompleted", typeof(string));

    private static Ex saveRecord => Ex.Property(null, typeof(SaveData), "r");

    public static tbool CampaignCompleted(string campaign) =>
        campaignCompleted.InstanceOf(saveRecord, Ex.Constant(campaign));



}
}