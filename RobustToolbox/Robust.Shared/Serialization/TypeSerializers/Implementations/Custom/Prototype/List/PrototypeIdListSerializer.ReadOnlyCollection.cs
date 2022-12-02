﻿using System.Collections.Generic;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List
{
    public partial class PrototypeIdListSerializer<T> :
        ITypeSerializer<IReadOnlyCollection<string>, SequenceDataNode>
        where T : class, IPrototype
    {
        ValidationNode ITypeValidator<IReadOnlyCollection<string>, SequenceDataNode>.Validate(
            ISerializationManager serializationManager,
            SequenceDataNode node,
            IDependencyCollection dependencies,
            ISerializationContext? context)
        {
            return ValidateInternal(serializationManager, node, dependencies, context);
        }

        IReadOnlyCollection<string> ITypeReader<IReadOnlyCollection<string>, SequenceDataNode>.Read(
            ISerializationManager serializationManager,
            SequenceDataNode node,
            IDependencyCollection dependencies,
            bool skipHook,
            ISerializationContext? context, IReadOnlyCollection<string>? rawValue)
        {
            if(rawValue != null)
                Logger.Warning($"Provided value to a Read-call for a {nameof(IReadOnlyCollection<string>)}. Ignoring...");

            var list = new List<string>();

            foreach (var dataNode in node.Sequence)
            {
                list.Add(PrototypeSerializer.Read(
                    serializationManager,
                    (ValueDataNode) dataNode,
                    dependencies,
                    skipHook,
                    context));
            }

            return list;
        }

        DataNode ITypeWriter<IReadOnlyCollection<string>>.Write(ISerializationManager serializationManager,
            IReadOnlyCollection<string> value,
            IDependencyCollection dependencies,
            bool alwaysWrite,
            ISerializationContext? context)
        {
            return WriteInternal(serializationManager, value, dependencies, alwaysWrite, context);
        }

        IReadOnlyCollection<string> ITypeCopier<IReadOnlyCollection<string>>.Copy(
            ISerializationManager serializationManager,
            IReadOnlyCollection<string> source,
            IReadOnlyCollection<string> target,
            bool skipHook,
            ISerializationContext? context)
        {
            return new List<string>(source);
        }
    }
}
