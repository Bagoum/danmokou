using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowHideMenu : MonoBehaviour {
    public Canvas canvas;

    protected virtual void Start() {
        Hide();
    }
    public virtual void Hide() {
        canvas.enabled = false;
    }

    public virtual void Show() {
        canvas.enabled = true;
    }
    
}
public class DefaultPauseMenu : ShowHideMenu { }