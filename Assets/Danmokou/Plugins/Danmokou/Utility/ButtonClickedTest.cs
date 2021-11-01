using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonClickedTest : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler {

    public void WasClicked() {
        Debug.Log($"Clicked {gameObject.name}");
    }

    public void OnMouseDown() {
        Debug.Log("Mouse down");
    }

    public void OnMouseEnter() {
        Debug.Log("Mouse enter");
    }

    public void OnMouseExit() {
        Debug.Log("Mouse exit");
    }

    public void OnMouseUpAsButton() {
        Debug.Log("Mouse up as button");
    }

    public void OnPointerEnter(PointerEventData eventData) {
        Debug.Log($"Pointer enter {gameObject.name} {eventData.enterEventCamera.gameObject.name}");
    }

    public void OnPointerExit(PointerEventData eventData) {
        Debug.Log($"Pointer exit {gameObject.name}");
    }
}