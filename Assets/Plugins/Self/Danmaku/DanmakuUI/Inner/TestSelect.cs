using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Danmaku.DanmakuUI;
using UnityEngine;
using UnityEngine.UIElements;
using PointerType = UnityEngine.UIElements.PointerType;

[RequireComponent(typeof(UIDocument))]
public class TestSelect : MonoBehaviour {
    // Start is called before the first frame update
    public int selected = -1;
    private List<VisualElement> opts;
    private ListView lis;

    public VisualTreeAsset listItem;

    private VisualElement visualTree;
    

    void Start() {
        visualTree = GetComponent<UIDocument>().rootVisualElement;
        //panelTransform = panelTransform.GetComponent<WorldQuadDisplay>();
        visualTree.RegisterCallback<KeyDownEvent>(KeyDown);
        visualTree.RegisterCallback<MouseDownEvent>(MouseDown);
        lis = visualTree.Query<ListView>("mylist").ToList()[0];
        opts = visualTree.Query(null, "testhello").ToList();
        Debug.Log(opts.Count);
    }

    private void NavDown() {
        if (++selected >= opts.Count) {
            selected = 0;
        }
        UpdateSelected();
    }
    private void NavUp() {
        if (--selected < 0) {
            selected = opts.Count - 1;
        }
        UpdateSelected();
    }

    private void MouseDown(MouseDownEvent me) {
        Debug.Log($"mouse down");
    }

    private void UpdateSelected() {
        opts.SetClassOfSelected(selected, "selected");
        //lis.Add(listItem.CloneTree()); //You can do this-- but you'll need to rebuild ops (or persist it as a query)
        //ScrollToItem only works for in-code objects
        lis.ScrollTo(opts[selected]);
    }

    private void KeyDown(KeyDownEvent ev) {
        if (ev.keyCode == KeyCode.UpArrow || ev.keyCode == KeyCode.LeftArrow) {
            NavUp();
        } else if (ev.keyCode == KeyCode.DownArrow || ev.keyCode == KeyCode.RightArrow) {
            NavDown();
        }
        ev.StopPropagation();
    }


}