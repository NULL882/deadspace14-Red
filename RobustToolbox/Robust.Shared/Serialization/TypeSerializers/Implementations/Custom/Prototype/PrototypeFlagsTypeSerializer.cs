using System.Collections.Generic;
using System.Linq;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;
using Robust.Shared.Utility;

namespace Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype
{
    [TypeSerializer]
    public sealed class PrototypeFlagsTypeSerializer<T>
        : ITypeSerializer<PrototypeFlags<T>, SequenceDataNode>, ITypeSerializer<PrototypeFlags<T>, ValueDataNode>
        where T : class, IPrototype
    {
        public ValidationNode Validate(ISerializationManager serializationManager, SequenceDataNode node,
            IDependencyCollection dependencies, ISerializationContext? context = null)
        {
            var list = new List<ValidationNode>();

            foreach (var dataNode in node.Sequence)
            {
                if (dataNode is not ValueDataNode value)
                {
                    list.Add(new ErrorNode(dataNode, $"Cannot cast node {dataNode} to ValueDataNode."));
                    continue;
                }

                list.Add(serializationManager.ValidateNodeWith<string, PrototypeIdSerializer<T>, ValueDataNode>(value, context));
            }

            return new ValidatedSequenceNode(list);
        }

        public PrototypeFlags<T> Read(ISerializationManager serializationManager, SequenceDataNode node,
            IDependencyCollection dependencies, bool skipHook, ISerializationContext? context = null,
            PrototypeFlags<T>? rawValue = null)
        {
            if(rawValue != null)
                Logger.Warning($"Provided value to a Read-call for a {nameof(PrototypeFlags<T>)}. Ignoring...");

            var flags = new List<string>(node.Sequence.Count);

            foreach (var dataNode in node.Sequence)
            {
                if (dataNode is not ValueDataNode value)
                    continue;

                flags.Add(value.Value);
            }

            return new PrototypeFlags<T>(flags);
        }

        public DataNode Write(ISerializationManager serializationManager, PrototypeFlags<T> value,
            IDependencyCollection dependencies, bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return new SequenceDataNode(value.ToArray());
        }

        public PrototypeFlags<T> Copy(ISerializationManager serializationManager, PrototypeFlags<T> source, PrototypeFlags<T> target,
            bool skipHook, ISerializationContext? context = null)
        {
            return new PrototypeFlags<T>(source);
        }

        public ValidationNode Validate(ISerializationManager serializationManager, ValueDataNode node,
            IDependencyCollection dependencies, ISerializationContext? context = null)
        {
            return serializationManager.ValidateNodeWith<string, PrototypeIdSerializer<T>, ValueDataNode>(node, context);
        }

        public PrototypeFlags<T> Read(ISerializationManager serializationManager, ValueDataNode node,
            IDependencyCollection dependencies, bool skipHook, ISerializationContext? context = null,
            PrototypeFlags<T>? rawValue = null)
        {
            if(rawValue != null)
                Logger.Warning($"Provided value to a Read-call for a {nameof(PrototypeFlags<T>)}. Ignoring...");

            return new PrototypeFlags<T>(node.Value);
        }
    }
}
