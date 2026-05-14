using UnityEngine;
using System.Collections.Generic;
using TMPro;

public partial class LaserPainter
{
    // Undo/Redo - Erasing System
    private class PaintAction
    {
        public enum ActionType { Draw, Erase }
        public ActionType type;
        public GameObject lineObj;
    }
    
    private List<GameObject> activeLines = new List<GameObject>();
    private Stack<PaintAction> undoStack = new Stack<PaintAction>();
    private Stack<PaintAction> redoStack = new Stack<PaintAction>();
    private bool hasReleasedUndoRedo     = true;

    // Feedback Text
    private GameObject feedbackTextObject;
    private TextMeshPro feedbackText;
    private float feedbackTimer = 0f;

    private void CreateFeedbackText()
    {
        feedbackTextObject     = new GameObject("UndoRedo_FeedbackText");
        feedbackText = feedbackTextObject.AddComponent<TextMeshPro>();
        feedbackText.alignment = TextAlignmentOptions.Center;
        feedbackText.fontSize  = 4f;
        feedbackText.color     = Color.black;
        feedbackTextObject.SetActive(false);
    }

    private void FinalizeCurrentLine()
    {
        if (currentLineObj != null)
        {
            activeLines.Add(currentLineObj);
            undoStack.Push(new PaintAction { type = PaintAction.ActionType.Draw, lineObj = currentLineObj });
            redoStack.Clear(); // Clear redo history when a new action is made
            currentLineObj = null;
            currentLine    = null;
        }
    }

    private void TryEraseLine(Vector3 erasePos)
    {
        // Iterate backwards - safely remove from the list
        for (int i = activeLines.Count - 1; i >= 0; i--)
        {
            GameObject lineObj = activeLines[i];
            LineRenderer lr    = lineObj.GetComponent<LineRenderer>();
            
            Vector3[] positions = new Vector3[lr.positionCount];
            lr.GetPositions(positions);

            foreach (Vector3 point in positions)
            {
                if (Vector3.Distance(point, erasePos) <= eraseRadius)
                {
                    // Erase it!
                    lineObj.SetActive(false);
                    activeLines.RemoveAt(i);
                    
                    undoStack.Push(new PaintAction { type = PaintAction.ActionType.Erase, lineObj = lineObj });
                    redoStack.Clear();
                    break; 
                }
            }
        }
    }

    private void HandleUndoRedoGestures()
    {
        string rightGesture = manoReceiver.currentRightGesture;

        if (rightGesture == "Thumb_Down")
        {
            if (hasReleasedUndoRedo)
            {
                PerformUndo();
                hasReleasedUndoRedo = false;
            }
        }
        else if (rightGesture == "Thumb_Up")
        {
            if (hasReleasedUndoRedo)
            {
                PerformRedo();
                hasReleasedUndoRedo = false;
            }
        }
        else hasReleasedUndoRedo = true; // Requires release before firing again
    }

    private void PerformUndo()
    {
        if (undoStack.Count == 0) return;

        PaintAction action = undoStack.Pop();
        redoStack.Push(action);

        if (action.type == PaintAction.ActionType.Draw)
        {
            action.lineObj.SetActive(false);
            activeLines.Remove(action.lineObj);
        }
        else if (action.type == PaintAction.ActionType.Erase)
        {
            action.lineObj.SetActive(true);
            activeLines.Add(action.lineObj);
        }

        ShowFeedback("Undo");
    }

    private void PerformRedo()
    {
        if (redoStack.Count == 0) return;

        PaintAction action = redoStack.Pop();
        undoStack.Push(action);

        if (action.type == PaintAction.ActionType.Draw)
        {
            action.lineObj.SetActive(true);
            activeLines.Add(action.lineObj);
        }
        else if (action.type == PaintAction.ActionType.Erase)
        {
            action.lineObj.SetActive(false);
            activeLines.Remove(action.lineObj);
        }

        ShowFeedback("Redo");
    }
}
