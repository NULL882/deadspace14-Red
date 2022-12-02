using System.Collections.Generic;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary
{

    public sealed class AbstractPrototypeIdDictionarySerializer<TValue, TPrototype> : PrototypeIdDictionarySerializer<TValue,
            TPrototype> where TPrototype : class, IPrototype, IInheritingPrototype
    {
        protected override PrototypeIdSerializer<TPrototype> PrototypeSerializer =>
            new AbstractPrototypeIdSerializer<TPrototype>();
    }

    [Virtual]
    public class PrototypeIdDictionarySerializer<TValue, TPrototype> :
        ITypeSerializer<Dictionary<string, TValue>, MappingDataNode>,
        ITypeSerializer<SortedDictionary<string, TValue>, MappingDataNode>,
        ITypeSerializer<IReadOnlyDictionary<string, TValue>, MappingDataNode>
        where TPrototype : class, IPrototype
    {
        private readonly DictionarySerializer<string, TValue> _dictionarySerializer = new();
        protected virtual PrototypeIdSerializer<TPrototype> PrototypeSerializer => new();

        private ValidationNode Validate(ISerializationManager serializationManager, MappingDataNode node, IDependencyCollection dependencies, ISerializationContext? context)
        {
            var mapping = new Dictionary<ValidationNode, ValidationNode>();

            foreach (var (key, val) in node.Children)
            {
                if (key is not ValueDataNode value)
                {
                    mapping.Add(new ErrorNode(key, $"Cannot cast node {key} to ValueDataNode."), serializationManager.ValidateNode(typeof(TValue), val, context));
                    continue;
                }

                mapping.Add(PrototypeSerializer.Validate(serializationManager, value, dependencies, context), serializationManager.ValidateNode(typeof(TValue), val, context));
            }

            return new ValidatedMappingNode(mapping);
        }

        ValidationNode ITypeValidator<Dictionary<string, TValue>, MappingDataNode>.Validate(
            ISerializationManager serializationManager, MappingDataNode node,
            IDependencyCollection dependencies, ISerializationContext? context)
        {
            return Validate(serializationManager, node, dependencies, context);
        }

        ValidationNode ITypeValidator<SortedDictionary<string, TValue>, MappingDataNode>.Validate(
            ISerializationManager serializationManager, MappingDataNode node,
            IDependencyCollection dependencies, ISerializationContext? context)
        {
            return Validate(serializationManager, node, dependencies, context);
        }

        ValidationNode ITypeValidator<IReadOnlyDictionary<string, TValue>, MappingDataNode>.Validate(
            ISerializationManager serializationManager, MappingDataNode node,
            IDependencyCollection dependencies, ISerializationContext? context)
        {
            return Validate(serializationManager, node, dependencies, context);
        }

        Dictionary<string, TValue> ITypeReader<Dictionary<string, TValue>, MappingDataNode>.Read(
            ISerializationManager serializationManager, MappingDataNode node,
            IDependencyCollection dependencies, bool skipHook, ISerializationContext? context,
            Dictionary<string, TValue>? value)
        {
            return _dictionarySerializer.Read(serializationManager, node, dependencies, skipHook, context, value);
        }

        SortedDictionary<string, TValue> ITypeReader<SortedDictionary<string, TValue>, MappingDataNode>.Read(
            ISerializationManager serializationManager, MappingDataNode node,
            IDependencyCollection dependencies, bool skipHook, ISerializationContext? context,
            SortedDictionary<string, TValue>? value)
        {
            return ((ITypeReader<SortedDictionary<string, TValue>, MappingDataNode>)_dictionarySerializer).Read(serializationManager, node, dependencies, skipHook, context, value);
        }

        IReadOnlyDictionary<string, TValue> ITypeReader<IReadOnlyDictionary<string, TValue>, MappingDataNode>.Read(
            ISerializationManager serializationManager, MappingDataNode node,
            IDependencyCollection dependencies, bool skipHook, ISerializationContext? context,
            IReadOnlyDictionary<string, TValue>? value)
        {
            return ((ITypeReader<IReadOnlyDictionary<string, TValue>, MappingDataNode>)_dictionarySerializer).Read(serializationManager, node, dependencies, skipHook, context, value);
        }

        public DataNode Write(ISerializationManager serializationManager, Dictionary<string, TValue> value,
            IDependencyCollection dependencies,
            bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return _dictionarySerializer.Write(serializationManager, value, dependencies, alwaysWrite, context);
        }

        public DataNode Write(ISerializationManager serializationManager, SortedDictionary<string, TValue> value,
            IDependencyCollection dependencies,
            bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return _dictionarySerializer.Write(serializationManager, value, dependencies, alwaysWrite, context);
        }

        public DataNode Write(ISerializationManager serializationManager, IReadOnlyDictionary<string, TValue> value,
            IDependencyCollection dependencies,
            bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return _dictionarySerializer.Write(serializationManager, value, dependencies, alwaysWrite, context);
        }

        public Dictionary<string, TValue> Copy(ISerializationManager serializationManager,
            Dictionary<string, TValue> source, Dictionary<string, TValue> target, bool skipHook,
            ISerializationContext? context = null)
        {
            return _dictionarySerializer.Copy(serializationManager, source, target, skipHook, context);
        }

        public SortedDictionary<string, TValue> Copy(ISerializationManager serializationManager,
            SortedDictionary<string, TValue> source, SortedDictionary<string, TValue> target,
            bool skipHook, ISerializationContext? context = null)
        {
            return _dictionarySerializer.Copy(serializationManager, source, target, skipHook, context);
        }

        public IReadOnlyDictionary<string, TValue> Copy(ISerializationManager serializationManager,
            IReadOnlyDictionary<string, TValue> source,
            IReadOnlyDictionary<string, TValue> target, bool skipHook, ISerializationContext? context = null)
        {
            return _dictionarySerializer.Copy(serializationManager, source, target, skipHook, context);
        }
    }
}
