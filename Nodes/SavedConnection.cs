using Godot;
using Godot.Collections;

namespace OpenScadGraphEditor.Nodes
{
    /// <summary>
    /// This resource represents a saved connection between nodes.
    /// </summary>
    public class SavedConnection : Resource
    {
        [Export]
        public string FromId;

        [Export] 
        public int FromPort;

        [Export]
        public string ToId;

        [Export]
        public int ToPort;
    }
}