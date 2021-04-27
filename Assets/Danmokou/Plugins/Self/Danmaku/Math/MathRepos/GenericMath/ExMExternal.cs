using DMK.Core;
using DMK.Expressions;
using Ex = System.Linq.Expressions.Expression;
using static DMK.Expressions.ExMHelpers;
using tfloat = DMK.Expressions.TEx<float>;
using tbool = DMK.Expressions.TEx<bool>;
using static DMK.Core.GameManagement;

namespace DMK.DMath.Functions {
/// <summary>
/// See <see cref="ExM"/>. This class contains functions related to variables external to the game instance, such as the state of save data. Note that these functions are not safe for use in replays.
/// </summary>
[Reflect]
public static class ExMExternal {
    private static readonly ExFunction campaignCompleted =
        ExUtils.Wrap<SaveData.Record>("CampaignCompleted", typeof(string));

    private static Ex saveRecord => Ex.Property(null, typeof(SaveData), "r");

    public static tbool CampaignCompleted(string campaign) =>
        campaignCompleted.InstanceOf(saveRecord, Ex.Constant(campaign));



}
}