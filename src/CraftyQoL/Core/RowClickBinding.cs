using System;

namespace CraftyQoL.Core
{
    // A recycled control must never dispatch a press that started on a previous recipe.
    internal sealed class RowClickBinding
    {
        private Action? _action;
        internal long Version { get; private set; }
        internal void Bind(Action? action){unchecked{Version++;}_action=action;}
        internal void Clear()=>Bind(null);
        internal void Invoke(long version){if(version==Version)_action?.Invoke();}
    }
}
