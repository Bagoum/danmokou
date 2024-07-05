using Danmokou.ADV;
using Danmokou.Core;
using UnityEngine.UIElements;
using static Danmokou.Services.GameManagement;
using static Danmokou.UI.XML.XMLUtils;

namespace Danmokou.UI.XML {
public static partial class XMLHelpers {
    private class SaveLoadData : UIViewModel {
        public int i { get; }
        public SerializedSave? Save =>
            SaveData.v.Saves.TryGetValue(i, out var s) ? s : null;

        public SaveLoadData(int i) {
            this.i = i;
        }

        public override long GetViewHash() =>
            (Save?.Description, Save?.Image.Texture).GetHashCode();
    }

    private class SaveLoadDataView : UIView<SaveLoadData> {
        public SaveLoadDataView(SaveLoadData data) : base(data) { }

        protected override BindingResult Update(in BindingContext context) {
            var title = HTML.Q<Label>("Title");
            title.text = $"Save #{ViewModel.i + 1}";
            var desc = HTML.Q<Label>("Description");
            var bg = HTML.Q("SS");
            if (ViewModel.Save is { } save) {
                title.RemoveFromClassList("saveentry-title-unset");
                desc.text = save.Description;
                bg.style.backgroundImage = save.Image.Texture;
            } else {
                title.AddToClassList("saveentry-title-unset");
                desc.text = "";
                bg.style.backgroundImage = UXMLPrefabs.defaultSaveLoadBG;
            }
            return base.Update(in context);
        }
    }
}
}