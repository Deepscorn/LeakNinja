using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Core
{
    public class Log
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Message(object message) => Debug.Log(message);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Warning(object message) => Debug.LogWarning(message);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Error(object message) => Debug.LogError(message);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scope(object message, Action action)
        {
            Debug.Log($"Start {message}");
            var failed = false;
            var ticks = Environment.TickCount;
            var elapsedTicks = 0;
            try
            {
                action();
            }
            catch
            {
                failed = true;
                elapsedTicks = Environment.TickCount - ticks;
                throw;
            }
            finally
            {
                if (elapsedTicks == 0)
                {
                    elapsedTicks = Environment.TickCount - ticks;
                }
                Debug.Log($"{(!failed ? "Finish" : "Fail")} {message} in {(float)elapsedTicks / TimeSpan.TicksPerMillisecond} ms");
            }
        }
    }
}