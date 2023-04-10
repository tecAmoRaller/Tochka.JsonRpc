﻿using System.Diagnostics.CodeAnalysis;
using Tochka.JsonRpc.Common.Models.Request.Untyped;

namespace Tochka.JsonRpc.Common.Models.Request.Wrappers;

[ExcludeFromCodeCoverage]
public sealed record BatchRequestWrapper(List<IUntypedCall> Calls) : IRequestWrapper;
