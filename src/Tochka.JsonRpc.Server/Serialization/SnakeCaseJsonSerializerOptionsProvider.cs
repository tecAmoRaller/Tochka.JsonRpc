﻿using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using Tochka.JsonRpc.Common;

namespace Tochka.JsonRpc.Server.Serialization;

/// <inheritdoc />
/// <summary>
/// <see cref="IJsonSerializerOptionsProvider" /> with <see cref="JsonRpcSerializerOptions.SnakeCase" /> options
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public class SnakeCaseJsonSerializerOptionsProvider : IJsonSerializerOptionsProvider
{
    /// <inheritdoc />
    /// <summary>
    ///     <see cref="JsonRpcSerializerOptions.SnakeCase" /> <see cref="JsonSerializerOptions" />
    /// </summary>
    public JsonSerializerOptions Options => JsonRpcSerializerOptions.SnakeCase;
}
