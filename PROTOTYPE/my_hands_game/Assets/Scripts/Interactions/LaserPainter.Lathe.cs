using UnityEngine;
using System.Collections.Generic;

public partial class LaserPainter
{
    // Lathe Mechanics
    private bool _showRotationAxis = false;
    public bool showRotationAxis
    {
        get { return _showRotationAxis; }
        set // Event Trigger...
        {
            if (_showRotationAxis == value) return;
            _showRotationAxis = value;

            if (paperTransform != null)
            {
                if (_showRotationAxis)
                {
                    ShrinkAndMovePaper();
                    CreateVisibleAxis();
                }
                else
                {
                    RestorePaper();
                    if (axisObj != null) axisObj.SetActive(false);
                }
            }
        }
    }
    
    private GameObject axisObj;
    private Vector3 originalPaperScale;
    private Vector3 originalPaperPos;
    private bool hasSavedOriginalPaper = false;

    private void CreateVisibleAxis()
    {
        if (axisObj != null) { axisObj.SetActive(true); return; }

        axisObj = new GameObject("PaperRotationAxis");
        axisObj.transform.SetParent(paperTransform);
        axisObj.transform.localPosition = Vector3.zero;
        axisObj.transform.localRotation = Quaternion.identity;

        LineRenderer axisLr  = axisObj.AddComponent<LineRenderer>();
        axisLr.useWorldSpace = false;
        axisLr.positionCount = 2;
        axisLr.startWidth    = 0.05f;
        axisLr.endWidth      = 0.05f;
        axisLr.material      = new Material(Shader.Find("Sprites/Default"));
        axisLr.startColor    = Color.blue;
        axisLr.endColor      = Color.blue;
        
        // Original center axis -> Left edge of the paper - Local Space (shift to my right + halve the width)
        MeshFilter mf   = paperTransform.GetComponent<MeshFilter>();
        float leftEdgeX = (mf != null && mf.sharedMesh != null) ? -mf.sharedMesh.bounds.extents.x : -0.5f;

        // Draw the axis line along the (new) left edge
        axisLr.SetPosition(0, new Vector3(leftEdgeX, 0, -0.5f));
        axisLr.SetPosition(1, new Vector3(leftEdgeX, 0,  0.5f));
    }

    private void ShrinkAndMovePaper()
    {
        MeshFilter mf = paperTransform.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        if (!hasSavedOriginalPaper)
        {
            originalPaperScale    = paperTransform.localScale;
            originalPaperPos      = paperTransform.localPosition;
            hasSavedOriginalPaper = true;
        }

        float unscaledWidth = mf.sharedMesh.bounds.size.x;

        // Halve the local scale X
        Vector3 newScale          = paperTransform.localScale;
        newScale.x               *= 0.5f;
        paperTransform.localScale = newScale;

        // Shift paper to my right (half the new scaled width) -> New left edge = Original center
        float offset             = (unscaledWidth * newScale.x) * 0.5f; 
        paperTransform.position += paperTransform.right * offset;
    }

    private void RestorePaper()
    {
        if (hasSavedOriginalPaper && paperTransform != null)
        {
            paperTransform.localScale    = originalPaperScale;
            paperTransform.localPosition = originalPaperPos;
        }
    }
}
