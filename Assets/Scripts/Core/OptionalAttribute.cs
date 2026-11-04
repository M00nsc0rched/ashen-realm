using System;
using UnityEngine;

namespace AshenRealm.Core
{
    /// <summary>
    /// Marks a serialized object reference as optional so the prototype validator does not report it when empty.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OptionalAttribute : PropertyAttribute
    {
    }
}
