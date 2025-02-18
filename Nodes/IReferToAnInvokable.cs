using OpenScadGraphEditor.Library;

namespace OpenScadGraphEditor.Nodes
{
    /// <summary>
    /// Interface to be implemented by nodes which refer to an invokable.
    /// </summary>
    public interface IReferToAnInvokable : ICannotBeCreated
    {
        InvokableDescription InvokableDescription { get; }
        
        /// <summary>
        /// Makes the nodes ports match the invokable description and connects the node to this invokable.
        /// </summary>
        void SetupPorts(InvokableDescription description);

        /// <summary>
        /// Returns the input port index that corresponds to the given parameter index. Returns -1 if no such port exists.
        /// The implementation is supposed to to not look at the actual invokable description but rather be a rule for how
        /// ports would be laid out. It should not change its return value based on the actual invokable description.
        /// </summary>
        int GetParameterInputPort(int parameterIndex);

        /// <summary>
        /// Returns the output port index that corresponds to the given parameter index. Returns -1 if no such port exists.
        /// The implementation is supposed to to not look at the actual invokable description but rather be a rule for how
        /// ports would be laid out. It should not change its return value based on the actual invokable description.
        /// </summary>
        int GetParameterOutputPort(int parameterIndex);

    }
}