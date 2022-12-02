using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Robust.Shared.Serialization.TypeSerializers.Implementations.Generic
{
    [TypeSerializer]
    public sealed class HashSetSerializer<T> :
        ITypeSerializer<HashSet<T>, SequenceDataNode>,
        ITypeSerializer<ImmutableHashSet<T>, SequenceDataNode>
    {
        HashSet<T> ITypeReader<HashSet<T>, SequenceDataNode>.Read(ISerializationManager serializationManager,
            SequenceDataNode node,
            IDependencyCollection dependencies,
            bool skipHook,
            ISerializationContext? context, HashSet<T>? set)
        {
            set ??= new HashSet<T>();

            foreach (var dataNode in node.Sequence)
            {
                set.Add(serializationManager.Read<T>(dataNode, context, skipHook));
            }

            return set;
        }

        ValidationNode ITypeValidator<ImmutableHashSet<T>, SequenceDataNode>.Validate(
            ISerializationManager serializationManager,
            SequenceDataNode node, IDependencyCollection dependencies, ISerializationContext? context)
        {
            return Validate(serializationManager, node, context);
        }

        ValidationNode ITypeValidator<HashSet<T>, SequenceDataNode>.Validate(ISerializationManager serializationManager,
            SequenceDataNode node, IDependencyCollection dependencies, ISerializationContext? context)
        {
            return Validate(serializationManager, node, context);
        }

        ValidationNode Validate(ISerializationManager serializationManager, SequenceDataNode node, ISerializationContext? context)
        {
            var list = new List<ValidationNode>();
            foreach (var elem in node.Sequence)
            {
                list.Add(serializationManager.ValidateNode(typeof(T), elem, context));
            }

            return new ValidatedSequenceNode(list);
        }

        public DataNode Write(ISerializationManager serializationManager, ImmutableHashSet<T> value,
            IDependencyCollection dependencies,
            bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return Write(serializationManager, value.ToHashSet(), dependencies, alwaysWrite, context);
        }

        public DataNode Write(ISerializationManager serializationManager, HashSet<T> value,
            IDependencyCollection dependencies, bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            var sequence = new SequenceDataNode();

            foreach (var elem in value)
            {
                sequence.Add(serializationManager.WriteValue(typeof(T), elem, alwaysWrite, context));
            }

            return sequence;
        }

        ImmutableHashSet<T> ITypeReader<ImmutableHashSet<T>, SequenceDataNode>.Read(
            ISerializationManager serializationManager,
            SequenceDataNode node,
            IDependencyCollection dependencies,
            bool skipHook,
            ISerializationContext? context, ImmutableHashSet<T>? rawValue)
        {
            if(rawValue != null)
                Logger.Warning($"Provided value to a Read-call for a {nameof(ImmutableHashSet<T>)}. Ignoring...");
            var set = ImmutableHashSet.CreateBuilder<T>();

            foreach (var dataNode in node.Sequence)
            {
                set.Add(serializationManager.Read<T>(dataNode, context, skipHook));
            }

            return set.ToImmutable();
        }

        [MustUseReturnValue]
        public HashSet<T> Copy(ISerializationManager serializationManager, HashSet<T> source, HashSet<T> target,
            bool skipHook,
            ISerializationContext? context = null)
        {
            target.Clear();
            target.EnsureCapacity(source.Count);

            foreach (var element in source)
            {
                var elementCopy = serializationManager.Copy(element, context) ?? throw new NullReferenceException();
                target.Add(elementCopy);
            }

            return target;
        }

        [MustUseReturnValue]
        public ImmutableHashSet<T> Copy(ISerializationManager serializationManager, ImmutableHashSet<T> source,
            ImmutableHashSet<T> target, bool skipHook, ISerializationContext? context = null)
        {
            var builder = ImmutableHashSet.CreateBuilder<T>();

            foreach (var element in source)
            {
                var elementCopy = serializationManager.Copy(element, context) ?? throw new NullReferenceException();
                builder.Add(elementCopy);
            }

            return builder.ToImmutable();
        }
    }
}
