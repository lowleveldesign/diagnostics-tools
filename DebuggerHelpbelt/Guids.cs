// Guids.cs
// MUST match guids.h
using System;

namespace LowLevelDesign.DebuggerHelpbelt
{
    static class GuidList
    {
        public const string guidDebuggerHelpbeltPkgString = "49257072-4100-4d3c-b603-b34ca35194bb";
        public const string guidDebuggerHelpbeltCmdSetString = "c61c3049-4ba2-4201-8327-7f9eb9468097";
        public const string guidToolWindowPersistanceString = "f3bebeda-4dff-4358-90db-498505eae4f8";

        public static readonly Guid guidDebuggerHelpbeltCmdSet = new Guid(guidDebuggerHelpbeltCmdSetString);
    };
}