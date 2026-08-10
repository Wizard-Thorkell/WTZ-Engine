// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Globalization;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;
using Robust.Shared.Utility;

namespace Robust.Shared.Serialization.TypeSerializers.Implementations;

[TypeSerializer]
public sealed class ZLevelTileIndicesSerializer :
    ITypeSerializer<ZLevelTileIndices, ValueDataNode>,
    ITypeCopyCreator<ZLevelTileIndices>
{
    public ZLevelTileIndices Read(
        ISerializationManager serializationManager,
        ValueDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<ZLevelTileIndices>? instanceProvider = null)
    {
        if (!VectorSerializerUtility.TryParseArgs(node.Value, 3, out var args))
            throw new InvalidMappingException($"Could not parse {nameof(ZLevelTileIndices)}: '{node.Value}'");

        return new ZLevelTileIndices(
            int.Parse(args[0], CultureInfo.InvariantCulture),
            int.Parse(args[1], CultureInfo.InvariantCulture),
            int.Parse(args[2], CultureInfo.InvariantCulture));
    }

    public ValidationNode Validate(
        ISerializationManager serializationManager,
        ValueDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        if (!VectorSerializerUtility.TryParseArgs(node.Value, 3, out var args))
            return new ErrorNode(node, $"Failed parsing values for {nameof(ZLevelTileIndices)}.");

        return int.TryParse(args[0], NumberStyles.Any, CultureInfo.InvariantCulture, out _) &&
               int.TryParse(args[1], NumberStyles.Any, CultureInfo.InvariantCulture, out _) &&
               int.TryParse(args[2], NumberStyles.Any, CultureInfo.InvariantCulture, out _)
            ? new ValidatedValueNode(node)
            : new ErrorNode(node, $"Failed parsing values for {nameof(ZLevelTileIndices)}.");
    }

    public DataNode Write(
        ISerializationManager serializationManager,
        ZLevelTileIndices value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
    {
        return new ValueDataNode(
            $"{value.X.ToString(CultureInfo.InvariantCulture)}," +
            $"{value.Y.ToString(CultureInfo.InvariantCulture)}," +
            value.Z.ToString(CultureInfo.InvariantCulture));
    }

    public ZLevelTileIndices CreateCopy(
        ISerializationManager serializationManager,
        ZLevelTileIndices source,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null)
    {
        return new ZLevelTileIndices(source.X, source.Y, source.Z);
    }
}
