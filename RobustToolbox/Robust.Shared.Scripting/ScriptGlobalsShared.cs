using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Robust.Shared.Scripting
{
    [PublicAPI]
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "CA1822")]
    public abstract class ScriptGlobalsShared
    {
        private const BindingFlags DefaultHelpFlags =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [field: Dependency] public IEntityManager ent { get; } = default!;
        [field: Dependency] public IEntitySystemManager esm { get; } = default!;
        [field: Dependency] public IPrototypeManager prot { get; } = default!;
        [field: Dependency] public IMapManager map { get; } = default!;
        [field: Dependency] public IDependencyCollection dependencies { get; } = default!;

        protected ScriptGlobalsShared(IDependencyCollection dependencies)
        {
            dependencies.InjectDependencies(this);
        }

        public IEnumerable<T> protos<T>() where T : class, IPrototype
        {
            return prot.EnumeratePrototypes<T>();
        }

        public IEnumerable<EntityPrototype> eprotos => prot.EnumeratePrototypes<EntityPrototype>();

        public EntityCoordinates gpos(double x, double y, int gridId)
        {
            return gpos(x, y, new EntityUid(gridId));
        }

        public EntityCoordinates gpos(double x, double y, EntityUid gridId)
        {
            if (!map.TryGetGrid(gridId, out var grid))
            {
                return new EntityCoordinates(EntityUid.Invalid, ((float) x, (float) y));
            }

            return new EntityCoordinates(grid.GridEntityId, ((float) x, (float) y));
        }

        public EntityUid eid(int i)
        {
            return new(i);
        }

        public T gcm<T>(int i)
        {
            return ent.GetComponent<T>(eid(i));
        }

        public MapGridComponent getgrid(int i)
        {
            return map.GetGrid(new EntityUid(i));
        }

        public MapGridComponent getgrid(EntityUid mapId)
        {
            return map.GetGrid(mapId);
        }

        public EntityUid spawn(string prototype, EntityCoordinates position)
        {
            return ent.SpawnEntity(prototype, position);
        }

        public T res<T>()
        {
            return dependencies.Resolve<T>();
        }

        public T ressys<T>() where T : EntitySystem
        {
            return esm.GetEntitySystem<T>();
        }

        public object? prop(object target, string name)
        {
            return target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)
                    !.GetValue(target);
        }

        public void setprop(object target, string name, object? value)
        {
            target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                !.SetValue(target, value);
        }

        public object? fld(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                !.GetValue(target);
        }

        public void setfld(object target, string name, object? value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                !.SetValue(target, value);
        }

        public object? call(object target, string name, params object[] args)
        {
            var t = target.GetType();
            // TODO: overloads
            var m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return m!.Invoke(target, args);
        }

        public void help()
        {
            help(GetType(), DefaultHelpFlags &~ BindingFlags.NonPublic, false, false);
        }

        public void help(Type type, BindingFlags flags = DefaultHelpFlags, bool specialNameMethods = true, bool modifiers = true)
        {
            var builder = new StringBuilder();

            foreach (var member in type.GetMembers(flags))
            {
                switch (member.MemberType)
                {
                    case MemberTypes.Method:
                        var method = (MethodInfo) member;

                        if (!specialNameMethods && method.IsSpecialName)
                            continue; // Let's not print constructors, property methods, etc.

                        builder.Append(method.PrintMethodSignature(modifiers));
                        builder.AppendLine(";");
                        break;

                    case MemberTypes.Property:
                        builder.AppendLine(((PropertyInfo)member).PrintPropertySignature(modifiers, true));
                        break;

                    case MemberTypes.Field:
                        builder.Append(((FieldInfo) member).PrintFieldSignature(modifiers));
                        builder.AppendLine(";");
                        break;

                    default:
                        continue;
                }

                builder.AppendLine();
            }

            // This is slow, so do it all at once.
            WriteSyntax(builder.ToString());
        }

        protected abstract void WriteSyntax(object toString);
        public abstract void write(object toString);
        public abstract void show(object obj);
    }
}
