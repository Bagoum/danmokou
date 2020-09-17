using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
/// <summary>
/// Controls the state of an object by reading from the KVR repo.
/// </summary>
public class StateController : MonoBehaviour, IKVRWatcher {
    public enum StateReceiver {
        GameObjectEnable,
        /// <summary>
        /// Will destroy the object if unsatisfied.
        /// Currently, you cannot watch anything if you use this state.
        /// This may only be run on init before the object initializes.
        /// </summary>
        GameObjectExists,
        BehaviorScript,
        DialogueIndex,
    }

    [Serializable]
    public struct State {
        public KVR.Restraint restraint;
        public bool watchRestraint;
        public StateReceiver receiver;
        public int value;
        /// <summary>
        /// Checks if the restraints are satisfied, and perform an action accordingly.
        /// <para>
        /// GameObjectEnable/Exists are "complementary": they will perform the opposite action
        /// if the restraints are not satisfied.
        /// </para>
        /// </summary>
        /// <returns>True iff the restraints are satisfied.</returns>
        public bool SetIfSatisfied(GameObject go) {
            bool sat = restraint.Satisfied();
            Set(go, sat);
            return sat;
        }

        /// <summary>
        /// Add a callback to KVR to update the StateController 
        /// </summary>
        /// <param name="sc"></param>
        public void WatchIfRequired(StateController sc) {
            if (watchRestraint) restraint.Watch(sc);
        }

        private void Set(GameObject go, bool act) {
            //Complementary
            if (receiver == StateReceiver.GameObjectEnable) {
                go.SetActive((!act) ^ (value > 0));
            } else if (receiver == StateReceiver.GameObjectExists) {
                if (act ^ (value > 0)) {
                    go.SetActive(false);
                    Destroy(go);
                }
            } else if (act) {
                //Non-complementary
                if (receiver == StateReceiver.BehaviorScript) {
                    go.GetComponent<Danmaku.BehaviorEntity>().RunBehaviorScript(value);
                } else if (receiver == StateReceiver.DialogueIndex) {
                    Log.UnityError("Not implemented: StateReciver.DialogueIndex");
                } else {
                    Log.UnityError("Not implemented: " + receiver);
                }
            }
        }
    }

    /// <summary>
    /// A list of possible states. The first state to have its requirements satisfied will be selected.
    /// </summary>
    public State[] states;
    private int currState;

    private void Awake() {
        for (currState = 0; currState < states.Length; ++currState) {
            states[currState].WatchIfRequired(this);
        }
        KVRReevaluate();
    }

    public void KVRReevaluate() {
        GameObject go = gameObject;
        for (currState = 0; currState < states.Length; ++currState) {
            if (states[currState].SetIfSatisfied(go)) return;
        }
    }
}*/