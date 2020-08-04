using System;
using System.Collections;
using JetBrains.Annotations;

/// <summary>
/// A class that permits manually stepping through IEnumerator-based coroutines.
/// </summary>
public class Coroutines {
    private readonly struct RCoroutine {
        public readonly IEnumerator ienum;
        [CanBeNull] public readonly Node<RCoroutine> parent;
        public readonly bool droppable;

        public RCoroutine(IEnumerator ienum, [CanBeNull] Node<RCoroutine> parent = null, bool droppable = false) {
            this.ienum = ienum;
            this.parent = parent;
            this.droppable = droppable;
        }
    }
    private readonly NodeLinkedList<RCoroutine> coroutines = new NodeLinkedList<RCoroutine>();

    public IEnumerator AsIEnum() {
        while (coroutines.count > 0) {
            Step();
            if (coroutines.count == 0) yield break;
            yield return null;
        }
    }

    [CanBeNull] private Node<RCoroutine> itrNode = null;
    public void Step() {
        //There are three situations where nodes get added during execution:
        //Case 1: Another coroutine yields an IEnum. (Insert-after)
        //Case 2: Another coroutine directly invokes Run (Append at end) or RunPrepend (Append before pointer)
        //Case 3: Coroutine has null MoveNext, return to parent. (Insert-after)
        //Note: This iteration method is safe for two possible actions by the executed IEnum:
        // - Append at end (via RunRIEnum)
        // - Destroy all (via InvokeCull)
        //It is not safe for actions like "destroy arbitrary" (which do not have any interfaces anyways). 
        //That would require DMCompactingArray.
        Node<RCoroutine> nextNode;
        for (itrNode = coroutines.first; itrNode != null; itrNode = nextNode) {
            if (itrNode.obj.ienum.MoveNext()) {
                //MoveNext() can trigger abh.done, which can bubble up
                //to InvokeCull and returning this object to a pool. That calls ClearCoroutines,
                //which sends all the currently iterating nodes to a cache. Any further iteration
                //would be invalid.
                if (coroutines.count == 0) break;
                nextNode = itrNode.next; //Ensures that case 2 is handled
                if (itrNode.obj.ienum.Current is IEnumerator ienum) {
                    nextNode = coroutines.AddAfter(itrNode, new RCoroutine(ienum, itrNode, itrNode.obj.droppable));
                    coroutines.Remove(itrNode, false);
                }
            } else {
                //Same as above.
                if (coroutines.count == 0) break;
                if (itrNode.obj.parent != null) {
                    coroutines.InsertAfter(itrNode, itrNode.obj.parent);
                }
                nextNode = itrNode.next;
                coroutines.Remove(itrNode, true);
            }
        }
    }

    public int Count => coroutines.count;
    
    public void Close() {
        if (coroutines.count > 0) {
            Step();
            Node<RCoroutine> nextNode;
            for (Node<RCoroutine> n = coroutines.first; n != null; n = nextNode) {
                nextNode = n.next;
                if (n.obj.droppable) {
                    if (n.obj.parent != null) {
                        coroutines.InsertAfter(n, n.obj.parent);
                    }
                    nextNode = n.next;
                    coroutines.Remove(n, true);
                }
            }
        }
    }
    
    
    /// <summary>
    /// Run a couroutine that will be updated once every engine frame (120 fps).
    /// This coroutine is expected to clean up immediately on cancellation,
    /// and is not permitted to be dropped.
    /// </summary>
    /// <param name="ienum">Coroutine</param>
    public void Run(IEnumerator ienum) {
        coroutines.Add(new RCoroutine(ienum));
    }

    private void StepInPlace(Node<RCoroutine> n) {
        //Roughly copied from step function
        var lastItrNode = itrNode;
        itrNode = n;
        if (n.obj.ienum.MoveNext()) {
            if (coroutines.count > 0) {
                if (n.obj.ienum.Current is IEnumerator ienum) {
                    var nxt = coroutines.AddAfter(n, new RCoroutine(ienum, n, n.obj.droppable));
                    coroutines.Remove(n, false);
                    StepInPlace(nxt);
                }
            }
        } else {
            if (coroutines.count > 0) {
                coroutines.Remove(n, true);
            }
        }
        itrNode = lastItrNode;
    }
    
    
    /// <summary>
    /// Run a couroutine that will be updated once every engine frame (120 fps).
    /// This coroutine expected to clean up immediately on cancellation,
    /// and is not permitted to be dropped.
    /// This function can only be called while the coroutine object is updating, and will place the new coroutine
    /// before the current iteration pointer.
    /// </summary>
    /// <param name="ienum">Coroutine</param>
    public void RunPrepend(IEnumerator ienum) {
        if (itrNode == null) throw new Exception("Cannot prepend when not iterating coroutines");
        StepInPlace(coroutines.AddBefore(itrNode, new RCoroutine(ienum, null, itrNode.obj.droppable)));
    }
    
    /// <summary>
    /// Run a coroutine that will be updated once every engine frame (120 fps).
    /// This coroutine may be freely dropped if the object is destroyed.
    /// Use if the coroutine is not awaited by any code.
    /// </summary>
    /// <param name="ienum">Coroutine</param>
    public void RunDroppable(IEnumerator ienum) {
        coroutines.Add(new RCoroutine(ienum, null, true));
    }
}