using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TransformState
{
    public Transform t;
    public Vector3 pos;
    public Quaternion rot;
    public Vector3 scale;
    public bool active;
    public int depth;
}

public class Level : MonoBehaviour
{
    [HideInInspector] public bool isPassed = false;
    List<TransformState> initialStates = new List<TransformState>();
    public Transform levelEndTrigger;

    Vector3 rootInitialPosition;
    Quaternion rootInitialRotation;

    void Awake()
    {
        CacheInitialStates();
        rootInitialPosition = transform.position;
        rootInitialRotation = transform.rotation;
    }

    // Cache child states (local) so ResetLevel returns children to authoring state
    void CacheInitialStates()
    {
        initialStates.Clear();

        // Get all descendant transforms (including inactive)
        Transform[] all = GetComponentsInChildren<Transform>(true);
        foreach (var tr in all)
        {
            if (tr == this.transform) continue; // skip root
            var st = new TransformState
            {
                t = tr,
                pos = tr.localPosition,
                rot = tr.localRotation,
                scale = tr.localScale,
                active = tr.gameObject.activeSelf,
                depth = GetDepth(tr)
            };
            initialStates.Add(st);
        }
    }

    // Helper: compute depth relative to this level root (root = 0)
    int GetDepth(Transform tr)
    {
        int depth = 0;
        Transform cur = tr.parent;
        while (cur != null && cur != this.transform)
        {
            depth++;
            cur = cur.parent;
        }
        return depth;
    }

    // Reset level: restore children to initial states (DO NOT modify root transform)
    public void ResetLevel()
    {
        // Ensure parents are restored before children by sorting ascending depth
        initialStates.Sort((a, b) => a.depth.CompareTo(b.depth));

        foreach (var st in initialStates)
        {
            if (st == null || st.t == null) continue;

            st.t.localPosition = st.pos;
            st.t.localRotation = st.rot;
            st.t.localScale = st.scale;

            // Setting active state in depth order ensures parents are active before children
            st.t.gameObject.SetActive(st.active);

            // If this transform hosts an Obstacle component, reset it to its initial health/state
            var obs = st.t.GetComponent<Obstacle>();
            if (obs != null)
            {
                obs.ResetObstacle();
            }
        }
    }


    // Restore the root to its original authoring position/rotation (useful for ResetAllProgress or editor)
    public void ResetRootPosition()
    {
        transform.position = rootInitialPosition;
        transform.rotation = rootInitialRotation;
    }
}
