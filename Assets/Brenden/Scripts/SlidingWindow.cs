using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GestureSmoother
{
    private Queue<float> scoreBuffer;
    private int bufferSize;

    // Constructor to set up the window size
    public GestureSmoother(int framesToKeep = 10)
    {
        bufferSize = framesToKeep;
        scoreBuffer = new Queue<float>();
    }

    /// <summary>
    /// Feeds a new frame's score into the buffer and returns the smoothed average.
    /// </summary>
    public float GetSmoothedScore(float currentFrameScore)
    {
        // 1. Add the newest score to the end of the line
        scoreBuffer.Enqueue(currentFrameScore);

        // 2. If the line is too long, kick out the oldest score
        if (scoreBuffer.Count > bufferSize)
        {
            scoreBuffer.Dequeue();
        }

        // 3. Calculate and return the average of the current window
        return scoreBuffer.Average();
    }

    // Optional: Clears the buffer if the user puts their hand down
    public void ResetBuffer()
    {
        scoreBuffer.Clear();
    }
}
