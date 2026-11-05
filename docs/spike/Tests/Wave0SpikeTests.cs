// Wave0SpikeTests.cs - PlayMode wrapper so the Wave 0 spike can run headless:
//   Unity.exe -batchmode -nographics -runTests -testPlatform PlayMode ...
// It only drives Wave0Spike to completion; it makes no verdicts. The "[SPIKE]" log lines in the
// test-results XML / Editor log are the deliverable. Throwaway, like the spike itself.

using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class Wave0SpikeTests
{
    // Hard frame cap. A normal run finishes in well under 200 frames.
    private const int MaxFrames = 20000;

    [UnityTest]
    public IEnumerator Wave0Spike_RunsToDone()
    {
        // The spike may emit warnings/errors (e.g. Physics2D or URP notices in -nographics);
        // they are data, not test failures.
        LogAssert.ignoreFailingMessages = true;

        var host = new GameObject("Wave0SpikeHost");
        host.AddComponent<Wave0Spike>();

        int frames = 0;
        while (!Wave0Spike.IsDone)
        {
            frames++;
            if (frames > MaxFrames)
            {
                Assert.Fail("timeout: Wave0Spike did not reach [SPIKE][DONE] within " + MaxFrames
                            + " frames (last phase=" + Wave0Spike.PhaseName + ")");
            }
            yield return null;
        }

        Debug.Log("[SPIKE][TEST] Wave0Spike reached DONE after " + frames + " frames (PlayMode test wrapper)");
        Object.Destroy(host);
        yield return null;
    }
}
