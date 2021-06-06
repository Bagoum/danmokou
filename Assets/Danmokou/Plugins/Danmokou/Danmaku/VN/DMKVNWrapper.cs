using System;
using System.Collections;
using System.Collections.Generic;
using Danmokou.Core;
using Danmokou.UI;
using SuzunoyaUnity;
using UnityEngine;

/// <summary>
/// Loads mimic information from GameManagement.References.SuzunoyaReferences.
/// Also adjusts position to account for UICamera offset.
/// </summary>
public class DMKVNWrapper : VNWrapper {
    public UIManager ui = null!;
    protected override void Awake() {
        var refs = GameManagement.References.suzunoyaReferences;
        if (refs != null) {
            renderGroupMimic = refs.renderGroupMimic;
            entityMimics = refs.entityMimics;
        }
        base.Awake();
    }

    private void Start() {
        tr.localPosition = ui.uiCamera.transform.localPosition;
    }
}