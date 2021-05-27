using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Util
{
    /// <summary>
    /// Iterates from center out.
    /// </summary>
    /// <param name="centerX"></param>
    /// <param name="centerY"></param>
    /// <returns></returns>
    public static void IterateCircular(int centerX, int centerY, int radius, Action<int, int> callback)
    {
        callback?.Invoke(centerX, centerY);
        int count = 2;
        for (int n = 0; n < radius; n++)
        {
            /*
                *****
                *---*
                *-.-*
                *---*
                *****
            */
            int x = -count / 2;
            int y = -count / 2;
            for (int i = 0; i < count; i++, x++)
            {
                callback?.Invoke(x, y);
            }
            for (int i = 0; i < count; i++, y++)
            {
                callback?.Invoke(x, y);
            }
            for (int i = 0; i < count; i++, x--)
            {
                callback?.Invoke(x, y);
            }
            for (int i = 0; i < count; i++, y--)
            {
                callback?.Invoke(x, y);
            }
            count += 2;
        }
    }
}