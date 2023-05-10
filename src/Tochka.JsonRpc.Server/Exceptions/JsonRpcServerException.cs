﻿namespace Tochka.JsonRpc.Server.Exceptions;

public class JsonRpcServerException : Exception
{
    public JsonRpcServerException()
    {
    }

    public JsonRpcServerException(string message) : base(message)
    {
    }

    public JsonRpcServerException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
